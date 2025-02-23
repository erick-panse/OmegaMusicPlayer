using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.Services
{
    public class TracksService
    {
        private readonly TracksRepository _tracksRepository;

        public TracksService(TracksRepository tracksRepository)
        {
            _tracksRepository = tracksRepository;
        }

        public async Task<Tracks> GetTrackById(int trackID)
        {
            try
            {
                return await _tracksRepository.GetTrackById(trackID);
            }
            catch (Exception ex)
            {
                // Log exception and rethrow or handle it
                Console.WriteLine($"Error fetching track by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<Tracks> GetTrackByPath(string filePath)
        {
            try
            {
                return await _tracksRepository.GetTrackByPath(filePath);
            }
            catch (Exception ex)
            {
                // Log exception and rethrow or handle it
                Console.WriteLine($"Error fetching track by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Tracks>> GetAllTracks()
        {
            try
            {
                return await _tracksRepository.GetAllTracks();
            }
            catch (Exception ex)
            {
                // Log exception and rethrow or handle it
                Console.WriteLine($"Error fetching all tracks: {ex.Message}");
                throw;
            }
        }

        public async Task<int> AddTrack(Tracks track)
        {
            try
            {
                return await _tracksRepository.AddTrack(track);
            }
            catch (Exception ex)
            {
                // Log exception and rethrow or handle it
                Console.WriteLine($"Error adding track: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateTrack(Tracks track)
        {
            try
            {
                await _tracksRepository.UpdateTrack(track);
            }
            catch (Exception ex)
            {
                // Log exception and rethrow or handle it
                Console.WriteLine($"Error updating track: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteTrack(int trackID)
        {
            try
            {
                await _tracksRepository.DeleteTrack(trackID);
            }
            catch (Exception ex)
            {
                // Log exception and rethrow or handle it
                Console.WriteLine($"Error deleting track: {ex.Message}");
                throw;
            }
        }
        public async Task UpdateTrackLike(int trackId, bool isLiked)
        {
            try
            {
                await _tracksRepository.UpdateTrackLike(trackId, isLiked);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating track like in service: {ex.Message}");
                throw;
            }
        }
        public async Task IncrementPlayCount(int trackId, int playCount)
        {
            try
            {
                await _tracksRepository.IncrementPlayCount(trackId, playCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error incrementing play count in service: {ex.Message}");
                throw;
            }
        }

    }
}
