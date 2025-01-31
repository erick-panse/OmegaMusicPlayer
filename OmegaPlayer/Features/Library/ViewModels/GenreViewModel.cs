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
    public partial class GenreViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly GenreDisplayService _genreDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly PlaylistViewModel _playlistViewModel;
        private readonly MainViewModel _mainViewModel;

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

        [ObservableProperty]
        private int _currentPage = 1;

        private const int _pageSize = 50;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public GenreViewModel(
            GenreDisplayService genreDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            PlaylistViewModel playlistViewModel,
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
        }
        protected override void ApplyCurrentSort()
        {
            IEnumerable<GenreDisplayModel> sortedGenres = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    Genres,
                    SortType.Duration,
                    CurrentSortDirection,
                    g => g.Name,
                    g => (int)g.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    Genres,
                    SortType.Name,
                    CurrentSortDirection,
                    g => g.Name)
            };

            var sortedGenresList = sortedGenres.ToList();
            Genres.Clear();
            foreach (var genre in sortedGenresList)
            {
                Genres.Add(genre);
            }
        }

        private async void LoadInitialGenres()
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
                var genresPage = await _genreDisplayService.GetGenresPageAsync(CurrentPage, _pageSize);

                var totalGenres = genresPage.Count;
                var current = 0;

                foreach (var genre in genresPage)
                {
                    await Task.Run(async () =>
                    {
                        await _genreDisplayService.LoadGenrePhotoAsync(genre);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Genres.Add(genre);
                            current++;
                            LoadingProgress = (current * 100.0) / totalGenres;
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

            foreach (var genre in Genres)
            {
                var tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);

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