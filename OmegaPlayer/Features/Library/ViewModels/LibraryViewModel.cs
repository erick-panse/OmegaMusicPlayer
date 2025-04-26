using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
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
using OmegaPlayer.Core.Messages;
using NAudio.Wave;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Features.Shell.Views;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Core.Enums;

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
        Search,
        Library,
        Artist,
        Album,
        Genre,
        Playlist,
        Folder,
        Config,
        Detail,
        NowPlaying
    }

    public partial class LibraryViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private AsyncRelayCommand _loadMoreItemsCommand;
        public ICommand LoadMoreItemsCommand => _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        private readonly TrackDisplayService _trackDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly TrackControlViewModel _trackControlViewModel;
        private readonly MainViewModel _mainViewModel;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly GenreDisplayService _genreDisplayService;
        private readonly PlaylistDisplayService _playlistDisplayService;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly TrackStatsService _trackStatsService;
        private readonly LocalizationService _localizationService;
        private readonly StandardImageService _standardImageService;

        [ObservableProperty]
        private ViewType _currentViewType = ViewType.Card;

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
        private string _playButtonText;

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

        [ObservableProperty]
        private int _dropIndex = -1;

        [ObservableProperty]
        private bool _showDropIndicator;


        #region properties required to hide the content specific to details view model
        [ObservableProperty]
        private bool _isReorderMode = false;

        [ObservableProperty]
        private bool _isPlaylistContent = false;

        [ObservableProperty]
        private bool _hideRemoveFromPlaylist = true; // "true" to hide the Remove button

        [ObservableProperty]
        private bool _isNowPlayingContent = false;

        [ObservableProperty]
        private TrackDisplayModel _draggedTrack = null;
        #endregion

        public LibraryViewModel(
            TrackDisplayService trackDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            AllTracksRepository allTracksRepository,
            TrackControlViewModel trackControlViewModel,
            MainViewModel mainViewModel,
            ArtistDisplayService artistDisplayService,
            AlbumDisplayService albumDisplayService,
            GenreDisplayService genreDisplayService,
            PlaylistDisplayService playlistDisplayService,
            PlaylistsViewModel playlistViewModel,
            TrackSortService trackSortService,
            TrackStatsService trackStatsService,
            LocalizationService localizationService,
            StandardImageService standardImageService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
            : base(trackSortService, messenger, errorHandlingService)
        {
            _trackDisplayService = trackDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _allTracksRepository = allTracksRepository;
            _trackControlViewModel = trackControlViewModel;
            _mainViewModel = mainViewModel;
            _artistDisplayService = artistDisplayService;
            _albumDisplayService = albumDisplayService;
            _genreDisplayService = genreDisplayService;
            _playlistDisplayService = playlistDisplayService;
            _playlistViewModel = playlistViewModel;
            _trackStatsService = trackStatsService;
            _localizationService = localizationService;
            _standardImageService = standardImageService;

            LoadAllTracksAsync();

            CurrentViewType = _mainViewModel.CurrentViewType;

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

            // Update Content on profile switch
            _messenger.Register<ProfileUpdateMessage>(this, async (r, m) => await HandleProfileSwitch(m));
        }

        private async Task HandleProfileSwitch(ProfileUpdateMessage message)
        {
            // LoadInitialTracksAsync already does the cleaning steps
            await LoadInitialTracksAsync();
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

        public override void OnSortSettingsReceived(SortType sortType, SortDirection direction, bool isUserInitiated = false)
        {
            // Library view always accepts sort settings
            // Update the sort settings
            CurrentSortType = sortType;
            CurrentSortDirection = direction;

            // Apply the new sort if we're initialized AND this is user-initiated
            if (isUserInitiated && AllTracks?.Any() == true)
            {
                ApplyCurrentSort();
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


        public async Task Initialize(bool forceReload = false)
        {
            // No longer need contentType parameter or data parameter
            ContentType = ContentType.Library;
            DeselectAllTracks();

            if (forceReload || !AllTracks?.Any() == true)
            {
                await LoadInitialTracksAsync();
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
            await _allTracksRepository.LoadTracks();
            AllTracks = _allTracksRepository.AllTracks.ToList();
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

        /// <summary>
        /// Updates the LoadMoreItems method to mark initially loaded tracks as visible
        /// </summary>
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

                        // Load thumbnails with visibility flag set to true since these are the initially visible tracks
                        await _trackDisplayService.LoadTrackCoverAsync(track, "low", true);
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
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading track library",
                    ex.Message,
                    ex,
                    true);
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
                _messenger.Send(new NavigationRequestMessage(ContentType.Artist, artistDisplay));
            }
        }

        [RelayCommand]
        public async Task OpenAlbum(int albumID)
        {
            var albumDisplay = await _albumDisplayService.GetAlbumByIdAsync(albumID);
            if (albumDisplay != null)
            {
                _messenger.Send(new NavigationRequestMessage(ContentType.Album, albumDisplay));
            }
        }

        [RelayCommand]
        public async Task OpenGenre(string genreName)
        {
            var genreDisplay = await _genreDisplayService.GetGenreByNameAsync(genreName);
            if (genreDisplay != null)
            {
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
        public void RandomizeTracks()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    if (HasNoTracks) return;

                    var sortedTracks = GetSortedAllTracks();
                    var randomizedTracks = sortedTracks.OrderBy(x => Guid.NewGuid()).ToList();

                    // Play first track but mark queue as shuffled
                    _trackQueueViewModel.PlayThisTrack(
                        randomizedTracks.First(),
                        new ObservableCollection<TrackDisplayModel>(randomizedTracks));

                    _trackQueueViewModel.IsShuffled = true;
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
            var tracksList = track == null
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
            var tracksList = track == null
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
                            ? new List<TrackDisplayModel> { track }
                            : SelectedTracks.ToList();

                        var dialog = new PlaylistSelectionDialog();
                        dialog.Initialize(_playlistViewModel, this, selectedTracks);
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
    }
}