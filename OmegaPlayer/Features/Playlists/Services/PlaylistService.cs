using Playlist = OmegaPlayer.Features.Playlists.Models.Playlist;
using OmegaPlayer.Infrastructure.Data.Repositories.Playlists;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Features.Playlists.Services
{
    public class PlaylistService
    {
        private readonly PlaylistRepository _playlistRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        public PlaylistService(
            PlaylistRepository playlistRepository,
            IErrorHandlingService errorHandlingService)
        {
            _playlistRepository = playlistRepository;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Playlist> GetPlaylistById(int playlistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () => await _playlistRepository.GetPlaylistById(playlistID),
                $"Getting playlist with ID {playlistID}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<Playlist>> GetAllPlaylists()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () => await _playlistRepository.GetAllPlaylists(),
                "Getting all playlists",
                new List<Playlist>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<int> AddPlaylist(Playlist playlistToAdd)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistToAdd == null)
                    {
                        throw new ArgumentNullException(nameof(playlistToAdd), "Cannot add a null playlist");
                    }

                    // Make sure the playlist has a title
                    if (string.IsNullOrWhiteSpace(playlistToAdd.Title))
                    {
                        playlistToAdd.Title = "Untitled Playlist";
                    }

                    // Ensure creation dates are set
                    if (playlistToAdd.CreatedAt == default)
                    {
                        playlistToAdd.CreatedAt = DateTime.Now;
                    }
                    if (playlistToAdd.UpdatedAt == default)
                    {
                        playlistToAdd.UpdatedAt = DateTime.Now;
                    }

                    return await _playlistRepository.AddPlaylist(playlistToAdd);
                },
                $"Adding playlist '{playlistToAdd?.Title ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                true // Show notification for user-initiated action
            );
        }

        public async Task UpdatePlaylist(Playlist playlistToUpdate)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistToUpdate == null)
                    {
                        throw new ArgumentNullException(nameof(playlistToUpdate), "Cannot update a null playlist");
                    }

                    // Make sure the playlist has a title
                    if (string.IsNullOrWhiteSpace(playlistToUpdate.Title))
                    {
                        playlistToUpdate.Title = "Untitled Playlist";
                    }

                    // Update the UpdatedAt timestamp
                    playlistToUpdate.UpdatedAt = DateTime.Now;

                    await _playlistRepository.UpdatePlaylist(playlistToUpdate);
                },
                $"Updating playlist '{playlistToUpdate?.Title ?? "Unknown"}' (ID: {playlistToUpdate?.PlaylistID ?? 0})",
                ErrorSeverity.NonCritical,
                true // Show notification for user-initiated action
            );
        }

        public async Task DeletePlaylist(int playlistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistID <= 0)
                    {
                        throw new ArgumentException("Invalid playlist ID", nameof(playlistID));
                    }

                    await _playlistRepository.DeletePlaylist(playlistID);
                },
                $"Deleting playlist with ID {playlistID}",
                ErrorSeverity.NonCritical,
                true // Show notification for user-initiated action
            );
        }
    }
}