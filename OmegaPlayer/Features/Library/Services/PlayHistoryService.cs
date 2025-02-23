using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using OmegaPlayer.Core.Services;

namespace OmegaPlayer.Features.Library.Services
{
    public class PlayHistoryService
    {
        private readonly PlayHistoryRepository _playHistoryRepository;
        private readonly TrackDisplayService _trackDisplayService;
        private readonly ProfileManager _profileManager;
        private readonly AllTracksRepository _allTracksRepository;

        public PlayHistoryService(
            PlayHistoryRepository playHistoryRepository,
            TrackDisplayService trackDisplayService,
            ProfileManager profileManager,
            AllTracksRepository allTracksRepository)
        {
            _playHistoryRepository = playHistoryRepository;
            _trackDisplayService = trackDisplayService;
            _profileManager = profileManager;
            _allTracksRepository = allTracksRepository;
        }

        public async Task<List<TrackDisplayModel>> GetRecentlyPlayedTracks(int limit = 10)
        {
            try
            {
                await _profileManager.InitializeAsync();
                var profileId = _profileManager.CurrentProfile.ProfileID;

                // Get all play history for this profile
                var history = await _playHistoryRepository.GetRecentlyPlayed(profileId);

                // Create a lookup of available tracks from AllTracksRepository
                var availableTracks = _allTracksRepository.AllTracks.ToDictionary(t => t.TrackID);

                // Filter and order tracks based on history
                var recentTracks = new List<TrackDisplayModel>();
                foreach (var historyEntry in history)
                {
                    // Check if track exists in available tracks
                    if (availableTracks.TryGetValue(historyEntry.TrackID, out var track))
                    {
                        recentTracks.Add(track);
                        if (recentTracks.Count >= limit) break; // Stop once we have enough tracks
                    }
                }

                return recentTracks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting recently played tracks: {ex.Message}");
                throw;
            }
        }

        public async Task AddToHistory(TrackDisplayModel track)
        {
            try
            {
                if (track == null) return;

                await _profileManager.InitializeAsync();
                var profileId = _profileManager.CurrentProfile.ProfileID;

                await _playHistoryRepository.AddToHistory(profileId, track.TrackID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding track to history: {ex.Message}");
                throw;
            }
        }

        public async Task ClearHistory()
        {
            try
            {
                await _profileManager.InitializeAsync();
                var profileId = _profileManager.CurrentProfile.ProfileID;

                await _playHistoryRepository.ClearHistory(profileId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing play history: {ex.Message}");
                throw;
            }
        }
    }
}