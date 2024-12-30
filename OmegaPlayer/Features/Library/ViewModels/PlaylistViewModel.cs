using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class PlaylistViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly PlaylistDisplayService _playlistDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty]
        private ObservableCollection<PlaylistDisplayModel> _playlists = new();

        [ObservableProperty]
        private ObservableCollection<PlaylistDisplayModel> _selectedPlaylists = new();

        [ObservableProperty]
        private bool _hasSelectedPlaylists;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private double _loadingProgress;

        [ObservableProperty]
        private int _currentPage = 1;

        private const int _pageSize = 50;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public PlaylistViewModel(
            PlaylistDisplayService playlistDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _playlistDisplayService = playlistDisplayService;
            _trackQueueViewModel = trackQueueViewModel;

            LoadInitialPlaylists();
            _mainViewModel = mainViewModel;
        }

        protected override void ApplyCurrentSort()
        {
            IEnumerable<PlaylistDisplayModel> sortedPlaylists = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    Playlists,
                    SortType.Duration,
                    CurrentSortDirection,
                    p => p.Title,
                    p => (int)p.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    Playlists,
                    SortType.Name,
                    CurrentSortDirection,
                    p => p.Title)
            };

            var sortedPlaylistsList = sortedPlaylists.ToList();
            Playlists.Clear();
            foreach (var playlist in sortedPlaylistsList)
            {
                Playlists.Add(playlist);
            }
        }

        private async void LoadInitialPlaylists()
        {
            await LoadMoreItems();
        }

        private async Task LoadMoreItems()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                var playlistsPage = await _playlistDisplayService.GetPlaylistsPageAsync(CurrentPage, _pageSize);

                var totalPlaylists = playlistsPage.Count;
                var current = 0;

                foreach (var playlist in playlistsPage)
                {
                    await Task.Run(async () =>
                    {
                        await _playlistDisplayService.LoadPlaylistCoverAsync(playlist);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Playlists.Add(playlist);
                            current++;
                            LoadingProgress = (current * 100.0) / totalPlaylists;
                        });
                    });
                }

                ApplyCurrentSort();
                CurrentPage++;
            }
            finally
            {
                await Task.Delay(500);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenPlaylistDetails(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;
            await _mainViewModel.NavigateToDetails(ContentType.Playlist, playlist);
        }

        [RelayCommand]
        private void SelectPlaylist(PlaylistDisplayModel playlist)
        {
            if (playlist.IsSelected)
            {
                SelectedPlaylists.Add(playlist);
            }
            else
            {
                SelectedPlaylists.Remove(playlist);
            }
            HasSelectedPlaylists = SelectedPlaylists.Any();
        }

        [RelayCommand]
        public void ClearSelection()
        {
            foreach (var playlist in Playlists)
            {
                playlist.IsSelected = false;
            }
            SelectedPlaylists.Clear();
            HasSelectedPlaylists = false;
        }

        [RelayCommand]
        private async Task PlayPlaylistTracks(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;

            var tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
            if (tracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        private async Task AddPlaylistTracksToNext(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;

            var tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        private async Task AddPlaylistTracksToQueue(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;

            var tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        private void CreateNewPlaylist()
        {
            // Implementation for creating a new playlist
        }

        public async Task LoadHighResCoversForVisiblePlaylistsAsync(IList<PlaylistDisplayModel> visiblePlaylists)
        {
            foreach (var playlist in visiblePlaylists)
            {
                if (playlist.CoverSize != "high")
                {
                    await _playlistDisplayService.LoadHighResPlaylistCoverAsync(playlist);
                    playlist.CoverSize = "high";
                }
            }
        }
    }
}