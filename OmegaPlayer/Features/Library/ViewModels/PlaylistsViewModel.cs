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
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Core.Enums;
using System.Collections.Concurrent;
using System.Threading;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class PlaylistsViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly PlaylistDisplayService _playlistDisplayService;
        private readonly PlaylistService _playlistService;
        private readonly PlaylistTracksService _playlistTracksService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly StandardImageService _standardImageService;
        private readonly MainViewModel _mainViewModel;

        private List<PlaylistDisplayModel> AllPlaylists { get; set; }

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

        private bool _isApplyingSort = false;
        private bool _isAllPlaylistsLoaded = false;
        private bool _isPlaylistsLoaded = false;
        private bool _isInitializing = false;
        private CancellationTokenSource _loadingCancellationTokenSource;

        // Track which playlists have had their images loaded to avoid redundant loading
        private readonly ConcurrentDictionary<int, bool> _playlistsWithLoadedImages = new();

        // Event to trigger visibility check from view
        public Action TriggerVisibilityCheck { get; set; }

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadPlaylists);

        public PlaylistsViewModel(
            PlaylistDisplayService playlistDisplayService,
            PlaylistService playlistService,
            PlaylistTracksService playlistTracksService,
            TrackQueueViewModel trackQueueViewModel,
            TrackSortService trackSortService,
            StandardImageService standardImageService,
            MainViewModel mainViewModel,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
            : base(trackSortService, messenger, errorHandlingService)
        {
            _playlistDisplayService = playlistDisplayService;
            _playlistService = playlistService;
            _playlistTracksService = playlistTracksService;
            _trackQueueViewModel = trackQueueViewModel;
            _standardImageService = standardImageService;
            _mainViewModel = mainViewModel;

            // Register for like updates and profile switch to keep favorites playlist in sync
            _messenger.Register<TrackLikeUpdateMessage>(this, HandleTrackLikeUpdate);
            _messenger.Register<ProfileUpdateMessage>(this, (r, m) => HandleProfileSwitch(m));
        }

        private void HandleTrackLikeUpdate(object recipient, TrackLikeUpdateMessage message)
        {
            // Update the favorites playlist when a track is liked/unliked
            _isInitializing = false;
            LoadInitialPlaylists();
        }

        private void HandleProfileSwitch(ProfileUpdateMessage message)
        {
            // Update the favorites playlist when active profile is changed
            _isInitializing = false;
            LoadInitialPlaylists();
        }

        // Cleanup method that can be called manually if needed
        public void Cleanup()
        {
            // Unregister from all messengers
            _messenger.UnregisterAll(this);

            // Perform any other cleanup needed
            AllPlaylists = null;
            SelectedPlaylists.Clear();
            Playlists.Clear();
        }

        protected override async void ApplyCurrentSort()
        {
            // Cancel any ongoing loading operation
            _loadingCancellationTokenSource?.Cancel();

            _isApplyingSort = true;

            try
            {
                // Reset loading state and clear cached images
                _playlistsWithLoadedImages.Clear();
                _isPlaylistsLoaded = false;

                // Small delay to ensure cancellation is processed
                await Task.Delay(10);

                // Reset cancellation token source for new operation
                _loadingCancellationTokenSource?.Dispose();
                _loadingCancellationTokenSource = new CancellationTokenSource();

                await LoadPlaylists();
            }
            finally
            {
                _isApplyingSort = false;
            }
        }

        public override void OnSortSettingsReceived(SortType sortType, SortDirection direction, bool isUserInitiated = false)
        {
            // Update the sort settings
            CurrentSortType = sortType;
            CurrentSortDirection = direction;

            // Apply the new sort if we're initialized AND this is user-initiated
            if (isUserInitiated && _isAllPlaylistsLoaded)
            {
                ApplyCurrentSort();
            }
        }

        public async Task Initialize()
        {
            // Prevent multiple initializations
            if (_isInitializing) return;

            _isInitializing = true;
            ClearSelection();

            try
            {
                // Small delay to let MainViewModel send sort settings first
                await Task.Delay(1);
                await LoadInitialPlaylists();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async Task LoadInitialPlaylists()
        {
            _playlistsWithLoadedImages.Clear();
            _isPlaylistsLoaded = false;

            // Ensure AllPlaylists is loaded first (might already be loaded from constructor)
            if (!_isAllPlaylistsLoaded)
            {
                await LoadAllPlaylistsAsync();
            }

            await LoadPlaylists();
        }

        /// <summary>
        /// Loads AllPlaylists in background without affecting UI
        /// </summary>
        private async Task LoadAllPlaylistsAsync()
        {
            if (_isAllPlaylistsLoaded) return;

            try
            {
                AllPlaylists = await _playlistDisplayService.GetAllPlaylistDisplaysAsync();
                _isAllPlaylistsLoaded = true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading AllPlaylists from database",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Notifies the image loading system about playlist visibility changes and loads images for visible playlists
        /// </summary>
        public async Task NotifyPlaylistVisible(PlaylistDisplayModel playlist, bool isVisible)
        {
            if (playlist?.CoverPath == null) return;

            try
            {
                // Notify the image service about visibility changes for optimization
                if (_standardImageService != null)
                {
                    await _standardImageService.NotifyImageVisible(playlist.CoverPath, isVisible);
                }

                // If playlist becomes visible and hasn't had its image loaded yet, load it now
                if (isVisible && !_playlistsWithLoadedImages.ContainsKey(playlist.PlaylistID))
                {
                    _playlistsWithLoadedImages[playlist.PlaylistID] = true;

                    // Load the image in the background with lower priority
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _playlistDisplayService.LoadPlaylistCoverAsync(playlist, "low", true);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error loading playlist image",
                                ex.Message,
                                ex,
                                false);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling playlist visibility notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        public async Task LoadPlaylists()
        {
            if (IsLoading || _isPlaylistsLoaded) return;

            // Cancel any previous loading operation
            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _loadingCancellationTokenSource.Token;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // If no playlists available, return empty
                if (!_isAllPlaylistsLoaded || AllPlaylists?.Any() != true)
                {
                    return;
                }

                // Clear playlists immediately on UI thread
                Playlists.Clear();

                // Get sorted playlists
                var sortedPlaylists = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sorted = GetSortedAllPlaylists();
                    var processed = new List<PlaylistDisplayModel>();

                    // Pre-process all playlists in background
                    foreach (var playlist in sorted)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        processed.Add(playlist);
                    }

                    return processed;
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Load playlists in chunks to keep UI responsive
                const int chunkSize = 10; // Smaller chunks for better responsiveness
                var totalPlaylists = sortedPlaylists.Count;
                var loadedCount = 0;

                for (int i = 0; i < sortedPlaylists.Count; i += chunkSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Trigger Visibility once per chunk to update the images (needed to load images when sorting changes)
                    TriggerVisibilityCheck?.Invoke();

                    // Get chunk of playlists
                    var chunk = sortedPlaylists.Skip(i).Take(chunkSize).ToList();

                    // Add chunk to UI in one operation
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        foreach (var playlist in chunk)
                        {
                            Playlists.Add(playlist);
                        }

                        loadedCount += chunk.Count;
                        LoadingProgress = Math.Min(100, (loadedCount * 100.0) / totalPlaylists);
                    }, Avalonia.Threading.DispatcherPriority.Background);

                    // Yield control back to UI thread between chunks (critical for responsiveness)
                    await Task.Delay(1, cancellationToken); // Very small delay to let UI process events
                }

                _isPlaylistsLoaded = true;
            }
            catch (OperationCanceledException)
            {
                // Loading was cancelled, this is expected
                _isPlaylistsLoaded = false;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading playlist library",
                    ex.Message,
                    ex,
                    true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private IEnumerable<PlaylistDisplayModel> GetSortedAllPlaylists()
        {
            if (AllPlaylists == null || !AllPlaylists.Any()) return new List<PlaylistDisplayModel>();

            var sortedPlaylists = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    AllPlaylists,
                    SortType.Duration,
                    CurrentSortDirection,
                    p => p.Title,
                    p => (int)p.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    AllPlaylists,
                    SortType.Name,
                    CurrentSortDirection,
                    p => p.Title)
            };

            return sortedPlaylists;
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
            if (playlist == null) return;

            if (playlist.IsSelected)
            {
                SelectedPlaylists.Add(playlist);
            }
            else
            {
                SelectedPlaylists.Remove(playlist);
            }
            HasSelectedPlaylists = SelectedPlaylists.Count > 0;
        }

        [RelayCommand]
        public void SelectAll()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    SelectedPlaylists.Clear();
                    foreach (var playlist in Playlists)
                    {
                        playlist.IsSelected = true;
                        SelectedPlaylists.Add(playlist);
                    }
                    HasSelectedPlaylists = SelectedPlaylists.Count > 0;
                },
                "Selecting all playlists",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public void ClearSelection()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    foreach (var playlist in Playlists)
                    {
                        playlist.IsSelected = false;
                    }
                    SelectedPlaylists.Clear();
                    HasSelectedPlaylists = SelectedPlaylists.Count > 0;
                },
                "Clearing playlist selection",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task PlayPlaylistTracks(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;

            var tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
            if (tracks.Count > 0)
            {
                _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddPlaylistTracksToNext(PlaylistDisplayModel playlist)
        {
            var tracks = await GetTracksToAdd(playlist);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }
            ClearSelection();
        }

        [RelayCommand]
        public async Task AddPlaylistTracksToQueue(PlaylistDisplayModel playlist)
        {
            var tracks = await GetTracksToAdd(playlist);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }

            ClearSelection();
        }

        /// <summary>
        /// Helper that returns the tracks to be added in Play next and Add to Queue methods
        /// </summary>
        public async Task<List<TrackDisplayModel>> GetTracksToAdd(PlaylistDisplayModel playlist)
        {
            var playlistList = SelectedPlaylists.Count > 0
                ? SelectedPlaylists
                : new ObservableCollection<PlaylistDisplayModel>();

            if (playlistList.Count < 1 && playlist != null)
            {
                playlistList.Add(playlist);
            }

            var tracks = new List<TrackDisplayModel>();

            foreach (var playlistToAdd in playlistList)
            {
                var playlistTracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlistToAdd.PlaylistID);

                if (playlistTracks.Count > 0)
                    tracks.AddRange(playlistTracks);
            }

            return tracks;
        }

        [RelayCommand]
        public async Task CreateNewPlaylist()
        {
            OpenCreatePlaylistDialog(true);
        }

        [RelayCommand]
        public async Task EditPlaylist(PlaylistDisplayModel playlistD)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistD == null) return;

                    var Playlist = new Playlist
                    {
                        PlaylistID = playlistD.PlaylistID, // Keep the original ID
                        Title = playlistD.Title,
                        ProfileID = playlistD.ProfileID,
                        CreatedAt = playlistD.CreatedAt, // Keep original creation date
                        UpdatedAt = DateTime.UtcNow
                    };
                    OpenCreatePlaylistDialog(false, Playlist);
                },
                "Editing playlist",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task OpenCreatePlaylistDialog(bool IsCreate, Playlist playlistToEdit = null)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
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

                        _isAllPlaylistsLoaded = false;
                        await LoadInitialPlaylists();
                    }
                },
                IsCreate ? "Opening create playlist dialog" : "Opening edit playlist dialog",
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task DeletePlaylist(PlaylistDisplayModel playlistD)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistD == null || playlistD.IsFavoritePlaylist) return;

                    // Remove any associated tracks
                    var playlistTracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlistD.PlaylistID);
                    if (playlistTracks.Count > 0)
                    {
                        await _playlistTracksService.DeletePlaylistTrack(playlistD.PlaylistID);
                    }

                    // Delete the playlist
                    await _playlistService.DeletePlaylist(playlistD.PlaylistID);

                    // If the playlist was selected, remove it from selection
                    if (playlistD.IsSelected)
                    {
                        SelectedPlaylists.Remove(playlistD);
                        HasSelectedPlaylists = SelectedPlaylists.Count > 0;
                    }

                    // Refresh the playlists view
                    await LoadPlaylists();
                },
                "Deleting playlist",
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task AddTracksToPlaylist(int playlistId, IEnumerable<TrackDisplayModel> tracks)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (!tracks.Any() || _playlistDisplayService.IsFavoritesPlaylist(playlistId)) return;

                    var playlistTracks = new List<PlaylistTracks>();
                    var existingTracks = await _playlistTracksService.GetAllPlaylistTracks();

                    // Get the highest current track order for this playlist
                    int maxOrder = existingTracks.Count > 0
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
                },
                "Adding tracks to playlist",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task SavePlaylistTracks(IEnumerable<PlaylistTracks> playlistTracks)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (!playlistTracks.Any()) return;

                    foreach (var playlistTrack in playlistTracks)
                    {
                        await _playlistTracksService.AddPlaylistTrack(playlistTrack);
                    }

                    // Refresh playlists display after saving
                    await LoadPlaylists();
                },
                "Saving playlist tracks",
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<List<TrackDisplayModel>> GetSelectedPlaylistTracks(int playlistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var selectedPlaylist = SelectedPlaylists;
                    if (selectedPlaylist.Count <= 1)
                    {
                        return await _playlistDisplayService.GetPlaylistTracksAsync(playlistID);
                    }

                    var trackTasks = selectedPlaylist.Select(playlist =>
                        _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID));

                    var allTrackLists = await Task.WhenAll(trackTasks);
                    return allTrackLists.SelectMany(tracks => tracks).ToList();
                },
                "Getting selected playlist tracks",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(PlaylistDisplayModel playlist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
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
                },
                "Showing playlist selection dialog for playlist tracks",
                ErrorSeverity.NonCritical,
                true);
        }
    }
}