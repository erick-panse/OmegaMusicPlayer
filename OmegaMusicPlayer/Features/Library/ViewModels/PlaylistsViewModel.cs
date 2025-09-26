using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Enums.LibraryEnums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Messages;
using OmegaMusicPlayer.Features.Configuration.ViewModels;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Library.Services;
using OmegaMusicPlayer.Features.Playback.ViewModels;
using OmegaMusicPlayer.Features.Playlists.Models;
using OmegaMusicPlayer.Features.Playlists.Services;
using OmegaMusicPlayer.Features.Playlists.Views;
using OmegaMusicPlayer.Features.Shell.ViewModels;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Library.ViewModels
{
    public partial class PlaylistsViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly PlaylistDisplayService _playlistDisplayService;
        private readonly PlaylistService _playlistService;
        private readonly PlaylistTracksService _playlistTracksService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly StandardImageService _standardImageService;
        private readonly LocalizationService _localizationService;
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
            LocalizationService localizationService,
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
            _localizationService = localizationService;
            _mainViewModel = mainViewModel;

            // Register for like updates and profile switch to keep favorites playlist in sync
            _messenger.Register<TrackLikeUpdateMessage>(this, (r, m) => UpdatePlaylist());

            // Register for playlist updates from other ViewModels
            _messenger.Register<PlaylistUpdateMessage>(this, (r, m) => UpdatePlaylist());

            // Register for language changes to update localized playlist names
            _messenger.Register<LanguageChangedMessage>(this, (r, m) => UpdatePlaylist());

            // Mark as false to load all tracks 
            _messenger.Register<AllTracksInvalidatedMessage>(this, (r, m) =>
            {
                _isAllPlaylistsLoaded = false;
                _isPlaylistsLoaded = false;

                // Clear UI for empty library case
                Playlists.Clear();
                AllPlaylists = new List<PlaylistDisplayModel>();
                ClearSelection();
            });
        }

        private void UpdatePlaylist()
        {
            // Refresh playlists when any playlist is changed / created
            _isInitializing = false;
            _isAllPlaylistsLoaded = false;
            _ = LoadInitialPlaylists();
        }

        protected override async void ApplyCurrentSort()
        {
            // Skip sorting if it is already running
            if (_isApplyingSort)
                return;

            // Cancel any ongoing loading operation
            _loadingCancellationTokenSource?.Cancel();

            _isApplyingSort = true;

            try
            {
                // Reset loading state
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
                if (isVisible)
                {
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
                var sortedPlaylists = await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sorted = GetSortedAllPlaylists();
                    var processed = new List<PlaylistDisplayModel>();
                    int progress = 0;

                    // Pre-process all playlists in background
                    foreach (var playlist in sorted)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        processed.Add(playlist);

                        progress++;
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LoadingProgress = Math.Min(95, (int)((progress * 100.0) / sorted.Count()));
                        }, Avalonia.Threading.DispatcherPriority.Background);
                    }

                    return processed;
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var playlist in sortedPlaylists)
                    {
                        Playlists.Add(playlist);
                    }

                    LoadingProgress = 100;
                }, Avalonia.Threading.DispatcherPriority.Background);

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
                await _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
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
            await OpenCreatePlaylistDialog(true);
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
                    await OpenCreatePlaylistDialog(false, Playlist);
                },
                _localizationService["EditPlaylistError"],
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
                    }
                },
                IsCreate ? _localizationService["CreatePlaylistDialogError"] : _localizationService["EditPlaylistDialogError"],
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

                    // Send deletion message
                    _messenger.Send(new PlaylistUpdateMessage());

                    // If the playlist was selected, remove it from selection
                    if (playlistD.IsSelected)
                    {
                        SelectedPlaylists.Remove(playlistD);
                        HasSelectedPlaylists = SelectedPlaylists.Count > 0;
                    }
                },
                _localizationService["ErrorDeletingPlaylist"],
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task AddTracksToPlaylist(int playlistId, IEnumerable<TrackDisplayModel> tracks)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (!tracks.Any() || await _playlistDisplayService.IsFavoritesPlaylistAsync(playlistId)) return;

                    var playlistTracks = new List<PlaylistTracks>();
                    var existingTracks = await _playlistTracksService.GetAllPlaylistTracksForPlaylist(playlistId);

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

                    // Send tracks changed message
                    _messenger.Send(new PlaylistUpdateMessage());
                },
                _localizationService["ErrorAddingTracksPlaylist"],
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
                },
                _localizationService["ErrorSavingPlaylistTracks"],
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
                _localizationService["ShowingPlaylistDialogError"],
                ErrorSeverity.NonCritical,
                true);
        }
    }
}