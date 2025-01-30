using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Playlists;

namespace OmegaPlayer.Features.Playlists.Services
{
    public class PlaylistTracksService
    {
        private readonly PlaylistTracksRepository _playlistTracksRepository;

        public PlaylistTracksService(PlaylistTracksRepository playlistTracksRepository)
        {
            _playlistTracksRepository = playlistTracksRepository;
        }

        public async Task<List<PlaylistTracks>> GetAllPlaylistTracksForPlaylist(int playlistID)
        {
            try
            {
                return await _playlistTracksRepository.GetPlaylistTrack(playlistID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching playlist tracks: {ex.Message}");
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

        public async Task DeletePlaylistTrack(int playlistID)
        {
            try
            {
                await _playlistTracksRepository.DeletePlaylistTrack(playlistID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting PlaylistTrack: {ex.Message}");
                throw;
            }
        }
    }
}
