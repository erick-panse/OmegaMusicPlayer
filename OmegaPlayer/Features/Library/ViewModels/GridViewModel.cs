using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Threading.Tasks;
using MsBox.Avalonia.Enums;
using MsBox.Avalonia;
using System;
using System.Linq;
using System.Windows.Input;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Playback.ViewModels;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public partial class GridViewModel : ViewModelBase, ILoadMoreItems
    {
        public ICommand LoadMoreItemsCommand { get; }

        private readonly TrackDisplayService _trackService;
        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _selectedTracks = new();

        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly TrackControlViewModel _trackControlViewModel;

        public ObservableCollection<TrackDisplayModel> Tracks { get; } = new();

        public List<TrackDisplayModel> AllTracks { get; set; }

        public GridViewModel(TrackDisplayService trackService, TrackQueueViewModel trackQueueViewModel, AllTracksRepository allTracksRepository, TrackControlViewModel trackControlViewModel)
        {
            _trackService = trackService;
            _trackQueueViewModel = trackQueueViewModel;
            _allTracksRepository = allTracksRepository;
            AllTracks = _allTracksRepository.AllTracks;
            _trackControlViewModel = trackControlViewModel;

            LoadMoreItemsCommand = new RelayCommand(LoadMoreItems);
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
        private List<Artists> _artists;

        [ObservableProperty]
        private string _albumTitle;


        [RelayCommand]
        public async void LoadInitialTracksAsync()
        {
            LoadMoreItems();
        }


        private async void LoadMoreItems()
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
                    Artists = track.Artists;
                    AlbumTitle = track.AlbumTitle;

                    track.Artists.Last().IsLastArtist = false;// Hides the Comma of the last Track
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
        public void PlayTrack(TrackDisplayModel track)
        {
            _trackControlViewModel.PlayCurrentTrack(track, Tracks);
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
                LoadMoreItems(); // Load more tracks if user scrolled enough
            }
        }




        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync(); // shows custom messages
        }
    }


}
