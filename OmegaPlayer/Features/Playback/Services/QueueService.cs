using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Playback.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Playback;
using OmegaPlayer.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Playback.Services
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

        public async Task<QueueWithTracks> GetCurrentQueueByProfileId(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Fetch the current queue for the profile
                    var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);
                    if (currentQueue == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No current queue found for profile",
                            $"Profile ID: {profileId}");
                        return null;
                    }

                    // Fetch the tracks associated with the queue
                    var queueTracks = await _queueTracksRepository.GetTracksByQueueId(currentQueue.QueueID);

                    // Return the queue and associated tracks as a tuple
                    return new QueueWithTracks
                    {
                        CurrentQueueByProfile = currentQueue,
                        Tracks = queueTracks ?? new List<QueueTracks>() // If no tracks found, return an empty list
                    };
                },
                $"Getting current queue for profile {profileId}",
                null,
                ErrorSeverity.Playback,
                false
            );
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

        public async Task UpdateQueuePlaybackState(int profileId, int currentTrackOrder, bool isShuffled, string repeatMode)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);
                    if (currentQueue != null)
                    {
                        currentQueue.CurrentTrackOrder = currentTrackOrder;
                        currentQueue.IsShuffled = isShuffled;
                        currentQueue.RepeatMode = repeatMode;
                        await _currentQueueRepository.UpdateCurrentTrackOrder(currentQueue);
                    }
                },
                $"Updating queue playback state for profile {profileId}",
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task SaveCurrentTrackAsync(int queueId, int currentTrackOrder, int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Fetch existing queue
                    var existingQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);

                    if (existingQueue == null)
                    {
                        // If queue does not exist, create a new one with the CurrentTrackOrder
                        var newQueue = new CurrentQueue
                        {
                            ProfileID = profileId,
                            CurrentTrackOrder = currentTrackOrder
                        };
                        await _currentQueueRepository.CreateCurrentQueue(newQueue);
                    }
                    else
                    {
                        // Update only the CurrentTrackOrder
                        existingQueue.CurrentTrackOrder = currentTrackOrder;
                        await _currentQueueRepository.UpdateCurrentTrackOrder(existingQueue);
                    }
                },
                $"Saving current track (order: {currentTrackOrder}) for profile {profileId}",
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task SaveCurrentQueueState(
            int profileId,
            List<TrackDisplayModel> tracks,
            int currentTrackIndex,
            bool isShuffled,
            string repeatMode,
            List<QueueTracks> shuffledQueueTracks = null)
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

                        queueId = await _currentQueueRepository.CreateCurrentQueue(newQueue);

                        if (queueId <= 0)
                        {
                            throw new InvalidOperationException($"Failed to create new queue for profile {profileId}");
                        }
                    }
                    else
                    {
                        queueId = currentQueue.QueueID;

                        // Only update if there's a change
                        if (currentQueue.CurrentTrackOrder != currentTrackIndex ||
                            currentQueue.IsShuffled != isShuffled ||
                            currentQueue.RepeatMode != repeatMode)
                        {
                            currentQueue.CurrentTrackOrder = currentTrackIndex;
                            currentQueue.IsShuffled = isShuffled;
                            currentQueue.RepeatMode = repeatMode;
                            await _currentQueueRepository.UpdateCurrentTrackOrder(currentQueue);
                        }
                    }

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

                    // The AddTrackToQueue method handles removal and addition in a single transaction
                    await _queueTracksRepository.AddTrackToQueue(queueTracks);
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

        // Only when deleting a profile
        public async Task DeleteQueueForProfile(int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);
                    if (currentQueue != null)
                    {
                        // Remove ALL tracks FROM QUEUE
                        await _currentQueueRepository.DeleteQueueById(currentQueue.QueueID);
                    }
                },
                $"Deleting queue for profile {profileId}",
                ErrorSeverity.NonCritical,
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