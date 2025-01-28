using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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
    public partial class FolderViewModel : SortableCollectionViewModel, ILoadMoreItems
    {
        private readonly FolderDisplayService _folderDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly MainViewModel _mainViewModel;

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
            MainViewModel mainViewModel,
            TrackSortService trackSortService,
            IMessenger messenger)
            : base(trackSortService, messenger)
        {
            _folderDisplayService = folderDisplayService;
            _trackQueueViewModel = trackQueueViewModel;

            LoadInitialFolders();
            _mainViewModel = mainViewModel;
        }
        protected override void ApplyCurrentSort()
        {
            IEnumerable<FolderDisplayModel> sortedFolders = CurrentSortType switch
            {
                SortType.Duration => _trackSortService.SortItems(
                    Folders,
                    SortType.Duration,
                    CurrentSortDirection,
                    f => f.FolderName,
                    f => (int)f.TotalDuration.TotalSeconds),
                _ => _trackSortService.SortItems(
                    Folders,
                    SortType.Name,
                    CurrentSortDirection,
                    f => f.FolderName)
            };

            var sortedFoldersList = sortedFolders.ToList();
            Folders.Clear();
            foreach (var folder in sortedFoldersList)
            {
                Folders.Add(folder);
            }
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
                var foldersPage = await _folderDisplayService.GetFoldersPageAsync(CurrentPage, _pageSize);

                var totalFolders = foldersPage.Count;
                var current = 0;

                foreach (var folder in foldersPage)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        Folders.Add(folder);
                        current++;
                        LoadingProgress = (current * 100.0) / totalFolders;
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

            foreach (var folder in Folders)
            {
                var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);

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