using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using System;
using System.Linq;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Infrastructure.Services.Cache;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.Features.Library.ViewModels;

namespace OmegaPlayer.Features.Home.ViewModels
{
    public partial class HomeViewModel : ViewModelBase
    {
        private readonly ProfileManager _profileManager;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly TrackDisplayService _trackDisplayService;
        private readonly TrackControlViewModel _trackControlViewModel;
        private readonly ImageCacheService _imageCacheService;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly PlaylistDisplayService _playlistDisplayService; 
        private readonly PlayHistoryService _playHistoryService;
        private readonly IServiceProvider _serviceProvider;

        [ObservableProperty]
        private string _profileName;

        [ObservableProperty]
        private Bitmap _profilePhoto;

        [ObservableProperty]
        private int _totalTracks;

        [ObservableProperty]
        private int _totalArtists;

        [ObservableProperty]
        private int _totalAlbums;

        [ObservableProperty]
        private int _totalPlaylists;

        public ObservableCollection<TrackDisplayModel> RecentTracks { get; } = new();
        public ObservableCollection<TrackDisplayModel> MostPlayedTracks { get; } = new();
        public ObservableCollection<ArtistDisplayModel> FavoriteArtists { get; } = new();

        public HomeViewModel(
            ProfileManager profileManager,
            AllTracksRepository allTracksRepository,
            TrackDisplayService trackDisplayService,
            TrackControlViewModel trackControlViewModel,
            ImageCacheService imageCacheService,
            ArtistDisplayService artistDisplayService,
            AlbumDisplayService albumDisplayService,
            PlaylistDisplayService playlistDisplayService,
            PlayHistoryService playHistoryService,
            IServiceProvider serviceProvider)
        {
            _profileManager = profileManager;
            _allTracksRepository = allTracksRepository;
            _trackDisplayService = trackDisplayService;
            _trackControlViewModel = trackControlViewModel;
            _imageCacheService = imageCacheService;
            _artistDisplayService = artistDisplayService;
            _albumDisplayService = albumDisplayService;
            _playlistDisplayService = playlistDisplayService;
            _playHistoryService = playHistoryService;
            _serviceProvider = serviceProvider;

            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            try
            {
                // Load profile info
                await _profileManager.InitializeAsync();
                var currentProfile = _profileManager.CurrentProfile;
                ProfileName = currentProfile.ProfileName;

                if (currentProfile.PhotoID > 0)
                {
                    // Wait for ProfileManager to initialize
                    await _profileManager.InitializeAsync();

                    if (_profileManager.CurrentProfile?.PhotoID > 0)
                    {
                        var profileService = _serviceProvider.GetRequiredService<ProfileService>();
                        ProfilePhoto = await profileService.LoadProfilePhoto(_profileManager.CurrentProfile.PhotoID);
                    }
                }

                // Load library stats
                await _allTracksRepository.LoadTracks();
                var allTracks = _allTracksRepository.AllTracks;

                TotalTracks = _allTracksRepository.AllTracks.Count;
                TotalArtists = _allTracksRepository.AllArtists.Count;
                TotalAlbums = _allTracksRepository.AllAlbums.Count;

                var playlists = await _playlistDisplayService.GetAllPlaylistDisplaysAsync();
                TotalPlaylists = playlists.Count;

                // Load most played tracks (top 10)
                var mostPlayedIds = allTracks
                    .OrderByDescending(t => t.PlayCount)
                    .Take(10)
                    .Select(t => t.TrackID)
                    .ToList();

                // Get the actual track references in the correct order
                foreach (var trackId in mostPlayedIds)
                {
                    var track = allTracks.First(t => t.TrackID == trackId);
                    await _trackDisplayService.LoadHighResThumbnailAsync(track);
                    MostPlayedTracks.Add(track);
                }

                // Load recently played tracks
                var recentlyPlayed = await _playHistoryService.GetRecentlyPlayedTracks(10);
                RecentTracks.Clear();
                foreach (var track in recentlyPlayed)
                {
                    await _trackDisplayService.LoadHighResThumbnailAsync(track);
                    RecentTracks.Add(track);
                }

                // Load favorite artists (based on track count for now)
                var artists = await _artistDisplayService.GetAllArtistsAsync();
                var topArtists = artists
                    .OrderByDescending(a => a.TrackIDs.Count)
                    .Take(10)
                    .ToList();

                foreach (var artist in topArtists)
                {
                    await _artistDisplayService.LoadArtistPhotoAsync(artist);
                    FavoriteArtists.Add(artist);
                }
            }
            catch (Exception ex)
            {
                // Log error and handle gracefully
                Console.WriteLine($"Error loading home data: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task PlayTopMostPlayedTracks(TrackDisplayModel track)
        {
            if (track == null) return;
            await _trackControlViewModel.PlayCurrentTrack(track, MostPlayedTracks);
        }

        [RelayCommand]
        private async Task PlayTopRecentTracks(TrackDisplayModel track)
        {
            if (track == null) return;
            await _trackControlViewModel.PlayCurrentTrack(track, RecentTracks);
        }


        [RelayCommand]
        private async Task PlayArtist(ArtistDisplayModel artist)
        {
            var artistVM = _serviceProvider.GetService<ArtistViewModel>();
            if (artist == null || artistVM == null) return;
            await artistVM.PlayArtistFromHere(artist); 
        }

        // Navigation Commands
        [RelayCommand]
        private async Task NavigateToLibrary()
        {
            var mainVm = _serviceProvider.GetService<MainViewModel>();
            if (mainVm == null) return;

            await mainVm.Navigate("Library");
        }

        [RelayCommand]
        private async Task NavigateToArtists()
        {
            var mainVm = _serviceProvider.GetService<MainViewModel>();
            if (mainVm == null) return;

            await mainVm.Navigate("Artists");
        }

        [RelayCommand]
        private async Task NavigateToAlbums()
        {
            var mainVm = _serviceProvider.GetService<MainViewModel>();
            if (mainVm == null) return;

            await mainVm.Navigate("Albums");
        }

        [RelayCommand]
        private async Task NavigateToPlaylists()
        {
            var mainVm = _serviceProvider.GetService<MainViewModel>();
            if (mainVm == null) return;

            await mainVm.Navigate("Playlists");
        }
    }
}