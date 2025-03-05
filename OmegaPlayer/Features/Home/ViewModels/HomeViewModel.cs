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
        private readonly ArtistDisplayService _artistDisplayService;
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

        [ObservableProperty]
        private bool _isLoading;

        public ObservableCollection<TrackDisplayModel> RecentTracks { get; set; } = new();
        public ObservableCollection<TrackDisplayModel> MostPlayedTracks { get; set; } = new();
        public ObservableCollection<ArtistDisplayModel> MostPlayedArtists { get; set; } = new();

        public HomeViewModel(
            ProfileManager profileManager,
            AllTracksRepository allTracksRepository,
            TrackDisplayService trackDisplayService,
            TrackControlViewModel trackControlViewModel,
            ArtistDisplayService artistDisplayService,
            PlaylistDisplayService playlistDisplayService,
            PlayHistoryService playHistoryService,
            IServiceProvider serviceProvider)
        {
            _profileManager = profileManager;
            _allTracksRepository = allTracksRepository;
            _trackDisplayService = trackDisplayService;
            _trackControlViewModel = trackControlViewModel;
            _artistDisplayService = artistDisplayService;
            _playlistDisplayService = playlistDisplayService;
            _playHistoryService = playHistoryService;
            _serviceProvider = serviceProvider;

        }

        public void Initialize()
        {
            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            if (IsLoading) return;

            IsLoading = true;

            try
            {
                // Clear existing collections
                RecentTracks.Clear();
                MostPlayedTracks.Clear();
                MostPlayedArtists.Clear();

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
                        ProfilePhoto = await profileService.LoadHighQualityProfilePhoto(_profileManager.CurrentProfile.PhotoID);
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

                // Load recently played tracks
                var recentlyPlayed = await _playHistoryService.GetRecentlyPlayedTracks(10);
                RecentTracks.Clear();
                foreach (var track in recentlyPlayed)
                {
                    await _trackDisplayService.LoadHighResThumbnailAsync(track);
                    RecentTracks.Add(track);
                }

                // Load most played tracks (top 10)
                var trackStatsService = _serviceProvider.GetService<TrackStatsService>();
                if (trackStatsService != null)
                {
                    var mostPlayedTracks = await trackStatsService.GetMostPlayedTracks(allTracks, 10);
                    foreach (var track in mostPlayedTracks)
                    {
                        await _trackDisplayService.LoadHighResThumbnailAsync(track);
                        MostPlayedTracks.Add(track);
                    }
                }

                // Load favorite artists (Based on play count)
                // Calculate total play counts for each artist using LINQ:
                // 1. SelectMany flattens the list of tracks and their artists into artist-playcount pairs
                // 2. Where to only include artists with tracks that have been played
                // 3. GroupBy combines all entries for the same artist
                // 4. Select creates a new anonymous type with the total play count for each artist
                // 5. Where to Double-check the total is greater than 0
                // 6. OrderByDescending sorts artists by their total play count
                // 7. Take(10) limits the result to top 10 most played artists
                var artistPlayCounts = allTracks
                   .SelectMany(t => t.Artists.Select(artist => new { Artist = artist, PlayCount = t.PlayCount }))
                   .Where(x => x.PlayCount > 0) // Only include artists with tracks that have been played
                   .GroupBy(x => x.Artist.ArtistID)
                   .Select(g => new {
                       ArtistID = g.Key,
                       TotalPlayCount = g.Sum(x => x.PlayCount)
                   })
                   .Where(x => x.TotalPlayCount > 0) // Double-check the total is greater than 0
                   .OrderByDescending(x => x.TotalPlayCount)
                   .Take(10)
                   .ToList();

                // Only proceed with artist loading if we have artists with tracks that have been played
                if (artistPlayCounts.Any())
                {
                    // Get full artist details for top played artists
                    var artists = await _artistDisplayService.GetAllArtistsAsync();
                    var topArtists = artists
                        .Where(a => artistPlayCounts.Any(ap => ap.ArtistID == a.ArtistID))
                        .ToList();

                    // Sort the artists to match play count order
                    topArtists = topArtists
                        .OrderByDescending(a => artistPlayCounts
                            .First(ap => ap.ArtistID == a.ArtistID).TotalPlayCount)
                        .ToList();

                    foreach (var artist in topArtists)
                    {
                        await _artistDisplayService.LoadArtistPhotoAsync(artist);
                        MostPlayedArtists.Add(artist);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error and handle gracefully
                Console.WriteLine($"Error loading home data: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
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
            var artistVM = _serviceProvider.GetService<ArtistsViewModel>();
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