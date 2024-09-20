using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
{
    public class PlaylistService
    {
        private readonly PlaylistRepository _playlistRepository;

        public PlaylistService()
        {
            _playlistRepository = new PlaylistRepository();
        }

        public async Task<Playlists> GetPlaylistById(int playlistID)
        {
            try
            {
                return await _playlistRepository.GetPlaylistById(playlistID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Playlist by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Playlists>> GetAllPlaylists()
        {
            try
            {
                return await _playlistRepository.GetAllPlaylists();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all Playlists: {ex.Message}");
                throw;
            }
        }

        public async Task<int> AddPlaylist(Playlists playlist)
        {
            try
            {
                return await _playlistRepository.AddPlaylist(playlist);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding Playlist: {ex.Message}");
                throw;
            }
        }

        public async Task UpdatePlaylist(Playlists playlist)
        {
            try
            {
                await _playlistRepository.UpdatePlaylist(playlist);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Playlist: {ex.Message}");
                throw;
            }
        }

        public async Task DeletePlaylist(int playlistID)
        {
            try
            {
                await _playlistRepository.DeletePlaylist(playlistID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting Playlist: {ex.Message}");
                throw;
            }
        }
    }
}
