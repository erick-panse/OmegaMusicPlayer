using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class MediaRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public MediaRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Media> GetMediaById(int mediaID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var media = await context.Media
                        .AsNoTracking()
                        .Where(m => m.MediaId == mediaID)
                        .Select(m => new Media
                        {
                            MediaID = m.MediaId,
                            CoverPath = m.CoverPath,
                            MediaType = m.MediaType
                        })
                        .FirstOrDefaultAsync();

                    if (media != null)
                    {
                        // Verify file exists if path is not null
                        if (!string.IsNullOrEmpty(media.CoverPath) &&
                            !File.Exists(media.CoverPath))
                        {
                            media.CoverPath = null;
                        }
                    }

                    return media;
                },
                $"Database operation: Get media with ID {mediaID}",
                null, // Return null on failure, no default media
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Media>> GetAllMedia()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var mediaList = await context.Media
                        .AsNoTracking()
                        .Select(m => new Media
                        {
                            MediaID = m.MediaId,
                            CoverPath = m.CoverPath,
                            MediaType = m.MediaType
                        })
                        .ToListAsync();

                    // Verify file exists for each media item
                    foreach (var media in mediaList)
                    {
                        if (!string.IsNullOrEmpty(media.CoverPath) &&
                            !File.Exists(media.CoverPath))
                        {
                            media.CoverPath = null;
                        }
                    }

                    return mediaList;
                },
                "Database operation: Get all media",
                new List<Media>(),
                ErrorSeverity.NonCritical,
                true);
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

                    // Ensure mediaType is set
                    string mediaType = !string.IsNullOrEmpty(media.MediaType) ?
                        media.MediaType : "unknown";

                    // CoverPath could be null - that's fine
                    string coverPath = media.CoverPath;

                    // Verify the file exists if path is not null
                    if (!string.IsNullOrEmpty(coverPath) && !File.Exists(coverPath))
                    {
                        coverPath = null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var newMedia = new Infrastructure.Data.Entities.Media
                    {
                        CoverPath = coverPath,
                        MediaType = mediaType
                    };

                    context.Media.Add(newMedia);
                    await context.SaveChangesAsync();

                    return newMedia.MediaId;
                },
                $"Database operation: Add media of type '{media?.MediaType ?? "unknown"}'",
                -1,
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

                    // Verify the file exists if path is not null
                    string coverPath = media.CoverPath;
                    if (!string.IsNullOrEmpty(coverPath) && !File.Exists(coverPath))
                    {
                        coverPath = null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var existingMedia = await context.Media
                        .Where(m => m.MediaId == media.MediaID)
                        .FirstOrDefaultAsync();

                    if (existingMedia == null)
                    {
                        throw new InvalidOperationException($"Media with ID {media.MediaID} not found");
                    }

                    existingMedia.CoverPath = coverPath;
                    existingMedia.MediaType = media.MediaType ?? "unknown";

                    await context.SaveChangesAsync();
                },
                $"Database operation: Update media with ID {media?.MediaID}",
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
                    if (!string.IsNullOrEmpty(coverPath) && !File.Exists(coverPath))
                    {
                        coverPath = null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    await context.Media
                        .Where(m => m.MediaId == mediaID)
                        .ExecuteUpdateAsync(s => s.SetProperty(m => m.CoverPath, coverPath));
                },
                $"Database operation: Update media file path for ID {mediaID}",
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

                    // First get the media to check if we need to delete the file
                    var media = await GetMediaById(mediaID);
                    string filePath = media?.CoverPath;

                    using var context = _contextFactory.CreateDbContext();

                    // Check if any albums or tracks reference this media
                    bool isReferenced = await IsMediaReferenced(context, mediaID);

                    if (isReferenced)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Media still in use",
                            $"Media ID {mediaID} is referenced by albums or tracks and cannot be deleted",
                            null,
                            true);
                        return;
                    }

                    // Delete the media record
                    await context.Media
                        .Where(m => m.MediaId == mediaID)
                        .ExecuteDeleteAsync();

                    // Try to delete the file if it exists
                    if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                    {
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Failed to delete media file",
                                $"Media record was deleted but the file at {filePath} could not be deleted: {ex.Message}",
                                ex,
                                false);
                        }
                    }
                },
                $"Database operation: Delete media with ID {mediaID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task<bool> IsMediaReferenced(OmegaPlayerDbContext context, int mediaID)
        {
            // Check Albums table for references
            var albumCount = await context.Albums
                .Where(a => a.CoverId == mediaID)
                .CountAsync();
            if (albumCount > 0)
            {
                return true;
            }

            // Check Tracks table for references
            var trackCount = await context.Tracks
                .Where(t => t.CoverId == mediaID)
                .CountAsync();
            if (trackCount > 0)
            {
                return true;
            }

            // Check Artists table for references (if photoID exists)
            try
            {
                var artistCount = await context.Artists
                    .Where(a => a.PhotoId == mediaID)
                    .CountAsync();
                if (artistCount > 0)
                {
                    return true;
                }
            }
            catch
            {
                // If the column doesn't exist, just ignore this check
            }

            // Check Profile table for references (if photoID exists)
            try
            {
                var profileCount = await context.Profiles
                    .Where(p => p.PhotoId == mediaID)
                    .CountAsync();
                if (profileCount > 0)
                {
                    return true;
                }
            }
            catch
            {
                // If the column doesn't exist, just ignore this check
            }

            return false;
        }

        /// <summary>
        /// Gets media by file path to avoid duplicates
        /// </summary>
        public async Task<Media> GetMediaByPath(string coverPath)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(coverPath))
                    {
                        return null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var media = await context.Media
                        .AsNoTracking()
                        .Where(m => m.CoverPath == coverPath)
                        .Select(m => new Media
                        {
                            MediaID = m.MediaId,
                            CoverPath = m.CoverPath,
                            MediaType = m.MediaType
                        })
                        .FirstOrDefaultAsync();

                    return media;
                },
                $"Database operation: Get media by path '{coverPath}'",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Cleans up media records that reference non-existent files
        /// </summary>
        public async Task<int> CleanupOrphanedMedia()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var allMedia = await GetAllMedia();
                    int cleanedCount = 0;

                    foreach (var media in allMedia)
                    {
                        if (!string.IsNullOrEmpty(media.CoverPath) && !File.Exists(media.CoverPath))
                        {
                            // Check if this media is referenced anywhere
                            using var context = _contextFactory.CreateDbContext();
                            bool isReferenced = await IsMediaReferenced(context, media.MediaID);
                            if (!isReferenced)
                            {
                                // Safe to delete orphaned media
                                await DeleteMedia(media.MediaID);
                                cleanedCount++;
                            }
                            else
                            {
                                // Just clear the path since file doesn't exist
                                await UpdateMediaFilePath(media.MediaID, null);
                            }
                        }
                    }

                    return cleanedCount;
                },
                "Cleaning up orphaned media files",
                0,
                ErrorSeverity.NonCritical,
                true);
        }
    }
}