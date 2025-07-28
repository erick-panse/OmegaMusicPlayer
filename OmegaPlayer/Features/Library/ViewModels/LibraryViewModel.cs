using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Windows.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using Avalonia;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using OmegaPlayer.Features.Playlists.Views;
using OmegaPlayer.Core.Messages;
using NAudio.Wave;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Features.Shell.Views;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Core.Enums;
using System.Collections.Concurrent;
using System.Threading;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public enum ViewType
    {
        List,
        Card,
        Image,
        RoundImage
    }

    public enum ContentType
    {
        Home,
        Search,
        Library,
        Artist,
        Album,
        Genre,
        Playlist,
        Folder,
        Config,
        Details,
        NowPlaying,
        Lyrics,
        VirtualizationTest
    }

    public partial class LibraryViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private AsyncRelayCommand _loadMoreItemsCommand;
        public ICommand LoadMoreItemsCommand => _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        private readonly TrackDisplayService _trackDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly TrackControlViewModel _trackControlViewModel;
        private readonly MainViewModel _mainViewModel;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly GenreDisplayService _genreDisplayService;
        private readonly PlaylistDisplayService _playlistDisplayService;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly TrackStatsService _trackStatsService;
        private readonly LocalizationService _localizationService;
        private readonly StandardImageService _standardImageService;

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.Card;

        [ObservableProperty]
        private ContentType _contentType = ContentType.Library;

        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _selectedTracks = new();

        public ObservableCollection<TrackDisplayModel> Tracks { get; } = new();

        public List<TrackDisplayModel> AllTracks { get; set; }

        [ObservableProperty]
        private bool _hasSelectedTracks;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private double _loadingProgress;

        [ObservableProperty]
        private string _playButtonText;

        [ObservableProperty]
        private bool _hasNoTracks;

        [ObservableProperty]
        private ObservableCollection<PlaylistDisplayModel> _availablePlaylists = new();

        private bool _isApplyingSort = false;
        private bool _isTracksLoaded = false;
        private bool _isAllTracksLoaded = false;
        private bool _isInitializing = false;
        private CancellationTokenSource _loadingCancellationTokenSource;

        public bool ShowPlayButton => !HasNoTracks;
        public bool ShowMainActions => !HasNoTracks;

        [ObservableProperty]
        private int _dropIndex = -1;

        [ObservableProperty]
        private bool _showDropIndicator;

        #region properties required to hide the content specific to details view model
        [ObservableProperty]
        private bool _isReorderMode = false;

        [ObservableProperty]
        private bool _isPlaylistContent = false;

        [ObservableProperty]
        private bool _hideRemoveFromPlaylist = true; // "true" to hide the Remove button

        [ObservableProperty]
        private bool _isNowPlayingContent = false;

        [ObservableProperty]
        private TrackDisplayModel _draggedTrack = null;
        #endregion

        public LibraryViewModel(
            TrackDisplayService trackDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            AllTracksRepository allTracksRepository,
            TrackControlViewModel trackControlViewModel,
            MainViewModel mainViewModel,
            ArtistDisplayService artistDisplayService,
            AlbumDisplayService albumDisplayService,
            GenreDisplayService genreDisplayService,
            PlaylistDisplayService playlistDisplayService,
            PlaylistsViewModel playlistViewModel,
            TrackSortService trackSortService,
            TrackStatsService trackStatsService,
            LocalizationService localizationService,
            StandardImageService standardImageService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
            : base(trackSortService, messenger, errorHandlingService)
        {
            _trackDisplayService = trackDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _allTracksRepository = allTracksRepository;
            _trackControlViewModel = trackControlViewModel;
            _mainViewModel = mainViewModel;
            _artistDisplayService = artistDisplayService;
            _albumDisplayService = albumDisplayService;
            _genreDisplayService = genreDisplayService;
            _playlistDisplayService = playlistDisplayService;
            _playlistViewModel = playlistViewModel;
            _trackStatsService = trackStatsService;
            _localizationService = localizationService;
            _standardImageService = standardImageService;

            CurrentViewType = _mainViewModel.CurrentViewType;

            LoadAvailablePlaylists();
            UpdatePlayButtonText();

            // Subscribe to property changes
            _trackControlViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TrackControlViewModel.CurrentlyPlayingTrack))
                {
                    UpdateTrackPlayingStatus(_trackControlViewModel.CurrentlyPlayingTrack);
                }
            };

            // Mark as false to load all tracks 
            _messenger.Register<AllTracksInvalidatedMessage>(this, (r, m) =>
            {
                _isAllTracksLoaded = false;
                _isTracksLoaded = false;
            });

            // Pre-load AllTracks in background but don't populate UI yet
            _ = Task.Run(async () =>
            {
                try
                {
                    await LoadAllTracksAsync();
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Error pre-loading AllTracks",
                        ex.Message,
                        ex,
                        false);
                }
            });
        }

        protected override async void ApplyCurrentSort()
        {
            // Skip sorting if in NowPlaying / is already running / is playlist
            if (ContentType == ContentType.NowPlaying || _isApplyingSort || ContentType == ContentType.Playlist)
                return;

            // Cancel any ongoing loading
            _loadingCancellationTokenSource?.Cancel();

            _isApplyingSort = true;

            try
            {
                // Clear existing tracks and reload with new sort
                _isTracksLoaded = false;

                // Small delay to ensure cancellation is processed
                await Task.Delay(10);

                // Reset cancellation token source for new operation
                _loadingCancellationTokenSource?.Dispose();
                _loadingCancellationTokenSource = new CancellationTokenSource();

                await LoadMoreItems();
            }
            finally
            {
                _isApplyingSort = false;
            }
        }

        public override void OnSortSettingsReceived(SortType sortType, SortDirection direction, bool isUserInitiated = false)
        {
            // Library view always accepts sort settings
            // Update the sort settings
            CurrentSortType = sortType;
            CurrentSortDirection = direction;

            // Apply the new sort if we're initialized AND this is user-initiated
            if (isUserInitiated && _isAllTracksLoaded)
            {
                ApplyCurrentSort();
            }
        }

        [RelayCommand]
        public void ChangeViewType(string viewType)
        {
            CurrentViewType = viewType.ToLower() switch
            {
                "list" => ViewType.List,
                "card" => ViewType.Card,
                "image" => ViewType.Image,
                "roundimage" => ViewType.RoundImage,
                _ => ViewType.Card
            };
        }

        public async Task Initialize(bool forceReload = false)
        {
            // Prevent multiple initializations
            if (_isInitializing) return;

            _isInitializing = true;

            try
            {
                ContentType = ContentType.Library;
                ClearSelection();

                // Small delay to let MainViewModel send sort settings first
                await Task.Delay(1);

                if (forceReload || !_isTracksLoaded)
                {
                    await LoadInitialTracksAsync();
                }
            }
            finally
            {
                _isInitializing = false;
            }
        }

        public async Task LoadInitialTracksAsync()
        {
            _isTracksLoaded = false;

            // Ensure AllTracks is loaded first (might already be loaded from constructor)
            if (!_isAllTracksLoaded)
            {
                await LoadAllTracksAsync();
            }

            await LoadMoreItems();
            HasNoTracks = !Tracks.Any();
        }

        /// <summary>
        /// Loads AllTracks in background without affecting UI
        /// </summary>
        private async Task LoadAllTracksAsync()
        {
            if (_isAllTracksLoaded) return;

            try
            {
                await _allTracksRepository.LoadTracks();
                AllTracks = _allTracksRepository.AllTracks.ToList();
                _isAllTracksLoaded = true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading AllTracks from database",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Notifies about track visibility changes and loads images for visible tracks
        /// </summary>
        public async Task NotifyTrackVisible(TrackDisplayModel track, bool isVisible)
        {
            if (track?.CoverPath == null) return;

            try
            {
                // Notify the image service about visibility changes
                if (_standardImageService != null)
                {
                    await _standardImageService.NotifyImageVisible(track.CoverPath, isVisible);
                }

                // Load image when track becomes visible
                if (isVisible)
                {
                    // Load the image in the background with lower priority
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _trackDisplayService.LoadTrackCoverAsync(track, "low", isVisible);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error loading track image",
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
                    "Error handling track visibility notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Load Tracks to UI with selected sort order.
        /// With virtualization, images are loaded only when items become visible.
        /// </summary>
        private async Task LoadMoreItems()
        {
            if (IsLoading || _isTracksLoaded) return;

            // Cancel any previous loading operation
            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _loadingCancellationTokenSource.Token;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // If AllTracks not loaded yet, return empty
                if (!_isAllTracksLoaded || AllTracks?.Any() != true)
                {
                    return;
                }

                // Clear tracks immediately on UI thread
                Tracks.Clear();

                // Get sorted tracks
                var sortedTracks = await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sorted = GetSortedAllTracks();
                    var processed = new List<TrackDisplayModel>();

                    // Pre-process all tracks in background
                    for (int i = 0; i < sorted.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var track = sorted[i];

                        // Set currently playing status if needed
                        if (_trackControlViewModel.CurrentlyPlayingTrack != null)
                        {
                            track.IsCurrentlyPlaying = track.TrackID == _trackControlViewModel.CurrentlyPlayingTrack.TrackID;
                        }

                        // Set positions
                        track.Position = i;
                        track.NowPlayingPosition = i;

                        // Fix artist formatting
                        if (track.Artists?.Any() == true)
                        {
                            track.Artists.Last().IsLastArtist = false;
                        }

                        processed.Add(track);

                        // Update progress
                        if (i % 100 == 0)
                        {
                            var progress = (i * 100.0) / sorted.Count;
                            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                LoadingProgress = Math.Min(95, progress);
                            }, Avalonia.Threading.DispatcherPriority.Background);
                        }
                    }

                    return processed;
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Add all tracks to UI in one operation - images load via virtualization events
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var track in sortedTracks)
                    {
                        Tracks.Add(track);
                    }

                    LoadingProgress = 100;
                }, Avalonia.Threading.DispatcherPriority.Background);

                _isTracksLoaded = true;
            }
            catch (OperationCanceledException)
            {
                // Loading was cancelled, this is expected
                _isTracksLoaded = false;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading track library",
                    ex.Message,
                    ex,
                    true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private ObservableCollection<TrackDisplayModel> GetSortedAllTracks()
        {
            if (AllTracks == null || !AllTracks.Any())
                return new ObservableCollection<TrackDisplayModel>();

            var sortedTracks = _trackSortService.SortTracks(
                AllTracks,
                CurrentSortType,
                CurrentSortDirection
            );

            return new ObservableCollection<TrackDisplayModel>(sortedTracks);
        }

        [RelayCommand]
        private void TrackSelection(TrackDisplayModel track)
        {
            if (track == null) return;

            if (track.IsSelected)
            {
                SelectedTracks.Add(track);
            }
            else
            {
                SelectedTracks.Remove(track);
            }

            HasSelectedTracks = SelectedTracks.Count > 0;

            UpdatePlayButtonText();
        }

        [RelayCommand]
        public void SelectAll()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    SelectedTracks.Clear();
                    foreach (var track in Tracks)
                    {
                        track.IsSelected = true;
                        SelectedTracks.Add(track);
                    }
                    HasSelectedTracks = SelectedTracks.Count > 0;
                    UpdatePlayButtonText();
                },
                "Selecting all tracks",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public void ClearSelection()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    foreach (var track in Tracks)
                    {
                        track.IsSelected = false;
                    }
                    SelectedTracks.Clear();
                    HasSelectedTracks = SelectedTracks.Count > 0;
                    UpdatePlayButtonText();
                },
                "Deselecting all tracks",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task OpenArtist(Artists artist)
        {
            var artistDisplay = await _artistDisplayService.GetArtistByIdAsync(artist.ArtistID);
            if (artistDisplay != null)
            {
                _messenger.Send(new NavigationRequestMessage(ContentType.Artist, artistDisplay));
            }
        }

        [RelayCommand]
        public async Task OpenAlbum(int albumID)
        {
            var albumDisplay = await _albumDisplayService.GetAlbumByIdAsync(albumID);
            if (albumDisplay != null)
            {
                _messenger.Send(new NavigationRequestMessage(ContentType.Album, albumDisplay));
            }
        }

        [RelayCommand]
        public async Task OpenGenre(string genreName)
        {
            var genreDisplay = await _genreDisplayService.GetGenreByNameAsync(genreName);
            if (genreDisplay != null)
            {
                _messenger.Send(new NavigationRequestMessage(ContentType.Genre, genreDisplay));
            }
        }

        private void UpdateTrackPlayingStatus(TrackDisplayModel currentTrack)
        {
            if (currentTrack == null) return;

            foreach (var track in Tracks)
            {
                if (ContentType == ContentType.Playlist)
                {
                    track.IsCurrentlyPlaying = track.PlaylistPosition == currentTrack.PlaylistPosition;
                }
                else if (ContentType == ContentType.NowPlaying)
                {
                    track.IsCurrentlyPlaying = track.NowPlayingPosition == _trackQueueViewModel.GetCurrentTrackIndex();
                }
                else
                {
                    track.IsCurrentlyPlaying = track.TrackID == currentTrack.TrackID;
                }
            }
        }

        [RelayCommand]
        public void PlayAllOrSelected()
        {
            var selectedTracks = SelectedTracks;
            if (selectedTracks.Count > 0)
            {
                _trackQueueViewModel.PlayThisTrack(selectedTracks.First(), selectedTracks);
            }
            else if (Tracks.Count > 0)
            {
                var sortedTracks = GetSortedAllTracks();
                if (sortedTracks.Count > 0)
                {
                    _trackQueueViewModel.PlayThisTrack(sortedTracks.First(), sortedTracks);
                }
            }
        }

        [RelayCommand]
        public async Task RandomizeTracks()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (HasNoTracks) return;

                    var sortedTracks = GetSortedAllTracks();

                    // Start new queue with flag to shuffle queue
                    await _trackQueueViewModel.PlayThisTrack(sortedTracks.First(), sortedTracks, true);
                },
                "Randomizing track playback order",
                ErrorSeverity.Playback,
                true);
        }

        private void UpdatePlayButtonText()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    PlayButtonText = SelectedTracks.Count > 0
                        ? _localizationService["PlaySelected"]
                        : _localizationService["PlayAll"];
                },
                "Updating play button text",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public void AddToQueue(TrackDisplayModel track = null)
        {
            // Add a list of tracks at the end of queue
            var tracksList = track == null || SelectedTracks.Count > 0
                ? SelectedTracks
                : new ObservableCollection<TrackDisplayModel>();

            if (tracksList.Count < 1 && track != null)
            {
                tracksList.Add(track);
            }

            _trackQueueViewModel.AddTrackToQueue(tracksList);
            ClearSelection();
        }

        [RelayCommand]
        public void PlayNextTracks(TrackDisplayModel track = null)
        {
            // Add a list of tracks to play next
            var tracksList = track == null || SelectedTracks.Count > 0
                ? SelectedTracks
                : new ObservableCollection<TrackDisplayModel>();

            if (tracksList.Count < 1 && track != null)
            {
                tracksList.Add(track);
            }

            _trackQueueViewModel.AddToPlayNext(tracksList);
            ClearSelection();
        }

        [RelayCommand]
        public async Task PlayTrack(TrackDisplayModel track)
        {
            if (track == null) return;

            var sortedTracks = GetSortedAllTracks();
            var trackToPlay = sortedTracks.FirstOrDefault(t => t.TrackID == track.TrackID && t.Position == track.Position);

            if (trackToPlay == null) return;

            await _trackControlViewModel.PlayCurrentTrack(trackToPlay, sortedTracks);
        }

        private async void LoadAvailablePlaylists()
        {
            var playlists = await _playlistDisplayService.GetAllPlaylistDisplaysAsync();
            AvailablePlaylists.Clear();
            foreach (var playlist in playlists)
            {
                AvailablePlaylists.Add(playlist);
            }
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(TrackDisplayModel track = null)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var mainWindow = desktop.MainWindow;
                        if (mainWindow == null || !mainWindow.IsVisible) return;

                        var selectedTracks = track == null || SelectedTracks.Count > 0
                            ? SelectedTracks
                            : new ObservableCollection<TrackDisplayModel>();

                        if (selectedTracks.Count < 1 && track != null)
                        {
                            selectedTracks.Add(track);
                        }

                        // if no selected tracks the track passed is null, stop here
                        if (selectedTracks.Count <= 1 && selectedTracks[0] == null) return;

                        var dialog = new PlaylistSelectionDialog();
                        dialog.Initialize(_playlistViewModel, this, selectedTracks);
                        await dialog.ShowDialog(mainWindow);

                        ClearSelection();
                    }
                },
                "Showing playlist selection dialog",
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        private async Task ToggleTrackLike(TrackDisplayModel track)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null) return;

                    track.IsLiked = !track.IsLiked;
                    track.LikeIcon = Application.Current?.FindResource(
                        track.IsLiked ? "LikeOnIcon" : "LikeOffIcon");

                    await _trackStatsService.UpdateTrackLike(track.TrackID, track.IsLiked);
                },
                "Toggling track favorite status",
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task ShowTrackProperties(TrackDisplayModel track)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var mainWindow = desktop.MainWindow;
                        if (mainWindow == null || !mainWindow.IsVisible) return;

                        var dialog = new TrackPropertiesDialog();
                        dialog.Initialize(track);
                        await dialog.ShowDialog(mainWindow);
                    }
                },
                "Showing track properties dialog",
                ErrorSeverity.NonCritical,
                true);
        }
    }
}