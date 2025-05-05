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

namespace OmegaPlayer.Features.Search.ViewModels
{
    public partial class SearchViewModel : ViewModelBase
    {
        private readonly SearchService _searchService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
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
            StandardImageService standardImageService,
            IServiceProvider serviceProvider,
            IErrorHandlingService errorHandlingService)
        {
            _searchService = searchService;
            _trackQueueViewModel = trackQueueViewModel;
            _standardImageService = standardImageService;
            _serviceProvider = serviceProvider;
            _errorHandlingService = errorHandlingService;
        }

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
                    _trackQueueViewModel.PlayThisTrack(track, new ObservableCollection<TrackDisplayModel>(Tracks));
                },
                $"Playing track '{track?.Title ?? "Unknown"}'",
                ErrorSeverity.Playback
            );
        }

        [RelayCommand]
        public void AddToPlayNext(TrackDisplayModel track)
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
                    var tracksList = new ObservableCollection<TrackDisplayModel> { track };
                    _trackQueueViewModel.AddToPlayNext(tracksList);
                },
                $"Adding track '{track?.Title ?? "Unknown"}' to play next",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        public void AddToQueue(TrackDisplayModel track)
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
                    var tracksList = new ObservableCollection<TrackDisplayModel> { track };
                    _trackQueueViewModel.AddTrackToQueue(tracksList);
                },
                $"Adding track '{track?.Title ?? "Unknown"}' to queue",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(TrackDisplayModel track)
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
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var _playlistViewModel = _serviceProvider.GetService<PlaylistsViewModel>();
                        if (_playlistViewModel == null)
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

                        var selectedTracks = new List<TrackDisplayModel> { track };

                        var dialog = new PlaylistSelectionDialog();
                        dialog.Initialize(_playlistViewModel, null, selectedTracks);
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
                },
                $"Showing playlist selection dialog for track '{track.Title}'",
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
    }
}