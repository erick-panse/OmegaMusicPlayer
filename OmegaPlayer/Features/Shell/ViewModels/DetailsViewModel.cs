using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Playback.ViewModels;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Linq;
using OmegaPlayer.Core.Interfaces;
using Avalonia;
using System.Collections.Generic;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Library.Services;

namespace OmegaPlayer.Features.Shell.ViewModels
{
    public enum ContentType
    {
        Artist,
        Album,
        Genre,
        Playlist,
        Folder,
        NowPlaying
    }

    public partial class DetailsViewModel : ViewModelBase, ILoadMoreItems
    {
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly GenreDisplayService _genreDisplayService;
        private readonly FolderDisplayService _folderDisplayService;
        private readonly PlaylistDisplayService _playlistDisplayService;
        private object _currentContent;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private Avalonia.Media.Imaging.Bitmap _image;

        [ObservableProperty]
        private ContentType _contentType;

        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _tracks = new();

        [ObservableProperty]
        private bool _isHeaderCollapsed;

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.List;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private double _loadingProgress;

        [ObservableProperty]
        private bool _showAddTracksButton;

        [ObservableProperty]
        private bool _hasNoTracks;

        private int _currentPage = 1;
        private const int _pageSize = 50;
        private double _lastScrollOffset = 0;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public DetailsViewModel(
            TrackQueueViewModel trackQueueViewModel,
            ArtistDisplayService artistDisplayService,
            AlbumDisplayService albumDisplayService,
            GenreDisplayService genreDisplayService,
            FolderDisplayService folderDisplayService,
            PlaylistDisplayService playlistDisplayService)
        {
            _trackQueueViewModel = trackQueueViewModel;
            _artistDisplayService = artistDisplayService;
            _albumDisplayService = albumDisplayService;
            _genreDisplayService = genreDisplayService;
            _folderDisplayService = folderDisplayService;
            _playlistDisplayService = playlistDisplayService;
        }

        public async Task Initialize(ContentType type, object data)
        {
            ContentType = type;
            ShowAddTracksButton = type == ContentType.Playlist;

            await LoadContent(data);
            await LoadInitialTracks();
        }

        private async Task<List<TrackDisplayModel>> LoadTracksForCurrentContent(int page, int pageSize)
        {
            if (_currentContent == null) return new List<TrackDisplayModel>();

            try
            {
                switch (ContentType)
                {
                    case ContentType.Artist:
                        var artist = _currentContent as ArtistDisplayModel;
                        if (artist != null)
                        {
                            Tracks.Clear(); // clear tracks loaded
                            return await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                        }
                        break;

                    case ContentType.Album:
                        var album = _currentContent as AlbumDisplayModel;
                        if (album != null)
                        {
                            Tracks.Clear(); // clear tracks loaded
                            // Here you would need to implement GetAlbumTracksAsync in AlbumDisplayService
                            return await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                        }
                        break;

                    case ContentType.Genre:
                        var genre = _currentContent as GenreDisplayModel;
                        if (genre != null)
                        {
                            Tracks.Clear(); // clear tracks loaded
                            return await _genreDisplayService.GetGenreTracksAsync(genre.Name);
                        }
                        break;

                    case ContentType.Folder:
                        var folder = _currentContent as FolderDisplayModel;
                        if (folder != null)
                        {
                            Tracks.Clear(); // clear tracks loaded
                            return await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
                        }
                        break;

                    case ContentType.Playlist:
                        var playlist = _currentContent as PlaylistDisplayModel;
                        if (playlist != null)
                        {
                            Tracks.Clear(); // clear tracks loaded
                            // Here you would need to implement GetPlaylistTracksAsync in PlaylistDisplayService
                            return await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading tracks: {ex.Message}");
                // Consider showing an error message to the user
            }

            return new List<TrackDisplayModel>();
        }

        private async Task LoadContent(object data)
        {
            _currentContent = data;
            switch (ContentType)
            {
                case ContentType.Artist:
                    await LoadArtistContent(data as ArtistDisplayModel);
                    break;
                case ContentType.Album:
                    await LoadAlbumContent(data as AlbumDisplayModel);
                    break;
                case ContentType.Genre:
                    await LoadGenreContent(data as GenreDisplayModel);
                    break;
                case ContentType.Playlist:
                    await LoadPlaylistContent(data as PlaylistDisplayModel);
                    break;
                case ContentType.Folder:
                    await LoadFolderContent(data as FolderDisplayModel);
                    break;
            }
        }

        private async Task LoadGenreContent(GenreDisplayModel genre)
        {
            if (genre == null) return;
            Title = genre.Name;
            Description = $"{genre.TrackCount} tracks • {genre.TotalDuration:hh\\:mm\\:ss}";
            Image = genre.Photo;
        }

        private async Task LoadPlaylistContent(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;
            Title = playlist.Title;
            Description = $"Created {playlist.CreatedAt:d} • {playlist.TrackCount} tracks • {playlist.TotalDuration:hh\\:mm\\:ss}";
            Image = playlist.Cover;
        }

        private async Task LoadFolderContent(FolderDisplayModel folder)
        {
            if (folder == null) return;
            Title = folder.FolderName;
            Description = $"{folder.TrackCount} tracks • {folder.TotalDuration:hh\\:mm\\:ss}";
            Image = folder.Cover;
        }
        // Implement content type specific loading methods
        private async Task LoadArtistContent(ArtistDisplayModel artist)
        {
            if (artist == null) return;
            Title = artist.Name;
            Description = artist.Bio;
            Image = artist.Photo;
        }

        private async Task LoadAlbumContent(AlbumDisplayModel album)
        {
            if (album == null) return;
            Title = album.Title;
            Description = $"By {album.ArtistName}";
            Image = album.Cover;
        }

        private async Task LoadInitialTracks()
        {
            await LoadMoreItems();
            HasNoTracks = !Tracks.Any();
        }

        private async Task LoadMoreItems()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // Load tracks based on content type
                var tracks = await LoadTracksForCurrentContent(_currentPage, _pageSize);

                var totalTracks = tracks.Count;
                var current = 0;

                foreach (var track in tracks)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Tracks.Add(track);
                        current++;
                        LoadingProgress = (current * 100.0) / totalTracks;
                    });
                }

                _currentPage++;
            }
            finally
            {
                await Task.Delay(500);
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void PlayAll()
        {
            if (!Tracks.Any()) return;
            _trackQueueViewModel.PlayThisTrack(Tracks.First(), new ObservableCollection<TrackDisplayModel>(Tracks));
        }

        [RelayCommand]
        public void AddToQueue()
        {
            foreach (var track in Tracks)
            {
                _trackQueueViewModel.AddTrackToQueue(track);
            }
        }

        [RelayCommand]
        public void AddTracks()
        {
            // Implement add tracks for playlist
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
                _ => ViewType.List
            };
        }



    }
}