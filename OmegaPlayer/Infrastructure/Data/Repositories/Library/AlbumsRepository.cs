using Microsoft.Data.Sqlite;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

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
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = @"SELECT albumid, title, artistid, releasedate, discnumber, trackcounter, 
                                       coverid, createdat, updatedat FROM albums WHERE albumid = @albumID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@albumID"] = albumID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return MapAlbumFromReader(reader);
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
                        string query = @"SELECT albumid, title, artistid, releasedate, discnumber, trackcounter, 
                                       coverid, createdat, updatedat FROM albums 
                                       WHERE title = @title AND artistid = @artistID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@title"] = title,
                            ["@artistID"] = artistID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return MapAlbumFromReader(reader);
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
                        string query = @"SELECT albumid, title, artistid, releasedate, discnumber, trackcounter, 
                                       coverid, createdat, updatedat FROM albums ORDER BY title";

                        using var cmd = db.CreateCommand(query);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var album = MapAlbumFromReader(reader);
                            albums.Add(album);
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
                        string query = @"SELECT albumid, title, artistid, releasedate, discnumber, trackcounter, 
                                       coverid, createdat, updatedat FROM albums 
                                       WHERE artistid = @artistID ORDER BY title";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@artistID"] = artistID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var album = MapAlbumFromReader(reader);
                            albums.Add(album);
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
                            INSERT INTO albums (title, artistid, releasedate, discnumber, trackcounter, coverid, createdat, updatedat)
                            VALUES (@title, @artistID, @releaseDate, @discNumber, @trackCounter, @coverID, @createdAt, @updatedAt)";

                        var parameters = BuildAlbumParameters(album);

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Get the inserted ID using SQLite's last_insert_rowid()
                        using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                        var result = await idCmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
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
                            UPDATE albums SET 
                                title = @title,
                                artistid = @artistID,
                                releasedate = @releaseDate,
                                discnumber = @discNumber,
                                trackcounter = @trackCounter,
                                coverid = @coverID,
                                updatedat = @updatedAt
                            WHERE albumid = @albumID";

                        var parameters = BuildAlbumParameters(album);
                        parameters["@albumID"] = album.AlbumID;

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
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
                        using var transaction = db.dbConn.BeginTransaction();
                        try
                        {
                            // First update tracks to set albumID to null (or 0 if schema requires it)
                            string updateTracksQuery = "UPDATE tracks SET albumid = NULL WHERE albumid = @albumID";
                            var parameters1 = new Dictionary<string, object>
                            {
                                ["@albumID"] = albumID
                            };

                            using var cmd1 = db.CreateCommand(updateTracksQuery, parameters1);
                            cmd1.Transaction = transaction;
                            await cmd1.ExecuteNonQueryAsync();

                            // Then delete the album
                            string deleteQuery = "DELETE FROM albums WHERE albumid = @albumID";

                            var parameters2 = new Dictionary<string, object>
                            {
                                ["@albumID"] = albumID
                            };

                            using var cmd2 = db.CreateCommand(deleteQuery, parameters2);
                            cmd2.Transaction = transaction;
                            await cmd2.ExecuteNonQueryAsync();

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                },
                $"Database operation: Delete album with ID {albumID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private Dictionary<string, object> BuildAlbumParameters(Albums album)
        {
            // Handle DateTime with default value if not set
            var releaseDate = album.ReleaseDate;
            if (releaseDate == default)
            {
                releaseDate = new DateTime(1900, 1, 1); // Default for unknown release date
            }

            // Ensure timestamps are set
            var createdAt = album.CreatedAt;
            if (createdAt == default)
            {
                createdAt = DateTime.Now;
            }

            var updatedAt = DateTime.Now; // Always update the UpdatedAt timestamp

            return new Dictionary<string, object>
            {
                ["@title"] = album.Title,
                ["@artistID"] = album.ArtistID > 0 ? album.ArtistID : null,
                ["@releaseDate"] = releaseDate,
                ["@discNumber"] = album.DiscNumber,
                ["@trackCounter"] = album.TrackCounter,
                ["@coverID"] = album.CoverID > 0 ? album.CoverID : null,
                ["@createdAt"] = createdAt,
                ["@updatedAt"] = updatedAt
            };
        }

        private Albums MapAlbumFromReader(SqliteDataReader reader)
        {
            return new Albums
            {
                AlbumID = reader.GetInt32("albumid"),
                Title = reader.GetString("title"),
                ArtistID = reader.IsDBNull("artistid") ? 0 : reader.GetInt32("artistid"),
                ReleaseDate = reader.GetDateTime("releasedate"),
                DiscNumber = reader.GetInt32("discnumber"),
                TrackCounter = reader.GetInt32("trackcounter"),
                CoverID = reader.IsDBNull("coverid") ? 0 : reader.GetInt32("coverid"),
                CreatedAt = reader.GetDateTime("createdat"),
                UpdatedAt = reader.GetDateTime("updatedat")
            };
        }
    }
}