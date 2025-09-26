using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Infrastructure.Data.Repositories.Library;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Enums;
using System.IO;
using System.Linq;

namespace OmegaMusicPlayer.Features.Library.Services
{
    public class MediaService
    {
        private readonly MediaRepository _mediaRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache for frequently accessed media to reduce DB load
        private readonly Dictionary<int, Media> _mediaCache = new Dictionary<int, Media>();
        private const int MAX_CACHE_SIZE = 200;

        public MediaService(
            MediaRepository mediaRepository,
            IErrorHandlingService errorHandlingService)
        {
            _mediaRepository = mediaRepository;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Media> GetMediaById(int mediaID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    if (_mediaCache.TryGetValue(mediaID, out var cachedMedia))
                    {
                        // Verify the file still exists if path is not null
                        if (!string.IsNullOrEmpty(cachedMedia.CoverPath) &&
                            !File.Exists(cachedMedia.CoverPath))
                        {
                            // File doesn't exist anymore, return media with null path
                            cachedMedia.CoverPath = null;
                            return cachedMedia;
                        }
                        return cachedMedia;
                    }

                    var media = await _mediaRepository.GetMediaById(mediaID);

                    if (media != null)
                    {
                        // Verify the file exists if path is not null
                        if (!string.IsNullOrEmpty(media.CoverPath) &&
                            !File.Exists(media.CoverPath))
                        {
                            // File doesn't exist anymore, set path to null
                            media.CoverPath = null;
                        }

                        // Add to cache
                        AddToCache(media);
                    }

                    return media;
                },
                $"Fetching media with ID {mediaID}",
                null, // Return null on failure, no default media
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Media>> GetAllMedia()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var mediaList = await _mediaRepository.GetAllMedia();

                    // Reset cache and add retrieved media up to cache limit
                    _mediaCache.Clear();

                    foreach (var media in mediaList.Take(MAX_CACHE_SIZE))
                    {
                        // Verify files exist
                        if (!string.IsNullOrEmpty(media.CoverPath) &&
                            !File.Exists(media.CoverPath))
                        {
                            media.CoverPath = null;
                        }

                        AddToCache(media);
                    }

                    return mediaList;
                },
                "Fetching all media",
                new List<Media>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<int> AddMedia(Media media)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (media == null)
                    {
                        throw new ArgumentNullException(nameof(media), "Cannot add null media to database");
                    }

                    // Ensure mediaType is set to a valid value
                    if (string.IsNullOrEmpty(media.MediaType))
                    {
                        media.MediaType = "unknown";
                    }

                    // Check if the coverPath is valid if set
                    if (!string.IsNullOrEmpty(media.CoverPath) && !File.Exists(media.CoverPath))
                    {
                        media.CoverPath = null;
                    }

                    int mediaId = await _mediaRepository.AddMedia(media);
                    media.MediaID = mediaId;

                    // Add to cache
                    AddToCache(media);

                    return mediaId;
                },
                $"Adding media of type {media?.MediaType ?? "unknown"}",
                -1, // Return -1 on failure
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateMedia(Media media)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (media == null || media.MediaID <= 0)
                    {
                        throw new ArgumentException("Cannot update null media or media with invalid ID", nameof(media));
                    }

                    // Check if the file exists if CoverPath is set
                    if (!string.IsNullOrEmpty(media.CoverPath) &&
                        !File.Exists(media.CoverPath))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Media file not found",
                            $"Media file at path {media.CoverPath} does not exist. Setting to null.",
                            null,
                            false);

                        media.CoverPath = null;
                    }

                    await _mediaRepository.UpdateMedia(media);

                    // Update cache
                    AddToCache(media);
                },
                $"Updating media with ID {media?.MediaID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateMediaFilePath(int mediaID, string coverPath)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (mediaID <= 0)
                    {
                        throw new ArgumentException("Cannot update media with invalid ID", nameof(mediaID));
                    }

                    // Verify the file exists if path is not null
                    if (!string.IsNullOrEmpty(coverPath) &&
                        !File.Exists(coverPath))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Media file not found",
                            $"Media file at path {coverPath} does not exist. Setting to null.",
                            null,
                            false);

                        coverPath = null;
                    }

                    await _mediaRepository.UpdateMediaFilePath(mediaID, coverPath);

                    // Update cache if media is in it
                    if (_mediaCache.TryGetValue(mediaID, out var media))
                    {
                        media.CoverPath = coverPath;
                    }
                },
                $"Updating media file path for ID {mediaID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteMedia(int mediaID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (mediaID <= 0)
                    {
                        throw new ArgumentException("Cannot delete media with invalid ID", nameof(mediaID));
                    }

                    // Get the media first to check if we need to delete the file
                    var media = await _mediaRepository.GetMediaById(mediaID);

                    // Try to delete the file if it exists
                    if (media != null && !string.IsNullOrEmpty(media.CoverPath) &&
                        File.Exists(media.CoverPath))
                    {
                        try
                        {
                            File.Delete(media.CoverPath);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Failed to delete media file",
                                $"Could not delete file at {media.CoverPath}: {ex.Message}",
                                ex,
                                false);
                        }
                    }

                    // Remove from cache
                    _mediaCache.Remove(mediaID);

                    // Delete from repository
                    await _mediaRepository.DeleteMedia(mediaID);
                },
                $"Deleting media with ID {mediaID}",
                ErrorSeverity.NonCritical,
                false);
        }

        private void AddToCache(Media media)
        {
            // Manage cache size
            if (_mediaCache.Count >= MAX_CACHE_SIZE)
            {
                // Remove oldest third of entries
                var keysToRemove = _mediaCache.Keys.Take(_mediaCache.Count / 3).ToList();
                foreach (var key in keysToRemove)
                {
                    _mediaCache.Remove(key);
                }
            }

            // Add/update in cache
            _mediaCache[media.MediaID] = media;
        }

        public void InvalidateCache()
        {
            _mediaCache.Clear();
        }
    }
}