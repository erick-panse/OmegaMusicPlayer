using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using NAudio.Wave;
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
using OmegaPlayer.UI;
using OmegaPlayer.UI.Services;
using OmegaPlayer.Features.Shell.Views;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Core.Enums;

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

        // Add cancellation token source for load operations
        private CancellationTokenSource _cts = new CancellationTokenSource();

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.Card;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _description;

        [ObservableProperty]
        private Avalonia.Media.Imaging.Bitmap _image;

        [ObservableProperty]
        private object _detailsIcon;

        [ObservableProperty]
        private ContentType _contentType;

        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _selectedTracks = new();

        public ObservableCollection<TrackDisplayModel> Tracks { get; } = new();

        public List<TrackDisplayModel> AllTracks { get; set; }

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
        private ObservableCollection<PlaylistDisplayModel> _availablePlaylists = new();

        private object _currentContent;
        private int _currentPage = 1;
        private const int _pageSize = 50;
        private bool _isApplyingSort = false;

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

            LoadAvailablePlaylists();

            UpdatePlayButtonText();

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
            // Cancel any ongoing loading operation
            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();

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
                // Cancel any ongoing loading operation
                _cts.Cancel();
                _cts.Dispose();
                _cts = new CancellationTokenSource();

                ApplyCurrentSort();
            }
        }

        public async Task Initialize(ContentType type, object data)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Cancel any ongoing loading operation
                    _cts.Cancel();
                    _cts.Dispose();
                    _cts = new CancellationTokenSource();

                    ContentType = type;
                    ChangeContentTypeText(type);
                    IsNowPlayingContent = type == ContentType.NowPlaying;
                    IsPlaylistContent = type == ContentType.Playlist;
                    DeselectAllTracks();

                    LoadContent(data);

                    await LoadAllTracksAsync();
                    await LoadMoreItems();
                },
                $"Initializing details view for {type}",
                ErrorSeverity.NonCritical,
                true);
        }

        /// <summary>
        /// Notifies the image loading system about track visibility changes
        /// </summary>
        public async Task NotifyTrackVisible(TrackDisplayModel track, bool isVisible)
        {
            if (track?.CoverPath == null) return;

            if (_standardImageService != null)
            {
                await _standardImageService.NotifyImageVisible(track.CoverPath, isVisible);
            }
        }

        public async Task LoadAllTracksAsync()
        {
            AllTracks = await LoadTracksForContent(1, int.MaxValue);
        }

        private async Task LoadMoreItems()
        {
            if (IsLoading) return;

            // Get the cancellation token for this load operation
            var cancellationToken = _cts.Token;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // Check if cancelled
                cancellationToken.ThrowIfCancellationRequested();

                await LoadAllTracksAsync();

                // Check again after potentially long operation
                cancellationToken.ThrowIfCancellationRequested();

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
                    // Check if cancelled before processing each track
                    cancellationToken.ThrowIfCancellationRequested();

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

                    // Load thumbnail for the track
                    await _trackDisplayService.LoadTrackCoverAsync(track, "low", true);
                }

                // Check if cancelled before updating UI
                cancellationToken.ThrowIfCancellationRequested();

                // Once all tracks are processed, add them to the collection
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var track in newTracks)
                    {
                        Tracks.Add(track);
                    }
                });

                _currentPage++;
                HasNoTracks = !Tracks.Any();
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled, just exit quietly
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading albums",
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
                                HideRemoveFromPlaylist = playlist.IsFavoritePlaylist;
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
                        await _trackDisplayService.LoadTrackCoverAsync(track, "low");
                    }

                    return tracks;
                },
                $"Loading tracks for {ContentType}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                true);
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

            UpdatePlayButtonText();
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
                    PlayButtonText = SelectedTracks.Any()
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
            var tracksList = track == null || SelectedTracks.Any()
                ? SelectedTracks
                : new ObservableCollection<TrackDisplayModel>();

            if (tracksList.Count < 1 && track != null)
            {
                tracksList.Add(track);
            }

            _trackQueueViewModel.AddTrackToQueue(tracksList);
            DeselectAllTracks();
        }

        [RelayCommand]
        public void PlayNextTracks(TrackDisplayModel track = null)
        {
            // Add a list of tracks to play next
            var tracksList = track == null || SelectedTracks.Any()
                ? SelectedTracks
                : new ObservableCollection<TrackDisplayModel>();

            if (tracksList.Count < 1 && track != null)
            {
                tracksList.Add(track);
            }

            _trackQueueViewModel.AddToPlayNext(tracksList);
            DeselectAllTracks();
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
                    var selectedTracks = SelectedTracks.Any() == false
                        ? new List<TrackDisplayModel> { track }
                        : SelectedTracks.ToList();

                    if (selectedTracks.Any() && ContentType == ContentType.Playlist)
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
                            if (remainingTracks.Any())
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
                    DeselectAllTracks();
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
                    var tracksToRemove = SelectedTracks.Any()
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

                        DeselectAllTracks();
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

                    // Refresh view
                    LoadContent(new NowPlayingInfo
                    {
                        CurrentTrack = _trackQueueViewModel.CurrentTrack,
                        AllTracks = newQueue,
                        CurrentTrackIndex = newIndex
                    });

                    DeselectAllTracks();
                },
                "Removing tracks from now playing queue",
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(TrackDisplayModel track)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var mainWindow = desktop.MainWindow;
                        if (mainWindow == null || !mainWindow.IsVisible) return;

                        var selectedTracks = SelectedTracks.Count <= 1
                            ? new List<TrackDisplayModel> { track } :
                            SelectedTracks.ToList();

                        var dialog = new PlaylistSelectionDialog();
                        dialog.Initialize(_playlistViewModel, null, selectedTracks);
                        await dialog.ShowDialog(mainWindow);

                        DeselectAllTracks();
                    }
                },
                "Showing playlist selection dialog",
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public void DeselectAllTracks()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    foreach (var track in Tracks)
                    {
                        track.IsSelected = false;
                    }
                    SelectedTracks.Clear();
                    UpdatePlayButtonText();
                },
                "Deselecting all tracks",
                ErrorSeverity.NonCritical,
                false);
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
                    if (ContentType == ContentType.Playlist)
                    {
                        var playlist = _currentContent as PlaylistDisplayModel;
                        if (playlist != null)
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
                                    await _playlistDisplayService.LoadPlaylistCoverAsync(playlist, "medium", true);
                                    // Update the display image
                                    Image = playlist.Cover;
                                }
                            }

                            // Reload tracks to show updated order
                            LoadContent(_currentContent);
                        }
                    }
                    else if (ContentType == ContentType.NowPlaying)
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

                        // prevent loading more items inadvertently
                        _currentPage = _currentPage > 0 ? _currentPage-- : 0;
                        // Reload the already loaded content to reflect changes
                        await LoadMoreItems();
                    }

                    IsReorderMode = false;
                    if (DraggedTrack != null)
                    {
                        DraggedTrack.IsBeingDragged = false;
                        DraggedTrack = null;
                    }
                    DropIndex = -1;
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
                true);
        }
    }
}