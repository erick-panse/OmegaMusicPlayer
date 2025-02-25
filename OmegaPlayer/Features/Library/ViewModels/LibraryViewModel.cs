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
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Playback.Services;

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
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly PlaylistTracksService _playlistTracksService;
        private readonly MediaService _mediaService;
        private readonly TrackStatsService _trackStatsService;

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
        private bool _isNowPlayingContent; // For NowPlaying mode

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


        // Edit order variables
        [ObservableProperty]
        private bool _isReorderMode;

        [ObservableProperty]
        private bool _showReorderControls;

        [ObservableProperty]
        private TrackDisplayModel _draggedTrack;

        [ObservableProperty]
        private int _dropIndex = -1;

        [ObservableProperty]
        private bool _showDropIndicator;

        private List<TrackDisplayModel> _originalOrder;

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
            PlaylistsViewModel playlistViewModel,
            TrackSortService trackSortService,
            PlaylistTracksService playlistTracksService,
            MediaService mediaService,
            TrackStatsService trackStatsService,
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
            _playlistTracksService = playlistTracksService;
            _trackStatsService = trackStatsService;
            _mediaService = mediaService;

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
        protected override async void ApplyCurrentSort()
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
                await LoadMoreItems();
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
            bool callInitial = true;
            if (ContentType == ContentType.Library) callInitial = false;

            IsDetailsMode = isDetails;
            ContentType = type;
            IsNowPlayingContent = type == ContentType.NowPlaying;
            DeselectAllTracks();

            if (isDetails)
            {
                LoadContent(data);
            }
            else
            {
                Title = "Library";
                Description = string.Empty;
                Image = null;

                if (callInitial) await LoadInitialTracksAsync();
            }

        }

        public async Task LoadInitialTracksAsync()
        {
            Tracks.Clear();
            _currentPage = 1;
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
            if (IsLoading) return;

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

                            // Initialize track position 
                            track.Position = startIndex + current;

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
            _currentPage = 1; // Reset paging
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
            if (genre == null) return;
            Title = genre.Name;
            Description = $"{genre.TrackCount} tracks • {genre.TotalDuration:hh\\:mm\\:ss}";
            Image = genre.Photo;
        }

        private void LoadPlaylistContent(PlaylistDisplayModel playlist)
        {
            if (playlist == null) return;
            Title = playlist.Title;
            Description = $"Created {playlist.CreatedAt:d} • {playlist.TrackCount} tracks • {playlist.TotalDuration:hh\\:mm\\:ss}";
            Image = playlist.Cover;
        }

        private void LoadFolderContent(FolderDisplayModel folder)
        {
            if (folder == null) return;
            Title = folder.FolderName;
            Description = $"{folder.TrackCount} tracks • {folder.TotalDuration:hh\\:mm\\:ss}";
            Image = folder.Cover;
        }
        // Implement content type specific loading methods
        private void LoadArtistContent(ArtistDisplayModel artist)
        {
            if (artist == null) return;
            Title = artist.Name;
            Description = artist.Bio;
            Image = artist.Photo;
        }

        private void LoadAlbumContent(AlbumDisplayModel album)
        {
            if (album == null) return;
            Title = album.Title;
            Description = $"By {album.ArtistName} • {album.TrackCount} tracks • {album.TotalDuration:hh\\:mm\\:ss}";
            Image = album.Cover;
        }
        private void LoadNowPlayingContent(NowPlayingInfo info)
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
                            IsPlaylistContent = playlist.IsFavoritePlaylist == true ? false : true; // Hide remove option if in Favorites playlit
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
            if (IsNowPlayingContent || HasNoTracks) return;

            var sortedTracks = GetSortedAllTracks();
            var randomizedTracks = sortedTracks.OrderBy(x => Guid.NewGuid()).ToList();

            // Play first track but mark queue as shuffled
            _trackQueueViewModel.PlayThisTrack(
                randomizedTracks.First(),
                new ObservableCollection<TrackDisplayModel>(randomizedTracks));

            _trackQueueViewModel.IsShuffled = true;
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
        public async Task PlayTrack(TrackDisplayModel track)
        {
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
                        LoadContent(_currentContent);
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
            if (track == null) return;

            track.IsLiked = !track.IsLiked;
            track.LikeIcon = Application.Current?.FindResource(
                track.IsLiked ? "LikeOnIcon" : "LikeOffIcon");

            await _trackStatsService.UpdateTrackLike(track.TrackID, track.IsLiked);

        }

        partial void OnContentTypeChanged(ContentType value)
        {
            ShowReorderControls = value == ContentType.Playlist || value == ContentType.NowPlaying;
        }

        partial void OnDropIndexChanged(int value)
        {
            ShowDropIndicator = value >= 0;
        }
        partial void OnIsReorderModeChanged(bool value)
        {
            _messenger.Send(new ReorderModeMessage(value,
                () => SaveReorderedTracksCommand.Execute(null),
                () => CancelReorderCommand.Execute(null)));
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
            if (ContentType == ContentType.Playlist)
            {
                var playlist = _currentContent as PlaylistDisplayModel;
                if (playlist != null)
                {
                    try
                    {
                        var allPlaylistTracks = await _playlistDisplayService.GetPlaylistTracksAsync(playlist.PlaylistID);
                        var reorderedTracks = new List<TrackDisplayModel>();

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
                            reorderedTracks.Add(track);
                        }

                        // Add remaining non-visible tracks
                        foreach (var track in allPlaylistTracks)
                        {
                            if (!visibleTracksOrder.ContainsKey((track.TrackID, track.PlaylistPosition)))
                            {
                                reorderedTracks.Add(track);
                            }
                        }

                        // Update positions sequentially
                        for (int i = 0; i < reorderedTracks.Count; i++)
                        {
                            reorderedTracks[i].PlaylistPosition = i;
                        }

                        // Save to database
                        await _playlistTracksService.UpdateTrackOrder(playlist.PlaylistID, reorderedTracks);

                        // Check if first track changed
                        var newFirstTrack = reorderedTracks.FirstOrDefault();
                        if (newFirstTrack != null && newFirstTrack.CoverPath != playlist.CoverPath)
                        {
                            // Get new cover path from media service
                            var media = await _mediaService.GetMediaById(newFirstTrack.CoverID);
                            if (media != null)
                            {
                                playlist.CoverPath = media.CoverPath;
                                // Load new cover image
                                await _playlistDisplayService.LoadPlaylistCoverAsync(playlist);
                                // Update the display image
                                Image = playlist.Cover;
                            }
                        }

                        // Reload tracks to show updated order
                        LoadContent(_currentContent);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error saving reordered tracks: {ex.Message}");
                    }

                }
            }
            else if (ContentType == ContentType.NowPlaying)
            {
                try
                {
                    // Get reference to all tracks in the queue
                    var allQueueTracks = _trackQueueViewModel.NowPlayingQueue.ToList();

                    // Get the track currently playing
                    var currentTrack = allQueueTracks.FirstOrDefault(t => t.IsCurrentlyPlaying);
                    int newCurrentTrackIndex = -1;

                    // Create a map of visible tracks with their current UI positions
                    var visibleTracksOrder = new Dictionary<(int TrackId, int OriginalPosition), int>();
                    for (int i = 0; i < Tracks.Count; i++)
                    {
                        var track = Tracks[i];
                        visibleTracksOrder[(track.TrackID, track.NowPlayingPosition)] = i;
                    }
                    // Create the final ordered list
                    var reorderedTracks = new List<TrackDisplayModel>();

                    // Add visible tracks in their new order (as shown in UI after drag-drop)
                    foreach (var track in Tracks)
                    {
                        reorderedTracks.Add(track);
                    }

                    // Add remaining non-visible tracks
                    foreach (var track in allQueueTracks)
                    {
                        if (!visibleTracksOrder.ContainsKey((track.TrackID, track.NowPlayingPosition)))
                        {
                            reorderedTracks.Add(track);
                        }
                    }

                    // Update positions to match the new order
                    for (int i = 0; i < reorderedTracks.Count; i++)
                    {
                        reorderedTracks[i].NowPlayingPosition = i;
                    }

                    if (currentTrack != null)
                    {
                        // Find its new position in reorderedTracks
                        newCurrentTrackIndex = reorderedTracks.FindIndex(t =>
                            t.TrackID == currentTrack.TrackID &&
                            t.NowPlayingPosition == currentTrack.NowPlayingPosition);
                    }

                    // Use bridge method to save the reordered queue
                    await _trackQueueViewModel.SaveReorderedQueue(reorderedTracks, newCurrentTrackIndex);

                    // Reload the content to reflect changes
                    LoadContent(_currentContent);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error saving reordered queue: {ex.Message}");
                }
            }

            IsReorderMode = false;
            DraggedTrack = null;
            DropIndex = -1;
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
            DraggedTrack = null;
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
            DraggedTrack = null;
            DropIndex = -1;
            UpdateDropIndicators(-1);
        }

        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync();
        }
    }
}