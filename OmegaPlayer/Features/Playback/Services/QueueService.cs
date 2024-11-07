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

        public async Task SaveNowPlayingQueueAsync(int queueId, List<QueueTracks> queueTracks, int profileId)
        {
            try
            {
                // Check if the queue exists
                var existingQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(profileId);

                if (existingQueue == null)
                {
                    // Create a new queue and tracks if none exists
                    var newQueue = new CurrentQueue
                    {
                        ProfileID = profileId,
                        CurrentTrackOrder = queueTracks.FirstOrDefault()?.TrackOrder ?? 0 // Default to 0 if no track order
                    };

                    int newQueueId = await _currentQueueRepository.CreateCurrentQueue(newQueue);

                    // Save tracks for the new queue
                    queueTracks.ForEach(t => t.QueueID = newQueueId);
                    await _queueTracksRepository.AddTrackToQueue(queueTracks);
                }
                else
                {
                    // Update existing queue tracks
                    await _queueTracksRepository.RemoveTracksByQueueId(existingQueue.QueueID);
                    queueTracks.ForEach(t => t.QueueID = existingQueue.QueueID);
                    await _queueTracksRepository.AddTrackToQueue(queueTracks);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving NowPlayingQueue: {ex.Message}");
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
                    // Remove ALL tracks FROM QUEUE
                    await _queueTracksRepository.RemoveAllTracksByQueueId(currentQueue.QueueID);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while clearing the current queue for profile: {ex.Message}");
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
                Console.WriteLine($"An error occurred while clearing the current queue for profile: {ex.Message}");
                throw;
            }
        }
    }
}
