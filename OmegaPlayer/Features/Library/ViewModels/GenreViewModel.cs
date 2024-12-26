using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class GenreViewModel : ViewModelBase, ILoadMoreItems
    {
        private readonly GenreDisplayService _genreDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
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
            MainViewModel mainViewModel)
        {
            _genreDisplayService = genreDisplayService;
            _trackQueueViewModel = trackQueueViewModel;

            LoadInitialGenres();
            _mainViewModel = mainViewModel;
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

                CurrentPage++;
            }
            finally
            {
                await Task.Delay(500);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenGenreDetails(GenreDisplayModel genre)
        {
            if (genre == null) return;
            await _mainViewModel.NavigateToDetails(ContentType.Genre, genre);
        }

        [RelayCommand]
        private void SelectGenre(GenreDisplayModel genre)
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
        private void ClearSelection()
        {
            foreach (var genre in SelectedGenres)
            {
                genre.IsSelected = false;
            }
            SelectedGenres.Clear();
            HasSelectedGenres = false;
        }

        [RelayCommand]
        private async Task PlayGenreTracks(GenreDisplayModel genre)
        {
            if (genre == null) return;

            var tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
            if (tracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        private async Task AddGenreTracksToNext(GenreDisplayModel genre)
        {
            if (genre == null) return;

            var tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        private async Task AddGenreTracksToQueue(GenreDisplayModel genre)
        {
            if (genre == null) return;

            var tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
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