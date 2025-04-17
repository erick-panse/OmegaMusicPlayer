using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Features.Library.Services
{
    public class PlayHistoryService
    {
        private readonly PlayHistoryRepository _playHistoryRepository;
        private readonly ProfileManager _profileManager;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        public PlayHistoryService(
            PlayHistoryRepository playHistoryRepository,
            ProfileManager profileManager,
            AllTracksRepository allTracksRepository,
            IErrorHandlingService errorHandlingService)
        {
            _playHistoryRepository = playHistoryRepository;
            _profileManager = profileManager;
            _allTracksRepository = allTracksRepository;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<TrackDisplayModel>> GetRecentlyPlayedTracks(int limit = 10)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    await _profileManager.InitializeAsync();
                    int profileId = _profileManager.CurrentProfile?.ProfileID ?? -1;

                    if (profileId < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to get current profile for play history",
                            "Current profile is not available, cannot retrieve play history.",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

                    // Get all play history for this profile
                    var history = await _playHistoryRepository.GetRecentlyPlayed(profileId);

                    // Create a lookup of available tracks from AllTracksRepository
                    var tracksCollection = _allTracksRepository.AllTracks;
                    if (tracksCollection == null || !tracksCollection.Any())
                    {
                        return new List<TrackDisplayModel>();
                    }

                    var availableTracks = tracksCollection.ToDictionary(t => t.TrackID);

                    // Filter and order tracks based on history
                    var recentTracks = new List<TrackDisplayModel>();
                    foreach (var historyEntry in history)
                    {
                        // Check if track exists in available tracks
                        if (availableTracks.TryGetValue(historyEntry.TrackID, out var track))
                        {
                            if (!recentTracks.Contains(track)) // don't add tracks more than once
                            {
                                recentTracks.Add(track);
                            }

                            if (recentTracks.Count >= limit) break; // Stop once we have enough tracks
                        }
                    }

                    return recentTracks;
                },
                $"Getting recently played tracks (limit: {limit})",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task AddToHistory(TrackDisplayModel track)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Null track provided to play history",
                            "Attempted to add a null track to play history.",
                            null,
                            false);
                        return;
                    }

                    await _profileManager.InitializeAsync();
                    int profileId = _profileManager.CurrentProfile?.ProfileID ?? -1;

                    if (profileId < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to get current profile for play history",
                            "Current profile is not available, cannot update play history.",
                            null,
                            false);
                        return;
                    }

                    await _playHistoryRepository.AddToHistory(profileId, track.TrackID);
                },
                $"Adding track '{track?.Title ?? "Unknown"}' to play history",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task ClearHistory()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    await _profileManager.InitializeAsync();
                    int profileId = _profileManager.CurrentProfile?.ProfileID ?? -1;

                    if (profileId < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to get current profile for play history",
                            "Current profile is not available, cannot clear play history.",
                            null,
                            true);
                        return;
                    }

                    await _playHistoryRepository.ClearHistory(profileId);
                },
                "Clearing play history",
                ErrorSeverity.NonCritical
            );
        }
    }
}