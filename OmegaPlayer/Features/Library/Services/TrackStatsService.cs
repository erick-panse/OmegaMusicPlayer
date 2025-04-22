using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Features.Library.Services
{
    public class TrackStatsService
    {
        private readonly TrackStatsRepository _trackStatsRepository;
        private readonly ProfileManager _profileManager;
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackStatsService(
            TrackStatsRepository trackStatsRepository,
            ProfileManager profileManager,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _trackStatsRepository = trackStatsRepository;
            _profileManager = profileManager;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;
        }

        private async Task<int> GetCurrentProfileId()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    return profile.ProfileID;
                },
                "Getting current profile ID for track stats",
                -1,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<bool> IsTrackLiked(int trackId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profileId = await GetCurrentProfileId();
                    if (profileId < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to get current profile",
                            "Current profile is not available, cannot check if track is liked.",
                            null,
                            false);
                        return false;
                    }

                    return await _trackStatsRepository.IsTrackLiked(trackId, profileId);
                },
                $"Checking if track {trackId} is liked",
                false,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task UpdateTrackLike(int trackId, bool isLiked)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profileId = await GetCurrentProfileId();
                    if (profileId < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to get current profile",
                            "Current profile is not available, cannot update track like status.",
                            null,
                            true);
                        return;
                    }

                    await _trackStatsRepository.UpdateTrackLike(trackId, profileId, isLiked);

                    // Only send message if operation was successful
                    _messenger.Send(new TrackLikeUpdateMessage(trackId, isLiked));
                },
                $"{(isLiked ? "Liking" : "Unliking")} track {trackId}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task IncrementPlayCount(int trackId, int playcount)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profileId = await GetCurrentProfileId();
                    if (profileId < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to get current profile",
                            "Current profile is not available, cannot increment play count.",
                            null,
                            false);
                        return;
                    }

                    await _trackStatsRepository.IncrementPlayCount(trackId, playcount, profileId);
                },
                $"Incrementing play count for track {trackId}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<TrackDisplayModel>> GetMostPlayedTracks(List<TrackDisplayModel> allTracks, int limit = 10)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (allTracks == null || !allTracks.Any())
                    {
                        return new List<TrackDisplayModel>();
                    }

                    var profileId = await GetCurrentProfileId();
                    if (profileId < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to get current profile",
                            "Current profile is not available, cannot get most played tracks.",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

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
                },
                $"Getting most played tracks (limit: {limit})",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<TrackDisplayModel>> GetLikedTracks(List<TrackDisplayModel> allTracks)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (allTracks == null || !allTracks.Any())
                    {
                        return new List<TrackDisplayModel>();
                    }

                    var profileId = await GetCurrentProfileId();
                    if (profileId < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to get current profile",
                            "Current profile is not available, cannot get liked tracks.",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

                    var likedIds = await _trackStatsRepository.GetLikedTracks(profileId);

                    // Create a lookup of all tracks
                    var trackLookup = allTracks.ToDictionary(t => t.TrackID);

                    // Get liked tracks
                    return likedIds
                        .Where(id => trackLookup.ContainsKey(id))
                        .Select(id => trackLookup[id])
                        .ToList();
                },
                "Getting liked tracks",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}