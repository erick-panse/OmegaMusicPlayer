using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Infrastructure.Data.Repositories.Playlists;
using OmegaPlayer.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Playlist = OmegaPlayer.Features.Playlists.Models.Playlist;

namespace OmegaPlayer.Features.Playlists.Services
{
    public class PlaylistService
    {
        private readonly PlaylistRepository _playlistRepository;
        private readonly LocalizationService _localizationService;
        private readonly IErrorHandlingService _errorHandlingService;
        private const int NAME_CHAR_LIMIT = 50;

        public PlaylistService(
            PlaylistRepository playlistRepository,
            LocalizationService localizationService,
            IErrorHandlingService errorHandlingService)
        {
            _playlistRepository = playlistRepository;
            _localizationService = localizationService;
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

        public async Task<bool> IsPlaylistNameExists(string playlistName, int profileID, int? excludePlaylistId = null)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(playlistName))
                        return false;

                    var playlists = await _playlistRepository.GetAllPlaylists();
                    return playlists.Any(p =>
                        string.Equals(p.Title, playlistName.Trim(), StringComparison.OrdinalIgnoreCase)
                        && p.PlaylistID != excludePlaylistId 
                        && p.ProfileID == profileID);
                },
                "Checking if playlist name exists",
                false,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public string ValidatePlaylistName(string playlistName, int? excludePlaylistId = null, bool isSystemCreated = false)
        {
            // Check for null/empty/whitespace
            if (string.IsNullOrWhiteSpace(playlistName))
                return _localizationService["PlaylistNameEmpty"];

            // Trim and check again
            playlistName = playlistName.Trim();
            if (string.IsNullOrEmpty(playlistName))
                return _localizationService["PlaylistNameEmpty"];

            // Check length
            if (playlistName.Length > NAME_CHAR_LIMIT)
                return _localizationService["PlaylistNameTooLongFirstHalf"] + NAME_CHAR_LIMIT + _localizationService["PlaylistNameTooLongSecondHalf"];

            if (playlistName.Length < 2)
                return _localizationService["PlaylistNameTooShort"];

            // Check for invalid characters
            var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' });
            if (playlistName.Any(c => invalidChars.Contains(c)))
                return _localizationService["PlaylistNameInvalidCharacters"];

            // Check for reserved playlist names - only for user-created playlists
            if (!isSystemCreated && string.Equals(playlistName, _localizationService["Favorites"], StringComparison.OrdinalIgnoreCase))
                return _localizationService["PlaylistNameFavoritesReserved"];

            return null; // Valid
        }

        public async Task<string> ValidatePlaylistNameAsync(string playlistName, int profileID, int? excludePlaylistId = null, bool isSystemCreated = false)
        {
            // First check basic validation
            var basicValidation = ValidatePlaylistName(playlistName, excludePlaylistId, isSystemCreated);
            if (basicValidation != null)
                return basicValidation;

            // Then check for duplicates
            var isDuplicate = await IsPlaylistNameExists(playlistName, profileID, excludePlaylistId);
            if (isDuplicate && !isSystemCreated)
                return _localizationService["PlaylistNameAlreadyExists"];

            return null; // Valid
        }

        public async Task<int> AddPlaylist(Playlist playlistToAdd, int profileID, bool isSystemCreated = false)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistToAdd == null)
                    {
                        throw new ArgumentNullException(nameof(playlistToAdd), "Cannot add a null playlist");
                    }

                    // Validate playlist name
                    var validationMessage = await ValidatePlaylistNameAsync(playlistToAdd.Title, profileID, null, isSystemCreated);
                    if (!string.IsNullOrEmpty(validationMessage))
                    {
                        throw new ArgumentException(validationMessage, nameof(playlistToAdd.Title));
                    }

                    // Trim the playlist name
                    playlistToAdd.Title = playlistToAdd.Title.Trim();

                    // Ensure creation dates are set
                    if (playlistToAdd.CreatedAt == default)
                    {
                        playlistToAdd.CreatedAt = DateTime.UtcNow;
                    }
                    if (playlistToAdd.UpdatedAt == default)
                    {
                        playlistToAdd.UpdatedAt = DateTime.UtcNow;
                    }

                    return await _playlistRepository.AddPlaylist(playlistToAdd);
                },
                $"Adding playlist '{playlistToAdd?.Title ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                true
            );
        }

        public async Task UpdatePlaylist(Playlist playlistToUpdate, int profileID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistToUpdate == null)
                    {
                        throw new ArgumentNullException(nameof(playlistToUpdate), "Cannot update a null playlist");
                    }

                    // Validate playlist name (excluding current playlist from duplicate check)
                    var validationMessage = await ValidatePlaylistNameAsync(playlistToUpdate.Title, profileID, playlistToUpdate.PlaylistID);
                    if (!string.IsNullOrEmpty(validationMessage))
                    {
                        throw new ArgumentException(validationMessage, nameof(playlistToUpdate.Title));
                    }

                    // Trim the playlist name
                    playlistToUpdate.Title = playlistToUpdate.Title.Trim();

                    // Update the UpdatedAt timestamp
                    playlistToUpdate.UpdatedAt = DateTime.UtcNow;

                    await _playlistRepository.UpdatePlaylist(playlistToUpdate);
                },
                $"Updating playlist '{playlistToUpdate?.Title ?? "Unknown"}' (ID: {playlistToUpdate?.PlaylistID ?? 0})",
                ErrorSeverity.NonCritical,
                true
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