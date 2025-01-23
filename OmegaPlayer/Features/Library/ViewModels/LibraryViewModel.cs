using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;
using System.Linq;
using System.Windows.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.UI;
using Microsoft.Extensions.DependencyInjection;
using Avalonia;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Controls;

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
        Library,
        Artist,
        Album,
        Genre,
        Playlist,
        Folder,
        NowPlaying
    }

    public partial class LibraryViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private AsyncRelayCommand _loadMoreItemsCommand;
        public ICommand LoadMoreItemsCommand => _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        private readonly TrackDisplayService _trackDisplayService;
        private readonly TracksService _tracksService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly TrackControlViewModel _trackControlViewModel;
        private readonly MainViewModel _mainViewModel;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly GenreDisplayService _genreDisplayService;
        private readonly FolderDisplayService _folderDisplayService;
        private readonly PlaylistDisplayService _playlistDisplayService;


        [ObservableProperty]
        private ViewType _currentViewType = ViewType.Card;

        [ObservableProperty]
        private string _title = "Library"; // Default for Library mode

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private Avalonia.Media.Imaging.Bitmap _image;

        [ObservableProperty]
        private ContentType _contentType = ContentType.Library;

        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _selectedTracks = new();

        public ObservableCollection<TrackDisplayModel> Tracks { get; } = new();

        public List<TrackDisplayModel> AllTracks { get; set; }

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private double _loadingProgress;

        [ObservableProperty]
        private bool _showAddTracksButton; // For Playlist mode

        [ObservableProperty]
        private bool _showRandomizeButton; // Hidden in NowPlaying mode

        [ObservableProperty]
        private bool _isDetailsMode;

        [ObservableProperty]
        private string _playButtonText = "Play All";

        [ObservableProperty]
        private bool _hasNoTracks;

        private object _currentContent;
        private int _currentPage = 1;
        private const int _pageSize = 50;

        public bool ShowPlayButton => !HasNoTracks;
        public bool ShowMainActions => !HasNoTracks;

        public LibraryViewModel(
            TrackDisplayService trackDisplayService,
            TracksService tracksService,
            TrackQueueViewModel trackQueueViewModel,
            AllTracksRepository allTracksRepository,
            TrackControlViewModel trackControlViewModel,
            MainViewModel mainViewModel,
            ArtistDisplayService artistDisplayService,
            AlbumDisplayService albumDisplayService,
            GenreDisplayService genreDisplayService,
            FolderDisplayService folderDisplayService,
            PlaylistDisplayService playlistDisplayService,
            TrackSortService trackSortService,
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _trackDisplayService = trackDisplayService;
            _tracksService = tracksService;
            _trackQueueViewModel = trackQueueViewModel;
            _allTracksRepository = allTracksRepository;
            _trackControlViewModel = trackControlViewModel;
            _mainViewModel = mainViewModel;
            _artistDisplayService = artistDisplayService;
            _albumDisplayService = albumDisplayService;
            _genreDisplayService = genreDisplayService;
            _folderDisplayService = folderDisplayService;
            _playlistDisplayService = playlistDisplayService;

            AllTracks = _allTracksRepository.AllTracks;

            CurrentViewType = _mainViewModel.CurrentViewType;
            LoadInitialTracksAsync();

            // Subscribe to property changes
            _trackControlViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TrackControlViewModel.CurrentlyPlayingTrack))
                {
                    UpdateTrackPlayingStatus(_trackControlViewModel.CurrentlyPlayingTrack);
                }
            };
        }

        protected override void ApplyCurrentSort()
        {
            // Skip sorting if in NowPlaying mode
            if (ContentType == ContentType.NowPlaying)
                return;

            var sortedTracks = _trackSortService.SortTracks(
                Tracks,
                CurrentSortType,
                CurrentSortDirection
            ).ToList();

            Tracks.Clear();
            foreach (var track in sortedTracks)
            {
                Tracks.Add(track);
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

        public async Task Initialize(bool isDetails = false, ContentType type = ContentType.Library, object data = null)
        {
            IsDetailsMode = isDetails;
            _mainViewModel.ShowBackButton = IsDetailsMode;
            ContentType = type;
            ShowAddTracksButton = type == ContentType.Playlist;
            ShowRandomizeButton = type != ContentType.NowPlaying;
            DeselectAllTracks();

            if (isDetails)
            {
                await LoadContent(data);
            }
            else
            {
                Title = "Library";
                Description = string.Empty;
                Image = null;
            }

            await LoadInitialTracksAsync();
        }

        [RelayCommand]
        public async Task NavigateBack()
        {
            await Initialize(false); // Reset to Library mode
            _currentContent = null;
            Tracks.Clear();
            _currentPage = 1;
            await LoadInitialTracksAsync();
        }

        public async Task LoadInitialTracksAsync()
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

                await Task.Run(async () =>
                {
                    var tracks = IsDetailsMode ?
                    await LoadTracksForContent(_currentPage, _pageSize) :
                    await _trackDisplayService.LoadTracksAsync(2, _currentPage, _pageSize);

                    _mainViewModel.ShowBackButton = IsDetailsMode;

                    var totalTracks = tracks.Count;
                    var current = 0;

                    foreach (var track in tracks)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (_trackControlViewModel.CurrentlyPlayingTrack != null)
                            {
                                track.IsCurrentlyPlaying = track.TrackID == _trackControlViewModel.CurrentlyPlayingTrack.TrackID;
                            }

                            Tracks.Add(track);
                            track.Artists.Last().IsLastArtist = false;

                            current++;
                            LoadingProgress = (current * 100.0) / totalTracks;
                        });
                    }

                    ApplyCurrentSort();
                    _currentPage++;
                });
            }
            finally
            {
                await Task.Delay(500); // Show completion message briefly
                IsLoading = false;
            }
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

        private async Task<List<TrackDisplayModel>> LoadTracksForContent(int page, int pageSize)
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
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                        }
                        break;

                    case ContentType.Album:
                        var album = _currentContent as AlbumDisplayModel;
                        if (album != null)
                        {
                            tracks = await _albumDisplayService.GetAlbumTracksAsync(album.AlbumID);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                        }
                        break;

                    case ContentType.Genre:
                        var genre = _currentContent as GenreDisplayModel;
                        if (genre != null)
                        {
                            tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                        }
                        break;

                    case ContentType.Folder:
                        var folder = _currentContent as FolderDisplayModel;
                        if (folder != null)
                        {
                            tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                        }
                        break;

                    case ContentType.Playlist:
                        var playlist = _currentContent as PlaylistDisplayModel;
                        if (playlist != null)
                        {
                            tracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                        }
                        break;

                    case ContentType.NowPlaying:
                        var nowPlayingInfo = _currentContent as NowPlayingInfo;
                        if (nowPlayingInfo?.CurrentTrack != null)
                        {
                            tracks = new List<TrackDisplayModel>(nowPlayingInfo.AllTracks);
                            tracks = tracks.Skip((page - 1) * pageSize).Take(pageSize).ToList();
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
                return new List<TrackDisplayModel>();
            }
        }

        // Track Selection and Navigation Methods
        [RelayCommand]
        private void TrackSelection(TrackDisplayModel track)
        {
            if (track == null) return;

            if (track.IsSelected && !GetSelectedTracks().Contains(track))
            {
                GetSelectedTracks().Add(track);
            }
            else if (!track.IsSelected)
            {
                GetSelectedTracks().Remove(track);
            }

            UpdatePlayButtonText();
        }

        [RelayCommand]
        public async Task OpenArtist(Artists artist)
        {
            var artistDisplay = await _artistDisplayService.GetArtistByIdAsync(artist.ArtistID);
            if (artistDisplay != null)
            {
                await Initialize(true, ContentType.Artist, artistDisplay);
            }
        }

        [RelayCommand]
        public async Task OpenAlbum(int albumID)
        {
            var albumDisplay = await _albumDisplayService.GetAlbumByIdAsync(albumID);
            if (albumDisplay != null)
            {
                await Initialize(true, ContentType.Album, albumDisplay);
            }
        }

        [RelayCommand]
        public async Task OpenGenre(string genreName)
        {
            var genreDisplay = await _genreDisplayService.GetGenreByNameAsync(genreName);
            if (genreDisplay != null)
            {
                await Initialize(true, ContentType.Genre, genreDisplay);
            }
        }

        // High Resolution Image Loading
        public async Task LoadHighResImagesForVisibleTracksAsync(IList<TrackDisplayModel> visibleTracks)
        {
            foreach (var track in visibleTracks)
            {
                if (track.ThumbnailSize != "high")
                {
                    await _trackDisplayService.LoadHighResThumbnailAsync(track);
                    track.ThumbnailSize = "high";
                }
            }
        }

        private void UpdateTrackPlayingStatus(TrackDisplayModel currentTrack)
        {
            if (currentTrack == null) return;

            foreach (var track in Tracks)
            {
                track.IsCurrentlyPlaying = track.TrackID == currentTrack.TrackID;
            }
        }

        private ObservableCollection<TrackDisplayModel> GetSelectedTracks()
        {
            return new ObservableCollection<TrackDisplayModel>(Tracks.Where(track => track.IsSelected));
        }

        [RelayCommand]
        public void PlayAllOrSelected()
        {
            var selectedTracks = GetSelectedTracks();
            if (selectedTracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(selectedTracks.First(), selectedTracks);
            }
            else if (Tracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(Tracks.First(), Tracks);
            }
        }

        [RelayCommand]
        public void RandomizeTracks()
        {
            if (!ShowRandomizeButton || HasNoTracks) return;

            var randomizedTracks = Tracks.OrderBy(x => Guid.NewGuid()).ToList();
            _trackQueueViewModel.PlayThisTrack(randomizedTracks.First(), new ObservableCollection<TrackDisplayModel>(randomizedTracks));
        }

        // Helper methods
        private void UpdatePlayButtonText()
        {
            PlayButtonText = GetSelectedTracks().Any() ? "Play Selected" : "Play All";
        }

        [RelayCommand]
        public void AddToQueue(TrackDisplayModel track = null)
        {
            // Add a list of tracks at the end of queue
            var tracksList = track == null ? GetSelectedTracks() : new ObservableCollection<TrackDisplayModel>();

            if (tracksList.Count < 1) tracksList.Add(track);

            _trackQueueViewModel.AddTrackToQueue(tracksList);
            DeselectAllTracks();

        }
            [RelayCommand]
        public void AddAsNextTracks(TrackDisplayModel track = null)
        {
            // Add a list of tracks to play next
            var tracksList = track == null ? GetSelectedTracks() : new ObservableCollection<TrackDisplayModel>();

            if (tracksList.Count < 1) tracksList.Add(track);

            _trackQueueViewModel.AddToPlayNext(tracksList);
            DeselectAllTracks();
        }

        [RelayCommand]
        public void PlayTrack(TrackDisplayModel track)
        {
            _trackControlViewModel.PlayCurrentTrack(track, Tracks);
        }

        [RelayCommand]
        public void AddTracks(TrackDisplayModel track)
        {
            // Implement add tracks for playlist
        }

        [RelayCommand]
        public void DeselectAllTracks()
        {
            foreach (var track in Tracks)
            {
                track.IsSelected = false;
            }
            SelectedTracks.Clear();
            UpdatePlayButtonText();
        }

        [RelayCommand]
        private async Task ToggleTrackLike(TrackDisplayModel track)
        {
            track.IsLiked = !track.IsLiked;
            track.LikeIcon = Application.Current?.FindResource(
                track.IsLiked ? "LikeOnIcon" : "LikeOffIcon");

            await _tracksService.UpdateTrackLike(track.TrackID, track.IsLiked);
            _messenger.Send(new TrackLikeUpdateMessage(track.TrackID, track.IsLiked));
        }

        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync();
        }
    }
}