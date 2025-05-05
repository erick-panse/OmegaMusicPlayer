using Npgsql;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.IO;

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
                        string query = "SELECT * FROM Media WHERE mediaID = @mediaID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("mediaID", mediaID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    var media = new Media
                                    {
                                        MediaID = reader.GetInt32(reader.GetOrdinal("mediaID")),
                                        CoverPath = reader.IsDBNull(reader.GetOrdinal("coverPath")) ?
                                            null : reader.GetString(reader.GetOrdinal("coverPath")),
                                        MediaType = reader.GetString(reader.GetOrdinal("mediaType"))
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
                        string query = "SELECT * FROM Media";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var media = new Media
                                    {
                                        MediaID = reader.GetInt32(reader.GetOrdinal("mediaID")),
                                        CoverPath = reader.IsDBNull(reader.GetOrdinal("coverPath")) ?
                                            null : reader.GetString(reader.GetOrdinal("coverPath")),
                                        MediaType = reader.GetString(reader.GetOrdinal("mediaType"))
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

                        // If CoverPath is provided, include it in the insert
                        if (!string.IsNullOrEmpty(coverPath))
                        {
                            query = @"
                                INSERT INTO Media (coverPath, mediaType)
                                VALUES (@coverPath, @mediaType)
                                RETURNING mediaID";

                            using (var cmd = new NpgsqlCommand(query, db.dbConn))
                            {
                                cmd.Parameters.AddWithValue("coverPath", coverPath);
                                cmd.Parameters.AddWithValue("mediaType", mediaType);
                                return (int)await cmd.ExecuteScalarAsync();
                            }
                        }
                        else
                        {
                            query = @"
                                INSERT INTO Media (mediaType)
                                VALUES (@mediaType)
                                RETURNING mediaID";

                            using (var cmd = new NpgsqlCommand(query, db.dbConn))
                            {
                                cmd.Parameters.AddWithValue("mediaType", mediaType);
                                return (int)await cmd.ExecuteScalarAsync();
                            }
                        }
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
                            UPDATE Media SET 
                                coverPath = @coverPath,
                                mediaType = @mediaType
                            WHERE mediaID = @mediaID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("mediaID", media.MediaID);
                            cmd.Parameters.AddWithValue("coverPath",
                                (object)coverPath ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("mediaType",
                                media.MediaType ?? "unknown");

                            await cmd.ExecuteNonQueryAsync();
                        }
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
                        string query = @"
                            UPDATE Media
                            SET coverPath = @coverPath
                            WHERE mediaID = @mediaID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("coverPath",
                                (object)coverPath ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("mediaID", mediaID);
                            await cmd.ExecuteNonQueryAsync();
                        }
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
                        string query = "DELETE FROM Media WHERE mediaID = @mediaID";
                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("mediaID", mediaID);
                            await cmd.ExecuteNonQueryAsync();
                        }
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
            string checkAlbumsQuery = "SELECT COUNT(*) FROM Albums WHERE coverID = @mediaID";
            using (var cmd = new NpgsqlCommand(checkAlbumsQuery, db.dbConn))
            {
                cmd.Parameters.AddWithValue("mediaID", mediaID);
                var albumCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (albumCount > 0)
                {
                    return true;
                }
            }

            // Check Tracks table for references
            string checkTracksQuery = "SELECT COUNT(*) FROM Tracks WHERE coverID = @mediaID";
            using (var cmd = new NpgsqlCommand(checkTracksQuery, db.dbConn))
            {
                cmd.Parameters.AddWithValue("mediaID", mediaID);
                var trackCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                if (trackCount > 0)
                {
                    return true;
                }
            }

            // Check Artists table for references (if photoID exists)
            try
            {
                string checkArtistsQuery = "SELECT COUNT(*) FROM Artists WHERE photoID = @mediaID";
                using (var cmd = new NpgsqlCommand(checkArtistsQuery, db.dbConn))
                {
                    cmd.Parameters.AddWithValue("mediaID", mediaID);
                    var artistCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    if (artistCount > 0)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                // If the column doesn't exist, just ignore this check
            }

            return false;
        }
    }
}