using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Enums.LibraryEnums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Playlists.Views;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class ArtistsViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly StandardImageService _standardImageService;
        private readonly LocalizationService _localizationService;
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

        [ObservableProperty]
        private string _playButtonText;

        private bool _isApplyingSort = false;
        private bool _isAllArtistsLoaded = false;
        private bool _isArtistsLoaded = false;
        private bool _isInitializing = false;
        private CancellationTokenSource _loadingCancellationTokenSource;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public ArtistsViewModel(
            ArtistDisplayService artistDisplayService,
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
            _artistDisplayService = artistDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _playlistViewModel = playlistViewModel;
            _standardImageService = standardImageService;
            _localizationService = localizationService;
            _mainViewModel = mainViewModel;

            UpdatePlayButtonText();

            // Mark as false to load all tracks 
            _messenger.Register<AllTracksInvalidatedMessage>(this, (r, m) => ClearCache());
            _messenger.Register<AllArtistsUpdatedMessage>(this, (r, m) => ClearCache(true));
        }

        public void ClearCache(bool reloadContent = false)
        {
            _isAllArtistsLoaded = false;
            _isArtistsLoaded = false;

            // Clear UI for empty library case
            Artists.Clear();
            AllArtists = new List<ArtistDisplayModel>();
            ClearSelection();

            if (reloadContent)
            {
                _ = Initialize();
            }
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
                _isArtistsLoaded = false;

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
            if (isUserInitiated && _isAllArtistsLoaded)
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
                await LoadInitialArtists();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async Task LoadInitialArtists()
        {
            _isArtistsLoaded = false;

            // Ensure AllArtists is loaded first (might already be loaded from constructor)
            if (!_isAllArtistsLoaded)
            {
                await LoadAllArtistsAsync();
            }

            await LoadMoreItems();
        }

        /// <summary>
        /// Loads AllArtists in background without affecting UI
        /// </summary>
        private async Task LoadAllArtistsAsync()
        {
            if (_isAllArtistsLoaded) return;

            try
            {
                AllArtists = await _artistDisplayService.GetAllArtistsAsync();
                _isAllArtistsLoaded = true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading AllArtists from database",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Notifies the image loading system about artist visibility changes and loads images for visible artists
        /// </summary>
        public async Task NotifyArtistVisible(ArtistDisplayModel artist, bool isVisible)
        {
            if (artist?.PhotoPath == null) return;

            try
            {
                // Notify the image service about visibility changes for optimization
                if (_standardImageService != null)
                {
                    await _standardImageService.NotifyImageVisible(artist.PhotoPath, isVisible);
                }

                // If artist becomes visible and hasn't had its image loaded yet, load it now
                if (isVisible)
                {
                    // Load the image in the background with lower priority
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _artistDisplayService.LoadArtistPhotoAsync(artist, "low", true);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error loading artist image",
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
                    "Error handling artist visibility notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Load Artists to UI with selected sort order.
        /// Chunked loading with UI thread yielding for better responsiveness.
        /// </summary>
        private async Task LoadMoreItems()
        {
            if (IsLoading || _isArtistsLoaded) return;

            // Cancel any previous loading operation
            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _loadingCancellationTokenSource.Token;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // If no artists available, return empty
                if (!_isAllArtistsLoaded || AllArtists?.Any() != true)
                {
                    return;
                }

                // Clear artists immediately on UI thread
                Artists.Clear();

                // Get sorted artists
                var sortedArtists = await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sorted = GetSortedAllArtists();
                    var processed = new List<ArtistDisplayModel>();
                    int progress = 0;

                    // Pre-process all artists in background
                    foreach (var artist in sorted)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        processed.Add(artist);

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

                    foreach (var artist in sortedArtists)
                    {
                        Artists.Add(artist);
                    }

                    LoadingProgress = 100;
                }, Avalonia.Threading.DispatcherPriority.Background);


                _isArtistsLoaded = true;
            }
            catch (OperationCanceledException)
            {
                // Loading was cancelled, this is expected
                _isArtistsLoaded = false;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading artist library",
                    ex.Message,
                    ex,
                    true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private IEnumerable<ArtistDisplayModel> GetSortedAllArtists()
        {
            if (AllArtists == null || !AllArtists.Any()) return new List<ArtistDisplayModel>();

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
            if (artist == null) return;

            if (artist.IsSelected)
            {
                SelectedArtists.Add(artist);
            }
            else
            {
                SelectedArtists.Remove(artist);
            }
            HasSelectedArtists = SelectedArtists.Count > 0;

            UpdatePlayButtonText();
        }

        [RelayCommand]
        public void SelectAll()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    SelectedArtists.Clear();
                    foreach (var artist in Artists)
                    {
                        artist.IsSelected = true;
                        SelectedArtists.Add(artist);
                    }
                    HasSelectedArtists = SelectedArtists.Count > 0;
                    UpdatePlayButtonText();
                },
                "Selecting all artists",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public void ClearSelection()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    foreach (var artist in Artists)
                    {
                        artist.IsSelected = false;
                    }
                    SelectedArtists.Clear();
                    HasSelectedArtists = SelectedArtists.Count > 0;
                    UpdatePlayButtonText();
                },
                "Clearing artist selection",
                ErrorSeverity.NonCritical,
                false);
        }

        private void UpdatePlayButtonText()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    PlayButtonText = SelectedArtists.Count > 0
                        ? _localizationService["PlaySelected"]
                        : _localizationService["PlayAll"];
                },
                "Updating play button text",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task PlayArtistFromHere(ArtistDisplayModel playedArtist = null)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (AllArtists.Count <= 0 && playedArtist == null && SelectedArtists.Count <= 0) return;

                    // Get sorted list of all artists
                    var sortedArtists = GetSortedAllArtists();

                    if (playedArtist == null && SelectedArtists.Count > 0)
                    {
                        sortedArtists = SelectedArtists;
                    }

                    var allArtistTracks = new List<TrackDisplayModel>();
                    var startPlayingFromIndex = 0;
                    var tracksAddedCount = 0;

                    foreach (var artist in sortedArtists)
                    {
                        // Get tracks for this artist and sort them by Title
                        var tracks = (await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID))
                            .OrderBy(t => t.Title)
                            .ToList();

                        if (playedArtist != null && artist.ArtistID == playedArtist.ArtistID)
                        {
                            startPlayingFromIndex = tracksAddedCount;
                        }

                        allArtistTracks.AddRange(tracks);
                        tracksAddedCount += tracks.Count;
                    }

                    if (allArtistTracks.Count < 1) return;

                    var startTrack = allArtistTracks[startPlayingFromIndex];
                    await _trackQueueViewModel.PlayThisTrack(startTrack, new ObservableCollection<TrackDisplayModel>(allArtistTracks));

                    ClearSelection();
                },
                _localizationService["ErrorPlayingArtistTracks"],
                ErrorSeverity.Playback,
                true);
        }

        [RelayCommand]
        public async Task PlayArtistTracks(ArtistDisplayModel artist)
        {
            if (artist == null) return;

            var tracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
            if (tracks.Count > 0)
            {
                await _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddArtistTracksToNext(ArtistDisplayModel artist)
        {
            var tracks = await GetTracksToAdd(artist);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }

            ClearSelection();
        }

        [RelayCommand]
        public async Task AddArtistTracksToQueue(ArtistDisplayModel artist)
        {
            var tracks = await GetTracksToAdd(artist);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }

            ClearSelection();
        }

        /// <summary>
        /// Helper that returns the tracks to be added in Play next and Add to Queue methods
        /// </summary>
        public async Task<List<TrackDisplayModel>> GetTracksToAdd(ArtistDisplayModel artist)
        {
            var artistsList = SelectedArtists.Count > 0
                ? SelectedArtists
                : new ObservableCollection<ArtistDisplayModel>();

            if (artistsList.Count < 1 && artist != null)
            {
                artistsList.Add(artist);
            }

            var tracks = new List<TrackDisplayModel>();

            foreach (var artistToAdd in artistsList)
            {
                var artistTracks = await _artistDisplayService.GetArtistTracksAsync(artistToAdd.ArtistID);

                if (artistTracks.Count > 0)
                    tracks.AddRange(artistTracks);
            }

            return tracks;
        }

        public async Task<List<TrackDisplayModel>> GetSelectedArtistTracks(int artistId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var selectedArtists = SelectedArtists;
                    if (selectedArtists.Count <= 1)
                    {
                        return await _artistDisplayService.GetArtistTracksAsync(artistId);
                    }

                    var trackTasks = selectedArtists.Select(artist =>
                        _artistDisplayService.GetArtistTracksAsync(artist.ArtistID));

                    var allTrackLists = await Task.WhenAll(trackTasks);
                    return allTrackLists.SelectMany(tracks => tracks).ToList();
                },
                "Getting selected artist tracks",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(ArtistDisplayModel artist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
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
                },
                _localizationService["ShowingPlaylistDialogError"],
                ErrorSeverity.NonCritical,
                true);
        }

        [RelayCommand]
        public async Task AddArtistPhoto(ArtistDisplayModel artist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null) return;

                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var mainWindow = desktop.MainWindow;
                        if (mainWindow?.StorageProvider == null) return;

                        // File picker options
                        var options = new FilePickerOpenOptions
                        {
                            Title = _localizationService["SelectArtistPhoto"],
                            AllowMultiple = false,
                            FileTypeFilter = new[]
                            {
                                new FilePickerFileType("Image Files")
                                {
                                    Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.webp" },
                                    MimeTypes = new[] { "image/*" }
                                }
                            }
                        };

                        var files = await mainWindow.StorageProvider.OpenFilePickerAsync(options);
                        if (files?.Count != 1) return;

                        var selectedFile = files[0];
                        var localPath = selectedFile.TryGetLocalPath();
                        if (string.IsNullOrEmpty(localPath)) return;

                        // Get services
                        var mediaService = App.ServiceProvider.GetRequiredService<MediaService>();
                        var artistService = App.ServiceProvider.GetRequiredService<ArtistsService>();
                        var trackMetadataService = App.ServiceProvider.GetRequiredService<TrackMetadataService>();

                        // Create and save media entry
                        var media = new Media
                        {
                            CoverPath = null,
                            MediaType = "artist_photo"
                        };

                        int mediaId = await mediaService.AddMedia(media);
                        media.MediaID = mediaId;

                        // Save the image file
                        using (var sourceStream = await selectedFile.OpenReadAsync())
                        {
                            var imageFilePath = await trackMetadataService.SaveImage(sourceStream, "artist_photo", mediaId);
                            media.CoverPath = imageFilePath;
                            await mediaService.UpdateMediaFilePath(mediaId, imageFilePath);
                        }

                        // Update artist with new photo
                        var artistEntity = await artistService.GetArtistById(artist.ArtistID);
                        if (artistEntity != null)
                        {
                            artistEntity.PhotoID = mediaId;
                            artistEntity.UpdatedAt = DateTime.UtcNow;
                            await artistService.UpdateArtist(artistEntity);

                            // Update display model
                            artist.PhotoPath = media.CoverPath;
                            await _artistDisplayService.LoadArtistPhotoAsync(artist, "low", true);

                            // Try to add photo to file metadata for tracks that support it
                            var artistTracks = await _artistDisplayService.GetArtistTracksAsync(artist.ArtistID);
                            foreach (var track in artistTracks.Take(5)) // Limit to first 5 tracks to avoid performance issues
                            {
                                if (!string.IsNullOrEmpty(track.FilePath))
                                {
                                    await trackMetadataService.SaveImageToArtistMetadata(
                                        track.FilePath,
                                        localPath,
                                        artistEntity.ArtistName);
                                }
                            }

                            _errorHandlingService.LogInfo(
                                "Artist photo added",
                                $"Successfully added photo for {artistEntity.ArtistName}",
                                false);
                        }
                    }
                },
                _localizationService["AddArtistPhotoError"] + artist?.Name ?? "Unknown",
                ErrorSeverity.NonCritical,
                true);
        }
    }
}