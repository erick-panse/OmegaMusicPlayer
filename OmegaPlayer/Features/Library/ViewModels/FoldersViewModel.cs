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
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class FoldersViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly FolderDisplayService _folderDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly PlaylistsViewModel _playlistViewModel;
        private readonly StandardImageService _standardImageService;
        private readonly LocalizationService _localizationService;
        private readonly MainViewModel _mainViewModel;

        private List<FolderDisplayModel> AllFolders { get; set; }

        [ObservableProperty]
        private ObservableCollection<FolderDisplayModel> _folders = new();

        [ObservableProperty]
        private ObservableCollection<FolderDisplayModel> _selectedFolders = new();

        [ObservableProperty]
        private bool _hasSelectedFolders;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private double _loadingProgress;

        [ObservableProperty]
        private string _playButtonText;

        private bool _isApplyingSort = false;
        private bool _isAllFoldersLoaded = false;
        private bool _isFoldersLoaded = false;
        private bool _isInitializing = false;
        private CancellationTokenSource _loadingCancellationTokenSource;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public FoldersViewModel(
            FolderDisplayService folderDisplayService,
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
            _folderDisplayService = folderDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _playlistViewModel = playlistViewModel;
            _standardImageService = standardImageService;
            _localizationService = localizationService;
            _mainViewModel = mainViewModel;

            UpdatePlayButtonText();

            // Mark as false to load all tracks 
            _messenger.Register<AllTracksInvalidatedMessage>(this, (r, m) =>
            {
                _isAllFoldersLoaded = false;
                _isFoldersLoaded = false;

                // Clear UI for empty library case
                Folders.Clear();
                AllFolders = new List<FolderDisplayModel>();
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
                _isFoldersLoaded = false;

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
            if (isUserInitiated && _isAllFoldersLoaded)
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
                await LoadInitialFolders();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async Task LoadInitialFolders()
        {
            _isFoldersLoaded = false;

            // Ensure AllFolders is loaded first (might already be loaded from constructor)
            if (!_isAllFoldersLoaded)
            {
                await LoadAllFoldersAsync();
            }

            await LoadMoreItems();
        }

        /// <summary>
        /// Loads AllFolders in background without affecting UI
        /// </summary>
        private async Task LoadAllFoldersAsync()
        {
            if (_isAllFoldersLoaded) return;

            try
            {
                AllFolders = await _folderDisplayService.GetAllFoldersAsync();
                _isAllFoldersLoaded = true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading AllFolders from database",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Notifies the image loading system about folder visibility changes and loads images for visible folders
        /// </summary>
        public async Task NotifyFolderVisible(FolderDisplayModel folder, bool isVisible)
        {
            if (folder == null) return;

            try
            {
                // Get the first track for this folder to get the cover path
                var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
                var firstTrack = tracks.FirstOrDefault();

                if (firstTrack?.CoverPath == null) return;

                // Notify the image service about visibility changes for optimization
                if (_standardImageService != null)
                {
                    await _standardImageService.NotifyImageVisible(firstTrack.CoverPath, isVisible);
                }

                // If folder becomes visible and hasn't had its image loaded yet, load it now
                if (isVisible)
                {
                    // Load the image in the background with lower priority
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _folderDisplayService.LoadFolderCoverAsync(folder, firstTrack.CoverPath, "low", true);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error loading folder image",
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
                    "Error handling folder visibility notification",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Load Folders to UI with selected sort order.
        /// Chunked loading with UI thread yielding for better responsiveness.
        /// </summary>
        private async Task LoadMoreItems()
        {
            if (IsLoading || _isFoldersLoaded) return;

            // Cancel any previous loading operation
            _loadingCancellationTokenSource?.Cancel();
            _loadingCancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _loadingCancellationTokenSource.Token;

            IsLoading = true;
            LoadingProgress = 0;

            try
            {
                // If no folders available, return empty
                if (!_isAllFoldersLoaded || AllFolders?.Any() != true)
                {
                    return;
                }

                // Clear folders immediately on UI thread
                Folders.Clear();

                // Get sorted folders
                var sortedFolders = await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var sorted = GetSortedAllFolders();
                    var processed = new List<FolderDisplayModel>();
                    int progress = 0;

                    // Pre-process all folders in background
                    foreach (var folder in sorted)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        processed.Add(folder);

                        progress++;
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            LoadingProgress = Math.Min(95, (int)((progress * 100.0) / sorted.Count()));
                        }, Avalonia.Threading.DispatcherPriority.Background);
                    }


                    return processed;
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Add chunk to UI in one operation
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    foreach (var folder in sortedFolders)
                    {
                        Folders.Add(folder);
                    }

                    LoadingProgress = 100;
                }, Avalonia.Threading.DispatcherPriority.Background);

                _isFoldersLoaded = true;
            }
            catch (OperationCanceledException)
            {
                // Loading was cancelled, this is expected
                _isFoldersLoaded = false;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error loading folder library",
                    ex.Message,
                    ex,
                    true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private IEnumerable<FolderDisplayModel> GetSortedAllFolders()
        {
            if (AllFolders == null || !AllFolders.Any()) return new List<FolderDisplayModel>();

            var sortedFolders = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    AllFolders,
                    SortType.Duration,
                    CurrentSortDirection,
                    f => f.FolderName,
                    f => (int)f.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    AllFolders,
                    SortType.Name,
                    CurrentSortDirection,
                    f => f.FolderName)
            };

            return sortedFolders;
        }

        [RelayCommand]
        public async Task OpenFolderDetails(FolderDisplayModel folder)
        {
            if (folder == null) return;
            await _mainViewModel.NavigateToDetails(ContentType.Folder, folder);
        }

        [RelayCommand]
        public void SelectFolder(FolderDisplayModel folder)
        {
            if (folder == null) return;

            if (folder.IsSelected)
            {
                SelectedFolders.Add(folder);
            }
            else
            {
                SelectedFolders.Remove(folder);
            }
            HasSelectedFolders = SelectedFolders.Count > 0;

            UpdatePlayButtonText();
        }

        [RelayCommand]
        public void SelectAll()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    SelectedFolders.Clear();
                    foreach (var folder in Folders)
                    {
                        folder.IsSelected = true;
                        SelectedFolders.Add(folder);
                    }
                    HasSelectedFolders = SelectedFolders.Count > 0;
                    UpdatePlayButtonText();
                },
                "Selecting all folders",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public void ClearSelection()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    foreach (var folder in Folders)
                    {
                        folder.IsSelected = false;
                    }
                    SelectedFolders.Clear();
                    HasSelectedFolders = SelectedFolders.Count > 0;
                    UpdatePlayButtonText();
                },
                "Clearing folder selection",
                ErrorSeverity.NonCritical,
                false);
        }

        private void UpdatePlayButtonText()
        {
            _errorHandlingService.SafeExecute(
                () =>
                {
                    PlayButtonText = SelectedFolders.Count > 0
                        ? _localizationService["PlaySelected"]
                        : _localizationService["PlayAll"];
                },
                "Updating play button text",
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task PlayFolderFromHere(FolderDisplayModel playedFolder = null)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (AllFolders.Count <= 0 && playedFolder == null && SelectedFolders.Count <= 0) return;

                    // Get sorted list of all folders
                    var sortedFolders = GetSortedAllFolders();

                    if (playedFolder == null && SelectedFolders.Count > 0)
                    {
                        sortedFolders = SelectedFolders;
                    }

                    var allFolderTracks = new List<TrackDisplayModel>();
                    var startPlayingFromIndex = 0;
                    var tracksAddedCount = 0;

                    foreach (var folder in sortedFolders)
                    {
                        // Get tracks for this folder and sort them by Title
                        var tracks = (await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath))
                            .OrderBy(t => t.Title)
                            .ToList();

                        if (playedFolder != null && folder.FolderPath == playedFolder.FolderPath)
                        {
                            startPlayingFromIndex = tracksAddedCount;
                        }

                        allFolderTracks.AddRange(tracks);
                        tracksAddedCount += tracks.Count;
                    }

                    if (allFolderTracks.Count < 1) return;

                    var startTrack = allFolderTracks[startPlayingFromIndex];
                    await _trackQueueViewModel.PlayThisTrack(startTrack, new ObservableCollection<TrackDisplayModel>(allFolderTracks));

                    ClearSelection();
                },
                _localizationService["ErrorPlayingSelectedFolders"],
                ErrorSeverity.Playback,
                true);
        }

        [RelayCommand]
        public async Task PlayFolderTracks(FolderDisplayModel folder)
        {
            if (folder == null) return;

            var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
            if (tracks.Count > 0)
            {
                await _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddFolderTracksToNext(FolderDisplayModel folder)
        {
            var tracks = await GetTracksToAdd(folder);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }

            ClearSelection();
        }

        [RelayCommand]
        public async Task AddFolderTracksToQueue(FolderDisplayModel folder)
        {
            var tracks = await GetTracksToAdd(folder);

            if (tracks != null && tracks.Count > 0)
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }

            ClearSelection();
        }

        /// <summary>
        /// Helper that returns the tracks to be added in Play next and Add to Queue methods
        /// </summary>
        public async Task<List<TrackDisplayModel>> GetTracksToAdd(FolderDisplayModel folder)
        {
            var foldersList = SelectedFolders.Count > 0
                ? SelectedFolders
                : new ObservableCollection<FolderDisplayModel>();

            if (foldersList.Count < 1 && folder != null)
            {
                foldersList.Add(folder);
            }

            var tracks = new List<TrackDisplayModel>();

            foreach (var folderToAdd in foldersList)
            {
                var folderTracks = await _folderDisplayService.GetFolderTracksAsync(folderToAdd.FolderPath);

                if (folderTracks.Count > 0)
                    tracks.AddRange(folderTracks);
            }

            return tracks;
        }

        public async Task<List<TrackDisplayModel>> GetSelectedFolderTracks(string folderPath)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var selectedFolder = SelectedFolders;
                    if (selectedFolder.Count <= 1)
                    {
                        return await _folderDisplayService.GetFolderTracksAsync(folderPath);
                    }

                    var trackTasks = selectedFolder.Select(folder =>
                        _folderDisplayService.GetFolderTracksAsync(folder.FolderPath));

                    var allTrackLists = await Task.WhenAll(trackTasks);
                    return allTrackLists.SelectMany(tracks => tracks).ToList();
                },
                "Getting selected folder tracks",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false);
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(FolderDisplayModel folder)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var mainWindow = desktop.MainWindow;
                        if (mainWindow == null || !mainWindow.IsVisible) return;

                        var selectedTracks = await GetSelectedFolderTracks(folder.FolderPath);

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