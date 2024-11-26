using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class ArtistViewModel : ViewModelBase, ILoadMoreItems
    {
        private readonly ArtistDisplayService _artistsDisplayService;
        private readonly TrackQueueViewModel _trackQueueViewModel;

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
        private int _currentPage = 1;

        private const int _pageSize = 50;

        private AsyncRelayCommand _loadMoreItemsCommand;
        public System.Windows.Input.ICommand LoadMoreItemsCommand =>
            _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        public ArtistViewModel(
            ArtistDisplayService artistsDisplayService,
            TrackQueueViewModel trackQueueViewModel)
        {
            _artistsDisplayService = artistsDisplayService;
            _trackQueueViewModel = trackQueueViewModel;

            LoadInitialArtists();
        }

        private async void LoadInitialArtists()
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
                // Load artists with track count and total duration
                var artistsPage = await _artistsDisplayService.GetArtistsPageAsync(CurrentPage, _pageSize);

                var totalArtists = artistsPage.Count;
                var current = 0;

                foreach (var artist in artistsPage)
                {
                    await Task.Run(async () =>
                    {
                        // Load the photo for each artist
                        await _artistsDisplayService.LoadArtistPhotoAsync(artist);

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            Artists.Add(artist);
                            current++;
                            LoadingProgress = (current * 100.0) / totalArtists;
                        });
                    });
                }

                CurrentPage++;
            }
            finally
            {
                await Task.Delay(500); // Brief delay to show completion
                IsLoading = false;
            }
        }

        [RelayCommand]
        private void SelectArtist(ArtistDisplayModel artist)
        {
            if (artist.IsSelected)
            {
                SelectedArtists.Add(artist);
            }
            else
            {
                SelectedArtists.Remove(artist);
            }
            HasSelectedArtists = SelectedArtists.Any();
        }

        [RelayCommand]
        private void PlayArtist(ArtistDisplayModel artist)
        {
            // Get all tracks from the artist and play them
            // Implementation depends on your track queue system
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var artist in SelectedArtists)
            {
                artist.IsSelected = false;
            }
            SelectedArtists.Clear();
            HasSelectedArtists = false;
        }

        [RelayCommand]
        private void PlayNext()
        {
            // Add selected artists' tracks to play next in queue
        }

        [RelayCommand]
        private void AddToQueue()
        {
            // Add selected artists' tracks to end of queue
        }

        public async Task LoadHighResPhotosForVisibleArtistsAsync(IList<ArtistDisplayModel> visibleArtists)
        {
            foreach (var artist in visibleArtists)
            {
                if (artist.PhotoSize != "high")
                {
                    await _artistsDisplayService.LoadHighResArtistPhotoAsync(artist);
                    artist.PhotoSize = "high";
                }
            }
        }
    }

}
