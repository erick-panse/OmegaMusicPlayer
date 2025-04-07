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

namespace OmegaPlayer.Features.Search.ViewModels
{
    public partial class SearchViewModel : ViewModelBase
    {
        private readonly SearchService _searchService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly IServiceProvider _serviceProvider;
        private readonly StandardImageService _standardImageService;

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
            IServiceProvider serviceProvider)
        {
            _searchService = searchService;
            _trackQueueViewModel = trackQueueViewModel;
            _standardImageService = standardImageService;
            _serviceProvider = serviceProvider;
        }

        [RelayCommand]
        public async Task Search()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
                return;

            var _mainViewModel = _serviceProvider.GetService<MainViewModel>();

            if (_mainViewModel == null) return;

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
                Console.WriteLine($"Error in search preview: {ex.Message}");
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
            var _mainViewModel = _serviceProvider.GetService<MainViewModel>();

            if (_mainViewModel == null) return;

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
            }
        }

        [RelayCommand]
        public void PlayTrack(TrackDisplayModel track)
        {
            if (track == null) return;
            _trackQueueViewModel.PlayThisTrack(track, new ObservableCollection<TrackDisplayModel>(Tracks));
        }

        [RelayCommand]
        public void AddToPlayNext(TrackDisplayModel track)
        {
            if (track == null) return;
            var tracksList = new ObservableCollection<TrackDisplayModel> { track };
            _trackQueueViewModel.AddToPlayNext(tracksList);
        }

        [RelayCommand]
        public void AddToQueue(TrackDisplayModel track)
        {
            if (track == null) return;
            var tracksList = new ObservableCollection<TrackDisplayModel> { track };
            _trackQueueViewModel.AddTrackToQueue(tracksList);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(TrackDisplayModel track)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var _playlistViewModel = _serviceProvider.GetService<PlaylistsViewModel>();
                if (_playlistViewModel == null) return;

                var mainWindow = desktop.MainWindow;
                if (mainWindow == null || !mainWindow.IsVisible) return;

                var selectedTracks = new List<TrackDisplayModel> { track };

                var dialog = new PlaylistSelectionDialog();
                dialog.Initialize(_playlistViewModel, null, selectedTracks);
                await dialog.ShowDialog(mainWindow);
            }
        }

        public void ClearSearch()
        {
            SearchQuery = string.Empty;
            ShowSearchFlyout = false;
            Tracks.Clear();
            Albums.Clear();
            Artists.Clear();
            PreviewTracks.Clear();
            PreviewAlbums.Clear();
            PreviewArtists.Clear();
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