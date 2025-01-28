using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Playlists.Views;
using OmegaPlayer.Features.Shell.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using OmegaPlayer.Features.Playlists.Services;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class PlaylistViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly PlaylistDisplayService _playlistDisplayService;
        private readonly PlaylistService _playlistService;
        private readonly PlaylistTracksService _playlistTracksService;
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
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadPlaylists);

        public PlaylistViewModel(
            PlaylistDisplayService playlistDisplayService,
            PlaylistService playlistService,
            PlaylistTracksService playlistTracksService,
            TrackQueueViewModel trackQueueViewModel,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _playlistDisplayService = playlistDisplayService;
            _playlistService = playlistService;
            _playlistTracksService = playlistTracksService;
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

        public async void LoadInitialPlaylists()
        {
            await LoadPlaylists();
        }

        private async Task LoadPlaylists()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                var allPlaylists = await _playlistDisplayService.GetAllPlaylistDisplaysAsync();

                var totalPlaylists = allPlaylists.Count;
                var current = 0;

                Playlists.Clear();

                foreach (var playlist in allPlaylists)
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
        public async Task OpenPlaylistDetails(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;
            await _mainViewModel.NavigateToDetails(ContentType.Playlist, playlist);
        }

        [RelayCommand]
        public void SelectPlaylist(PlaylistDisplayModel playlist)
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
        public async Task PlayPlaylistTracks(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;

            var tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
            if (tracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddPlaylistTracksToNext(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;

            var tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddPlaylistTracksToQueue(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;

            var tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task CreateNewPlaylist()
        {
            OpenCreatePlaylistDialog(true);
        }

        [RelayCommand]
        public async Task EditPlaylist(PlaylistDisplayModel playlistD)
        {
            var Playlist = new Playlist
            {
                PlaylistID = playlistD.PlaylistID, // Keep the original ID
                Title = playlistD.Title,
                ProfileID = playlistD.ProfileID,
                CreatedAt = playlistD.CreatedAt, // Keep original creation date
                UpdatedAt = DateTime.Now
            };
            OpenCreatePlaylistDialog(false, Playlist);
        }

        private async Task OpenCreatePlaylistDialog(bool IsCreate, Playlist playlistToEdit = null)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow == null || !mainWindow.IsVisible) return;

                var dialog = new PlaylistDialogView();

                if (IsCreate)
                {
                    dialog.Initialize(GetProfile());
                }
                else
                {
                    dialog.Initialize(GetProfile(), playlistToEdit);
                }

                await dialog.ShowDialog<Playlist>(mainWindow);

                await LoadPlaylists();
            }
        }

        [RelayCommand]
        public async Task DeletePlaylist(PlaylistDisplayModel playlistD)
        {
            try
            {
                // Remove any associated tracks
                var playlistTracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlistD.PlaylistID);
                if (playlistTracks.Any())
                {
                    await _playlistTracksService.DeletePlaylistTrack(playlistD.PlaylistID);
                }

                // Delete the playlist
                await _playlistService.DeletePlaylist(playlistD.PlaylistID);

                // If the playlist was selected, remove it from selection
                if (playlistD.IsSelected)
                {
                    SelectedPlaylists.Remove(playlistD);
                    HasSelectedPlaylists = SelectedPlaylists.Any();
                }

                // Refresh the playlists view
                await LoadPlaylists();
            }
            catch (Exception ex)
            {
                // Log the error and notify the user if needed
                Console.WriteLine($"Error deleting playlist: {ex.Message}");
                if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    var messageBox = MessageBoxManager.GetMessageBoxStandard(
                        "Error",
                        "Failed to delete playlist. Please try again.",
                        ButtonEnum.Ok,
                        Icon.Error
                    );
                    await messageBox.ShowAsync();
                }
            }
        }
        private int GetProfile()
        {
            return 2; // mock user during development
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