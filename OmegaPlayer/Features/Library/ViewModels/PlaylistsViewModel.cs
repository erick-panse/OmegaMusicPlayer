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
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Features.Profile.ViewModels;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class PlaylistsViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly PlaylistDisplayService _playlistDisplayService;
        private readonly PlaylistService _playlistService;
        private readonly TracksService _tracksService;
        private readonly AllTracksRepository _allTracksRepository;
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

        private bool _isInitialized = false;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadPlaylists);

        public PlaylistsViewModel(
            PlaylistDisplayService playlistDisplayService,
            PlaylistService playlistService,
            TracksService tracksService,
            AllTracksRepository allTracksRepository,
            PlaylistTracksService playlistTracksService,
            TrackQueueViewModel trackQueueViewModel,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _playlistDisplayService = playlistDisplayService;
            _playlistService = playlistService;
            _tracksService = tracksService;
            _allTracksRepository = allTracksRepository;
            _playlistTracksService = playlistTracksService;
            _trackQueueViewModel = trackQueueViewModel;

            LoadInitialPlaylists();
            _mainViewModel = mainViewModel;

            // Register for like updates and profile switch to keep favorites playlist in sync
            _messenger.Register<TrackLikeUpdateMessage>(this, HandleTrackLikeUpdate);
            _messenger.Register<ProfileUpdateMessage>(this, (r, m) => HandleProfileSwitch(m));
        }

        private void HandleTrackLikeUpdate(object recipient, TrackLikeUpdateMessage message)
        {
            // Update the favorites playlist when a track is liked/unliked
            _isInitialized = false;
            LoadInitialPlaylists();
        }

        private void HandleProfileSwitch(ProfileUpdateMessage message)
        {
            // Update the favorites playlist when active profile is changed
            _isInitialized = false;
            LoadInitialPlaylists();
        }

        protected override void ApplyCurrentSort()
        {
            if (!_isInitialized) return;

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
            if (!_isInitialized)
            {
                _isInitialized = true;
            }
            await LoadPlaylists();
        }

        public override void OnSortSettingsReceived(SortType sortType, SortDirection direction, bool isUserInitiated = false)
        {
            base.OnSortSettingsReceived(sortType, direction, false); // Never auto-apply sort

            if (!_isInitialized)
            {
                LoadInitialPlaylists();
            }
            else if (isUserInitiated)
            {
                // Only apply sort if user initiated the change
                ApplyCurrentSort();
            }
        }

        public async Task LoadPlaylists()
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
                    dialog.Initialize();
                }
                else
                {
                    dialog.Initialize(playlistToEdit);
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
                if (playlistD == null || playlistD.IsFavoritePlaylist) return;

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

        public async Task AddTracksToPlaylist(int playlistId, IEnumerable<TrackDisplayModel> tracks)
        {
            if (!tracks.Any() || _playlistDisplayService.IsFavoritesPlaylist(playlistId)) return;

            try
            {
                var playlistTracks = new List<PlaylistTracks>();
                var existingTracks = await _playlistTracksService.GetAllPlaylistTracks();

                // Get the highest current track order for this playlist
                int maxOrder = existingTracks.Any()
                    ? existingTracks.Max(pt => pt.TrackOrder) : 0;

                // Create new playlist track entries - allowing duplicate tracks
                foreach (var track in tracks)
                {
                    maxOrder++;
                    playlistTracks.Add(new PlaylistTracks
                    {
                        PlaylistID = playlistId,
                        TrackID = track.TrackID,
                        TrackOrder = maxOrder
                    });
                }

                await SavePlaylistTracks(playlistTracks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding tracks to playlist: {ex.Message}");
                throw;
            }
        }

        private async Task SavePlaylistTracks(IEnumerable<PlaylistTracks> playlistTracks)
        {
            if (!playlistTracks.Any()) return;

            try
            {
                foreach (var playlistTrack in playlistTracks)
                {
                    await _playlistTracksService.AddPlaylistTrack(playlistTrack);
                }

                // Refresh playlists display after saving
                await LoadPlaylists();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving playlist tracks: {ex.Message}");
                throw;
            }
        }


        public async Task<List<TrackDisplayModel>> GetSelectedPlaylistTracks(int playlistID)
        {
            var selectedPlaylist = SelectedPlaylists;
            if (selectedPlaylist.Count <= 1)
            {
                return await _playlistDisplayService.GetPlaylistTracksAsync(playlistID);
            }

            var trackTasks = selectedPlaylist.Select(Playlist =>
                _playlistDisplayService.GetPlaylistTracksAsync(Playlist.PlaylistID));

            var allTrackLists = await Task.WhenAll(trackTasks);
            return allTrackLists.SelectMany(tracks => tracks).ToList();
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(PlaylistDisplayModel playlist)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow == null || !mainWindow.IsVisible) return;

                var selectedTracks = await GetSelectedPlaylistTracks(playlist.PlaylistID);

                var dialog = new PlaylistSelectionDialog();
                dialog.Initialize(this, null, selectedTracks);
                await dialog.ShowDialog(mainWindow);

                ClearSelection();
            }
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