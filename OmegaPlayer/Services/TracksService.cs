using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
{
    public class TracksService
    {
        private readonly TracksRepository _tracksRepository;

        public TracksService()
        {
            _tracksRepository = new TracksRepository();
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
    }
}
