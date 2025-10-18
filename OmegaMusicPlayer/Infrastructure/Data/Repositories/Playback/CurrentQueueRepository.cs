using Microsoft.EntityFrameworkCore;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Features.Playback.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Infrastructure.Data.Repositories.Playback
{
    public class CurrentQueueRepository
    {
        private readonly IDbContextFactory<OmegaMusicPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public CurrentQueueRepository(
            IDbContextFactory<OmegaMusicPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<CurrentQueue> GetCurrentQueueByProfileId(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (profileId <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid profile ID",
                            $"Attempted to get queue with invalid profile ID: {profileId}",
                            null,
                            false);
                        return null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var currentQueue = await context.CurrentQueues
                        .AsNoTracking()
                        .Where(cq => cq.ProfileId == profileId)
                        .Select(cq => new CurrentQueue
                        {
                            QueueID = cq.QueueId,
                            ProfileID = cq.ProfileId,
                            CurrentTrackOrder = cq.CurrentTrackOrder ?? -1,
                            IsShuffled = cq.IsShuffled,
                            RepeatMode = cq.RepeatMode,
                            LastModified = cq.LastModified
                        })
                        .FirstOrDefaultAsync();

                    return currentQueue;
                },
                $"Getting current queue for profile {profileId}",
                null,
                ErrorSeverity.Playback,
                false);
        }

        public async Task<int> CreateCurrentQueue(CurrentQueue currentQueue, CancellationToken cancellationToken = default)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (currentQueue == null)
                    {
                        throw new ArgumentNullException(nameof(currentQueue), "Cannot create a null queue");
                    }

                    if (currentQueue.ProfileID <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(currentQueue));
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    using var context = _contextFactory.CreateDbContext();

                    var newCurrentQueue = new Infrastructure.Data.Entities.CurrentQueue
                    {
                        ProfileId = currentQueue.ProfileID,
                        CurrentTrackOrder = currentQueue.CurrentTrackOrder,
                        IsShuffled = currentQueue.IsShuffled,
                        RepeatMode = currentQueue.RepeatMode,
                        LastModified = DateTime.UtcNow
                    };

                    context.CurrentQueues.Add(newCurrentQueue);
                    await context.SaveChangesAsync(cancellationToken);

                    return newCurrentQueue.QueueId;
                },
                $"Creating new queue for profile {currentQueue?.ProfileID ?? 0}",
                -1,
                ErrorSeverity.Playback,
                false);
        }

        public async Task UpdateCurrentTrackOrder(CurrentQueue currentQueue, CancellationToken cancellationToken = default)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (currentQueue == null)
                    {
                        throw new ArgumentNullException(nameof(currentQueue), "Cannot update a null queue");
                    }

                    if (currentQueue.QueueID <= 0)
                    {
                        throw new ArgumentException("Invalid queue ID", nameof(currentQueue));
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    using var context = _contextFactory.CreateDbContext();

                    var existingQueue = await context.CurrentQueues
                        .Where(cq => cq.QueueId == currentQueue.QueueID)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (existingQueue == null)
                    {
                        throw new InvalidOperationException($"Queue with ID {currentQueue.QueueID} not found");
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    existingQueue.CurrentTrackOrder = currentQueue.CurrentTrackOrder;
                    existingQueue.IsShuffled = currentQueue.IsShuffled;
                    existingQueue.RepeatMode = currentQueue.RepeatMode;
                    existingQueue.LastModified = DateTime.UtcNow;

                    await context.SaveChangesAsync(cancellationToken);
                },
                $"Updating queue {currentQueue?.QueueID ?? 0} with track order {currentQueue?.CurrentTrackOrder ?? 0}",
                ErrorSeverity.Playback,
                false);
        }

        /// <summary>
        /// Updates only queue metadata without touching queue tracks.
        /// This is a lightweight single-row UPDATE for navigation operations.
        /// </summary>
        public async Task UpdateQueueMetadata(
            int queueId,
            int currentTrackOrder,
            bool isShuffled,
            string repeatMode,
            CancellationToken cancellationToken = default)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (queueId <= 0)
                    {
                        throw new ArgumentException("Invalid queue ID", nameof(queueId));
                    }

                    // Check for cancellation before database operation
                    cancellationToken.ThrowIfCancellationRequested();

                    using var context = _contextFactory.CreateDbContext();

                    var existingQueue = await context.CurrentQueues
                        .Where(cq => cq.QueueId == queueId)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (existingQueue == null)
                    {
                        throw new InvalidOperationException($"Queue with ID {queueId} not found");
                    }

                    // Check for cancellation before update
                    cancellationToken.ThrowIfCancellationRequested();

                    // Update only metadata fields
                    existingQueue.CurrentTrackOrder = currentTrackOrder;
                    existingQueue.IsShuffled = isShuffled;
                    existingQueue.RepeatMode = repeatMode;
                    existingQueue.LastModified = DateTime.UtcNow;

                    await context.SaveChangesAsync(cancellationToken);
                },
                $"Updating queue metadata for queue {queueId}",
                ErrorSeverity.Playback,
                false);
        }

        public async Task DeleteQueueById(int queueID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (queueID <= 0)
                    {
                        throw new ArgumentException("Invalid queue ID", nameof(queueID));
                    }

                    using var context = _contextFactory.CreateDbContext();

                    await context.CurrentQueues
                        .Where(cq => cq.QueueId == queueID)
                        .ExecuteDeleteAsync();
                },
                $"Deleting queue with ID {queueID}",
                ErrorSeverity.Playback,
                false);
        }
    }
}