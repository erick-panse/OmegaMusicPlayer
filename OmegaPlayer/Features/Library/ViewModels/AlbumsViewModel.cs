using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using System.Collections.Generic;
using OmegaPlayer.Features.Shell.ViewModels;
using CommunityToolkit.Mvvm.Messaging;
using Avalonia.Controls.ApplicationLifetimes;
using OmegaPlayer.Features.Playlists.Views;
using Avalonia;
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.UI;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Navigation.Services;

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

        private int _currentPage = 1;

        private const int _pageSize = 50;

        private bool _isInitialized = false;

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
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _albumsDisplayService = albumsDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _playlistViewModel = playlistViewModel;
            _standardImageService = standardImageService;
            _mainViewModel = mainViewModel;

            LoadInitialAlbums();

            // Update Content on profile switch
            _messenger.Register<ProfileUpdateMessage>(this, async (r, m) => await HandleProfileSwitch(m));
        }

        private async Task HandleProfileSwitch(ProfileUpdateMessage message)
        {
            // Reset state
            _isInitialized = false;
            AllAlbums = null;

            // Clear collections on UI thread to prevent cross-thread exceptions
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                Albums.Clear();
                SelectedAlbums.Clear();
                HasSelectedAlbums = false;
            });

            // Reset pagination
            _currentPage = 1;

            // Trigger reload
            await LoadMoreItems();
        }
        
        // Cleanup method that can be called manually if needed
        public void Cleanup()
        {
            // Unregister from all messengers
            _messenger.UnregisterAll(this);

            // Perform any other cleanup needed
            AllAlbums = null;
            SelectedAlbums.Clear();
            Albums.Clear();
        }

        protected override void ApplyCurrentSort()
        {
            if (!_isInitialized) return;

            // Clear existing albums
            Albums.Clear();
            _currentPage = 1;

            // Load first page with new sort settings
            LoadMoreItems().ConfigureAwait(false);
        }


        private async void LoadInitialAlbums()
        {
            await Task.Delay(100);

            if (!_isInitialized)
            {
                _isInitialized = true;
                await LoadMoreItems();
            }

        }

        public override void OnSortSettingsReceived(SortType sortType, SortDirection direction, bool isUserInitiated = false)
        {
            base.OnSortSettingsReceived(sortType, direction, false); // Never auto-apply sort

            if (!_isInitialized)
            {
                LoadInitialAlbums();
            }
            else if (isUserInitiated)
            {
                // Only apply sort if user initiated the change
                ApplyCurrentSort();
            }
        }

        // visibility tracking
        public async Task NotifyAlbumVisible(AlbumDisplayModel album, bool isVisible)
        {
            if (album?.CoverPath == null) return;

            // Let the image services know about visibility change
            if (_standardImageService != null)
            {
                await _standardImageService.NotifyImageVisible(album.CoverPath, isVisible);
            }
        }

        private async Task LoadMoreItems()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // Load all albums for correct sorting
                AllAlbums = await _albumsDisplayService.GetAllAlbumsAsync();

                // Get the sorted list based on current sort settings
                var sortedAlbums = GetSortedAllAlbums();

                // Calculate the page range
                var startIndex = (_currentPage - 1) * _pageSize;
                var pageItems = sortedAlbums
                    .Skip(startIndex)
                    .Take(_pageSize)
                    .ToList();

                var totalAlbums = pageItems.Count;
                var current = 0;
                var newAlbums = new List<AlbumDisplayModel>();

                foreach (var album in pageItems)
                {
                    await Task.Run(async () =>
                    {
                        // Mark as visible when loading
                        await _albumsDisplayService.LoadAlbumCoverAsync(album, "low", true);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            newAlbums.Add(album);
                            current++;
                            LoadingProgress = (current * 100.0) / totalAlbums;
                        });
                    });
                }

                // Add all processed albums to the collection
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var album in newAlbums)
                    {
                        Albums.Add(album);
                    }
                });

                _currentPage++;
            }
            finally
            {
                IsLoading = false;
            }
        }

        private IEnumerable<AlbumDisplayModel> GetSortedAllAlbums()
        {
            if (AllAlbums == null) return new List<AlbumDisplayModel>();

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
            if (album.IsSelected)
            {
                SelectedAlbums.Add(album);
            }
            else
            {
                SelectedAlbums.Remove(album);
            }
            HasSelectedAlbums = SelectedAlbums.Any();
        }


        [RelayCommand]
        public void ClearSelection()
        {
            foreach (var album in Albums)
            {
                album.IsSelected = false;
            }
            SelectedAlbums.Clear();
            HasSelectedAlbums = false;
        }

        [RelayCommand]
        public async Task PlayAlbumFromHere(AlbumDisplayModel selectedAlbum)
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

            if (!allAlbumTracks.Any()) return;

            var startTrack = allAlbumTracks[startPlayingFromIndex];
            _trackQueueViewModel.PlayThisTrack(startTrack, new ObservableCollection<TrackDisplayModel>(allAlbumTracks));
        }


        [RelayCommand]
        public async Task PlayAlbumTracks(AlbumDisplayModel album)
        {
            if (album == null) return;

            var tracks = await _albumsDisplayService.GetAlbumTracksAsync(album.AlbumID);
            if (tracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddAlbumTracksToNext(AlbumDisplayModel album)
        {
            if (album == null) return;

            var tracks = await _albumsDisplayService.GetAlbumTracksAsync(album.AlbumID);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddAlbumTracksToQueue(AlbumDisplayModel album)
        {
            if (album == null) return;

            var tracks = await _albumsDisplayService.GetAlbumTracksAsync(album.AlbumID);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        public async Task<List<TrackDisplayModel>> GetSelectedAlbumTracks(int albumId)
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
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(AlbumDisplayModel album)
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
        }

    }
}