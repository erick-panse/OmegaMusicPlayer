using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Playback.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Playback;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Playback.Services
{
    public class QueueService
    {
        private readonly CurrentQueueRepository _currentQueueRepository;
        private readonly QueueTracksRepository _queueTracksRepository;

        public QueueService(CurrentQueueRepository currentQueueRepository, QueueTracksRepository queueTracksRepository)
        {
            _currentQueueRepository = currentQueueRepository;
            _queueTracksRepository = queueTracksRepository;
        }

        public async Task<QueueWithTracks> GetCurrentQueueByProfileId(int profileId)
        {
            try
            {
                // Fetch the current queue for the profile
                var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);
                if (currentQueue == null)
                {
                    Console.WriteLine("No current queue found for the profile.");
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while getting the current queue for profile: {ex.Message}");
                throw;
            }
        }
        public async Task<QueueStateInfo> GetCurrentQueueState(int profileId)
        {
            try
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting queue state: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateQueuePlaybackState(int profileId, int currentTrackOrder, bool isShuffled, string repeatMode)
        {
            try
            {
                var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);
                if (currentQueue != null)
                {
                    currentQueue.CurrentTrackOrder = currentTrackOrder;
                    currentQueue.IsShuffled = isShuffled;
                    currentQueue.RepeatMode = repeatMode;
                    await _currentQueueRepository.UpdateCurrentTrackOrder(currentQueue);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating queue playback state: {ex.Message}");
                throw;
            }
        }

        public async Task SaveCurrentTrackAsync(int queueId, int currentTrackOrder, int profileId)
        {
            try
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving current track: {ex.Message}");
                throw;
            }
        }

        public async Task SaveCurrentQueueState(int profileId, List<TrackDisplayModel> tracks, int currentTrackIndex, bool isShuffled, string repeatMode)
        {
            try
            {
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
                }
                else
                {
                    queueId = currentQueue.QueueID;
                    currentQueue.CurrentTrackOrder = currentTrackIndex;
                    currentQueue.IsShuffled = isShuffled;
                    currentQueue.RepeatMode = repeatMode;
                    await _currentQueueRepository.UpdateCurrentTrackOrder(currentQueue);
                }

                // First remove existing tracks
                await _queueTracksRepository.RemoveTracksByQueueId(queueId);

                // Create queue tracks with correct order
                var queueTracks = new List<QueueTracks>();
                for (int i = 0; i < tracks.Count; i++)
                {
                    queueTracks.Add(new QueueTracks
                    {
                        QueueID = queueId,
                        TrackID = tracks[i].TrackID,
                        TrackOrder = i,                  // Current order in queue
                        OriginalOrder = isShuffled
                            ? tracks[i].NowPlayingPosition  // Use position if shuffled
                            : i                            // Use current order if not shuffled
                    });
                }

                await _queueTracksRepository.AddTrackToQueue(queueTracks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving queue state: {ex.Message}");
                throw;
            }
        }



        public async Task ClearCurrentQueueForProfile(int profileId)
        {
            try
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing queue: {ex.Message}");
                throw;
            }
        }

        // Only when deleting a profile
        public async Task DeleteQueueForProfile(int profileId)
        {
            try
            {
                var currentQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);
                if (currentQueue != null)
                {
                    // Remove ALL tracks FROM QUEUE
                    await _currentQueueRepository.DeleteQueueById(currentQueue.QueueID);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting queue: {ex.Message}");
                throw;
            }
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
