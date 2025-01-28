using Playlist = OmegaPlayer.Features.Playlists.Models.Playlist;
using OmegaPlayer.Infrastructure.Data.Repositories.Playlists;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Playlists.Services
{
    public class PlaylistService
    {
        private readonly PlaylistRepository _playlistRepository;

        public PlaylistService(PlaylistRepository playlistRepository)
        {
            _playlistRepository = playlistRepository;
        }

        public async Task<Playlist> GetPlaylistById(int playlistID)
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

        public async Task<List<Playlist>> GetAllPlaylists()
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

        public async Task<int> AddPlaylist(Playlist playlistToAdd)
        {
            try
            {
                return await _playlistRepository.AddPlaylist(playlistToAdd);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding Playlist: {ex.Message}");
                throw;
            }
        }

        public async Task UpdatePlaylist(Playlist playlistToUpdate)
        {
            try
            {
                await _playlistRepository.UpdatePlaylist(playlistToUpdate);
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
