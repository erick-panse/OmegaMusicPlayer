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
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using Avalonia;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using OmegaPlayer.Features.Playlists.Views;

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
        Config,
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
        private readonly PlaylistViewModel _playlistViewModel;


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
        private bool _isPlaylistContent; // For Playlist mode

        [ObservableProperty]
        private bool _showRandomizeButton; // Hidden in NowPlaying mode

        [ObservableProperty]
        private bool _isDetailsMode;

        [ObservableProperty]
        private string _playButtonText = "Play All";

        [ObservableProperty]
        private bool _hasNoTracks;

        [ObservableProperty]
        private ObservableCollection<PlaylistDisplayModel> _availablePlaylists = new();

        private object _currentContent;
        private int _currentPage = 1;
        private const int _pageSize = 50;
        private bool _isApplyingSort = false;

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
            PlaylistViewModel playlistViewModel,
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
            _playlistViewModel = playlistViewModel;

            LoadAllTracksAsync();

            CurrentViewType = _mainViewModel.CurrentViewType;

            LoadAvailablePlaylists();

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
            // Skip sorting if in NowPlaying / is already running / is playlist
            if (ContentType == ContentType.NowPlaying || _isApplyingSort || ContentType == ContentType.Playlist)
                return;

            _isApplyingSort = true;

            try
            {
                // Clear existing tracks
                Tracks.Clear();
                _currentPage = 1;

                // Load first page with new sort settings
                LoadMoreItems().ConfigureAwait(false);
            }
            finally
            {
                _isApplyingSort = false;
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
            IsPlaylistContent = type == ContentType.Playlist;
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
                await LoadInitialTracksAsync();
            }

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
            _currentPage = 1;
            Tracks.Clear();
            await LoadMoreItems();
            HasNoTracks = !Tracks.Any();
        }


        public async Task LoadAllTracksAsync()
        {
            if (IsDetailsMode)
            {
                AllTracks = await LoadTracksForContent(1, int.MaxValue);
            }
            else
            {
                await _allTracksRepository.LoadTracks();
                AllTracks = _allTracksRepository.AllTracks;
            }
        }


        private async Task LoadMoreItems()
        {
            if (_isLoading) return;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                await Task.Run(async () =>
                {
                    // First, ensure all tracks are loaded
                    await LoadAllTracksAsync();

                    // Get the sorted list of all tracks based on current sort settings
                    var sortedTracks = GetSortedAllTracks();

                    // Calculate the page range
                    var startIndex = (_currentPage - 1) * _pageSize;
                    var pageItems = sortedTracks
                        .Skip(startIndex)
                        .Take(_pageSize)
                        .ToList();

                    var totalTracks = pageItems.Count;
                    var current = 0;
                    var newTracks = new List<TrackDisplayModel>();

                    foreach (var track in pageItems)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (_trackControlViewModel.CurrentlyPlayingTrack != null)
                            {
                                track.IsCurrentlyPlaying = track.TrackID == _trackControlViewModel.CurrentlyPlayingTrack.TrackID;
                            }

                            // Add to temporary list instead of directly to Tracks
                            newTracks.Add(track);
                            if (track.Artists.Any())
                            {
                                track.Artists.Last().IsLastArtist = false;
                            }
                            track.NowPlayingPosition = startIndex + current;

                            current++;
                            LoadingProgress = (current * 100.0) / totalTracks;
                        });

                        // Load high-res thumbnail for the track
                        await _trackDisplayService.LoadHighResThumbnailAsync(track);
                    }

                    // Once all tracks are processed, add them to the collection
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        foreach (var track in newTracks)
                        {
                            Tracks.Add(track);
                        }
                    });

                    _currentPage++;
                });
            }
            finally
            {
                await Task.Delay(500); // Small delay for smoother UI
                IsLoading = false;
            }
        }
        private ObservableCollection<TrackDisplayModel> GetSortedAllTracks()
        {
            if (AllTracks == null) return new ObservableCollection<TrackDisplayModel>();

            if (ContentType == ContentType.NowPlaying)
            {
                return new ObservableCollection<TrackDisplayModel>(AllTracks);
            }

            var sortedTracks = _trackSortService.SortTracks(
                AllTracks,
                CurrentSortType,
                CurrentSortDirection
            );

            return new ObservableCollection<TrackDisplayModel>(sortedTracks);
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
                            // Important: Use ToList() to create a new list that maintains the queue order
                            tracks = nowPlayingInfo.AllTracks.ToList();

                            // Update NowPlayingPosition for each track based on its queue position
                            for (int i = 0; i < tracks.Count; i++)
                            {
                                tracks[i].NowPlayingPosition = i;
                            }

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

            if (track.IsSelected)
            {
                SelectedTracks.Add(track);
            }
            else
            {
                SelectedTracks.Remove(track);
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
            if (selectedTracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(selectedTracks.First(), selectedTracks);
            }
            else if (Tracks.Any())
            {
                var sortedTracks = GetSortedAllTracks();
                if (sortedTracks.Any())
                {
                    _trackQueueViewModel.PlayThisTrack(sortedTracks.First(), sortedTracks);
                }

            }
        }

        [RelayCommand]
        public void RandomizeTracks()
        {
            if (!ShowRandomizeButton || HasNoTracks) return;

            var sortedTracks = GetSortedAllTracks();
            var randomizedTracks = sortedTracks.OrderBy(x => Guid.NewGuid()).ToList();
            _trackQueueViewModel.PlayThisTrack(randomizedTracks.First(), new ObservableCollection<TrackDisplayModel>(randomizedTracks));
        }

        // Helper methods
        private void UpdatePlayButtonText()
        {
            PlayButtonText = SelectedTracks.Any() ? "Play Selected" : "Play All";
        }

        [RelayCommand]
        public void AddToQueue(TrackDisplayModel track = null)
        {
            // Add a list of tracks at the end of queue
            var tracksList = track == null ? SelectedTracks : new ObservableCollection<TrackDisplayModel>();

            if (tracksList.Count < 1) tracksList.Add(track);

            _trackQueueViewModel.AddTrackToQueue(tracksList);
            DeselectAllTracks();

        }
            [RelayCommand]
        public void AddAsNextTracks(TrackDisplayModel track = null)
        {
            // Add a list of tracks to play next
            var tracksList = track == null ? SelectedTracks : new ObservableCollection<TrackDisplayModel>();

            if (tracksList.Count < 1) tracksList.Add(track);

            _trackQueueViewModel.AddToPlayNext(tracksList);
            DeselectAllTracks();
        }

        [RelayCommand]
        public void PlayTrack(TrackDisplayModel track)
        {
            var sortedTracks = GetSortedAllTracks();
            _trackControlViewModel.PlayCurrentTrack(track, sortedTracks);
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
            try
            {
                var selectedTracks = SelectedTracks.Any() == false
                    ? new List<TrackDisplayModel> { track }
                    : SelectedTracks.ToList();

                if (selectedTracks.Any() && ContentType == ContentType.Playlist)
                {
                    var playlist = _currentContent as PlaylistDisplayModel;
                    if (playlist != null)
                    {
                        await _playlistViewModel.RemoveTracksFromPlaylist(playlist.PlaylistID, selectedTracks);
                        await LoadContent(_currentContent);
                    }
                }
                DeselectAllTracks();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing tracks from playlist: {ex.Message}");
                throw;
            }
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(TrackDisplayModel track)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow == null || !mainWindow.IsVisible) return;

                var selectedTracks = SelectedTracks.Count <= 1
                    ? new List<TrackDisplayModel> { track } : SelectedTracks.ToList();

                var dialog = new PlaylistSelectionDialog();
                dialog.Initialize(_playlistViewModel, this, selectedTracks);
                await dialog.ShowDialog(mainWindow);

                DeselectAllTracks();
            }
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