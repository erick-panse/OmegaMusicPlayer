using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;

namespace OmegaPlayer.Features.Library.Services
{
    public class TrackArtistService
    {
        private readonly TrackArtistRepository _trackArtistRepository;

        public TrackArtistService(TrackArtistRepository trackArtistRepository)
        {
            _trackArtistRepository = trackArtistRepository;
        }

        public async Task<TrackArtist> GetTrackArtist(int trackID, int artistID)
        {
            try
            {
                return await _trackArtistRepository.GetTrackArtist(trackID, artistID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching TrackArtist: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TrackArtist>> GetAllTrackArtists()
        {
            try
            {
                return await _trackArtistRepository.GetAllTrackArtists();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all TrackArtists: {ex.Message}");
                throw;
            }
        }

        public async Task AddTrackArtist(TrackArtist trackArtist)
        {
            try
            {
                await _trackArtistRepository.AddTrackArtist(trackArtist);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding TrackArtist: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteTrackArtist(int trackID, int artistID)
        {
            try
            {
                await _trackArtistRepository.DeleteTrackArtist(trackID, artistID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting TrackArtist: {ex.Message}");
                throw;
            }
        }
    }
}
