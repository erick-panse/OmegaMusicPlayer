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
using OmegaPlayer.Infrastructure.Data.Repositories;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class FolderViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly FolderDisplayService _folderDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly PlaylistViewModel _playlistViewModel;
        private readonly AllTracksRepository _allTracksRepository;
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
        private int _currentPage = 1;

        private const int _pageSize = 50;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public FolderViewModel(
            FolderDisplayService folderDisplayService,
            TrackQueueViewModel trackQueueViewModel,
            PlaylistViewModel playlistViewModel,
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            AllTracksRepository allTracksRepository,
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _folderDisplayService = folderDisplayService;
            _trackQueueViewModel = trackQueueViewModel;
            _playlistViewModel = playlistViewModel;
            _allTracksRepository = allTracksRepository;
            _mainViewModel = mainViewModel;

            LoadInitialFolders();
        }
        protected override void ApplyCurrentSort()
        {
            // Clear existing folders
            Folders.Clear();
            _currentPage = 1;

            // Load first page with new sort settings
            LoadMoreItems().ConfigureAwait(false);
        }


        private async void LoadInitialFolders()
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
                // First load all folders if not already loaded
                if (AllFolders == null)
                {
                    await LoadAllFoldersAsync();
                }

                // Get the sorted list based on current sort settings
                var sortedFolders = GetSortedAllFolders();

                // Calculate the page range
                var startIndex = (_currentPage - 1) * _pageSize;
                var pageItems = sortedFolders
                    .Skip(startIndex)
                    .Take(_pageSize)
                    .ToList();

                var totalFolders = pageItems.Count;
                var current = 0;
                var newFolders = new List<FolderDisplayModel>();

                foreach (var folder in pageItems)
                {
                    await Task.Run(async () =>
                    {
                        var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
                        var firstTrack = tracks.FirstOrDefault();
                        if (firstTrack != null)
                        {
                            await _folderDisplayService.LoadHighResFolderCoverAsync(folder, firstTrack.CoverPath);
                        }

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            newFolders.Add(folder);
                            current++;
                            LoadingProgress = (current * 100.0) / totalFolders;
                        });
                    });
                }

                // Add all processed folders to the collection
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var folder in newFolders)
                    {
                        Folders.Add(folder);
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

        private async Task LoadAllFoldersAsync()
        {
            AllFolders = await _folderDisplayService.GetAllFoldersAsync();
        }

        private IEnumerable<FolderDisplayModel> GetSortedAllFolders()
        {
            if (AllFolders == null) return new List<FolderDisplayModel>();

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
            if (folder.IsSelected)
            {
                SelectedFolders.Add(folder);
            }
            else
            {
                SelectedFolders.Remove(folder);
            }
            HasSelectedFolders = SelectedFolders.Any();
        }

        [RelayCommand]
        public void ClearSelection()
        {
            foreach (var folder in Folders)
            {
                folder.IsSelected = false;
            }
            SelectedFolders.Clear();
            HasSelectedFolders = false;
        }

        [RelayCommand]
        public async Task PlayFolderFromHere(FolderDisplayModel selectedFolder)
        {
            if (selectedFolder == null) return;

            var allFolderTracks = new List<TrackDisplayModel>();
            var startPlayingFromIndex = 0;
            var tracksAdded = 0;

            // Get sorted list of all folders
            var sortedFolders = GetSortedAllFolders();

            foreach (var folder in sortedFolders)
            {
                // Get tracks for this folder and sort them by Title
                var tracks = (await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath))
                    .OrderBy(t => t.Title)
                    .ToList();

                if (folder.FolderPath == selectedFolder.FolderPath)
                {
                    startPlayingFromIndex = tracksAdded;
                }

                allFolderTracks.AddRange(tracks);
                tracksAdded += tracks.Count;
            }

            if (!allFolderTracks.Any()) return;

            var startTrack = allFolderTracks[startPlayingFromIndex];
            _trackQueueViewModel.PlayThisTrack(startTrack, new ObservableCollection<TrackDisplayModel>(allFolderTracks));
        }


        [RelayCommand]
        public async Task PlayFolderTracks(FolderDisplayModel folder)
        {
            if (folder == null) return;

            var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
            if (tracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddFolderTracksToNext(FolderDisplayModel folder)
        {
            if (folder == null) return;

            var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        public async Task AddFolderTracksToQueue(FolderDisplayModel folder)
        {
            if (folder == null) return;

            var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddTrackToQueue(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        public async Task<List<TrackDisplayModel>> GetSelectedFolderTracks(string folderPath)
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
        }

        [RelayCommand]
        public async Task ShowPlaylistSelectionDialog(FolderDisplayModel folder)
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
        }

        public async Task LoadHighResCoversForVisibleFoldersAsync(IList<FolderDisplayModel> visibleFolders)
        {
            foreach (var folder in visibleFolders)
            {
                if (folder.CoverSize != "high")
                {
                    var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
                    var firstTrack = tracks.FirstOrDefault();
                    if (firstTrack != null)
                    {
                        await _folderDisplayService.LoadHighResFolderCoverAsync(folder, firstTrack.CoverPath);
                        folder.CoverSize = "high";
                    }
                }
            }
        }
    }
}