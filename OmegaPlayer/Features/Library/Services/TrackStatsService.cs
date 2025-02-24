using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;

namespace OmegaPlayer.Features.Library.Services
{
    public class TrackStatsService
    {
        private readonly TrackStatsRepository _trackStatsRepository;
        private readonly ProfileManager _profileManager;
        private readonly IMessenger _messenger;

        public TrackStatsService(
            TrackStatsRepository trackStatsRepository,
            ProfileManager profileManager,
            IMessenger messenger)
        {
            _trackStatsRepository = trackStatsRepository;
            _profileManager = profileManager;
            _messenger = messenger;
        }

        private async Task<int> GetCurrentProfileId()
        {
            await _profileManager.InitializeAsync();
            return _profileManager.CurrentProfile.ProfileID;
        }

        public async Task<bool> IsTrackLiked(int trackId)
        {
            try
            {
                var profileId = await GetCurrentProfileId();
                return await _trackStatsRepository.IsTrackLiked(trackId, profileId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking if track is liked: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateTrackLike(int trackId, bool isLiked)
        {
            try
            {
                var profileId = await GetCurrentProfileId();
                await _trackStatsRepository.UpdateTrackLike(trackId, profileId, isLiked);
                _messenger.Send(new TrackLikeUpdateMessage(trackId, isLiked));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating track like status: {ex.Message}");
                throw;
            }
        }

        public async Task IncrementPlayCount(int trackId, int playcount)
        {
            try
            {
                var profileId = await GetCurrentProfileId();
                await _trackStatsRepository.IncrementPlayCount(trackId, playcount, profileId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error incrementing play count: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TrackDisplayModel>> GetMostPlayedTracks(List<TrackDisplayModel> allTracks, int limit = 10)
        {
            try
            {
                var profileId = await GetCurrentProfileId();
                var mostPlayedIds = await _trackStatsRepository.GetMostPlayedTracks(profileId, limit);

                // Create a lookup of all tracks
                var trackLookup = allTracks.ToDictionary(t => t.TrackID);

                // Get tracks in order of play count
                var result = new List<TrackDisplayModel>();
                foreach (var (trackId, _) in mostPlayedIds)
                {
                    if (trackLookup.TryGetValue(trackId, out var track))
                    {
                        result.Add(track);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting most played tracks: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TrackDisplayModel>> GetLikedTracks(List<TrackDisplayModel> allTracks)
        {
            try
            {
                var profileId = await GetCurrentProfileId();
                var likedIds = await _trackStatsRepository.GetLikedTracks(profileId);

                // Create a lookup of all tracks
                var trackLookup = allTracks.ToDictionary(t => t.TrackID);

                // Get liked tracks
                return likedIds
                    .Where(id => trackLookup.ContainsKey(id))
                    .Select(id => trackLookup[id])
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting liked tracks: {ex.Message}");
                throw;
            }
        }
    }
}