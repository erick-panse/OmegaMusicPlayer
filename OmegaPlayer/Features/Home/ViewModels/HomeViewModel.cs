using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Core.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Core.Messages;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

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
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

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
        
        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _recentTracks = new();

        [ObservableProperty]
        private ObservableCollection<TrackDisplayModel> _mostPlayedTracks = new();

        [ObservableProperty]
        private ObservableCollection<ArtistDisplayModel> _mostPlayedArtists = new();

        public HomeViewModel(
            ProfileManager profileManager,
            AllTracksRepository allTracksRepository,
            TrackDisplayService trackDisplayService,
            TrackControlViewModel trackControlViewModel,
            ArtistDisplayService artistDisplayService,
            PlaylistDisplayService playlistDisplayService,
            PlayHistoryService playHistoryService,
            IServiceProvider serviceProvider,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _profileManager = profileManager;
            _allTracksRepository = allTracksRepository;
            _trackDisplayService = trackDisplayService;
            _trackControlViewModel = trackControlViewModel;
            _artistDisplayService = artistDisplayService;
            _playlistDisplayService = playlistDisplayService;
            _playHistoryService = playHistoryService;
            _serviceProvider = serviceProvider;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            // Register for profile change messages
            _messenger.Register<ProfileChangedMessage>(this, (r, m) => UpdateProfile());
        }

        public void Initialize()
        {
            LoadDataAsync();
        }

        public async void UpdateProfile()
        {
            // Delay to let update be processed
            await Task.Delay(100);

            // Load profile info
            var currentProfile = await _profileManager.GetCurrentProfileAsync();
            ProfileName = currentProfile.ProfileName;
            // Load profile photo if available
            await LoadProfilePhotoAsync(currentProfile);
        }

        private async void LoadDataAsync()
        {
            if (IsLoading) return;

            IsLoading = true;

            try
            {
                // Load profile info
                var currentProfile = await _profileManager.GetCurrentProfileAsync();
                ProfileName = currentProfile.ProfileName;

                // Load All data
                await Task.WhenAll(
                    LoadProfilePhotoAsync(currentProfile),
                    LoadLibraryStatsAsync(),
                    LoadRecentlyPlayedTracksAsync(),
                    LoadMostPlayedTracksAsync(),
                    LoadMostPlayedArtistsAsync()
                );
            }
            catch (Exception ex) 
            {
                _errorHandlingService.LogError(
                ErrorSeverity.NonCritical,
                "Loading home screen data",
                null,
                ex,
                true);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadProfilePhotoAsync(Profiles currentProfile)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (currentProfile.PhotoID > 0)
                    {
                        var profile = await _profileManager.GetCurrentProfileAsync();

                        if (profile.PhotoID > 0)
                        {
                            var profileService = _serviceProvider.GetRequiredService<ProfileService>();
                            ProfilePhoto = await profileService.LoadProfilePhotoAsync(profile.PhotoID, "high", true);
                        }
                    }
                },
                "Loading profile photo",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task LoadLibraryStatsAsync()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    await _allTracksRepository.LoadTracks();
                    var allTracks = _allTracksRepository.AllTracks.ToList();

                    TotalTracks = _allTracksRepository.AllTracks.Count;
                    TotalArtists = _allTracksRepository.AllArtists.Count;
                    TotalAlbums = _allTracksRepository.AllAlbums.Count;

                    var playlists = await _playlistDisplayService.GetAllPlaylistDisplaysAsync();
                    TotalPlaylists = playlists.Count;
                },
                "Loading library statistics",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task LoadRecentlyPlayedTracksAsync()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var recentlyPlayed = await _playHistoryService.GetRecentlyPlayedTracks(10);
                    RecentTracks.Clear();
                    foreach (var track in recentlyPlayed)
                    {
                        await _trackDisplayService.LoadTrackCoverAsync(track, "high", true);
                    }
                    RecentTracks = new ObservableCollection<TrackDisplayModel>(recentlyPlayed);
                },
                "Loading recently played tracks",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task LoadMostPlayedTracksAsync()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var trackStatsService = _serviceProvider.GetService<TrackStatsService>();
                    if (trackStatsService != null)
                    {
                        var allTracks = _allTracksRepository.AllTracks.ToList();
                        var mostPlayedTracks = await trackStatsService.GetMostPlayedTracks(allTracks, 10);
                        MostPlayedTracks.Clear();
                        foreach (var track in mostPlayedTracks)
                        {
                            await _trackDisplayService.LoadTrackCoverAsync(track, "high", true);
                        }
                        MostPlayedTracks = new ObservableCollection<TrackDisplayModel>(mostPlayedTracks);
                    }
                    else
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Missing track stats service",
                            "Could not load most played tracks because the TrackStatsService is not available",
                            null,
                            false);
                    }
                },
                "Loading most played tracks",
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task LoadMostPlayedArtistsAsync()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var allTracks = _allTracksRepository.AllTracks.ToList();

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
                       .Select(g => new
                       {
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

                        MostPlayedArtists.Clear();
                        foreach (var artist in topArtists)
                        {
                            await _artistDisplayService.LoadArtistPhotoAsync(artist);
                        }
                        MostPlayedArtists = new ObservableCollection<ArtistDisplayModel>(topArtists);
                    }
                },
                "Loading most played artists",
                ErrorSeverity.NonCritical,
                false
            );
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
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var mainVm = _serviceProvider.GetService<MainViewModel>();
                    if (mainVm == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Missing main view model",
                            "Could not navigate to Library because MainViewModel is not available",
                            null,
                            true);
                        return;
                    }

                    await mainVm.Navigate("Library");
                },
                "Navigating to Library",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        private async Task NavigateToArtists()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var mainVm = _serviceProvider.GetService<MainViewModel>();
                    if (mainVm == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Missing main view model",
                            "Could not navigate to Artists because MainViewModel is not available",
                            null,
                            true);
                        return;
                    }

                    await mainVm.Navigate("Artists");
                },
                "Navigating to Artists",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        private async Task NavigateToAlbums()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var mainVm = _serviceProvider.GetService<MainViewModel>();
                    if (mainVm == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Missing main view model",
                            "Could not navigate to Albums because MainViewModel is not available",
                            null,
                            true);
                        return;
                    }

                    await mainVm.Navigate("Albums");
                },
                "Navigating to Albums",
                ErrorSeverity.NonCritical
            );
        }

        [RelayCommand]
        private async Task NavigateToPlaylists()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var mainVm = _serviceProvider.GetService<MainViewModel>();
                    if (mainVm == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Missing main view model",
                            "Could not navigate to Playlists because MainViewModel is not available",
                            null,
                            true);
                        return;
                    }

                    await mainVm.Navigate("Playlists");
                },
                "Navigating to Playlists",
                ErrorSeverity.NonCritical
            );
        }
    }
}