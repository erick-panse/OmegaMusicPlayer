using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    public partial class PlaylistViewModel : ViewModelBase, ILoadMoreItems
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
            MainViewModel mainViewModel)
        {
            _playlistDisplayService = playlistDisplayService;
            _trackQueueViewModel = trackQueueViewModel;

            LoadInitialPlaylists();
            _mainViewModel = mainViewModel;
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

                CurrentPage++;
            }
            finally
            {
                await Task.Delay(500);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenArtistDetails(PlaylistDisplayModel playlist)
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
        private void PlayPlaylist(PlaylistDisplayModel playlist)
        {
            // Implementation for playing all tracks in the playlist
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var playlist in SelectedPlaylists)
            {
                playlist.IsSelected = false;
            }
            SelectedPlaylists.Clear();
            HasSelectedPlaylists = false;
        }

        [RelayCommand]
        private void PlayNext()
        {
            // Implementation for playing next
        }

        [RelayCommand]
        private void AddToQueue()
        {
            // Implementation for adding to queue
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