using CommunityToolkit.Mvvm.ComponentModel;
using OmegaPlayer.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;
using System.Linq;
using OmegaPlayer.Models;
using System.Windows.Input;

namespace OmegaPlayer.ViewModels
{
    public partial class GridViewModel : ViewModelBase
    {
        private readonly TrackDisplayService _trackService;

        private ObservableCollection<TrackDisplayModel> _selectedTracks = new();
        public ObservableCollection<TrackDisplayModel> SelectedTracks
        {
            get => _selectedTracks;
            set => SetProperty(ref _selectedTracks, value);
        }


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
        private bool _isSelected;

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
                var tracks = await _trackService.LoadTracksAsync( 2, CurrentPage, _pageSize);// 2 is a dummy profileid made for testing
                foreach (var track in tracks)
                {
                    Tracks.Add(track);
                    Title = track.Title;
                    Artists = String.Join(",", track.Artists);
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

        [RelayCommand]
        public void OpenArtist(Artists artist)
        {
            // Logic to open the artist's view
            // You could navigate to a new ArtistView and pass the selected artist
            ShowMessageBox($"Opening artist: {artist.ArtistName}");  // Placeholder for now
        }

        [RelayCommand]
        public void OpenAlbum(string albumTitle)
        {
            // Logic to open the album's view
            ShowMessageBox($"Opening album: {albumTitle}");  // Placeholder for now
        }

        [RelayCommand]
        public void TrackSelection(TrackDisplayModel track)
        {
            if (track.IsSelected)
            {
                SelectedTracks.Add(track);
                ShowMessageBox("adding " + track.Title.ToString()+ "Current Playlist: " + String.Join(",", SelectedTracks.Select(x => x.Title).ToList()));//Indicator of the current selected tracks
            }
            else
            {
                SelectedTracks.Remove(track);
                ShowMessageBox("removing " + track.Title.ToString() + "Current Playlist: " + String.Join(", ", SelectedTracks.Select(x => x.Title).ToList()));//Indicator of the current selected tracks
            }
            //track.ToggleSelected();
        }

        public void DeselectAllTracks()
        {
            SelectedTracks.Clear();
        }

        public async void OnScrollChanged(double verticalOffset, double scrollableHeight)
        {
            if (!IsLoading && verticalOffset >= scrollableHeight * 0.8) // 80% scroll
            {
                await LoadTracksAsync(); // Load more tracks if user scrolled enough
            }
        }




        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync(); // shows custom messages
        }
    }


}
