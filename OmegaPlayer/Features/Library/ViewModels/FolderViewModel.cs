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
    public partial class FolderViewModel : ViewModelBase, ILoadMoreItems
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
            MainViewModel mainViewModel)
        {
            _folderDisplayService = folderDisplayService;
            _trackQueueViewModel = trackQueueViewModel;

            LoadInitialFolders();
            _mainViewModel = mainViewModel;
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

                CurrentPage++;
            }
            finally
            {
                await Task.Delay(500);
                IsLoading = false;
            }
        }

        [RelayCommand]
        private async Task OpenFolderDetails(FolderDisplayModel folder)
        {
            if (folder == null) return;
            await _mainViewModel.NavigateToDetails(ContentType.Folder, folder);
        }

        [RelayCommand]
        private void SelectFolder(FolderDisplayModel folder)
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
        private void ClearSelection()
        {
            foreach (var folder in SelectedFolders)
            {
                folder.IsSelected = false;
            }
            SelectedFolders.Clear();
            HasSelectedFolders = false;
        }

        [RelayCommand]
        private async Task PlayFolderFromHere(FolderDisplayModel selectedFolder)
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
        private async Task PlayFolderTracks(FolderDisplayModel folder)
        {
            if (folder == null) return;

            var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
            if (tracks.Any())
            {
                _trackQueueViewModel.PlayThisTrack(tracks.First(), new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        private async Task AddFolderTracksToNext(FolderDisplayModel folder)
        {
            if (folder == null) return;

            var tracks = await _folderDisplayService.GetFolderTracksAsync(folder.FolderPath);
            if (tracks.Any())
            {
                _trackQueueViewModel.AddToPlayNext(new ObservableCollection<TrackDisplayModel>(tracks));
            }
        }

        [RelayCommand]
        private async Task AddFolderTracksToQueue(FolderDisplayModel folder)
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