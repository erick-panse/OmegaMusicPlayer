using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Enums.LibraryEnums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Messages;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Library.Services;
using OmegaMusicPlayer.Features.Playback.ViewModels;
using OmegaMusicPlayer.Features.Playlists.Views;
using OmegaMusicPlayer.Features.Shell.ViewModels;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Library.ViewModels
{
    public partial class GenresViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly GenreDisplayService _genreDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly StandardImageService _standardImageService;
        private readonly LocalizationService _localizationService;
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

        [ObservableProperty]
        private string _playButtonText;

        private bool _isApplyingSort = false;
        private bool _isAllGenresLoaded = false;
        private bool _isGenresLoaded = false;
        private bool _isInitializing = false;
        private CancellationTokenSource _loadingCancellationTokenSource;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public GenresViewModel(
            GenreDisplayService genreDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            PlaylistsViewModel playlistViewModel,
            LocalizationService localizationService,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            StandardImageService standardImageService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
            : base(trackSortService, messenger, errorHandlingService)
        {
            _genreDisplayService = genreDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _playlistViewModel = playlistViewModel;
            _standardImageService = standardImageService;
            _localizationService = localizationService;
            _mainViewModel = mainViewModel;

            UpdatePlayButtonText();

            // Mark as false to load all tracks 
            _messenger.Register<AllTracksInvalidatedMessage>(this, (r, m) =>
            {
                _isAllGenresLoaded = false;
                _isGenresLoaded = false;

                // Clear UI for empty library case
                Genres.Clear();
                AllGenres = new List<GenreDisplayModel>();
                ClearSelection();
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
                _isGenresLoaded = false;

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
            if (isUserInitiated && _isAllGenresLoaded)
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
                await LoadInitialGenres();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async Task LoadInitialGenres()
        {
            _isGenresLoaded = false;

            // Ensure AllGenres is loaded first (might already be loaded from constructor)
            if (!_isAllGenresLoaded)
            {
                await LoadAllGenresAsync();
            }

            await LoadMoreItems();
        }

        /// <summary>
        /// Loads AllGenres in background without affecting UI
        /// </summary>
        private async Task LoadAllGenresAsync()
        {
            if (_isAllGenresLoaded) return;

            try
            {
                AllGenres = await _genreDisplayService.GetAllGenresAsync();
                _isAllGenresLoaded = true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading AllGenres from database",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Notifies the image loading system about genre visibility changes and loads images for visible genres
        /// </summary>
        public async Task NotifyGenreVisible(GenreDisplayModel genre, bool isVisible)
        {
            if (genre?.PhotoPath == null) return;

            try
            {
                // Notify the image service about visibility changes for optimization
                if (_standardImageService != null)
                {
                    await _standardImageService.NotifyImageVisible(genre.PhotoPath, isVisible);
                }

                // If genre becomes visible and hasn't had its image loaded yet, load it now
                if (isVisible)
                {
                    // Load the image in the background with lower priority
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _genreDisplayService.LoadGenrePhotoAsync(genre, "low", true);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error loading genre image",
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
                    "Error handling genre visibility notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Load Genres to UI with selected sort order.
        /// Chunked loading with UI thread yielding for better responsiveness.
        /// </summary>
        private async Task LoadMoreItems()
        {
            if (IsLoading || _isGenresLoaded) return;

            // Cancel any previous loading operation
            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _loadingCancellationTokenSource.Token;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // If no genres available, return empty
                if (!_isAllGenresLoaded || AllGenres?.Any() != true)
                {
                    return;
                }

                // Clear genres immediately on UI thread
                Genres.Clear();

                // Get sorted genres
                var sortedGenres = await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sorted = GetSortedAllGenres();
                    var processed = new List<GenreDisplayModel>();
                    int progress = 0;

                    // Pre-process all genres in background
                    foreach (var genre in sorted)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        processed.Add(genre);

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

                    foreach (var genre in sortedGenres)
                    {
                        Genres.Add(genre);
                    }

                    LoadingProgress = 100;
                }, Avalonia.Threading.DispatcherPriority.Background);


                _isGenresLoaded = true;
            }
            catch (OperationCanceledException)
            {
                // Loading was cancelled, this is expected
                _isGenresLoaded = false;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading genre library",
                    ex.Message,
                    ex,
                    true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private IEnumerable<GenreDisplayModel> GetSortedAllGenres()
        {
            if (AllGenres == null || !AllGenres.Any()) return new List<GenreDisplayModel>();

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
            if (genre == null) return;

            if (genre.IsSelected)
            {
                SelectedGenres.Add(genre);
            }
            else
            {
                SelectedGenres.Remove(genre);
            }
            HasSelectedGenres = SelectedGenres.Count > 0;

            UpdatePlayButtonText();
        }

        [RelayCommand]
        public void SelectAll()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    SelectedGenres.Clear();
                    foreach (var genre in Genres)
                    {
                        genre.IsSelected = true;
                        SelectedGenres.Add(genre);
                    }
                    HasSelectedGenres = SelectedGenres.Count > 0;
                    UpdatePlayButtonText();
                },
                "Selecting all genres",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public void ClearSelection()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    foreach (var genre in Genres)
                    {
                        genre.IsSelected = false;
                    }
                    SelectedGenres.Clear();
                    HasSelectedGenres = SelectedGenres.Count > 0;
                    UpdatePlayButtonText();
                },
                "Clearing genre selection",
                ErrorSeverity.NonCritical,
                false);
        }

        private void UpdatePlayButtonText()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    PlayButtonText = SelectedGenres.Count > 0
                        ? _localizationService["PlaySelected"]
                        : _localizationService["PlayAll"];
                },
                "Updating play button text",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task PlayGenreFromHere(GenreDisplayModel playedGenre = null)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (AllGenres.Count <= 0 && playedGenre == null && SelectedGenres.Count <= 0) return;

                    // Get sorted list of all genres
                    var sortedGenres = GetSortedAllGenres();

                    if (playedGenre == null && SelectedGenres.Count > 0)
                    {
                        sortedGenres = SelectedGenres;
                    }

                    var allGenreTracks = new List<TrackDisplayModel>();
                    var startPlayingFromIndex = 0;
                    var tracksAddedCount = 0;

                    foreach (var genre in sortedGenres)
                    {
                        // Get tracks for this genre and sort them by Title
                        var tracks = (await _genreDisplayService.GetGenreTracksAsync(genre.Name))
                            .OrderBy(t => t.Title)
                            .ToList();

                        if (playedGenre != null && genre.Name == playedGenre.Name)
                        {
                            startPlayingFromIndex = tracksAddedCount;
                        }

                        allGenreTracks.AddRange(tracks);
                        tracksAddedCount += tracks.Count;
                    }

                    if (allGenreTracks.Count < 1) return;

                    var startTrack = allGenreTracks[startPlayingFromIndex];
                    await _trackQueueViewModel.PlayThisTrack(startTrack, new ObservableCollection<TrackDisplayModel>(allGenreTracks));

                    ClearSelection();
                },
                _localizationService["ErrorPlayingSelectedGenres"],
                ErrorSeverity.Playback,
                true);
        }

        [RelayCommand]
        public async Task PlayGenreTracks(GenreDisplayModel genre)
        {
            if (genre == null) return;

            var tracks = await _genreDisplayService.GetGenreTracksAsync(genre.Name);
            if (tracks.Count > 0)
            {
                await _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddGenreTracksToNext(GenreDisplayModel genre)
        {
            var tracks = await GetTracksToAdd(genre);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }

            ClearSelection();
        }

        [RelayCommand]
        public async Task AddGenreTracksToQueue(GenreDisplayModel genre)
        {
            var tracks = await GetTracksToAdd(genre);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }

            ClearSelection();
        }

        /// <summary>
        /// Helper that returns the tracks to be added in Play next and Add to Queue methods
        /// </summary>
        public async Task<List<TrackDisplayModel>> GetTracksToAdd(GenreDisplayModel genre)
        {
            var genresList = SelectedGenres.Count > 0
                ? SelectedGenres
                : new ObservableCollection<GenreDisplayModel>();

            if (genresList.Count < 1 && genre != null)
            {
                genresList.Add(genre);
            }

            var tracks = new List<TrackDisplayModel>();

            foreach (var genreToAdd in genresList)
            {
                var genreTracks = await _genreDisplayService.GetGenreTracksAsync(genreToAdd.Name);

                if (genreTracks.Count > 0)
                    tracks.AddRange(genreTracks);
            }

            return tracks;
        }

        public async Task<List<TrackDisplayModel>> GetSelectedGenreTracks(string name)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var selectedGenre = SelectedGenres;
                    if (selectedGenre.Count <= 1)
                    {
                        return await _genreDisplayService.GetGenreTracksAsync(name);
                    }

                    var trackTasks = selectedGenre.Select(genre =>
                        _genreDisplayService.GetGenreTracksAsync(genre.Name));

                    var allTrackLists = await Task.WhenAll(trackTasks);
                    return allTrackLists.SelectMany(tracks => tracks).ToList();
                },
                "Getting selected genre tracks",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(GenreDisplayModel genre)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
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
                },
                _localizationService["ShowingPlaylistDialogError"],
                ErrorSeverity.NonCritical,
                true);
        }
    }
}