using CommunityToolkit.Mvvm.ComponentModel;
using OmegaPlayer.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Models;
using System.Linq;

namespace OmegaPlayer.ViewModels
{
    public partial class GridViewModel : ViewModelBase
    {
        private readonly TrackDisplayService _trackService;
        public ObservableCollection<TrackDisplayModel> SelectedTracks { get; } = new();

        public ObservableCollection<TrackDisplayModel> Tracks { get; } = new();

        public GridViewModel(TrackDisplayService trackService)
        {
            _trackService = trackService;
            LoadInitialTracksAsync();
        }


        [ObservableProperty]
        private bool _hasSelectedTracks;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isHovered;

        [ObservableProperty]
        private int _currentPage = 1;

        private const int _pageSize = 100;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _artists;

        [ObservableProperty]
        private string _albumTitle;


        [RelayCommand]
        public async void LoadInitialTracksAsync()
        {
            await LoadTracksAsync();
        }

        [RelayCommand]
        public async Task LoadTracksAsync()
        {
            if (IsLoading) return;

            IsLoading = true;

            try
            {
                var tracks = await _trackService.LoadTracksAsync(CurrentPage, _pageSize);
                foreach (var track in tracks)
                {
                    Tracks.Add(track);
                    Title = track.Title;
                    Artists = track.Artists.ToString();
                    AlbumTitle = track.AlbumTitle;
                }

                CurrentPage++; // Move to the next page after loading
            }
            finally
            {
                IsLoading = false;
            }
        }

        [RelayCommand]
        public void PlayNext()
        {
            // Logic to play next with SelectedTracks
        }

        [RelayCommand]
        public void AddToQueue()
        {
            // Logic to add selected tracks to queue
        }

        [RelayCommand]
        public async Task LoadHighResImagesForVisibleTracksAsync(IList<TrackDisplayModel> visibleTracks)
        {
            foreach (var track in visibleTracks)
            {
                if (track.ThumbnailSize != "high")
                {
                    await _trackService.LoadHighResThumbnailAsync(track);
                    track.ThumbnailSize = "high"; // Mark as high-res to prevent reloading
                }
            }
        }

        public async void OnScrollChanged(double verticalOffset, double scrollableHeight)
        {
            if (!IsLoading && verticalOffset >= scrollableHeight * 0.8) // 80% scroll
            {
                await LoadTracksAsync(); // Load more tracks if user scrolled enough
            }
        }

        public async void TrackSelectionChanged(TrackDisplayModel track, bool isSelected)
        {
            if (isSelected)
                SelectedTracks.Add(track);
            else
                SelectedTracks.Remove(track);

            HasSelectedTracks = SelectedTracks.Any();
        }

        [RelayCommand]
        public async void TrackHoverChanged(bool IsHovered)
        {
            IsHovered = !IsHovered;
        }

    }

}
