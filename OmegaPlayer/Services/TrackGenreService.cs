using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
{
    public class TrackGenreService
    {
        private readonly TrackGenreRepository _trackGenreRepository;

        public TrackGenreService()
        {
            _trackGenreRepository = new TrackGenreRepository();
        }

        public async Task<TrackGenre> GetTrackGenre(int trackID, int genreID)
        {
            try
            {
                return await _trackGenreRepository.GetTrackGenre(trackID, genreID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching TrackGenre: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TrackGenre>> GetAllTrackGenres()
        {
            try
            {
                return await _trackGenreRepository.GetAllTrackGenres();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all TrackGenres: {ex.Message}");
                throw;
            }
        }

        public async Task AddTrackGenre(TrackGenre trackGenre)
        {
            try
            {
                await _trackGenreRepository.AddTrackGenre(trackGenre);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding TrackGenre: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteTrackGenre(int trackID, int genreID)
        {
            try
            {
                await _trackGenreRepository.DeleteTrackGenre(trackID, genreID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting TrackGenre: {ex.Message}");
                throw;
            }
        }
    }
}
