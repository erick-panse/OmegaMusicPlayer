using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Enums.LibraryEnums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Playlists.Views;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class AlbumsViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly AlbumDisplayService _albumsDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly StandardImageService _standardImageService;
        private readonly MainViewModel _mainViewModel;

        private List<AlbumDisplayModel> AllAlbums { get; set; }

        [ObservableProperty]
        private ObservableCollection<AlbumDisplayModel> _albums = new();

        [ObservableProperty]
        private ObservableCollection<AlbumDisplayModel> _selectedAlbums = new();

        [ObservableProperty]
        private bool _hasSelectedAlbums;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private double _loadingProgress;

        private bool _isApplyingSort = false;
        private bool _isAllAlbumsLoaded = false;
        private bool _isAlbumsLoaded = false;
        private bool _isInitializing = false;
        private CancellationTokenSource _loadingCancellationTokenSource;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public AlbumsViewModel(
            AlbumDisplayService albumsDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            PlaylistsViewModel playlistViewModel,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            StandardImageService standardImageService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
            : base(trackSortService, messenger, errorHandlingService)
        {
            _albumsDisplayService = albumsDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _playlistViewModel = playlistViewModel;
            _standardImageService = standardImageService;
            _mainViewModel = mainViewModel;

            // Mark as false to load all tracks 
            _messenger.Register<AllTracksInvalidatedMessage>(this, (r, m) =>
            {
                _isAllAlbumsLoaded = false;
                _isAlbumsLoaded = false;
            });
        }

        protected override async void ApplyCurrentSort()
        {
            // Skip sorting if it is already running
            if (_isApplyingSort)
                return;

            // Cancel any ongoing loading operation
            _loadingCancellationTokenSource?.Cancel();

            _isApplyingSort = true;

            try
            {
                // Reset loading state
                _isAlbumsLoaded = false;

                // Small delay to ensure cancellation is processed
                await Task.Delay(10);

                // Reset cancellation token source for new operation
                _loadingCancellationTokenSource?.Dispose();
                _loadingCancellationTokenSource = new CancellationTokenSource();

                await LoadMoreItems();
            } 
            finally
            {
                _isApplyingSort = false;
            }
        }

        public override void OnSortSettingsReceived(SortType sortType, SortDirection direction, bool isUserInitiated = false)
        {
            // Update the sort settings
            CurrentSortType = sortType;
            CurrentSortDirection = direction;

            // Apply the new sort if we're initialized AND this is user-initiated
            if (isUserInitiated && _isAllAlbumsLoaded)
            {
                ApplyCurrentSort();
            }
        }

        public async Task Initialize()
        {
            // Prevent multiple initializations
            if (_isInitializing) return;

            _isInitializing = true;
            ClearSelection();

            try
            {
                // Small delay to let MainViewModel send sort settings first
                await Task.Delay(1);
                await LoadInitialAlbums();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async Task LoadInitialAlbums()
        {
            _isAlbumsLoaded = false;

            // Ensure AllTracks is loaded first (might already be loaded from constructor)
            if (!_isAllAlbumsLoaded)
            {
                await LoadAllAlbumsAsync();
            }

            await LoadMoreItems();
        }

        /// <summary>
        /// Loads AllAlbums in background without affecting UI
        /// </summary>
        private async Task LoadAllAlbumsAsync()
        {
            if (_isAllAlbumsLoaded) return;

            try
            {
                AllAlbums = await _albumsDisplayService.GetAllAlbumsAsync();
                _isAllAlbumsLoaded = true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading AllAlbums from database",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Notifies the image loading system about album visibility changes and loads images for visible albums
        /// </summary>
        public async Task NotifyAlbumVisible(AlbumDisplayModel album, bool isVisible)
        {
            if (album?.CoverPath == null) return;

            try
            {
                // Notify the image service about visibility changes for optimization
                if (_standardImageService != null)
                {
                    await _standardImageService.NotifyImageVisible(album.CoverPath, isVisible);
                }

                // If album becomes visible and hasn't had its image loaded yet, load it now
                if (isVisible)
                {
                    // Load the image in the background with lower priority
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _albumsDisplayService.LoadAlbumCoverAsync(album, "low", true);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error loading album image",
                                ex.Message,
                                ex,
                                false);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling album visibility notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Load Albums to UI with selected sort order.
        /// With virtualization, images are loaded only when items become visible.
        /// </summary>
        private async Task LoadMoreItems()
        {
            if (IsLoading || _isAlbumsLoaded) return;

            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _loadingCancellationTokenSource.Token;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // If no albums available, return empty
                if (!_isAllAlbumsLoaded || AllAlbums?.Any() != true)
                {
                    return;
                }

                // Clear albums immediately on UI thread
                Albums.Clear();

                // Get sorted albums
                var sortedAlbums = await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sorted = GetSortedAllAlbums();
                    var processed = new List<AlbumDisplayModel>();
                    int progress = 0;

                    // Pre-process albums WITHOUT loading images
                    foreach (var album in sorted)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        processed.Add(album);

                        progress++;
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LoadingProgress = Math.Min(95, (int)((progress * 100.0) / sorted.Count()));
                        }, Avalonia.Threading.DispatcherPriority.Background);
                    }

                    return processed;
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var album in sortedAlbums)
                    {
                        Albums.Add(album);
                    }

                    LoadingProgress = 100;
                }, Avalonia.Threading.DispatcherPriority.Background);

                _isAlbumsLoaded = true;
            }
            catch (OperationCanceledException)
            {
                _isAlbumsLoaded = false;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading album library",
                    ex.Message,
                    ex,
                    true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private IEnumerable<AlbumDisplayModel> GetSortedAllAlbums()
        {
            if (AllAlbums == null || !AllAlbums.Any()) return new List<AlbumDisplayModel>();

            var sortedAlbums = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    AllAlbums,
                    SortType.Duration,
                    CurrentSortDirection,
                    a => a.Title,
                    a => (int)a.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    AllAlbums,
                    SortType.Name,
                    CurrentSortDirection,
                    a => a.Title)
            };

            return sortedAlbums;
        }


        [RelayCommand]
        public async Task OpenAlbumDetails(AlbumDisplayModel album)
        {
            if (album == null) return;
            await _mainViewModel.NavigateToDetails(ContentType.Album, album);
        }

        [RelayCommand]
        public void SelectAlbum(AlbumDisplayModel album)
        {
            if (album == null) return;

            if (album.IsSelected)
            {
                SelectedAlbums.Add(album);
            }
            else
            {
                SelectedAlbums.Remove(album);
            }
            HasSelectedAlbums = SelectedAlbums.Count > 0;
        }

        [RelayCommand]
        public void SelectAll()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    SelectedAlbums.Clear();
                    foreach (var album in Albums)
                    {
                        album.IsSelected = true;
                        SelectedAlbums.Add(album);
                    }
                    HasSelectedAlbums = SelectedAlbums.Count > 0;
                },
                "Selecting all albums",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public void ClearSelection()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    foreach (var album in Albums)
                    {
                        album.IsSelected = false;
                    }
                    SelectedAlbums.Clear();
                    HasSelectedAlbums = SelectedAlbums.Count > 0;
                },
                "Clearing album selection",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task PlayAlbumFromHere(AlbumDisplayModel selectedAlbum)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (selectedAlbum == null) return;

                    var allAlbumTracks = new List<TrackDisplayModel>();
                    var startPlayingFromIndex = 0;
                    var tracksAdded = 0;

                    // Get sorted list of all albums
                    var sortedAlbums = GetSortedAllAlbums();

                    foreach (var album in sortedAlbums)
                    {
                        // Get tracks for this album and sort them by Title
                        var tracks = (await _albumsDisplayService.GetAlbumTracksAsync(album.AlbumID))
                            .OrderBy(t => t.Title)
                            .ToList();

                        if (album.AlbumID == selectedAlbum.AlbumID)
                        {
                            startPlayingFromIndex = tracksAdded;
                        }

                        allAlbumTracks.AddRange(tracks);
                        tracksAdded += tracks.Count;
                    }

                    if (allAlbumTracks.Count < 1) return;

                    var startTrack = allAlbumTracks[startPlayingFromIndex];
                    await _trackQueueViewModel.PlayThisTrack(startTrack, new ObservableCollection<TrackDisplayModel>(allAlbumTracks));
                },
                "Playing tracks from selected album",
                ErrorSeverity.Playback,
                false);
        }


        [RelayCommand]
        public async Task PlayAlbumTracks(AlbumDisplayModel album)
        {
            if (album == null) return;

            var tracks = await _albumsDisplayService.GetAlbumTracksAsync(album.AlbumID);
            if (tracks.Count > 0)
            {
                await _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddAlbumTracksToNext(AlbumDisplayModel album)
        {
            var tracks = await GetTracksToAdd(album);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }

            ClearSelection();
        }

        [RelayCommand]
        public async Task AddAlbumTracksToQueue(AlbumDisplayModel album)
        {
            var tracks = await GetTracksToAdd(album);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }

            ClearSelection();
        }

        /// <summary>
        /// Helper that returns the tracks to be added in Play next and Add to Queue methods
        /// </summary>
        public async Task<List<TrackDisplayModel>> GetTracksToAdd(AlbumDisplayModel album)
        {
            var albumsList = SelectedAlbums.Count > 0
                ? SelectedAlbums
                : new ObservableCollection<AlbumDisplayModel>();

            if (albumsList.Count < 1 && album != null)
            {
                albumsList.Add(album);
            }

            var tracks = new List<TrackDisplayModel>();

            foreach (var albumToAdd in albumsList)
            {
                var albumTracks = await _albumsDisplayService.GetAlbumTracksAsync(albumToAdd.AlbumID);

                if (albumTracks.Count > 0)
                    tracks.AddRange(albumTracks);
            }

            return tracks;
        }

        public async Task<List<TrackDisplayModel>> GetSelectedAlbumTracks(int albumId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var selectedAlbums = SelectedAlbums;
                    if (selectedAlbums.Count <= 1)
                    {
                        return await _albumsDisplayService.GetAlbumTracksAsync(albumId);
                    }

                    var trackTasks = selectedAlbums.Select(album =>
                        _albumsDisplayService.GetAlbumTracksAsync(album.AlbumID));

                    var allTrackLists = await Task.WhenAll(trackTasks);
                    return allTrackLists.SelectMany(tracks => tracks).ToList();
                },
                "Getting selected album tracks",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(AlbumDisplayModel album)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var mainWindow = desktop.MainWindow;
                        if (mainWindow == null || !mainWindow.IsVisible) return;

                        var selectedTracks = await GetSelectedAlbumTracks(album.AlbumID);

                        var dialog = new PlaylistSelectionDialog();
                        dialog.Initialize(_playlistViewModel, null, selectedTracks);
                        await dialog.ShowDialog(mainWindow);

                        ClearSelection();
                    }
                },
                "Showing playlist selection dialog for album tracks",
                ErrorSeverity.NonCritical,
                true);
        }
    }
}