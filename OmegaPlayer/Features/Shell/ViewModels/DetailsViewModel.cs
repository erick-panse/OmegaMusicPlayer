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
using OmegaPlayer.UI;
using Microsoft.Extensions.DependencyInjection;
using System.Reactive.Linq;

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
        private readonly TrackDisplayService _trackDisplayService;
        private readonly TrackControlViewModel _trackControlViewModel;
        private readonly MainViewModel _mainViewModel;
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
            PlaylistDisplayService playlistDisplayService,
            TrackDisplayService trackDisplayService,
            TrackControlViewModel trackControlViewModel,
            MainViewModel mainViewModel)
        {
            _trackQueueViewModel = trackQueueViewModel;
            _artistDisplayService = artistDisplayService;
            _albumDisplayService = albumDisplayService;
            _genreDisplayService = genreDisplayService;
            _folderDisplayService = folderDisplayService;
            _playlistDisplayService = playlistDisplayService;
            _trackDisplayService = trackDisplayService;
            _trackControlViewModel = trackControlViewModel;
            _mainViewModel = mainViewModel;

            _trackControlViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TrackControlViewModel.CurrentlyPlayingTrack))
                {
                    UpdateTrackPlayingStatus(_trackControlViewModel.CurrentlyPlayingTrack);
                }
            };
        }

        private void UpdateTrackPlayingStatus(TrackDisplayModel currentTrack)
        {
            if (currentTrack == null) return;

            foreach (var track in Tracks)
            {
                track.IsCurrentlyPlaying = track.TrackID == currentTrack.TrackID;
            }
        }

        public async Task Initialize(ContentType type, object data)
        {
            ContentType = type;
            ShowAddTracksButton = type == ContentType.Playlist;

            await LoadContent(data);
            await LoadInitialTracks();

            if (ContentType == ContentType.NowPlaying && data is NowPlayingInfo nowPlayingInfo)
            {
                foreach (var track in Tracks)
                {
                    track.IsCurrentlyPlaying = track == nowPlayingInfo.CurrentTrack;
                }
            }
        }

        private async Task<List<TrackDisplayModel>> LoadTracksForCurrentContent(int page, int pageSize)
        {
            if (_currentContent == null) return new List<TrackDisplayModel>();

            try
            {
                List<TrackDisplayModel> tracks = new List<TrackDisplayModel>();
                switch (ContentType)
                {
                    case ContentType.Artist:
                        var artist = _currentContent as ArtistDisplayModel;
                        if (artist != null)
                        {
                            tracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();// Apply paging
                        }
                        break;

                    case ContentType.Album:
                        var album = _currentContent as AlbumDisplayModel;
                        if (album != null)
                        {
                            // Here you would need to implement GetAlbumTracksAsync in AlbumDisplayService
                            tracks = await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();// Apply paging
                        }
                        break;

                    case ContentType.Genre:
                        var genre = _currentContent as GenreDisplayModel;
                        if (genre != null)
                        {
                            tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();// Apply paging
                        }
                        break;

                    case ContentType.Folder:
                        var folder = _currentContent as FolderDisplayModel;
                        if (folder != null)
                        {
                            tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();// Apply paging
                        }
                        break;

                    case ContentType.Playlist:
                        var playlist = _currentContent as PlaylistDisplayModel;
                        if (playlist != null)
                        {
                            // Here you would need to implement GetPlaylistTracksAsync in PlaylistDisplayService
                            tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();// Apply paging
                        }
                        break;
                }
                foreach (var track in tracks)
                {
                    await _trackDisplayService.LoadHighResThumbnailAsync(track);
                }

                return tracks;

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
            Tracks.Clear(); // clear tracks loaded
            _currentPage = 1; // Reset paging
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
                case ContentType.NowPlaying:
                    await LoadNowPlayingContent(data as NowPlayingInfo);
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
            Description = $"By {album.ArtistName} • {album.TrackCount} tracks • {album.TotalDuration:hh\\:mm\\:ss}";
            Image = album.Cover;
        }
        private async Task LoadNowPlayingContent(NowPlayingInfo info)
        {
            if (info?.CurrentTrack == null) return;

            Title = "Now Playing";
            Description = $"{info.AllTracks.Count} tracks • Total: {_trackQueueViewModel.TotalDuration:hh\\:mm\\:ss} • Remaining: {_trackQueueViewModel.RemainingDuration:hh\\:mm\\:ss}";
            Image = info.CurrentTrack.Thumbnail;

            foreach (var track in info.AllTracks)
            {
                Tracks.Add(track);
            }
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
                        if (_trackQueueViewModel.CurrentTrack != null)
                        {
                            track.IsCurrentlyPlaying = track.TrackID == _trackQueueViewModel.CurrentTrack.TrackID;
                        }

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
        public void AddTracks()
        {
            // Implement add tracks for playlist
        }
        [RelayCommand]
        public void AddToQueue()
        {
            var selectedTracks = GetSelectedTracks();
            foreach (var track in selectedTracks)
            {
                _trackQueueViewModel.AddTrackToQueue(track);
            }
        }

        [RelayCommand]
        public void AddAsNextTracks()
        {
            var selectedTracks = GetSelectedTracks();
            foreach (var track in selectedTracks)
            {
                _trackQueueViewModel.AddToPlayNext(track);
            }
        }

        [RelayCommand]
        public void PlaySelectedTracks()
        {
            var selectedTracks = GetSelectedTracks();
            _trackQueueViewModel.PlayThisTrack(selectedTracks.First(), selectedTracks);
        }

        public ObservableCollection<TrackDisplayModel> GetSelectedTracks()
        {
            ObservableCollection<TrackDisplayModel> selectedTracks = new(Tracks.Where(track => track.IsSelected));
            return selectedTracks;
        }

        [RelayCommand]
        public async Task OpenArtist(Artists artist)
        {
            var artistDisplayService = App.ServiceProvider.GetRequiredService<ArtistDisplayService>();
            var artistDisplay = await artistDisplayService.GetArtistByIdAsync(artist.ArtistID);
            if (artistDisplay != null)
            {
                await _mainViewModel.NavigateToDetails(ContentType.Artist, artistDisplay);
            }
        }

        [RelayCommand]
        public async Task OpenAlbum(int albumID)
        {
            var albumDisplayService = App.ServiceProvider.GetRequiredService<AlbumDisplayService>();
            var albumDisplay = await albumDisplayService.GetAlbumByIdAsync(albumID);
            if (albumDisplay != null)
            {
                await _mainViewModel.NavigateToDetails(ContentType.Album, albumDisplay);
            }
        }

        [RelayCommand]
        public async Task OpenGenre(string genreName)
        {
            var genreDisplayService = App.ServiceProvider.GetRequiredService<GenreDisplayService>();
            var genreDisplay = await genreDisplayService.GetGenreByNameAsync(genreName);
            if (genreDisplay != null)
            {
                await _mainViewModel.NavigateToDetails(ContentType.Genre, genreDisplay);
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
                _ => ViewType.List
            };
        }



    }
}