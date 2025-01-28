using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using System.Collections.Generic;
using OmegaPlayer.Features.Shell.ViewModels;
using CommunityToolkit.Mvvm.Messaging;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class AlbumViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly AlbumDisplayService _albumsDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly MainViewModel _mainViewModel;

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

        [ObservableProperty]
        private int _currentPage = 1;

        private const int _pageSize = 50;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public AlbumViewModel(
            AlbumDisplayService albumsDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _albumsDisplayService = albumsDisplayService;
            _trackQueueViewModel = trackQueueViewModel;

            LoadInitialAlbums();
            _mainViewModel = mainViewModel;
        }
        protected override void ApplyCurrentSort()
        {
            IEnumerable<AlbumDisplayModel> sortedAlbums = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    Albums,
                    SortType.Duration,
                    CurrentSortDirection,
                    a => a.Title,
                    a => (int)a.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    Albums,
                    SortType.Name,
                    CurrentSortDirection,
                    a => a.Title)
            };

            var sortedAlbumsList = sortedAlbums.ToList();
            Albums.Clear();
            foreach (var album in sortedAlbumsList)
            {
                Albums.Add(album);
            }
        }


        private async void LoadInitialAlbums()
        {
            await LoadMoreItems();
        }

        private async Task LoadMoreItems()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                var albumsPage = await _albumsDisplayService.GetAlbumsPageAsync(CurrentPage, _pageSize);

                var totalAlbums = albumsPage.Count;
                var current = 0;

                foreach (var album in albumsPage)
                {
                    await Task.Run(async () =>
                    {
                        await _albumsDisplayService.LoadAlbumCoverAsync(album);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Albums.Add(album);
                            current++;
                            LoadingProgress = (current * 100.0) / totalAlbums;
                        });
                    });
                }

                ApplyCurrentSort();
                CurrentPage++;
            }
            finally
            {
                await Task.Delay(500);
                IsLoading = false;
            }
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

            foreach (var album in Albums)
            {
                var tracks = await _albumsDisplayService.GetAlbumTracksAsync(album.AlbumID);

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

        public async Task LoadHighResCoversForVisibleAlbumsAsync(IList<AlbumDisplayModel> visibleAlbums)
        {
            foreach (var album in visibleAlbums)
            {
                if (album.CoverSize != "high")
                {
                    await _albumsDisplayService.LoadHighResAlbumCoverAsync(album);
                    album.CoverSize = "high";
                }
            }
        }
    }
}
