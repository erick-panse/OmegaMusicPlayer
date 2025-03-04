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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class GenresViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly GenreDisplayService _genreDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly MainViewModel _mainViewModel;
        private List<GenreDisplayModel> AllGenres { get; set; }

        [ObservableProperty]
        private ObservableCollection<GenreDisplayModel> _genres = new();

        [ObservableProperty]
        private ObservableCollection<GenreDisplayModel> _selectedGenres = new();

        [ObservableProperty]
        private bool _hasSelectedGenres;

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

        public GenresViewModel(
            GenreDisplayService genreDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            PlaylistsViewModel playlistViewModel,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _genreDisplayService = genreDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _playlistViewModel = playlistViewModel;
            _mainViewModel = mainViewModel;

            LoadInitialGenres();

            // Update Content on profile switch
            _messenger.Register<ProfileUpdateMessage>(this, async (r, m) => await HandleProfileSwitch(m));
        }

        private async Task HandleProfileSwitch(ProfileUpdateMessage message)
        {
            // Reset state
            _isInitialized = false;
            AllGenres = null;

            // Clear collections on UI thread to prevent cross-thread exceptions
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                Genres.Clear();
                SelectedGenres.Clear();
                HasSelectedGenres = false;
            });

            // Reset pagination
            _currentPage = 1;

            // Trigger reload
            await LoadMoreItems();
        }

        protected override void ApplyCurrentSort()
        {
            if (!_isInitialized) return;

            // Clear existing genres
            Genres.Clear();
            _currentPage = 1;

            // Load first page with new sort settings
            LoadMoreItems().ConfigureAwait(false);
        }


        private async void LoadInitialGenres()
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
                LoadInitialGenres();
            }
            else if (isUserInitiated)
            {
                // Only apply sort if user initiated the change
                ApplyCurrentSort();
            }
        }

        private async Task LoadMoreItems()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // Load all genres for correct sorting
                AllGenres = await _genreDisplayService.GetAllGenresAsync();

                // Get the sorted list based on current sort settings
                var sortedGenres = GetSortedAllGenres();

                // Calculate the page range
                var startIndex = (_currentPage - 1) * _pageSize;
                var pageItems = sortedGenres
                    .Skip(startIndex)
                    .Take(_pageSize)
                    .ToList();

                var totalGenres = pageItems.Count;
                var current = 0;
                var newGenres = new List<GenreDisplayModel>();

                foreach (var genre in pageItems)
                {
                    await Task.Run(async () =>
                    {
                        // Load genre photo
                        await _genreDisplayService.LoadGenrePhotoAsync(genre);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            newGenres.Add(genre);
                            current++;
                            LoadingProgress = (current * 100.0) / totalGenres;
                        });
                    });
                }

                // Add all processed genres to the collection
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var genre in newGenres)
                    {
                        Genres.Add(genre);
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

        private IEnumerable<GenreDisplayModel> GetSortedAllGenres()
        {
            if (AllGenres == null) return new List<GenreDisplayModel>();

            var sortedGenres = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    AllGenres,
                    SortType.Duration,
                    CurrentSortDirection,
                    g => g.Name,
                    g => (int)g.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    AllGenres,
                    SortType.Name,
                    CurrentSortDirection,
                    g => g.Name)
            };

            return sortedGenres;
        }

        [RelayCommand]
        public async Task OpenGenreDetails(GenreDisplayModel genre)
        {
            if (genre == null) return;
            await _mainViewModel.NavigateToDetails(ContentType.Genre, genre);
        }

        [RelayCommand]
        public void SelectGenre(GenreDisplayModel genre)
        {
            if (genre.IsSelected)
            {
                SelectedGenres.Add(genre);
            }
            else
            {
                SelectedGenres.Remove(genre);
            }
            HasSelectedGenres = SelectedGenres.Any();
        }

        [RelayCommand]
        public void ClearSelection()
        {
            foreach (var genre in Genres)
            {
                genre.IsSelected = false;
            }
            SelectedGenres.Clear();
            HasSelectedGenres = false;
        }

        [RelayCommand]
        public async Task PlayGenreFromHere(GenreDisplayModel selectedGenre)
        {
            if (selectedGenre == null) return;

            var allGenreTracks = new List<TrackDisplayModel>();
            var startPlayingFromIndex = 0;
            var tracksAdded = 0;

            // Get sorted list of all genres
            var sortedGenres = GetSortedAllGenres();

            foreach (var genre in sortedGenres)
            {
                // Get tracks for this genre and sort them by Title
                var tracks = (await _genreDisplayService.GetGenreTracksAsync(genre.Name))
                    .OrderBy(t => t.Title)
                    .ToList();

                if (genre.Name == selectedGenre.Name)
                {
                    startPlayingFromIndex = tracksAdded;
                }

                allGenreTracks.AddRange(tracks);
                tracksAdded += tracks.Count;
            }

            if (!allGenreTracks.Any()) return;

            var startTrack = allGenreTracks[startPlayingFromIndex];
            _trackQueueViewModel.PlayThisTrack(startTrack, new ObservableCollection<TrackDisplayModel>(allGenreTracks));
        }

        [RelayCommand]
        public async Task PlayGenreTracks(GenreDisplayModel genre)
        {
            if (genre == null) return;

            var tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
            if (tracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddGenreTracksToNext(GenreDisplayModel genre)
        {
            if (genre == null) return;

            var tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddGenreTracksToQueue(GenreDisplayModel genre)
        {
            if (genre == null) return;

            var tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        public async Task<List<TrackDisplayModel>> GetSelectedGenreTracks(string name)
        {
            var selectedGenre = SelectedGenres;
            if (selectedGenre.Count <= 1)
            {
                return await _genreDisplayService.GetGenreTracksAsync(name);
            }

            var trackTasks = selectedGenre.Select(Genre =>
                _genreDisplayService.GetGenreTracksAsync(Genre.Name));

            var allTrackLists = await Task.WhenAll(trackTasks);
            return allTrackLists.SelectMany(tracks => tracks).ToList();
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(GenreDisplayModel genre)
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow == null || !mainWindow.IsVisible) return;

                var selectedTracks = await GetSelectedGenreTracks(genre.Name);

                var dialog = new PlaylistSelectionDialog();
                dialog.Initialize(_playlistViewModel, null, selectedTracks);
                await dialog.ShowDialog(mainWindow);

                ClearSelection();
            }
        }

        public async Task LoadHighResPhotosForVisibleGenresAsync(IList<GenreDisplayModel> visibleGenres)
        {
            foreach (var genre in visibleGenres)
            {
                if (genre.PhotoSize != "high")
                {
                    await _genreDisplayService.LoadHighResGenrePhotoAsync(genre);
                    genre.PhotoSize = "high";
                }
            }
        }
    }
}