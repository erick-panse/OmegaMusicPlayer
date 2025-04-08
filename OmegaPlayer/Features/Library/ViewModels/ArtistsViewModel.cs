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
using OmegaPlayer.Features.Profile.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.Infrastructure.Services.Images;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class ArtistsViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly ArtistDisplayService _artistsDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly StandardImageService _standardImageService;
        private readonly MainViewModel _mainViewModel;
        private List<ArtistDisplayModel> AllArtists { get; set; }

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

        private int _currentPage = 1;

        private const int _pageSize = 50;

        private bool _isInitialized = false;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public ArtistsViewModel(
            ArtistDisplayService artistsDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            PlaylistsViewModel playlistViewModel,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            StandardImageService standardImageService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
            : base(trackSortService, messenger, errorHandlingService)
        {
            _artistsDisplayService = artistsDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _playlistViewModel = playlistViewModel;
            _standardImageService = standardImageService;
            _mainViewModel = mainViewModel;

            LoadInitialArtists();

            // Update Content on profile switch
            _messenger.Register<ProfileUpdateMessage>(this, async (r, m) => await HandleProfileSwitch(m));
        }

        private async Task HandleProfileSwitch(ProfileUpdateMessage message)
        {
            // Reset state
            _isInitialized = false;
            AllArtists = null;

            // Clear collections on UI thread to prevent cross-thread exceptions
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                Artists.Clear();
                SelectedArtists.Clear();
                HasSelectedArtists = false;
            });

            // Reset pagination
            _currentPage = 1;

            // Trigger reload
            await LoadMoreItems();
        }

        protected override void ApplyCurrentSort()
        {
            if (!_isInitialized) return;

            // Clear existing artists
            Artists.Clear();
            _currentPage = 1;

            // Load first page with new sort settings
            LoadMoreItems().ConfigureAwait(false);
        }

        private async void LoadInitialArtists()
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
                LoadInitialArtists();
            }
            else if (isUserInitiated)
            {
                // Only apply sort if user initiated the change
                ApplyCurrentSort();
            }
        }

        /// <summary>
        /// Notifies the image loading system about artist visibility changes
        /// </summary>
        public async Task NotifyArtistVisible(ArtistDisplayModel artist, bool isVisible)
        {
            if (artist?.PhotoPath == null) return;

            if (_standardImageService != null)
            {
                await _standardImageService.NotifyImageVisible(artist.PhotoPath, isVisible);
            }
        }

        private async Task LoadMoreItems()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // Load all artists for correct sorting
                AllArtists = await _artistsDisplayService.GetAllArtistsAsync();

                // Get the sorted list based on current sort settings
                var sortedArtists = GetSortedAllArtists();

                // Calculate the page range
                var startIndex = (_currentPage - 1) * _pageSize;
                var pageItems = sortedArtists
                    .Skip(startIndex)
                    .Take(_pageSize)
                    .ToList();

                var totalArtists = pageItems.Count;
                var current = 0;
                var newArtists = new List<ArtistDisplayModel>();

                foreach (var artist in pageItems)
                {
                    await Task.Run(async () =>
                    {
                        // Load artist photo
                        await _artistsDisplayService.LoadArtistPhotoAsync(artist, "low", true);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            newArtists.Add(artist);
                            current++;
                            LoadingProgress = (current * 100.0) / totalArtists;
                        });
                    });
                }

                // Add all processed artists to the collection
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var artist in newArtists)
                    {
                        Artists.Add(artist);
                    }
                });

                _currentPage++;
            }
            finally
            {
                await Task.Delay(500); // Small delay for smoother UI
                IsLoading = false;
            }
        }

        private IEnumerable<ArtistDisplayModel> GetSortedAllArtists()
        {
            if (AllArtists == null) return new List<ArtistDisplayModel>();

            var sortedArtists = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    AllArtists,
                    SortType.Duration,
                    CurrentSortDirection,
                    a => a.Name,
                    a => (int)a.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    AllArtists,
                    SortType.Name,
                    CurrentSortDirection,
                    a => a.Name)
            };

            return sortedArtists;
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

            // Make sure AllArtists is not empty
            if (AllArtists == null)
            {
                AllArtists = await _artistsDisplayService.GetAllArtistsAsync();
            }

            // Get sorted list of all artists
            var sortedArtists = GetSortedAllArtists();

            foreach (var artist in sortedArtists)
            {
                // Get tracks for this artist and sort them by album and Title
                var tracks = (await _artistsDisplayService.GetArtistTracksAsync(artist.ArtistID))
                    .OrderBy(t => t.AlbumTitle)
                    .ThenBy(t => t.Title)
                    .ToList();

                if (artist.ArtistID == selectedArtist.ArtistID)
                {
                    startPlayingFromIndex = tracksAdded;
                }

                allArtistTracks.AddRange(tracks);
                tracksAdded += tracks.Count;
            }

            if (!allArtistTracks.Any()) return;

            var startTrack = allArtistTracks[startPlayingFromIndex];
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
    }

}