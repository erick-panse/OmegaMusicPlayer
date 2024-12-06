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
using OmegaPlayer.Features.Shell.ViewModels;

namespace OmegaPlayer.Features.Library.ViewModels
{
    public enum ViewType
    {
        List,
        Card,
        Image,
        RoundImage
    }

    public partial class LibraryViewModel : ViewModelBase, ILoadMoreItems
    {
        private AsyncRelayCommand _loadMoreItemsCommand;
        public ICommand LoadMoreItemsCommand => _loadMoreItemsCommand ??= new AsyncRelayCommand(LoadMoreItems);

        private readonly TrackDisplayService _trackService;
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly TrackControlViewModel _trackControlViewModel;
        private readonly MainViewModel _mainViewModel;


        [ObservableProperty]
        private ViewType _currentViewType;


        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _selectedTracks = new();

        public ObservableCollection<TrackDisplayModel> Tracks { get; } = new();

        public List<TrackDisplayModel> AllTracks { get; set; }

        [ObservableProperty]
        private bool _hasSelectedTracks;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private double _loadingProgress;

        [ObservableProperty]
        private int _currentPage = 1;

        private const int _pageSize = 100;

        public LibraryViewModel(
            TrackDisplayService trackService,
            TrackQueueViewModel trackQueueViewModel,
            AllTracksRepository allTracksRepository,
            TrackControlViewModel trackControlViewModel,
            MainViewModel mainViewModel)
        {
            _trackService = trackService;
            _trackQueueViewModel = trackQueueViewModel;
            _allTracksRepository = allTracksRepository;
            _trackControlViewModel = trackControlViewModel;
            _mainViewModel = mainViewModel;

            AllTracks = _allTracksRepository.AllTracks;

            CurrentViewType = _mainViewModel.CurrentViewType;
            LoadInitialTracksAsync();

            if (_trackControlViewModel.CurrentlyPlayingTrack != null)
            {
                UpdateTrackPlayingStatus(_trackControlViewModel.CurrentlyPlayingTrack);
            }

            // Subscribe to property changes
            _trackControlViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(TrackControlViewModel.CurrentlyPlayingTrack))
                {
                    UpdateTrackPlayingStatus(_trackControlViewModel.CurrentlyPlayingTrack);
                }
            };
        }
        private void UpdateTrackPlayingStatus(TrackDisplayModel currentTrack)
        {
            if (currentTrack == null) return;

            foreach (var track in Tracks)
            {
                track.IsCurrentlyPlaying = track.TrackID == currentTrack.TrackID;
            }
        }


        [RelayCommand]
        public void ChangeViewType(string viewType)
        {
            CurrentViewType = viewType.ToLower() switch
            {
                "list" => ViewType.List,
                "card" => ViewType.Card,
                "image" => ViewType.Image,
                "roundimage" => ViewType.RoundImage,
                _ => ViewType.Card
            };
        }

        [RelayCommand]
        public async Task LoadInitialTracksAsync()
        {
            //ShowMessageBox("Starting LoadInitialTracksAsync");
            await LoadMoreItems();
        }

        private async Task LoadMoreItems()
        {
            if (IsLoading) return;

            IsLoading = true;
            LoadingProgress = 0;


            //ShowMessageBox($"Starting LoadMoreItems, Current tracks count: {Tracks.Count}");

            try
            {
                await Task.Run(async () =>
                {
                    var tracks = await _trackService.LoadTracksAsync(2, CurrentPage, _pageSize);

                    var totalTracks = tracks.Count;
                    var currentTrack = 0;

                    foreach (var track in tracks)
                    {
                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (_trackControlViewModel.CurrentlyPlayingTrack != null)
                            {
                                track.IsCurrentlyPlaying = track.TrackID == _trackControlViewModel.CurrentlyPlayingTrack.TrackID;
                            }

                            Tracks.Add(track);
                            track.Artists.Last().IsLastArtist = false;

                            currentTrack++;
                            LoadingProgress = (currentTrack * 100.0) / totalTracks;
                        });

                        // Add a small delay to prevent UI thread from being overwhelmed
                    }

                        CurrentPage++;
                });
            }
            finally
            {
                await Task.Delay(500); // Show completion message briefly
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
        public void TrackSelection(TrackDisplayModel track)
        {
            if (track.IsSelected)
            {
                SelectedTracks.Add(track);
                ShowMessageBox("adding " + track.Title.ToString() + "Current Playlist: " + String.Join(",", SelectedTracks.Select(x => x.Title).ToList()));
            }
            else
            {
                SelectedTracks.Remove(track);
                ShowMessageBox("removing " + track.Title.ToString() + "Current Playlist: " + String.Join(", ", SelectedTracks.Select(x => x.Title).ToList()));
            }
        }


        private void Track_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TrackDisplayModel.IsSelected))
            {
                UpdateHasSelectedTracks();
            }
        }

        private void UpdateHasSelectedTracks()
        {
            HasSelectedTracks = Tracks.Any(t => t.IsSelected);
        }
        [RelayCommand]
        public void OpenArtist(Artists artist)
        {
            ShowMessageBox($"Opening artist: {artist.ArtistName}");
        }

        [RelayCommand]
        public void OpenAlbum(string albumTitle)
        {
            ShowMessageBox($"Opening album: {albumTitle}");
        }
        
        [RelayCommand]
        public void OpenGenre(string albumTitle)
        {
            ShowMessageBox($"Opening album: {albumTitle}");
        }

        [RelayCommand]
        public void DeselectAllTracks()
        {
            SelectedTracks.Clear();
        }

        public async Task LoadHighResImagesForVisibleTracksAsync(IList<TrackDisplayModel> visibleTracks)
        {
            foreach (var track in visibleTracks)
            {
                if (track.ThumbnailSize != "high")
                {
                    await _trackService.LoadHighResThumbnailAsync(track);
                    track.ThumbnailSize = "high";
                }
            }
        }


        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync();
        }
    }
}