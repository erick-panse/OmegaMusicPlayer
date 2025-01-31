using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Playlists.Views;
using OmegaPlayer.Features.Shell.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class ArtistViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly ArtistDisplayService _artistsDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly PlaylistViewModel _playlistViewModel;
        private readonly MainViewModel _mainViewModel;

        [ObservableProperty]
        private ObservableCollection<ArtistDisplayModel> _artists = new();

        [ObservableProperty]
        private ObservableCollection<ArtistDisplayModel> _selectedArtists = new();

        [ObservableProperty]
        private bool _hasSelectedArtists;

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

        public ArtistViewModel(
            ArtistDisplayService artistsDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            PlaylistViewModel playlistViewModel,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _artistsDisplayService = artistsDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _playlistViewModel = playlistViewModel;
            _mainViewModel = mainViewModel;

            LoadInitialArtists();
        }

        protected override void ApplyCurrentSort()
        {
            IEnumerable<ArtistDisplayModel> sortedArtists = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    Artists,
                    SortType.Duration,
                    CurrentSortDirection,
                    a => a.Name,
                    a => (int)a.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    Artists,
                    SortType.Name,
                    CurrentSortDirection,
                    a => a.Name)
            };

            var sortedArtistsList = sortedArtists.ToList();
            Artists.Clear();
            foreach (var artist in sortedArtistsList)
            {
                Artists.Add(artist);
            }
        }

        private async void LoadInitialArtists()
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
                // Load artists with track count and total duration
                var artistsPage = await _artistsDisplayService.GetArtistsPageAsync(CurrentPage, _pageSize);

                var totalArtists = artistsPage.Count;
                var current = 0;

                foreach (var artist in artistsPage)
                {
                    await Task.Run(async () =>
                    {
                        // Load the photo for each artist
                        await _artistsDisplayService.LoadArtistPhotoAsync(artist);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Artists.Add(artist);
                            current++;
                            LoadingProgress = (current * 100.0) / totalArtists;
                        });
                    });
                }

                ApplyCurrentSort();
                CurrentPage++;
            }
            finally
            {
                await Task.Delay(500); // Brief delay to show completion
                IsLoading = false;
            }
        }

        [RelayCommand]
        public async Task OpenArtistDetails(ArtistDisplayModel artist)
        {
            if (artist == null) return;
            await _mainViewModel.NavigateToDetails(ContentType.Artist, artist);
        }

        [RelayCommand]
        public void SelectArtist(ArtistDisplayModel artist)
        {
            if (artist.IsSelected)
            {
                SelectedArtists.Add(artist);
            }
            else
            {
                SelectedArtists.Remove(artist);
            }
            HasSelectedArtists = SelectedArtists.Any();
        }

        [RelayCommand]
        public void ClearSelection()
        {
            foreach (var artist in Artists)
            {
                artist.IsSelected = false;
            }
            SelectedArtists.Clear();
            HasSelectedArtists = false;
        }

        [RelayCommand]
        public async Task PlayArtistFromHere(ArtistDisplayModel selectedArtist)
        {
            if (selectedArtist == null) return;

            var allArtistTracks = new List<TrackDisplayModel>();
            var startPlayingFromIndex = 0;
            var tracksAdded = 0;

            // Get tracks for all artists and maintain order
            foreach (var artist in Artists)
            {
                var tracks = await _artistsDisplayService.GetArtistTracksAsync(artist.ArtistID);

                // Mark where to start playing from
                if (artist.ArtistID == selectedArtist.ArtistID)
                {
                    startPlayingFromIndex = tracksAdded;
                }

                allArtistTracks.AddRange(tracks);
                tracksAdded += tracks.Count;
            }

            if (!allArtistTracks.Any()) return;

            // Get the first track of the selected artist
            var startTrack = allArtistTracks[startPlayingFromIndex];

            // Play this track and add all tracks to queue
            _trackQueueViewModel.PlayThisTrack(startTrack, new ObservableCollection<TrackDisplayModel>(allArtistTracks));
        }

        [RelayCommand]
        public async Task PlayArtistTracks(ArtistDisplayModel artist)
        {
            if (artist == null) return;

            var tracks = await _artistsDisplayService.GetArtistTracksAsync(artist.ArtistID);
            if (tracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddArtistTracksToNext(ArtistDisplayModel artist)
        {
            if (artist == null) return;

            var tracks = await _artistsDisplayService.GetArtistTracksAsync(artist.ArtistID);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddArtistTracksToQueue(ArtistDisplayModel artist)
        {
            if (artist == null) return;

            var tracks = await _artistsDisplayService.GetArtistTracksAsync(artist.ArtistID);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        public async Task<List<TrackDisplayModel>> GetSelectedArtistTracks(int artistId)
        {
            var selectedArtists = SelectedArtists;
            if (selectedArtists.Count <= 1)
            {
                return await _artistsDisplayService.GetArtistTracksAsync(artistId);
            }

            var trackTasks = selectedArtists.Select(Artist =>
                _artistsDisplayService.GetArtistTracksAsync(Artist.ArtistID));

            var allTrackLists = await Task.WhenAll(trackTasks);
            return allTrackLists.SelectMany(tracks => tracks).ToList();
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(ArtistDisplayModel artist)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow == null || !mainWindow.IsVisible) return;

                var selectedTracks = await GetSelectedArtistTracks(artist.ArtistID);

                var dialog = new PlaylistSelectionDialog();
                dialog.Initialize(_playlistViewModel, null, selectedTracks);
                await dialog.ShowDialog(mainWindow);

                ClearSelection();
            }
        }

        public async Task LoadHighResPhotosForVisibleArtistsAsync(IList<ArtistDisplayModel> visibleArtists)
        {
            foreach (var artist in visibleArtists)
            {
                if (artist.PhotoSize != "high")
                {
                    await _artistsDisplayService.LoadHighResArtistPhotoAsync(artist);
                    artist.PhotoSize = "high";
                }
            }
        }
    }

}
