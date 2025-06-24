using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TrackStatsRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackStatsRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<bool> IsTrackLiked(int trackId, int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var isLiked = await context.Likes
                        .AnyAsync(l => l.TrackId == trackId && l.ProfileId == profileId);

                    return isLiked;
                },
                $"Checking if track {trackId} is liked by profile {profileId}",
                false,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<int> GetPlayCount(int trackId, int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var playCount = await context.PlayCounts
                        .Where(pc => pc.TrackId == trackId && pc.ProfileId == profileId)
                        .Select(pc => pc.Count)
                        .FirstOrDefaultAsync();

                    return playCount;
                },
                $"Getting play count for track {trackId}, profile {profileId}",
                0, // Default to 0 plays if there's an error
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task UpdateTrackLike(int trackId, int profileId, bool isLiked)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    if (isLiked)
                    {
                        // Check if like already exists
                        var existingLike = await context.Likes
                            .Where(l => l.TrackId == trackId && l.ProfileId == profileId)
                            .FirstOrDefaultAsync();

                        if (existingLike == null)
                        {
                            // Add new like
                            var newLike = new Infrastructure.Data.Entities.Like
                            {
                                TrackId = trackId,
                                ProfileId = profileId,
                                LikedAt = DateTime.UtcNow
                            };

                            context.Likes.Add(newLike);
                            await context.SaveChangesAsync();
                        }
                    }
                    else
                    {
                        // Remove like
                        await context.Likes
                            .Where(l => l.TrackId == trackId && l.ProfileId == profileId)
                            .ExecuteDeleteAsync();
                    }
                },
                $"{(isLiked ? "Liking" : "Unliking")} track {trackId} for profile {profileId}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task IncrementPlayCount(int trackId, int playCount, int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    // Check if play count record exists
                    var existingPlayCount = await context.PlayCounts
                        .Where(pc => pc.TrackId == trackId && pc.ProfileId == profileId)
                        .FirstOrDefaultAsync();

                    if (existingPlayCount != null)
                    {
                        // Update existing record
                        existingPlayCount.Count = playCount;
                        existingPlayCount.LastPlayed = DateTime.UtcNow;
                    }
                    else
                    {
                        // Create new record
                        var newPlayCount = new Infrastructure.Data.Entities.PlayCount
                        {
                            TrackId = trackId,
                            ProfileId = profileId,
                            Count = playCount,
                            LastPlayed = DateTime.UtcNow
                        };

                        context.PlayCounts.Add(newPlayCount);
                    }

                    await context.SaveChangesAsync();
                },
                $"Updating play count to {playCount} for track {trackId}, profile {profileId}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<(int TrackId, int PlayCount)>> GetMostPlayedTracks(int profileId, int limit = 10)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var results = await context.PlayCounts
                        .AsNoTracking()
                        .Where(pc => pc.ProfileId == profileId)
                        .OrderByDescending(pc => pc.Count)
                        .Take(limit)
                        .Select(pc => new { pc.TrackId, pc.Count })
                        .ToListAsync();

                    return results.Select(r => (r.TrackId, r.Count)).ToList();
                },
                $"Getting most played tracks for profile {profileId}",
                new List<(int, int)>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<int>> GetLikedTracks(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var results = await context.Likes
                        .AsNoTracking()
                        .Where(l => l.ProfileId == profileId)
                        .OrderByDescending(l => l.LikedAt)
                        .Select(l => l.TrackId)
                        .ToListAsync();

                    return results;
                },
                $"Getting liked tracks for profile {profileId}",
                new List<int>(),
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}