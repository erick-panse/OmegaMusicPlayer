using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Playback.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playback
{
    public class QueueTracksRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public QueueTracksRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<QueueTracks>> GetTracksByQueueId(int queueID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (queueID <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid queue ID",
                            $"Attempted to get tracks with invalid queue ID: {queueID}",
                            null,
                            false);
                        return new List<QueueTracks>();
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var trackList = await context.QueueTracks
                        .AsNoTracking()
                        .Where(qt => qt.QueueId == queueID)
                        .OrderBy(qt => qt.TrackOrder)
                        .Select(qt => new QueueTracks
                        {
                            QueueID = qt.QueueId,
                            TrackID = qt.TrackId,
                            TrackOrder = qt.TrackOrder,
                            OriginalOrder = qt.OriginalOrder
                        })
                        .ToListAsync();

                    return trackList;
                },
                $"Getting tracks for queue {queueID}",
                new List<QueueTracks>(),
                ErrorSeverity.Playback,
                false);
        }

        public async Task AddTrackToQueue(List<QueueTracks> tracks)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (tracks == null || !tracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Empty tracks list provided",
                            "Attempted to add no tracks to queue",
                            null,
                            false);
                        return;
                    }

                    if (tracks.Any(t => t.QueueID <= 0))
                    {
                        throw new ArgumentException("One or more tracks has an invalid queue ID", nameof(tracks));
                    }

                    if (tracks.Any(t => t.TrackID <= 0))
                    {
                        throw new ArgumentException("One or more tracks has an invalid track ID", nameof(tracks));
                    }

                    using var context = _contextFactory.CreateDbContext();
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        var queueId = tracks.First().QueueID;

                        // First, remove all existing tracks
                        await context.QueueTracks
                            .Where(qt => qt.QueueId == queueId)
                            .ExecuteDeleteAsync();

                        // Then insert all new tracks in batch
                        var newQueueTracks = tracks.Select(track => new Infrastructure.Data.Entities.QueueTrack
                        {
                            QueueId = track.QueueID,
                            TrackId = track.TrackID,
                            TrackOrder = track.TrackOrder,
                            OriginalOrder = track.OriginalOrder
                        }).ToList();

                        context.QueueTracks.AddRange(newQueueTracks);
                        await context.SaveChangesAsync();

                        // Update LastModified in CurrentQueue
                        await context.CurrentQueues
                            .Where(cq => cq.QueueId == queueId)
                            .ExecuteUpdateAsync(s => s.SetProperty(cq => cq.LastModified, DateTime.UtcNow));

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _errorHandlingService.LogError(
                            ErrorSeverity.Playback,
                            "Failed to update queue tracks",
                            $"Error occurred while updating tracks for queue {tracks.First().QueueID}",
                            ex,
                            false);
                        throw; // Rethrow to be handled by the SafeExecuteAsync wrapper
                    }
                },
                $"Adding {tracks?.Count ?? 0} tracks to queue {tracks?.FirstOrDefault()?.QueueID ?? 0}",
                ErrorSeverity.Playback,
                false);
        }

        public async Task RemoveTracksByQueueId(int queueID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (queueID <= 0)
                    {
                        throw new ArgumentException("Invalid queue ID", nameof(queueID));
                    }

                    using var context = _contextFactory.CreateDbContext();

                    await context.QueueTracks
                        .Where(qt => qt.QueueId == queueID)
                        .ExecuteDeleteAsync();
                },
                $"Removing all tracks from queue {queueID}",
                ErrorSeverity.Playback,
                false);
        }
    }
}