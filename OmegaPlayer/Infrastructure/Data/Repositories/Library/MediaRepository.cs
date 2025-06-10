using Microsoft.Data.Sqlite;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class MediaRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public MediaRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Media> GetMediaById(int mediaID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = "SELECT mediaid, coverpath, mediatype FROM media WHERE mediaid = @mediaID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@mediaID"] = mediaID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            var media = new Media
                            {
                                MediaID = reader.GetInt32("mediaid"),
                                CoverPath = reader.IsDBNull("coverpath") ?
                                    null : reader.GetString("coverpath"),
                                MediaType = reader.GetString("mediatype")
                            };

                            // Verify file exists if path is not null
                            if (!string.IsNullOrEmpty(media.CoverPath) &&
                                !File.Exists(media.CoverPath))
                            {
                                media.CoverPath = null;
                            }

                            return media;
                        }
                    }
                    return null;
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
                    var mediaList = new List<Media>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT mediaid, coverpath, mediatype FROM media";

                        using var cmd = db.CreateCommand(query);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var media = new Media
                            {
                                MediaID = reader.GetInt32("mediaid"),
                                CoverPath = reader.IsDBNull("coverpath") ?
                                    null : reader.GetString("coverpath"),
                                MediaType = reader.GetString("mediatype")
                            };

                            // Verify file exists if path is not null
                            if (!string.IsNullOrEmpty(media.CoverPath) &&
                                !File.Exists(media.CoverPath))
                            {
                                media.CoverPath = null;
                            }

                            mediaList.Add(media);
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

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query;
                        Dictionary<string, object> parameters;

                        // Build query based on whether CoverPath is provided
                        if (!string.IsNullOrEmpty(coverPath))
                        {
                            query = "INSERT INTO media (coverpath, mediatype) VALUES (@coverPath, @mediaType)";
                            parameters = new Dictionary<string, object>
                            {
                                ["@coverPath"] = coverPath,
                                ["@mediaType"] = mediaType
                            };
                        }
                        else
                        {
                            query = "INSERT INTO media (mediatype) VALUES (@mediaType)";
                            parameters = new Dictionary<string, object>
                            {
                                ["@mediaType"] = mediaType
                            };
                        }

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Get the inserted ID using SQLite's last_insert_rowid()
                        using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                        var result = await idCmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
                    }
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

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            UPDATE media SET 
                                coverpath = @coverPath,
                                mediatype = @mediaType
                            WHERE mediaid = @mediaID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@mediaID"] = media.MediaID,
                            ["@coverPath"] = coverPath,
                            ["@mediaType"] = media.MediaType ?? "unknown"
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
                    }
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

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "UPDATE media SET coverpath = @coverPath WHERE mediaid = @mediaID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@coverPath"] = coverPath,
                            ["@mediaID"] = mediaID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
                    }
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

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // Check if any albums or tracks reference this media
                        bool isReferenced = await IsMediaReferenced(db, mediaID);

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
                        string query = "DELETE FROM media WHERE mediaid = @mediaID";
                        var parameters = new Dictionary<string, object>
                        {
                            ["@mediaID"] = mediaID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
                    }

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

        private async Task<bool> IsMediaReferenced(DbConnection db, int mediaID)
        {
            // Check Albums table for references
            string checkAlbumsQuery = "SELECT COUNT(*) FROM albums WHERE coverid = @mediaID";
            var parameters1 = new Dictionary<string, object>
            {
                ["@mediaID"] = mediaID
            };

            using var cmd1 = db.CreateCommand(checkAlbumsQuery, parameters1);
            var albumCount = Convert.ToInt32(await cmd1.ExecuteScalarAsync());
            if (albumCount > 0)
            {
                return true;
            }

            // Check Tracks table for references
            string checkTracksQuery = "SELECT COUNT(*) FROM tracks WHERE coverid = @mediaID";
            var parameters2 = new Dictionary<string, object>
            {
                ["@mediaID"] = mediaID
            };

            using var cmd2 = db.CreateCommand(checkTracksQuery, parameters2);
            var trackCount = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
            if (trackCount > 0)
            {
                return true;
            }

            // Check Artists table for references (if photoID exists)
            try
            {
                string checkArtistsQuery = "SELECT COUNT(*) FROM artists WHERE photoid = @mediaID";
                var parameters3 = new Dictionary<string, object>
                {
                    ["@mediaID"] = mediaID
                };

                using var cmd3 = db.CreateCommand(checkArtistsQuery, parameters3);
                var artistCount = Convert.ToInt32(await cmd3.ExecuteScalarAsync());
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
                string checkProfilesQuery = "SELECT COUNT(*) FROM profile WHERE photoid = @mediaID";
                var parameters4 = new Dictionary<string, object>
                {
                    ["@mediaID"] = mediaID
                };

                using var cmd4 = db.CreateCommand(checkProfilesQuery, parameters4);
                var profileCount = Convert.ToInt32(await cmd4.ExecuteScalarAsync());
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

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT mediaid, coverpath, mediatype FROM media WHERE coverpath = @coverPath";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@coverPath"] = coverPath
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return new Media
                            {
                                MediaID = reader.GetInt32("mediaid"),
                                CoverPath = reader.GetString("coverpath"),
                                MediaType = reader.GetString("mediatype")
                            };
                        }
                    }
                    return null;
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
                            using (var db = new DbConnection(_errorHandlingService))
                            {
                                bool isReferenced = await IsMediaReferenced(db, media.MediaID);
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