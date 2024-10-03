using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
{
    public class ArtistsService
    {
        private readonly ArtistsRepository _artistsRepository;

        public ArtistsService(ArtistsRepository artistsRepository)
        {
            _artistsRepository = artistsRepository;
        }

        public async Task<Artists> GetArtistById(int artistID)
        {
            try
            {
                return await _artistsRepository.GetArtistById(artistID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching artist by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<Artists> GetArtistByName(string artistName)
        {
            try
            {
                return await _artistsRepository.GetArtistByName(artistName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching artist by Name: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Artists>> GetAllArtists()
        {
            try
            {
                return await _artistsRepository.GetAllArtists();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all artists: {ex.Message}");
                throw;
            }
        }

        public async Task<int> AddArtist(Artists artist)
        {
            try
            {
                return await _artistsRepository.AddArtist(artist);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding artist: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateArtist(Artists artist)
        {
            try
            {
                await _artistsRepository.UpdateArtist(artist);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating artist: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteArtist(int artistID)
        {
            try
            {
                await _artistsRepository.DeleteArtist(artistID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting artist: {ex.Message}");
                throw;
            }
        }
    }
}
