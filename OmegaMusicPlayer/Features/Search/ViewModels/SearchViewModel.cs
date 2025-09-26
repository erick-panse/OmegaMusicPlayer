using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Enums.LibraryEnums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.ViewModels;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Library.Services;
using OmegaMusicPlayer.Features.Library.ViewModels;
using OmegaMusicPlayer.Features.Playback.ViewModels;
using OmegaMusicPlayer.Features.Playlists.Views;
using OmegaMusicPlayer.Features.Search.Services;
using OmegaMusicPlayer.Features.Shell.ViewModels;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Search.ViewModels
{
    public partial class SearchViewModel : ViewModelBase
    {
        private readonly SearchService _searchService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly LocalizationService _localizationService;
        private readonly StandardImageService _standardImageService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IErrorHandlingService _errorHandlingService;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSearching;

        [ObservableProperty]
        private bool _showSearchFlyout;

        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _tracks = new();

        [ObservableProperty]
        private ObservableCollection<AlbumDisplayModel> _albums = new();

        [ObservableProperty]
        private ObservableCollection<ArtistDisplayModel> _artists = new();

        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _previewTracks = new();

        [ObservableProperty]
        private ObservableCollection<AlbumDisplayModel> _previewAlbums = new();

        [ObservableProperty]
        private ObservableCollection<ArtistDisplayModel> _previewArtists = new();

        [ObservableProperty]
        private bool _isLoadingResults;
        
        [ObservableProperty]
        private bool _isTracksExpanded = true;

        [ObservableProperty]
        private bool _isAlbumsExpanded = true;

        [ObservableProperty]
        private bool _isArtistsExpanded = true;

        // All search results (for chunked loading)
        private List<TrackDisplayModel> AllTracks { get; set; }
        private List<AlbumDisplayModel> AllAlbums { get; set; }
        private List<ArtistDisplayModel> AllArtists { get; set; }

        // Loading states
        private bool _isTracksLoaded = false;
        private bool _isAlbumsLoaded = false;
        private bool _isArtistsLoaded = false;
        private CancellationTokenSource _loadingCancellationTokenSource;

        // Events to trigger visibility checks from view
        public Action TriggerTracksVisibilityCheck { get; set; }
        public Action TriggerAlbumsVisibilityCheck { get; set; }
        public Action TriggerArtistsVisibilityCheck { get; set; }

        public SearchViewModel(
            SearchService searchService,
            TrackQueueViewModel trackQueueViewModel,
            ArtistDisplayService artistDisplayService,
            AlbumDisplayService albumDisplayService,
            LocalizationService localizationService,
            StandardImageService standardImageService,
            IServiceProvider serviceProvider,
            IErrorHandlingService errorHandlingService)
        {
            _searchService = searchService;
            _trackQueueViewModel = trackQueueViewModel;
            _artistDisplayService = artistDisplayService;
            _albumDisplayService = albumDisplayService;
            _localizationService = localizationService;
            _standardImageService = standardImageService;
            _serviceProvider = serviceProvider;
            _errorHandlingService = errorHandlingService;
        }

        #region Search Commands

        [RelayCommand]
        public async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
                return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var _mainViewModel = _serviceProvider.GetService<MainViewModel>();
                    if (_mainViewModel == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Missing main view model",
                            "Could not access MainViewModel for search navigation.",
                            null,
                            false);
                        return;
                    }

                    IsSearching = true;

                    try
                    {
                        // Get search results (this loads all items but without images)
                        var results = await _searchService.SearchAsync(SearchQuery);

                        // Store all results for chunked loading
                        AllTracks = results.Tracks;
                        AllAlbums = results.Albums;
                        AllArtists = results.Artists;

                        // Reset loading states
                        _isTracksLoaded = false;
                        _isAlbumsLoaded = false;
                        _isArtistsLoaded = false;

                        // Clear current collections
                        Tracks.Clear();
                        Albums.Clear();
                        Artists.Clear();

                        await _mainViewModel.Navigate("Search");
                        ShowSearchFlyout = false;

                        // Start chunked loading after navigation
                        await LoadSearchResultsChunked();
                    }
                    finally
                    {
                        IsSearching = false;
                    }
                },
                _localizationService["ErrorSearching"] + SearchQuery,
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task SearchPreview()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                ShowSearchFlyout = false;
                return;
            }

            try
            {
                IsSearching = true;
                ShowSearchFlyout = true;

                var results = await _searchService.SearchAsync(SearchQuery);

                // Yield to prevent blocking
                await Task.Delay(1);

                PreviewTracks.Clear();
                PreviewAlbums.Clear();
                PreviewArtists.Clear();

                foreach (var track in results.PreviewTracks)
                {
                    PreviewTracks.Add(track);
                    await NotifyTrackVisible(track, true);
                }
                foreach (var album in results.PreviewAlbums)
                {
                    PreviewAlbums.Add(album);
                    await NotifyAlbumVisible(album, true);
                }
                foreach (var artist in results.PreviewArtists)
                {
                    PreviewArtists.Add(artist);
                    await NotifyArtistVisible(artist, true);
                }

                ShowSearchFlyout = true;

                // Yield to prevent blocking
                await Task.Delay(1);
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                ErrorSeverity.NonCritical,
                "Error in search preview",
                $"Error generating search preview for query: '{SearchQuery}'",
                ex,
                false);
                ShowSearchFlyout = false;
            }
            finally
            {
                IsSearching = false;
            }
        }

        /// <summary>
        /// Load search results in chunks to keep UI responsive.
        /// Images are loaded only when items become visible via virtualization.
        /// </summary>
        private async Task LoadSearchResultsChunked()
        {
            if (IsLoadingResults) return;

            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _loadingCancellationTokenSource.Token;

            IsLoadingResults = true;

            try
            {
                // Load data
                await Task.WhenAll(
                    LoadTracksChunked(cancellationToken),
                    LoadArtistsChunked(cancellationToken),
                    LoadAlbumsChunked(cancellationToken)
                );

                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                // Loading was cancelled, this is expected
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading search results",
                    ex.Message,
                    ex,
                    false);
            }
            finally
            {
                IsLoadingResults = false;
            }
        }

        private async Task LoadTracksChunked(CancellationToken cancellationToken)
        {
            if (_isTracksLoaded || AllTracks?.Any() != true) return;

            const int chunkSize = 10;
            var totalTracks = AllTracks.Count;

            for (int i = 0; i < AllTracks.Count; i += chunkSize)
            {
                if (cancellationToken.IsCancellationRequested) return;

                TriggerTracksVisibilityCheck?.Invoke();

                var chunk = AllTracks.Skip(i).Take(chunkSize).ToList();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    // Add tracks WITHOUT loading images
                    foreach (var track in chunk)
                    {
                        Tracks.Add(track);
                    }
                }, Avalonia.Threading.DispatcherPriority.Background);

                await Task.Delay(1, cancellationToken);
            }

            _isTracksLoaded = true;
        }


        private async Task LoadArtistsChunked(CancellationToken cancellationToken)
        {
            if (_isArtistsLoaded || AllArtists?.Any() != true) return;

            const int chunkSize = 10;
            var totalArtists = AllArtists.Count;

            for (int i = 0; i < AllArtists.Count; i += chunkSize)
            {
                if (cancellationToken.IsCancellationRequested) return;

                TriggerArtistsVisibilityCheck?.Invoke();

                var chunk = AllArtists.Skip(i).Take(chunkSize).ToList();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    // Add artists WITHOUT loading images
                    foreach (var artist in chunk)
                    {
                        Artists.Add(artist);
                    }
                }, Avalonia.Threading.DispatcherPriority.Background);

                await Task.Delay(1, cancellationToken);
            }

            _isArtistsLoaded = true;
        }
        private async Task LoadAlbumsChunked(CancellationToken cancellationToken)
        {
            if (_isAlbumsLoaded || AllAlbums?.Any() != true) return;

            const int chunkSize = 10;
            var totalAlbums = AllAlbums.Count;

            for (int i = 0; i < AllAlbums.Count; i += chunkSize)
            {
                if (cancellationToken.IsCancellationRequested) return;

                TriggerAlbumsVisibilityCheck?.Invoke();

                var chunk = AllAlbums.Skip(i).Take(chunkSize).ToList();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    // Add albums WITHOUT loading images
                    foreach (var album in chunk)
                    {
                        Albums.Add(album);
                    }
                }, Avalonia.Threading.DispatcherPriority.Background);

                await Task.Delay(1, cancellationToken);
            }

            _isAlbumsLoaded = true;
        }


        [RelayCommand]
        public async Task SelectPreviewItem(object item)
        {
            if (item == null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Invalid selection",
                    "Attempted to select a null item from search results.",
                    null,
                    false);
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var _mainViewModel = _serviceProvider.GetService<MainViewModel>();
                    if (_mainViewModel == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Missing main view model",
                            "Could not access MainViewModel for search item navigation.",
                            null,
                            false);
                        return;
                    }

                    ShowSearchFlyout = false;

                    switch (item)
                    {
                        case TrackDisplayModel track:
                            await _mainViewModel.Navigate("Search");
                            break;
                        case AlbumDisplayModel album:
                            await _mainViewModel.NavigateToDetails(ContentType.Album, album);
                            break;
                        case ArtistDisplayModel artist:
                            await _mainViewModel.NavigateToDetails(ContentType.Artist, artist);
                            break;
                        default:
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Unknown item type",
                                $"Could not determine how to navigate to the selected item of type: {item.GetType().Name}",
                                null,
                                false);
                            break;
                    }
                },
                _localizationService["ErrorNavigatingToItem"],
                ErrorSeverity.NonCritical,
                true);
        }

        public void ClearSearch()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    SearchQuery = string.Empty;
                    ShowSearchFlyout = false;
                    Tracks.Clear();
                    Albums.Clear();
                    Artists.Clear();
                    PreviewTracks.Clear();
                    PreviewAlbums.Clear();
                    PreviewArtists.Clear();

                    // Clear all results
                    AllTracks = null;
                    AllAlbums = null;
                    AllArtists = null;

                    // Reset loading states
                    _isTracksLoaded = false;
                    _isAlbumsLoaded = false;
                    _isArtistsLoaded = false;
                },
                "Clearing search results",
                ErrorSeverity.NonCritical,
                false);
        }

        #endregion

        #region Visibility Notifications

        /// <summary>
        /// Notifies that a track is visible/invisible for prioritized loading
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

                // If track becomes visible and hasn't had its image loaded yet, load it now
                if (isVisible)
                {
                    // Load the image in the background with lower priority
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Load thumbnail for tracks
                            track.Thumbnail = await _standardImageService.LoadLowQualityAsync(track.CoverPath, true);
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
        /// Notifies that an album is visible/invisible for prioritized loading
        /// </summary>
        public async Task NotifyAlbumVisible(AlbumDisplayModel album, bool isVisible)
        {
            if (album?.CoverPath == null) return;

            try
            {
                // Notify the image service about visibility changes for optimization
                if (_standardImageService != null)
                {
                    await _standardImageService.NotifyImageVisible(album.CoverPath, isVisible);
                }

                // If album becomes visible and hasn't had its image loaded yet, load it now
                if (isVisible)
                {
                    // Load the image in the background with lower priority
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _albumDisplayService.LoadAlbumCoverAsync(album, "low", true);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error loading album image",
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
                    "Error handling album visibility notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Notifies that an artist is visible/invisible for prioritized loading
        /// </summary>
        public async Task NotifyArtistVisible(ArtistDisplayModel artist, bool isVisible)
        {
            if (artist?.PhotoPath == null) return;

            try
            {
                // Notify the image service about visibility changes for optimization
                if (_standardImageService != null)
                {
                    await _standardImageService.NotifyImageVisible(artist.PhotoPath, isVisible);
                }

                // If artist becomes visible and hasn't had its image loaded yet, load it now
                if (isVisible)
                {

                    // Load the image in the background with lower priority
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _artistDisplayService.LoadArtistPhotoAsync(artist, "low", true);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error loading artist image",
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
                    "Error handling artist visibility notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        #endregion

        #region Core Playback Methods

        private void PlayTracks(ObservableCollection<TrackDisplayModel> tracks)
        {
            if (tracks == null || tracks.Count == 0) return;
            _trackQueueViewModel.PlayThisTrack(tracks.First(), tracks);
        }

        private void AddTracksToNext(ObservableCollection<TrackDisplayModel> tracks)
        {
            if (tracks == null || tracks.Count == 0) return;
            _trackQueueViewModel.AddToPlayNext(tracks);
        }

        private void AddTracksToQueue(ObservableCollection<TrackDisplayModel> tracks)
        {
            if (tracks == null || tracks.Count == 0) return;
            _trackQueueViewModel.AddTrackToQueue(tracks);
        }

        private async Task ShowPlaylistSelectionForTracks(List<TrackDisplayModel> tracks)
        {
            if (tracks == null || tracks.Count == 0) return;

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var playlistViewModel = _serviceProvider.GetService<PlaylistsViewModel>();
                if (playlistViewModel == null)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Missing playlist view model",
                        "Could not access PlaylistsViewModel for playlist selection dialog.",
                        null,
                        false);
                    return;
                }

                var mainWindow = desktop.MainWindow;
                if (mainWindow == null || !mainWindow.IsVisible)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Missing main window",
                        "Could not find main window for showing playlist selection dialog.",
                        null,
                        false);
                    return;
                }

                var dialog = new PlaylistSelectionDialog();
                dialog.Initialize(playlistViewModel, null, tracks);
                await dialog.ShowDialog(mainWindow);
            }
            else
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Invalid application lifetime",
                    "Could not show playlist selection dialog because application is not running in desktop mode.",
                    null,
                    false);
            }
        }

        #endregion

        #region Track Commands

        [RelayCommand]
        public void PlayTrack(TrackDisplayModel track)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    if (track == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Null track selected",
                            "Attempted to play a null track.",
                            null,
                            false);
                        return;
                    }
                    PlayTracks(new ObservableCollection<TrackDisplayModel> { track });
                },
                _localizationService["ErrorPlayingSelectedTrack"],
                ErrorSeverity.Playback,
                true);
        }

        [RelayCommand]
        public void PlayAllTracks()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    if (Tracks == null || Tracks.Count == 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No tracks available",
                            "No tracks to play.",
                            null,
                            false);
                        return;
                    }
                    PlayTracks(new ObservableCollection<TrackDisplayModel>(Tracks));
                },
                _localizationService["ErrorPlayingAllTracks"],
                ErrorSeverity.Playback,
                true);
        }

        [RelayCommand]
        public async Task PlayAllArtistsTracks()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (Artists == null || Artists.Count == 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No tracks available for artists",
                            "No tracks to play.",
                            null,
                            false);
                        return;
                    }

                    var tracksList = new List<TrackDisplayModel>();
                    foreach (var artist in Artists)
                    {
                        var tracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                        tracksList.AddRange(tracks);
                    }
                    PlayTracks(new ObservableCollection<TrackDisplayModel>(tracksList));
                },
                _localizationService["ErrorPlayingAllArtists"],
                ErrorSeverity.Playback,
                true);
        }

        [RelayCommand]
        public async Task PlayAllAlbumsTracks()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (Albums == null || Albums.Count == 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No tracks available for albums",
                            "No tracks to play.",
                            null,
                            false);
                        return;
                    }

                    var tracksList = new List<TrackDisplayModel>();
                    foreach (var album in Albums)
                    {
                        var tracks = await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                        tracksList.AddRange(tracks);
                    }
                    PlayTracks(new ObservableCollection<TrackDisplayModel>(tracksList));
                },
                _localizationService["ErrorPlayingAllAlbums"],
                ErrorSeverity.Playback,
                true);
        }

        [RelayCommand]
        public void AddTrackToPlayNext(TrackDisplayModel track)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    if (track == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Null track selected",
                            "Attempted to add a null track to play next queue.",
                            null,
                            false);
                        return;
                    }
                    AddTracksToNext(new ObservableCollection<TrackDisplayModel> { track });
                },
                _localizationService["ErrorAddingPlayNext"],
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public void AddTrackToQueue(TrackDisplayModel track)
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    if (track == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Null track selected",
                            "Attempted to add a null track to queue.",
                            null,
                            false);
                        return;
                    }
                    AddTracksToQueue(new ObservableCollection<TrackDisplayModel> { track });
                },
                _localizationService["ErrorAddingToQueue"],
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionForTrack(TrackDisplayModel track)
        {
            if (track == null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Null track selected",
                    "Attempted to add a null track to playlist.",
                    null,
                    false);
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    await ShowPlaylistSelectionForTracks(new List<TrackDisplayModel> { track });
                },
                _localizationService["ShowingPlaylistDialogError"],
                ErrorSeverity.NonCritical,
                true);
        }

        #endregion

        #region Artist Commands

        [RelayCommand]
        public async Task PlayArtist(ArtistDisplayModel artist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Null artist selected",
                            "Attempted to play a null artist.",
                            null,
                            false);
                        return;
                    }

                    var tracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                    PlayTracks(new ObservableCollection<TrackDisplayModel>(tracks));
                },
                _localizationService["ErrorPlayingSelectedArtists"],
                ErrorSeverity.Playback,
                true);
        }

        [RelayCommand]
        public async Task AddArtistToPlayNext(ArtistDisplayModel artist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Null artist selected",
                            "Attempted to add a null artist to play next queue.",
                            null,
                            false);
                        return;
                    }

                    var tracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                    AddTracksToNext(new ObservableCollection<TrackDisplayModel>(tracks));
                },
                _localizationService["ErrorAddingPlayNext"],
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task AddArtistToQueue(ArtistDisplayModel artist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Null artist selected",
                            "Attempted to add a null artist to queue.",
                            null,
                            false);
                        return;
                    }

                    var tracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                    AddTracksToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
                },
                _localizationService["ErrorAddingToQueue"],
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionForArtist(ArtistDisplayModel artist)
        {
            if (artist == null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Null artist selected",
                    "Attempted to add a null artist to playlist.",
                    null,
                    false);
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var tracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                    await ShowPlaylistSelectionForTracks(tracks);
                },
                _localizationService["ShowingPlaylistDialogError"],
                ErrorSeverity.NonCritical,
                true);
        }

        #endregion

        #region Album Commands

        [RelayCommand]
        public async Task PlayAlbum(AlbumDisplayModel album)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (album == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Null album selected",
                            "Attempted to play a null album.",
                            null,
                            false);
                        return;
                    }

                    var tracks = await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                    PlayTracks(new ObservableCollection<TrackDisplayModel>(tracks));
                },
                _localizationService["ErrorPlayingSelectedAlbums"],
                ErrorSeverity.Playback,
                true);
        }

        [RelayCommand]
        public async Task AddAlbumToPlayNext(AlbumDisplayModel album)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (album == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Null album selected",
                            "Attempted to add a null album to play next queue.",
                            null,
                            false);
                        return;
                    }

                    var tracks = await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                    AddTracksToNext(new ObservableCollection<TrackDisplayModel>(tracks));
                },
                _localizationService["ErrorAddingPlayNext"],
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task AddAlbumToQueue(AlbumDisplayModel album)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (album == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Null album selected",
                            "Attempted to add a null album to queue.",
                            null,
                            false);
                        return;
                    }

                    var tracks = await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                    AddTracksToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
                },
                _localizationService["ErrorAddingToQueue"],
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionForAlbum(AlbumDisplayModel album)
        {
            if (album == null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Null album selected",
                    "Attempted to add a null album to playlist.",
                    null,
                    false);
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var tracks = await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                    await ShowPlaylistSelectionForTracks(tracks);
                },
                _localizationService["ShowingPlaylistDialogError"],
                ErrorSeverity.NonCritical,
                true);
        }

        #endregion

        #region Toggle Collapse / Expand Commands
        [RelayCommand]
        public void ToggleTracksExpanded()
        {
            IsTracksExpanded = !IsTracksExpanded;
        }

        [RelayCommand]
        public void ToggleAlbumsExpanded()
        {
            IsAlbumsExpanded = !IsAlbumsExpanded;
        }

        [RelayCommand]
        public void ToggleArtistsExpanded()
        {
            IsArtistsExpanded = !IsArtistsExpanded;
        }
        #endregion
    }
}