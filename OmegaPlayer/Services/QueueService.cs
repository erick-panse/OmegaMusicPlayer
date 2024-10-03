using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
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

        public async Task CreateOrUpdateCurrentQueue(CurrentQueue currentQueue, List<QueueTracks> qTracks)
        {
            try
            {
                // Check if the queue already exists for the profile
                var existingQueue = await _currentQueueRepository.GetCurrentQueueByProfileId(currentQueue.ProfileID);

                if (existingQueue == null)
                {
                    // Create new queue
                    int newQueueId = await _currentQueueRepository.CreateCurrentQueue(currentQueue);

                    // Insert all tracks into the queue
                    foreach (var qTrack in qTracks)
                    {
                        qTrack.QueueID = newQueueId;
                    }

                    await _queueTracksRepository.AddTrackToQueue(qTracks);
                }
                else
                {
                    // Update existing queue
                    existingQueue.CurrentTrackOrder = currentQueue.CurrentTrackOrder;

                    await _currentQueueRepository.UpdateCurrentQueue(existingQueue);

                    // Remove existing tracks and add the new ones
                    await _queueTracksRepository.RemoveTracksByQueueId(existingQueue.QueueID);
                    foreach (var qTrack in qTracks)
                    {
                        qTrack.QueueID = existingQueue.QueueID;
                    }

                    await _queueTracksRepository.AddTrackToQueue(qTracks);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while creating or updating the current queue: {ex.Message}");
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
