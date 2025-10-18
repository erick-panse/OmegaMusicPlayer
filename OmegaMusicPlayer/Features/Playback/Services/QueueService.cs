using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Services;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Playback.Models;
using OmegaMusicPlayer.Infrastructure.Data.Repositories.Playback;
using OmegaMusicPlayer.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Playback.Services
{
    public class QueueService
    {
        private readonly CurrentQueueRepository _currentQueueRepository;
        private readonly QueueTracksRepository _queueTracksRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        public QueueService(CurrentQueueRepository currentQueueRepository, QueueTracksRepository queueTracksRepository, IErrorHandlingService errorHandlingService)
        {
            _currentQueueRepository = currentQueueRepository;
            _queueTracksRepository = queueTracksRepository;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<QueueStateInfo> GetCurrentQueueState(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Fetch the current queue for the profile
                    var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);
                    if (currentQueue == null)
                    {
                        return null;
                    }

                    // Fetch the tracks associated with the queue
                    var queueTracks = await _queueTracksRepository.GetTracksByQueueId(currentQueue.QueueID);

                    return new QueueStateInfo
                    {
                        CurrentQueue = currentQueue,
                        Tracks = queueTracks ?? new List<QueueTracks>(),
                        IsShuffled = currentQueue.IsShuffled,
                        RepeatMode = currentQueue.RepeatMode
                    };
                },
                $"Getting queue state for profile {profileId}",
                null,
                ErrorSeverity.Playback,
                false
            );
        }

        /// <summary>
        /// Modified version of SaveCurrentQueueState with cancellation token support.
        /// </summary>
        public async Task SaveCurrentQueueState(
            int profileId,
            List<TrackDisplayModel> tracks,
            int currentTrackIndex,
            bool isShuffled,
            string repeatMode,
            List<QueueTracks> shuffledQueueTracks = null,
            CancellationToken cancellationToken = default)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Validate inputs
                    if (profileId <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(profileId));
                    }

                    if (tracks == null || !tracks.Any())
                    {
                        _errorHandlingService.LogInfo(
                            "No tracks to save to queue",
                            $"Profile ID: {profileId}, skipping save operation.");
                        return;
                    }

                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();

                    // Ensure current track index is valid
                    if (currentTrackIndex < 0 || currentTrackIndex >= tracks.Count)
                    {
                        currentTrackIndex = 0;
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Invalid track index corrected",
                            $"Current track index was out of range. Reset to 0 for profile {profileId}.",
                            null,
                            false);
                    }

                    // Get or create queue for profile
                    var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);
                    int queueId;

                    if (currentQueue == null)
                    {
                        // Create new queue
                        var newQueue = new CurrentQueue
                        {
                            ProfileID = profileId,
                            CurrentTrackOrder = currentTrackIndex,
                            IsShuffled = isShuffled,
                            RepeatMode = repeatMode
                        };

                        queueId = await _currentQueueRepository.CreateCurrentQueue(newQueue, cancellationToken);

                        if (queueId <= 0)
                        {
                            throw new InvalidOperationException($"Failed to create new queue for profile {profileId}");
                        }
                    }
                    else
                    {
                        queueId = currentQueue.QueueID;

                        // Check for cancellation before update
                        cancellationToken.ThrowIfCancellationRequested();

                        // Update queue metadata
                        currentQueue.CurrentTrackOrder = currentTrackIndex;
                        currentQueue.IsShuffled = isShuffled;
                        currentQueue.RepeatMode = repeatMode;
                        await _currentQueueRepository.UpdateCurrentTrackOrder(currentQueue, cancellationToken);
                    }

                    // Check for cancellation before heavy track save operation
                    cancellationToken.ThrowIfCancellationRequested();

                    // Create queue tracks with correct order
                    var queueTracks = new List<QueueTracks>();

                    // If custom queue tracks were provided, use them
                    if (shuffledQueueTracks != null && shuffledQueueTracks.Any())
                    {
                        foreach (var queueTrack in shuffledQueueTracks)
                        {
                            queueTrack.QueueID = queueId;
                            queueTracks.Add(queueTrack);
                        }
                    }
                    else
                    {
                        // Otherwise create queue tracks based on the tracks list
                        for (int i = 0; i < tracks.Count; i++)
                        {
                            queueTracks.Add(new QueueTracks
                            {
                                QueueID = queueId,
                                TrackID = tracks[i].TrackID,
                                TrackOrder = i,     // Current order in queue
                                OriginalOrder = i   // By default, original order matches TrackOrder (current order)
                            });
                        }
                    }

                    // Final cancellation check before database write
                    cancellationToken.ThrowIfCancellationRequested();

                    // The AddTrackToQueue method handles removal and addition in a single transaction
                    await _queueTracksRepository.AddTrackToQueue(queueTracks, cancellationToken);
                },
                $"Saving queue state for profile {profileId} with {tracks?.Count ?? 0} tracks",
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task ClearCurrentQueueForProfile(int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);
                    if (currentQueue != null)
                    {
                        await _queueTracksRepository.RemoveTracksByQueueId(currentQueue.QueueID);

                        // Reset queue state
                        currentQueue.CurrentTrackOrder = 0;
                        currentQueue.IsShuffled = false;
                        currentQueue.RepeatMode = "none";
                        await _currentQueueRepository.UpdateCurrentTrackOrder(currentQueue);
                    }
                },
                $"Clearing queue for profile {profileId}",
                ErrorSeverity.Playback,
                false
            );
        }

        /// <summary>
        /// Provides a convenience method alias for the error recovery service.
        /// </summary>
        public async Task ClearQueue()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Use the ProfileManager to get the current profile ID
                    var profileManager = App.ServiceProvider.GetService<ProfileManager>();
                    if (profileManager != null)
                    {
                        var profile = await profileManager.GetCurrentProfileAsync();
                        await ClearCurrentQueueForProfile(profile.ProfileID);
                    }
                    else
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to clear queue",
                            "Current profile is not available",
                            null,
                            false);
                    }
                },
                "Clearing queue via convenience method",
                ErrorSeverity.NonCritical,
                false
            );
        }
        /// <summary>
        /// Saves only queue metadata (current track index, shuffle state, repeat mode) without touching queue tracks.
        /// This is a lightweight operation for navigation (next/previous) that only updates a single row.
        /// </summary>
        public async Task SaveQueueMetadataOnly(
            int profileId,
            int currentTrackIndex,
            bool isShuffled,
            string repeatMode,
            CancellationToken cancellationToken = default)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Validate inputs
                    if (profileId <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(profileId));
                    }

                    if (currentTrackIndex < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Invalid track index in metadata save",
                            $"Track index {currentTrackIndex} is negative. Setting to 0.",
                            null,
                            false);
                        currentTrackIndex = 0;
                    }

                    // Check for cancellation before proceeding
                    cancellationToken.ThrowIfCancellationRequested();

                    // Get existing queue
                    var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);

                    if (currentQueue == null)
                    {
                        // If queue doesn't exist, we can't just save metadata - need full queue
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "No queue exists for metadata-only save",
                            $"Profile {profileId} has no queue. Metadata save skipped.",
                            null,
                            false);
                        return;
                    }

                    // Check for cancellation again before database operation
                    cancellationToken.ThrowIfCancellationRequested();

                    // Update only the metadata fields
                    await _currentQueueRepository.UpdateQueueMetadata(
                        currentQueue.QueueID,
                        currentTrackIndex,
                        isShuffled,
                        repeatMode,
                        cancellationToken);
                },
                $"Saving queue metadata for profile {profileId}",
                ErrorSeverity.Playback,
                false
            );
        }

        public class QueueStateInfo
        {
            public CurrentQueue CurrentQueue { get; set; }
            public List<QueueTracks> Tracks { get; set; }
            public bool IsShuffled { get; set; }
            public string RepeatMode { get; set; }
        }
    }
}