using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
{
    public class PlaylistTracksService
    {
        private readonly PlaylistTracksRepository _playlistTracksRepository;

        public PlaylistTracksService()
        {
            _playlistTracksRepository = new PlaylistTracksRepository();
        }

        public async Task<PlaylistTracks> GetPlaylistTrack(int playlistID, int profileID)
        {
            try
            {
                return await _playlistTracksRepository.GetPlaylistTrack(playlistID, profileID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching PlaylistTrack: {ex.Message}");
                throw;
            }
        }

        public async Task<List<PlaylistTracks>> GetAllPlaylistTracks()
        {
            try
            {
                return await _playlistTracksRepository.GetAllPlaylistTracks();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all PlaylistTracks: {ex.Message}");
                throw;
            }
        }

        public async Task AddPlaylistTrack(PlaylistTracks playlistTrack)
        {
            try
            {
                await _playlistTracksRepository.AddPlaylistTrack(playlistTrack);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding PlaylistTrack: {ex.Message}");
                throw;
            }
        }

        public async Task DeletePlaylistTrack(int playlistID, int profileID)
        {
            try
            {
                await _playlistTracksRepository.DeletePlaylistTrack(playlistID, profileID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting PlaylistTrack: {ex.Message}");
                throw;
            }
        }
    }
}
