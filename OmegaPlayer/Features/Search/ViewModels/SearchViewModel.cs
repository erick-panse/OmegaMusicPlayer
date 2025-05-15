using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Search.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playback.ViewModels;
using System;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Controls.ApplicationLifetimes;
using OmegaPlayer.Features.Playlists.Views;
using System.Collections.Generic;
using Avalonia;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Features.Library.Services;
using System.Linq;

namespace OmegaPlayer.Features.Search.ViewModels
{
    public partial class SearchViewModel : ViewModelBase
    {
        private readonly SearchService _searchService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly IServiceProvider _serviceProvider;
        private readonly StandardImageService _standardImageService;
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

        public SearchViewModel(
            SearchService searchService,
            TrackQueueViewModel trackQueueViewModel,
            ArtistDisplayService artistDisplayService,
            AlbumDisplayService albumDisplayService,
            StandardImageService standardImageService,
            IServiceProvider serviceProvider,
            IErrorHandlingService errorHandlingService)
        {
            _searchService = searchService;
            _trackQueueViewModel = trackQueueViewModel;
            _artistDisplayService = artistDisplayService;
            _albumDisplayService = albumDisplayService;
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
                        var results = await _searchService.SearchAsync(SearchQuery);

                        // Update full results
                        Tracks.Clear();
                        Albums.Clear();
                        Artists.Clear();

                        foreach (var track in results.Tracks)
                            Tracks.Add(track);
                        foreach (var album in results.Albums)
                            Albums.Add(album);
                        foreach (var artist in results.Artists)
                            Artists.Add(artist);

                        await _mainViewModel.NavigateToSearch(this);
                        ShowSearchFlyout = false;
                    }
                    finally
                    {
                        IsSearching = false;
                    }
                },
                $"Searching for '{SearchQuery}'",
                ErrorSeverity.NonCritical
            );
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

                PreviewTracks.Clear();
                PreviewAlbums.Clear();
                PreviewArtists.Clear();

                foreach (var track in results.PreviewTracks)
                    PreviewTracks.Add(track);
                foreach (var album in results.PreviewAlbums)
                    PreviewAlbums.Add(album);
                foreach (var artist in results.PreviewArtists)
                    PreviewArtists.Add(artist);

                ShowSearchFlyout = true;
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
                            await _mainViewModel.NavigateToSearch(this);
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
                $"Navigating to selected search item",
                ErrorSeverity.NonCritical
            );
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
                },
                "Clearing search results",
                ErrorSeverity.NonCritical,
                false
            );
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
                $"Playing track '{track?.Title ?? "Unknown"}'",
                ErrorSeverity.Playback
            );
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
                "Playing all tracks",
                ErrorSeverity.Playback
            );
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
                "Playing all tracks",
                ErrorSeverity.Playback
            );
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
                "Playing all tracks",
                ErrorSeverity.Playback
            );
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
                $"Adding track '{track?.Title ?? "Unknown"}' to play next",
                ErrorSeverity.NonCritical
            );
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
                $"Adding track '{track?.Title ?? "Unknown"}' to queue",
                ErrorSeverity.NonCritical
            );
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
                $"Showing playlist selection dialog for track '{track.Title}'",
                ErrorSeverity.NonCritical
            );
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
                $"Playing artist '{artist?.Name ?? "Unknown"}'",
                ErrorSeverity.Playback
            );
        }

        [RelayCommand]
        public async Task PlayAllArtists()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (Artists == null || Artists.Count == 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No artists available",
                            "No artists to play.",
                            null,
                            false);
                        return;
                    }

                    var allTracks = new List<TrackDisplayModel>();
                    foreach (var artist in Artists)
                    {
                        var tracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                        allTracks.AddRange(tracks);
                    }

                    PlayTracks(new ObservableCollection<TrackDisplayModel>(allTracks));
                },
                "Playing all artists",
                ErrorSeverity.Playback
            );
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
                $"Adding artist '{artist?.Name ?? "Unknown"}' to play next",
                ErrorSeverity.NonCritical
            );
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
                $"Adding artist '{artist?.Name ?? "Unknown"}' to queue",
                ErrorSeverity.NonCritical
            );
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
                $"Showing playlist selection dialog for artist '{artist.Name}'",
                ErrorSeverity.NonCritical
            );
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
                $"Playing album '{album?.Title ?? "Unknown"}'",
                ErrorSeverity.Playback
            );
        }

        [RelayCommand]
        public async Task PlayAllAlbums()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (Albums == null || Albums.Count == 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No albums available",
                            "No albums to play.",
                            null,
                            false);
                        return;
                    }

                    var allTracks = new List<TrackDisplayModel>();
                    foreach (var album in Albums)
                    {
                        var tracks = await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                        allTracks.AddRange(tracks);
                    }

                    PlayTracks(new ObservableCollection<TrackDisplayModel>(allTracks));
                },
                "Playing all albums",
                ErrorSeverity.Playback
            );
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
                $"Adding album '{album?.Title ?? "Unknown"}' to play next",
                ErrorSeverity.NonCritical
            );
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
                $"Adding album '{album?.Title ?? "Unknown"}' to queue",
                ErrorSeverity.NonCritical
            );
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
                $"Showing playlist selection dialog for album '{album.Title}'",
                ErrorSeverity.NonCritical
            );
        }

        #endregion


        #region Visibility Notifications

        /// <summary>
        /// Notifies that a track is visible/invisible for prioritized loading
        /// </summary>
        public async Task NotifyTrackVisible(TrackDisplayModel track, bool isVisible)
        {
            if (track?.CoverPath == null) return;

            await _standardImageService.NotifyImageVisible(track.CoverPath, isVisible);
        }

        /// <summary>
        /// Notifies that an album is visible/invisible for prioritized loading
        /// </summary>
        public async Task NotifyAlbumVisible(AlbumDisplayModel album, bool isVisible)
        {
            if (album?.CoverPath == null) return;

            await _standardImageService.NotifyImageVisible(album.CoverPath, isVisible);
        }

        /// <summary>
        /// Notifies that an artist is visible/invisible for prioritized loading
        /// </summary>
        public async Task NotifyArtistVisible(ArtistDisplayModel artist, bool isVisible)
        {
            if (artist?.PhotoPath == null) return;

            await _standardImageService.NotifyImageVisible(artist.PhotoPath, isVisible);
        }

        #endregion
    }
}