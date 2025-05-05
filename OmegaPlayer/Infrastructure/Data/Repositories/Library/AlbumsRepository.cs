using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class AlbumRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public AlbumRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Albums> GetAlbumById(int albumID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Albums WHERE albumID = @albumID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("albumID", albumID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return MapAlbumFromReader(reader);
                                }
                            }
                        }
                    }
                    return null;
                },
                $"Database operation: Get album with ID {albumID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Albums> GetAlbumByTitle(string title, int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return null;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Albums WHERE title = @title AND artistID = @artistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("title", title);
                            cmd.Parameters.AddWithValue("artistID", artistID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return MapAlbumFromReader(reader);
                                }
                            }
                        }
                    }
                    return null;
                },
                $"Database operation: Get album by title '{title}' for artist ID {artistID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Albums>> GetAllAlbums()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var albums = new List<Albums>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Albums ORDER BY title";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var album = MapAlbumFromReader(reader);
                                    albums.Add(album);
                                }
                            }
                        }
                    }

                    return albums;
                },
                "Database operation: Get all albums",
                new List<Albums>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<List<Albums>> GetAlbumsByArtistId(int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var albums = new List<Albums>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Albums WHERE artistID = @artistID ORDER BY title";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("artistID", artistID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var album = MapAlbumFromReader(reader);
                                    albums.Add(album);
                                }
                            }
                        }
                    }

                    return albums;
                },
                $"Database operation: Get albums for artist ID {artistID}",
                new List<Albums>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> AddAlbum(Albums album)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (album == null)
                    {
                        throw new ArgumentNullException(nameof(album), "Cannot add null album to database");
                    }

                    if (string.IsNullOrWhiteSpace(album.Title))
                    {
                        throw new ArgumentException("Album must have a title", nameof(album));
                    }

                    // Check if album already exists
                    var existingAlbum = await GetAlbumByTitle(album.Title, album.ArtistID);
                    if (existingAlbum != null)
                    {
                        return existingAlbum.AlbumID;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            INSERT INTO Albums (title, artistID, releaseDate, discNumber, trackCounter, coverID, createdAt, updatedAt)
                            VALUES (@title, @artistID, @releaseDate, @discNumber, @trackCounter, @coverID, @createdAt, @updatedAt)
                            RETURNING albumID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            AddAlbumParameters(cmd, album);
                            var albumID = (int)await cmd.ExecuteScalarAsync();
                            return albumID;
                        }
                    }
                },
                $"Database operation: Add album '{album?.Title ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateAlbum(Albums album)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (album == null || album.AlbumID <= 0)
                    {
                        throw new ArgumentException("Cannot update null album or album with invalid ID", nameof(album));
                    }

                    if (string.IsNullOrWhiteSpace(album.Title))
                    {
                        throw new ArgumentException("Album must have a title", nameof(album));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            UPDATE Albums SET 
                                title = @title,
                                artistID = @artistID,
                                releaseDate = @releaseDate,
                                discNumber = @discNumber,
                                trackCounter = @trackCounter,
                                coverID = @coverID,
                                updatedAt = @updatedAt
                            WHERE albumID = @albumID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("albumID", album.AlbumID);
                            AddAlbumParameters(cmd, album);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Update album '{album?.Title ?? "Unknown"}' (ID: {album?.AlbumID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteAlbum(int albumID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (albumID <= 0)
                    {
                        throw new ArgumentException("Cannot delete album with invalid ID", nameof(albumID));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // First update tracks to set albumID to 0 (unknown album)
                        string updateTracksQuery = "UPDATE Tracks SET albumID = 0 WHERE albumID = @albumID";
                        using (var cmd = new NpgsqlCommand(updateTracksQuery, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("albumID", albumID);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Then delete the album
                        string query = "DELETE FROM Albums WHERE albumID = @albumID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("albumID", albumID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Delete album with ID {albumID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private void AddAlbumParameters(NpgsqlCommand cmd, Albums album)
        {
            cmd.Parameters.AddWithValue("title", album.Title);
            cmd.Parameters.AddWithValue("artistID", album.ArtistID);

            // Handle DateTime with default value if not set
            var releaseDate = album.ReleaseDate;
            if (releaseDate == default)
            {
                releaseDate = new DateTime(1900, 1, 1); // Default for unknown release date
            }
            cmd.Parameters.AddWithValue("releaseDate", releaseDate);

            cmd.Parameters.AddWithValue("discNumber", album.DiscNumber);
            cmd.Parameters.AddWithValue("trackCounter", album.TrackCounter);
            cmd.Parameters.AddWithValue("coverID", album.CoverID);

            // Ensure timestamps are set
            if (album.CreatedAt == default)
            {
                album.CreatedAt = DateTime.Now;
            }
            cmd.Parameters.AddWithValue("createdAt", album.CreatedAt);

            album.UpdatedAt = DateTime.Now; // Always update the UpdatedAt timestamp
            cmd.Parameters.AddWithValue("updatedAt", album.UpdatedAt);
        }

        private Albums MapAlbumFromReader(NpgsqlDataReader reader)
        {
            return new Albums
            {
                AlbumID = reader.GetInt32(reader.GetOrdinal("albumID")),
                Title = reader.GetString(reader.GetOrdinal("title")),
                ArtistID = reader.GetInt32(reader.GetOrdinal("artistID")),
                ReleaseDate = reader.GetDateTime(reader.GetOrdinal("releaseDate")),
                DiscNumber = reader.GetInt32(reader.GetOrdinal("discNumber")),
                TrackCounter = reader.GetInt32(reader.GetOrdinal("trackCounter")),
                CoverID = reader.GetInt32(reader.GetOrdinal("coverID")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
            };
        }
    }
}