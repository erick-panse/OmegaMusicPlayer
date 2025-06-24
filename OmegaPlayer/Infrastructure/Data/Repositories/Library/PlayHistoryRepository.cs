using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class PlayHistoryRepository
    {
        private const int MAX_HISTORY_PER_PROFILE = 100;
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public PlayHistoryRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<PlayHistory>> GetRecentlyPlayed(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var history = await context.PlayHistories
                        .AsNoTracking()
                        .Where(ph => ph.ProfileId == profileId)
                        .OrderByDescending(ph => ph.PlayedAt)
                        .Take(MAX_HISTORY_PER_PROFILE)
                        .Select(ph => new PlayHistory
                        {
                            HistoryID = ph.HistoryId,
                            ProfileID = ph.ProfileId,
                            TrackID = ph.TrackId,
                            PlayedAt = ph.PlayedAt
                        })
                        .ToListAsync();

                    return history;
                },
                $"Getting recently played tracks for profile {profileId}",
                new List<PlayHistory>(),
                ErrorSeverity.NonCritical
            );
        }

        public async Task AddToHistory(int profileId, int trackId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        // First check and maintain history size limit
                        // Get the IDs of records that should be kept (most recent MAX_HISTORY_PER_PROFILE - 1)
                        var historyToKeep = await context.PlayHistories
                            .Where(ph => ph.ProfileId == profileId)
                            .OrderByDescending(ph => ph.PlayedAt)
                            .Take(MAX_HISTORY_PER_PROFILE - 1) // Leave room for the new entry
                            .Select(ph => ph.HistoryId)
                            .ToListAsync();

                        // Delete records that are not in the keep list
                        if (historyToKeep.Any())
                        {
                            await context.PlayHistories
                                .Where(ph => ph.ProfileId == profileId && !historyToKeep.Contains(ph.HistoryId))
                                .ExecuteDeleteAsync();
                        }
                        else
                        {
                            // If no records to keep, delete all for this profile
                            await context.PlayHistories
                                .Where(ph => ph.ProfileId == profileId)
                                .ExecuteDeleteAsync();
                        }

                        // Add new history entry
                        var newHistory = new Infrastructure.Data.Entities.PlayHistory
                        {
                            ProfileId = profileId,
                            TrackId = trackId,
                            PlayedAt = DateTime.UtcNow
                        };

                        context.PlayHistories.Add(newHistory);
                        await context.SaveChangesAsync();

                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                },
                $"Adding track {trackId} to play history for profile {profileId}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task ClearHistory(int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    await context.PlayHistories
                        .Where(ph => ph.ProfileId == profileId)
                        .ExecuteDeleteAsync();
                },
                $"Clearing play history for profile {profileId}",
                ErrorSeverity.NonCritical
            );
        }
    }
}