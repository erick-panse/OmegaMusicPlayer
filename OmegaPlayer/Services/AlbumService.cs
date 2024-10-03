using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
{
    public class AlbumService
    {
        private readonly AlbumRepository _albumRepository;

        public AlbumService(AlbumRepository albumRepository)
        {
            _albumRepository = albumRepository;
        }

        public async Task<Albums> GetAlbumById(int albumID)
        {
            try
            {
                return await _albumRepository.GetAlbumById(albumID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Album by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<Albums> GetAlbumByTitle(string title, int artistID)
        {
            try
            {
                return await _albumRepository.GetAlbumByTitle(title, artistID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Album by Title: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Albums>> GetAllAlbums()
        {
            try
            {
                return await _albumRepository.GetAllAlbums();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all Albums: {ex.Message}");
                throw;
            }
        }

        public async Task<int> AddAlbum(Albums album)
        {
            try
            {
                return await _albumRepository.AddAlbum(album);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding Album: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateAlbum(Albums album)
        {
            try
            {
                await _albumRepository.UpdateAlbum(album);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Album: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteAlbum(int albumID)
        {
            try
            {
                await _albumRepository.DeleteAlbum(albumID);
            }
            catch (Exception ex) { Console.WriteLine($"Error deleting Album: {ex.Message}"); throw; }
        }
    }

}
