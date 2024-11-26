using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using System.Collections.Generic;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class AlbumViewModel : ViewModelBase, ILoadMoreItems
    {
        private readonly AlbumDisplayService _albumsDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;

        [ObservableProperty]
        private ObservableCollection<AlbumDisplayModel> _albums = new();

        [ObservableProperty]
        private ObservableCollection<AlbumDisplayModel> _selectedAlbums = new();

        [ObservableProperty]
        private bool _hasSelectedAlbums;

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

        public AlbumViewModel(
            AlbumDisplayService albumsDisplayService,
            TrackQueueViewModel trackQueueViewModel)
        {
            _albumsDisplayService = albumsDisplayService;
            _trackQueueViewModel = trackQueueViewModel;

            LoadInitialAlbums();
        }

        private async void LoadInitialAlbums()
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
                var albumsPage = await _albumsDisplayService.GetAlbumsPageAsync(CurrentPage, _pageSize);

                var totalAlbums = albumsPage.Count;
                var current = 0;

                foreach (var album in albumsPage)
                {
                    await Task.Run(async () =>
                    {
                        await _albumsDisplayService.LoadAlbumCoverAsync(album);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Albums.Add(album);
                            current++;
                            LoadingProgress = (current * 100.0) / totalAlbums;
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
        private void SelectAlbum(AlbumDisplayModel album)
        {
            if (album.IsSelected)
            {
                SelectedAlbums.Add(album);
            }
            else
            {
                SelectedAlbums.Remove(album);
            }
            HasSelectedAlbums = SelectedAlbums.Any();
        }

        [RelayCommand]
        private void PlayAlbum(AlbumDisplayModel album)
        {
            // Implementation for playing album
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var album in SelectedAlbums)
            {
                album.IsSelected = false;
            }
            SelectedAlbums.Clear();
            HasSelectedAlbums = false;
        }

        [RelayCommand]
        private void PlayNext()
        {
            // Implementation for playing next
        }

        [RelayCommand]
        private void AddToQueue()
        {
            // Implementation for adding to queue
        }

        public async Task LoadHighResCoversForVisibleAlbumsAsync(IList<AlbumDisplayModel> visibleAlbums)
        {
            foreach (var album in visibleAlbums)
            {
                if (album.CoverSize != "high")
                {
                    await _albumsDisplayService.LoadHighResAlbumCoverAsync(album);
                    album.CoverSize = "high";
                }
            }
        }
    }
}
