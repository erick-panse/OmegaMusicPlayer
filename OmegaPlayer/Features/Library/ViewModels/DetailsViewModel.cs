using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NAudio.Wave;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Enums.LibraryEnums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Playlists.Views;
using OmegaPlayer.Features.Shell.Views;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.UI;
using OmegaPlayer.UI.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class DetailsViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly TrackDisplayService _trackDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly TrackControlViewModel _trackControlViewModel;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly GenreDisplayService _genreDisplayService;
        private readonly FolderDisplayService _folderDisplayService;
        private readonly PlaylistDisplayService _playlistDisplayService;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly PlaylistTracksService _playlistTracksService;
        private readonly MediaService _mediaService;
        private readonly TrackStatsService _trackStatsService;
        private readonly ProfileManager _profileManager;
        private readonly QueueService _queueService;
        private readonly LocalizationService _localizationService;
        private readonly StandardImageService _standardImageService;

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.Card;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private Bitmap _image;

        [ObservableProperty]
        private object _detailsIcon;

        [ObservableProperty]
        private ContentType _contentType;

        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _selectedTracks = new();

        public ObservableCollection<TrackDisplayModel> Tracks { get; } = new();

        public List<TrackDisplayModel> AllTracks { get; set; }

        [ObservableProperty]
        private bool _hasSelectedTracks;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private double _loadingProgress;

        [ObservableProperty]
        private bool _isPlaylistContent;

        [ObservableProperty]
        private bool _hideRemoveFromPlaylist;

        [ObservableProperty]
        private bool _isNowPlayingContent;

        [ObservableProperty]
        private bool _isArtistContent;

        [ObservableProperty]
        private string _playButtonText;

        [ObservableProperty]
        private bool _hasNoTracks;

        [ObservableProperty]
        private string _contentTypeText;

        [ObservableProperty]
        private object _currentData;

        [ObservableProperty]
        private ObservableCollection<PlaylistDisplayModel> _availablePlaylists = new();

        private object _currentContent;
        private bool _isApplyingSort = false;
        private bool _isAllTracksLoaded = false;
        private bool _isTracksLoaded = false;
        private bool _isInitializing = false;
        private CancellationTokenSource _loadingCancellationTokenSource;

        // Track which tracks have had their images loaded to avoid redundant loading
        private readonly ConcurrentDictionary<Guid, bool> _tracksWithLoadedImages = new();
        private readonly ConcurrentDictionary<string, Bitmap> _sharedImages = new();

        public bool ShowPlayButton => !HasNoTracks;
        public bool ShowMainActions => !HasNoTracks;

        [ObservableProperty]
        private bool _isReorderMode;

        [ObservableProperty]
        private TrackDisplayModel _draggedTrack;

        [ObservableProperty]
        private int _dropIndex = -1;

        [ObservableProperty]
        private bool _showDropIndicator;

        private List<TrackDisplayModel> _originalOrder;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public ICommand LoadMoreItemsCommand => _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public DetailsViewModel(
            TrackDisplayService trackDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            TrackControlViewModel trackControlViewModel,
            ArtistDisplayService artistDisplayService,
            AlbumDisplayService albumDisplayService,
            GenreDisplayService genreDisplayService,
            FolderDisplayService folderDisplayService,
            PlaylistDisplayService playlistDisplayService,
            PlaylistsViewModel playlistViewModel,
            TrackSortService trackSortService,
            PlaylistTracksService playlistTracksService,
            MediaService mediaService,
            TrackStatsService trackStatsService,
            QueueService queueService,
            ProfileManager profileManager,
            LocalizationService localizationService,
            StandardImageService standardImageService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
            : base(trackSortService, messenger, errorHandlingService)
        {
            _trackDisplayService = trackDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _trackControlViewModel = trackControlViewModel;
            _artistDisplayService = artistDisplayService;
            _albumDisplayService = albumDisplayService;
            _genreDisplayService = genreDisplayService;
            _folderDisplayService = folderDisplayService;
            _playlistDisplayService = playlistDisplayService;
            _playlistViewModel = playlistViewModel;
            _playlistTracksService = playlistTracksService;
            _mediaService = mediaService;
            _trackStatsService = trackStatsService;
            _queueService = queueService;
            _profileManager = profileManager;
            _localizationService = localizationService;
            _standardImageService = standardImageService;

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
        }

        protected override async void ApplyCurrentSort()
        {
            // Skip sorting if in NowPlaying / is already running / is playlist
            if (ContentType == ContentType.NowPlaying || _isApplyingSort || ContentType == ContentType.Playlist)
                return;

            // Cancel any ongoing loading operation
            _loadingCancellationTokenSource?.Cancel();

            _isApplyingSort = true;

            try
            {
                // Reset loading state and clear cached images
                _tracksWithLoadedImages.Clear();
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
            // Skip sort updates for non-sortable content types
            if (ContentType == ContentType.NowPlaying || ContentType == ContentType.Playlist)
                return;

            // Update the sort settings
            CurrentSortType = sortType;
            CurrentSortDirection = direction;

            // Only apply the new sort if this is user-initiated
            if (isUserInitiated)
            {
                ApplyCurrentSort();
            }
        }

        public async Task Initialize(ContentType type, object data)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Prevent multiple initializations
                    if (_isInitializing) return;

                    _isInitializing = true;

                    try
                    {
                        // Cancel any ongoing loading operation
                        _loadingCancellationTokenSource?.Cancel();

                        // Small delay to let MainViewModel send sort settings first
                        await Task.Delay(1);

                        _loadingCancellationTokenSource?.Dispose();
                        _loadingCancellationTokenSource = new CancellationTokenSource();

                        ContentType = type;
                        ChangeContentTypeText(type);
                        IsNowPlayingContent = type == ContentType.NowPlaying;
                        IsPlaylistContent = type == ContentType.Playlist;
                        ClearSelection();
                        _trackControlViewModel.IsNowPlayingOpen = IsNowPlayingContent; // Show TrackControl indicator

                        CurrentData = data;
                        LoadContent(data);
                        LoadAvailablePlaylists();

                        // Reset loading state and clear cached images
                        _tracksWithLoadedImages.Clear();
                        _isTracksLoaded = false;
                        _isAllTracksLoaded = false;

                        // Small delay to let MainViewModel send sort settings first
                        await Task.Delay(1);

                        await LoadAllTracksAsync();
                        await LoadMoreItems();
                    }
                    finally
                    {
                        _isInitializing = false;
                    }
                },
                $"Initializing details view for {type}",
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task LoadAllTracksAsync()
        {
            if (_isAllTracksLoaded) return;

            try
            {
                AllTracks = await LoadTracksForContent(1, int.MaxValue);
                _isAllTracksLoaded = true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading AllTracks from content",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Notifies the image loading system about track visibility changes and loads images for visible tracks
        /// </summary>
        public async Task NotifyTrackVisible(TrackDisplayModel track, bool isVisible)
        {
            if (track?.CoverPath == null) return;

            try
            {
                // Notify the image service about visibility changes for optimization
                if (_standardImageService != null)
                {
                    await _standardImageService.NotifyImageVisible(track.CoverPath, isVisible);
                }

                // Update NotifyTrackVisible method:
                if (isVisible && !_tracksWithLoadedImages.ContainsKey(track.InstanceId))
                {
                    _tracksWithLoadedImages[track.InstanceId] = true;

                    // Check if we already have this image loaded for another track instance
                    if (_sharedImages.TryGetValue(track.CoverPath, out var existingBitmap))
                    {
                        // Reuse the existing bitmap
                        track.Thumbnail = existingBitmap;
                    }
                    else
                    {
                        // Load the image for the first time
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _trackDisplayService.LoadTrackCoverAsync(track, "low", isVisible);

                                // Store the loaded image for reuse
                                if (track.Thumbnail != null)
                                {
                                    _sharedImages[track.CoverPath] = track.Thumbnail;

                                    // Apply to all other instances of this track that are visible
                                    foreach (var otherTrack in Tracks.Where(t =>
                                        t.CoverPath == track.CoverPath &&
                                        t.InstanceId != track.InstanceId &&
                                        _tracksWithLoadedImages.ContainsKey(t.InstanceId)))
                                    {
                                        otherTrack.Thumbnail = track.Thumbnail;
                                    }
                                }
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
        /// Chunked loading with UI thread yielding for better responsiveness.
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
                // If AllTracks not loaded yet, load them first
                if (!_isAllTracksLoaded)
                {
                    await LoadAllTracksAsync();
                }

                // If no tracks available, return empty
                if (!_isAllTracksLoaded || AllTracks?.Any() != true)
                {
                    HasNoTracks = true;
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

                await ScrollToCurrentlyPlayingTrack();
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
                    "Error loading track details",
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

            if (ContentType == ContentType.NowPlaying || ContentType == ContentType.Playlist)
            {
                return new ObservableCollection<TrackDisplayModel>(AllTracks);
            }

            var sortedTracks = _trackSortService.SortTracks(
                AllTracks,
                CurrentSortType,
                CurrentSortDirection
            );

            int position = 0;
            foreach (var track in sortedTracks)
            {
                track.Position = position;  // Set position to keep order of tracks
                position++;
            }

            return new ObservableCollection<TrackDisplayModel>(sortedTracks);
        }

        private void LoadContent(object data)
        {
            Tracks.Clear(); // clear tracks loaded
            _currentContent = data;

            switch (ContentType)
            {
                case ContentType.Artist:
                    LoadArtistContent(data as ArtistDisplayModel);
                    break;
                case ContentType.Album:
                    LoadAlbumContent(data as AlbumDisplayModel);
                    break;
                case ContentType.Genre:
                    LoadGenreContent(data as GenreDisplayModel);
                    break;
                case ContentType.Playlist:
                    LoadPlaylistContent(data as PlaylistDisplayModel);
                    break;
                case ContentType.Folder:
                    LoadFolderContent(data as FolderDisplayModel);
                    break;
                case ContentType.NowPlaying:
                    LoadNowPlayingContent(data as NowPlayingInfo);
                    break;
            }
        }

        private void LoadGenreContent(GenreDisplayModel genre)
        {
            if (genre == null || genre.TrackCount <= 0)
            {
                LoadEmptyContent();
                return;
            }
            Title = genre.Name;
            Description = $"{genre.TrackCount} {_localizationService["tracks"]} • {genre.TotalDuration:hh\\:mm\\:ss}";
            Image = genre.Photo;
            DetailsIcon = Application.Current.FindResource("GenreIconV2");
        }

        private void LoadPlaylistContent(PlaylistDisplayModel playlist)
        {
            if (playlist == null)
            {
                LoadEmptyContent();
                return;
            }

            if (playlist.TrackCount <= 0)
            {
                playlist.Cover = null;
            }

            Title = playlist.Title;
            Description = $"{_localizationService["PlaylistCreated"]} {playlist.CreatedAt:d} • {playlist.TrackCount} {_localizationService["tracks"]} • {playlist.TotalDuration:hh\\:mm\\:ss}";
            Image = playlist.Cover;
            DetailsIcon = Application.Current.FindResource("PlaylistIcon");
        }

        private void LoadFolderContent(FolderDisplayModel folder)
        {
            if (folder == null || folder.TrackCount <= 0)
            {
                LoadEmptyContent();
                return;
            }
            Title = folder.FolderName;
            Description = $"{folder.TrackCount} {_localizationService["tracks"]} • {folder.TotalDuration:hh\\:mm\\:ss}";
            Image = folder.Cover;
            DetailsIcon = Application.Current.FindResource("FolderIcon");
        }

        private void LoadArtistContent(ArtistDisplayModel artist)
        {
            if (artist == null || artist.TrackCount <= 0)
            {
                LoadEmptyContent();
                return;
            }
            Title = artist.Name;
            Description = $"{artist.TrackCount} {_localizationService["tracks"]} • {artist.TotalDuration:hh\\:mm\\:ss}";
            Image = artist.Photo;
            DetailsIcon = Application.Current.FindResource("ArtistIconV2");
        }

        private void LoadAlbumContent(AlbumDisplayModel album)
        {
            if (album == null || album.TrackCount <= 0)
            {
                LoadEmptyContent();
                return;
            }
            Title = album.Title;
            Description = $"{album.TrackCount} {_localizationService["tracks"]} • {album.TotalDuration:hh\\:mm\\:ss}";
            Image = album.Cover;
            DetailsIcon = Application.Current.FindResource("AlbumIcon");
        }

        private void LoadNowPlayingContent(NowPlayingInfo info)
        {
            if (info?.CurrentTrack == null)
            {
                LoadEmptyContent();
                return;
            }

            Title = info.CurrentTrack.Title;
            Description = $"{info.AllTracks.Count} {_localizationService["tracks"]} • {_localizationService["Total"]}: {_trackQueueViewModel.TotalDuration:hh\\:mm\\:ss} • Remaining: {_trackQueueViewModel.RemainingDuration:hh\\:mm\\:ss}";
            Image = info.CurrentTrack.Thumbnail;
            DetailsIcon = Application.Current.FindResource("TrackIcon");
        }

        private void LoadEmptyContent()
        {
            Title = String.Empty;
            Description = String.Empty;
            Image = null;
            DetailsIcon = null;
        }

        private async Task<List<TrackDisplayModel>> LoadTracksForContent(int page, int pageSize)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (_currentContent == null) return new List<TrackDisplayModel>();

                    IsPlaylistContent = ContentType == ContentType.Playlist;
                    IsNowPlayingContent = ContentType == ContentType.NowPlaying;
                    IsArtistContent = ContentType == ContentType.Artist;
                    HideRemoveFromPlaylist = true;  // default "true" to hide the Remove button

                    List<TrackDisplayModel> tracks = new List<TrackDisplayModel>();

                    switch (ContentType)
                    {
                        case ContentType.Artist:
                            var artist = _currentContent as ArtistDisplayModel;
                            if (artist != null)
                            {
                                tracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                            }
                            break;

                        case ContentType.Album:
                            var album = _currentContent as AlbumDisplayModel;
                            if (album != null)
                            {
                                tracks = await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                            }
                            break;

                        case ContentType.Genre:
                            var genre = _currentContent as GenreDisplayModel;
                            if (genre != null)
                            {
                                tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
                            }
                            break;

                        case ContentType.Folder:
                            var folder = _currentContent as FolderDisplayModel;
                            if (folder != null)
                            {
                                tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
                            }
                            break;

                        case ContentType.Playlist:
                            var playlist = _currentContent as PlaylistDisplayModel;
                            if (playlist != null)
                            {
                                HideRemoveFromPlaylist = playlist.IsFavoritePlaylist;
                                tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
                            }
                            break;

                        case ContentType.NowPlaying:
                            var nowPlayingInfo = _currentContent as NowPlayingInfo;
                            if (nowPlayingInfo?.CurrentTrack != null)
                            {
                                // Important: Use ToList() to create a new list that maintains the queue order
                                tracks = nowPlayingInfo.AllTracks.ToList();

                                // Update NowPlayingPosition for each track based on its queue position
                                for (int i = 0; i < tracks.Count; i++)
                                {
                                    tracks[i].NowPlayingPosition = i;
                                }
                            }
                            break;
                    }

                    // Don't load track covers here - they'll be loaded on visibility
                    return tracks;
                },
                $"Loading tracks for {ContentType}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                true);
        }

        /// <summary>
        /// Scrolls to the currently playing track in the track list
        /// </summary>
        public async Task ScrollToCurrentlyPlayingTrack()
        {
            if (ContentType != ContentType.NowPlaying || !Tracks.Any()) return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var currentTrackIndex = _trackQueueViewModel.GetCurrentTrackIndex();
                    if (currentTrackIndex >= 0 && currentTrackIndex < Tracks.Count)
                    {
                        // Send message to scroll to specific track
                        _messenger.Send(new ScrollToTrackMessage(currentTrackIndex));
                    }
                },
                "Scrolling to currently playing track",
                ErrorSeverity.NonCritical,
                false);
        }

        public void ChangeContentTypeText(ContentType type)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    ContentTypeText = type switch
                    {
                        ContentType.Library => _localizationService["Library"],
                        ContentType.Artist => _localizationService["Artists"],
                        ContentType.Album => _localizationService["Album"],
                        ContentType.Genre => _localizationService["Genre"],
                        ContentType.Playlist => _localizationService["Playlists"],
                        ContentType.NowPlaying => _localizationService["NowPlaying"],
                        ContentType.Folder => _localizationService["Folder"],
                        _ => _localizationService["Library"]
                    };
                },
                "Updating content type header",
                ErrorSeverity.NonCritical,
                false);
        }

        // Track Selection and Navigation Methods
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
                // Navigation handled by MainViewModel
                _messenger.Send(new NavigationRequestMessage(ContentType.Artist, artistDisplay));
            }
        }

        [RelayCommand]
        public async Task OpenAlbum(int albumID)
        {
            var albumDisplay = await _albumDisplayService.GetAlbumByIdAsync(albumID);
            if (albumDisplay != null)
            {
                // Navigation handled by MainViewModel
                _messenger.Send(new NavigationRequestMessage(ContentType.Album, albumDisplay));
            }
        }

        [RelayCommand]
        public async Task OpenGenre(string genreName)
        {
            var genreDisplay = await _genreDisplayService.GetGenreByNameAsync(genreName);
            if (genreDisplay != null)
            {
                // Navigation handled by MainViewModel
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
        public async Task PlayAllOrSelected()
        {
            var selectedTracks = SelectedTracks;
            if (selectedTracks.Count > 0)
            {
                await _trackQueueViewModel.PlayThisTrack(selectedTracks.First(), selectedTracks);
            }
            else if (Tracks.Count > 0)
            {
                var sortedTracks = GetSortedAllTracks();
                if (sortedTracks.Count > 0)
                {
                    await _trackQueueViewModel.PlayThisTrack(sortedTracks.First(), sortedTracks);
                }
            }
        }

        [RelayCommand]
        public async Task RandomizeTracks()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (IsNowPlayingContent || HasNoTracks) return;

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
        public async Task RemoveTracksFromPlaylist(TrackDisplayModel track)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var selectedTracks = SelectedTracks.Count > 0 == false
                        ? new List<TrackDisplayModel> { track }
                        : SelectedTracks.ToList();

                    if (selectedTracks.Count > 0 && ContentType == ContentType.Playlist)
                    {
                        var playlist = _currentContent as PlaylistDisplayModel;
                        if (playlist != null)
                        {
                            // Get all tracks for this playlist
                            var allPlaylistTracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);

                            // Get positions to remove
                            var positionsToRemove = selectedTracks
                                .Select(t => t.PlaylistPosition)
                                .Where(pos => pos >= 0)
                                .ToHashSet();

                            // Filter out tracks that should be removed
                            var remainingTracks = allPlaylistTracks
                                .Where((t, index) => !positionsToRemove.Contains(index))
                                .ToList();

                            // Remove tracks from the playlist in the database
                            await _playlistTracksService.DeletePlaylistTrack(playlist.PlaylistID);

                            // Update track IDs list in playlist display model to reflect the new count
                            playlist.TrackIDs = remainingTracks.Select(t => t.TrackID).ToList();

                            // Re-add the remaining tracks with updated order
                            for (int i = 0; i < remainingTracks.Count; i++)
                            {
                                await _playlistTracksService.AddPlaylistTrack(new PlaylistTracks
                                {
                                    PlaylistID = playlist.PlaylistID,
                                    TrackID = remainingTracks[i].TrackID,
                                    TrackOrder = i
                                });
                            }

                            // Update duration
                            playlist.TotalDuration = TimeSpan.FromTicks(remainingTracks.Sum(t => t.Duration.Ticks));

                            // Update cover if needed (if first track changed)
                            if (remainingTracks.Count > 0)
                            {
                                var firstTrack = remainingTracks.First();
                                var media = await _mediaService.GetMediaById(firstTrack.CoverID);
                                if (media != null && media.CoverPath != playlist.CoverPath)
                                {
                                    playlist.CoverPath = media.CoverPath;
                                    await _playlistDisplayService.LoadPlaylistCoverAsync(playlist, "medium", true);
                                    // Update the display image
                                    Image = playlist.Cover;
                                }
                            }

                            // Refresh the view
                            LoadContent(_currentContent);
                        }
                    }
                    ClearSelection();
                },
                "Removing tracks from playlist",
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task RemoveTracksFromNowPlaying(TrackDisplayModel track)
        {
            if (ContentType != ContentType.NowPlaying) return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Get tracks to remove (either selected tracks or the passed track)
                    var tracksToRemove = SelectedTracks.Count > 0
                    ? SelectedTracks.ToList()
                    : new List<TrackDisplayModel> { track };

                    // Get the positions of the tracks to remove
                    var positionsToRemove = tracksToRemove
                        .Select(t => t.NowPlayingPosition)
                        .Where(pos => pos >= 0)
                        .OrderBy(pos => pos)
                        .ToList();

                    if (!positionsToRemove.Any()) return;

                    // Get current queue and current track index
                    var currentQueue = _trackQueueViewModel.NowPlayingQueue.ToList();
                    int currentIndex = _trackQueueViewModel.GetCurrentTrackIndex();
                    bool removingCurrentTrack = currentIndex >= 0 && positionsToRemove.Contains(currentIndex);

                    // Create new queue without the tracks at the positions to remove
                    var newQueue = new List<TrackDisplayModel>();
                    for (int i = 0; i < currentQueue.Count; i++)
                    {
                        if (!positionsToRemove.Contains(i))
                        {
                            newQueue.Add(currentQueue[i]);
                        }
                    }

                    // Handle empty queue case
                    if (!newQueue.Any())
                    {
                        // Stop playback and clear queue
                        _trackControlViewModel.StopPlayback();

                        var profile = await _profileManager.GetCurrentProfileAsync();

                        await _queueService.ClearCurrentQueueForProfile(profile.ProfileID);
                        _trackQueueViewModel.NowPlayingQueue.Clear();
                        _trackQueueViewModel.CurrentTrack = null;
                        await _trackControlViewModel.UpdateTrackInfo();

                        // Refresh view with empty queue
                        LoadContent(new NowPlayingInfo
                        {
                            CurrentTrack = null,
                            AllTracks = new List<TrackDisplayModel>(),
                            CurrentTrackIndex = -1
                        });

                        ClearSelection();
                        return;
                    }

                    // Calculate new current track index
                    int newIndex;

                    if (removingCurrentTrack)
                    {
                        // Current track is being removed

                        // Try to find the next track that's not being removed
                        int nextPos = currentIndex;
                        while (nextPos < currentQueue.Count && positionsToRemove.Contains(nextPos))
                        {
                            nextPos++;
                        }

                        if (nextPos < currentQueue.Count)
                        {
                            // Found a track after the current one
                            // Calculate its new position after removal
                            int offset = positionsToRemove.Count(p => p < nextPos);
                            newIndex = nextPos - offset;
                        }
                        else
                        {
                            // No track found after current, use first available
                            newIndex = 0;
                        }
                    }
                    else
                    {
                        // Current track is not being removed
                        // Calculate its new position after removal
                        int offset = positionsToRemove.Count(p => p < currentIndex);
                        newIndex = currentIndex - offset;
                    }

                    // Ensure index is valid
                    if (newIndex < 0 || newIndex >= newQueue.Count)
                    {
                        newIndex = 0;
                    }

                    // Update NowPlayingPosition for all tracks
                    for (int i = 0; i < newQueue.Count; i++)
                    {
                        newQueue[i].NowPlayingPosition = i;
                    }

                    // Save the reordered queue
                    await _trackQueueViewModel.SaveReorderedQueue(newQueue, newIndex);

                    // If current track was removed, update playback
                    if (removingCurrentTrack)
                    {
                        var newTrack = newQueue[newIndex];
                        await _trackControlViewModel.PlayCurrentTrack(newTrack, new ObservableCollection<TrackDisplayModel>(newQueue));
                    }

                    ClearSelection();

                    // Refresh view
                    _currentContent = new NowPlayingInfo
                    {
                        CurrentTrack = _trackQueueViewModel.CurrentTrack,
                        AllTracks = newQueue,
                        CurrentTrackIndex = newIndex
                    };

                    LoadContent(_currentContent);

                    _isTracksLoaded = false;
                    _isAllTracksLoaded = false;

                    // Reload tracks and header
                    await LoadMoreItems();
                },
                "Removing tracks from now playing queue",
                ErrorSeverity.NonCritical,
                true);
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
                        dialog.Initialize(_playlistViewModel, null, selectedTracks);
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

        partial void OnDropIndexChanged(int value)
        {
            ShowDropIndicator = value >= 0;
        }

        [RelayCommand]
        private void EnterReorderMode()
        {
            IsReorderMode = true;
            _originalOrder = new List<TrackDisplayModel>(Tracks);
        }

        [RelayCommand]
        private async Task SaveReorderedTracks()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Cancel ongoing loading
                    if (IsLoading)
                    {
                        _loadingCancellationTokenSource?.Cancel();
                        // Small delay to ensure cancellation is processed
                        await Task.Delay(10);
                        _loadingCancellationTokenSource?.Dispose();
                        _loadingCancellationTokenSource = new CancellationTokenSource();
                    }

                    if (ContentType == ContentType.Playlist)
                    {

                        var playlist = _currentContent as PlaylistDisplayModel;
                        if (playlist == null) return;

                        // Do heavy work in background thread
                        var (reorderedTracks, newFirstTrack) = await Task.Run(async () =>
                        {
                            var allPlaylistTracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
                            var reordered = new List<TrackDisplayModel>();

                            // Create a map of visible tracks with their current UI positions
                            var visibleTracksOrder = new Dictionary<(int TrackId, int OriginalPosition), int>();
                            for (int i = 0; i < Tracks.Count; i++)
                            {
                                var track = Tracks[i];
                                visibleTracksOrder[(track.TrackID, track.PlaylistPosition)] = i;
                            }

                            // Add visible tracks in their new order (as shown in UI after drag-drop)
                            foreach (var track in Tracks)
                            {
                                reordered.Add(track);
                            }

                            // Add remaining non-visible tracks
                            foreach (var track in allPlaylistTracks)
                            {
                                if (!visibleTracksOrder.ContainsKey((track.TrackID, track.PlaylistPosition)))
                                {
                                    reordered.Add(track);
                                }
                            }

                            // Update positions sequentially
                            for (int i = 0; i < reordered.Count; i++)
                            {
                                reordered[i].PlaylistPosition = i;
                            }

                            var firstTrack = reordered.FirstOrDefault();
                            return (reordered, firstTrack);
                        });

                        // Save queue in background
                        await Task.Run(async () =>
                        {
                            await _playlistTracksService.UpdateTrackOrder(playlist.PlaylistID, reorderedTracks);
                        });

                        // Update cover if first track changed
                        if (newFirstTrack != null && newFirstTrack.CoverPath != playlist.CoverPath)
                        {
                            var media = await _mediaService.GetMediaById(newFirstTrack.CoverID);
                            if (media != null)
                            {
                                playlist.CoverPath = media.CoverPath;
                                await _playlistDisplayService.LoadPlaylistCoverAsync(playlist, "medium", true);

                                // Update UI on main thread
                                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    Image = playlist.Cover;
                                });
                            }
                        }

                        // Reset reorder mode after save
                        IsReorderMode = false;
                        if (DraggedTrack != null)
                        {
                            DraggedTrack.IsBeingDragged = false;
                            DraggedTrack = null;
                        }
                        DropIndex = -1;

                        _tracksWithLoadedImages.Clear();
                        _isTracksLoaded = false;
                        _isAllTracksLoaded = false;

                        // Update AllTracks with reordered data
                        AllTracks = reorderedTracks;

                        // Update the playlist object with new data
                        playlist.TrackIDs = reorderedTracks.Select(t => t.TrackID).ToList();
                        playlist.TotalDuration = TimeSpan.FromTicks(reorderedTracks.Sum(t => t.Duration.Ticks));

                        // Update current content
                        _currentContent = playlist;

                        // Reload tracks to show updated order
                        await LoadMoreItems();

                    }
                    else if (ContentType == ContentType.NowPlaying)
                    {
                        // Do heavy work in background thread
                        var (reorderedTracks, newCurrentTrackIndex) = await Task.Run(() =>
                        {
                            // Get reference to all tracks in the queue
                            var allQueueTracks = _trackQueueViewModel.NowPlayingQueue.ToList();

                            // Get the track currently playing
                            var currentTrack = allQueueTracks.FirstOrDefault(t => t.IsCurrentlyPlaying);
                            int calculatedIndex = -1;

                            // Create a map of visible tracks with their current UI positions
                            var visibleTracksOrder = new Dictionary<(int TrackId, int OriginalPosition), int>();
                            for (int i = 0; i < Tracks.Count; i++)
                            {
                                var track = Tracks[i];
                                visibleTracksOrder[(track.TrackID, track.NowPlayingPosition)] = i;
                            }

                            // Create the final ordered list
                            var reordered = new List<TrackDisplayModel>();

                            // Add visible tracks in their new order (as shown in UI after drag-drop)
                            foreach (var track in Tracks)
                            {
                                reordered.Add(track);
                            }

                            // Add remaining non-visible tracks
                            foreach (var track in allQueueTracks)
                            {
                                if (!visibleTracksOrder.ContainsKey((track.TrackID, track.NowPlayingPosition)))
                                {
                                    reordered.Add(track);
                                }
                            }

                            // Update positions to match the new order
                            for (int i = 0; i < reordered.Count; i++)
                            {
                                reordered[i].NowPlayingPosition = i;
                            }

                            if (currentTrack != null)
                            {
                                // Find its new position in reordered tracks
                                calculatedIndex = reordered.FindIndex(t =>
                                    t.TrackID == currentTrack.TrackID &&
                                    t.NowPlayingPosition == currentTrack.NowPlayingPosition);
                            }

                            return (reordered, calculatedIndex);
                        });

                        // Save queue in background
                        await Task.Run(async () =>
                        {
                            await _trackQueueViewModel.SaveReorderedQueue(reorderedTracks, newCurrentTrackIndex);
                        });

                        // Reset reorder mode after save
                        IsReorderMode = false;
                        if (DraggedTrack != null)
                        {
                            DraggedTrack.IsBeingDragged = false;
                            DraggedTrack = null;
                        }
                        DropIndex = -1;

                        // Reset loading states
                        _tracksWithLoadedImages.Clear();
                        _isTracksLoaded = false;
                        _isAllTracksLoaded = false;

                        // Create new NowPlayingInfo with reordered tracks
                        var updatedInfo = new NowPlayingInfo
                        {
                            CurrentTrack = _trackQueueViewModel.CurrentTrack,
                            AllTracks = reorderedTracks,
                            CurrentTrackIndex = newCurrentTrackIndex
                        };

                        // Update current content
                        _currentContent = updatedInfo;
                        // Reload tracks
                        await LoadMoreItems();
                    }
                },
                $"Saving reordered tracks for {ContentType}",
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public void CancelReorder()
        {
            // Restore original order
            Tracks.Clear();
            foreach (var track in _originalOrder)
            {
                Tracks.Add(track);
            }

            IsReorderMode = false;
            if (DraggedTrack != null)
            {
                DraggedTrack.IsBeingDragged = false;
                DraggedTrack = null;
            }
            DropIndex = -1;
            UpdateDropIndicators(-1);
        }

        private void UpdateDropIndicators(int dropIndex)
        {
            // Reset all indicators first
            foreach (var track in Tracks)
            {
                track.ShowDropIndicator = false;
            }

            // Show indicator only for drop target
            if (dropIndex >= 0 && dropIndex < Tracks.Count)
            {
                Tracks[dropIndex].ShowDropIndicator = true;
            }
        }

        public void HandleTrackDragStarted(TrackDisplayModel track)
        {
            DraggedTrack = track;
            DraggedTrack.IsBeingDragged = true;
            UpdateDropIndicators(-1);
        }

        public void HandleTrackDragOver(int newIndex)
        {
            if (newIndex != DropIndex)
            {
                DropIndex = newIndex;
                UpdateDropIndicators(newIndex);
            }
        }

        public void HandleTrackDrop()
        {
            if (DropIndex >= 0 && DropIndex < Tracks.Count)
            {
                int oldIndex = Tracks.IndexOf(DraggedTrack);
                if (oldIndex >= 0 && oldIndex != DropIndex)
                {
                    Tracks.Move(oldIndex, DropIndex);
                }
            }
            if (DraggedTrack != null)
            {
                DraggedTrack.IsBeingDragged = false;
                DraggedTrack = null;
            }
            DropIndex = -1;
            UpdateDropIndicators(-1);
        }


        [RelayCommand]
        private async Task ClearQueue()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (ContentType != ContentType.NowPlaying) return;

                    // Show confirmation dialog
                    bool confirmed = await MessageBoxService.ShowConfirmationDialog(
                        _localizationService["ClearQueueTitle"],
                        _localizationService["ClearQueueMessage"]);

                    if (confirmed)
                    {
                        // Clear the queue from memory
                        _trackQueueViewModel.NowPlayingQueue.Clear();

                        // Stop playback if something is playing
                        if (_trackControlViewModel.IsPlaying == PlaybackState.Playing)
                        {
                            _trackControlViewModel.StopPlayback();
                        }

                        // Clear the current track
                        Title = string.Empty;
                        Image = null;
                        Description = string.Empty;
                        _trackQueueViewModel.CurrentTrack = null;
                        await _trackControlViewModel.UpdateTrackInfo();

                        // Clear from database
                        var profileManager = App.ServiceProvider.GetService<ProfileManager>();
                        if (profileManager != null)
                        {
                            var profile = await profileManager.GetCurrentProfileAsync();
                            var profileId = profile.ProfileID;
                            await _queueService.ClearCurrentQueueForProfile(profileId);
                        }

                        // Update UI
                        _trackQueueViewModel.UpdateDurations();

                        // Refresh the NowPlaying view
                        NowPlayingInfo emptyInfo = new NowPlayingInfo
                        {
                            CurrentTrack = null,
                            AllTracks = new List<TrackDisplayModel>(),
                            CurrentTrackIndex = -1
                        };

                        LoadContent(emptyInfo);

                        // Send notification to update other components
                        _messenger.Send(new TrackQueueUpdateMessage(
                            null,
                            new ObservableCollection<TrackDisplayModel>(),
                            -1));

                        // Show confirmation
                        await MessageBoxService.ShowMessageDialog(
                            _localizationService["QueueClearedTitle"],
                            _localizationService["QueueClearedMessage"]);
                    }
                },
                "Clearing playback queue",
                ErrorSeverity.Playback,
                false);
        }
    }
}