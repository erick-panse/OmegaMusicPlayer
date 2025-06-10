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
    public class ArtistsRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public ArtistsRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Artists> GetArtistById(int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = "SELECT artistid, artistname, photoid, bio, createdat, updatedat FROM artists WHERE artistid = @artistID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@artistID"] = artistID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return MapArtistFromReader(reader);
                        }
                    }
                    return null;
                },
                $"Database operation: Get artist with ID {artistID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Artists> GetArtistByName(string artistName)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(artistName))
                    {
                        return null;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT artistid, artistname, photoid, bio, createdat, updatedat FROM artists WHERE artistname = @artistName";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@artistName"] = artistName
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return MapArtistFromReader(reader);
                        }
                    }
                    return null;
                },
                $"Database operation: Get artist by name '{artistName}'",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Artists>> GetAllArtists()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var artists = new List<Artists>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT artistid, artistname, photoid, bio, createdat, updatedat FROM artists ORDER BY artistname";

                        using var cmd = db.CreateCommand(query);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var artist = MapArtistFromReader(reader);
                            artists.Add(artist);
                        }
                    }

                    return artists;
                },
                "Database operation: Get all artists",
                new List<Artists>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> AddArtist(Artists artist)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null)
                    {
                        throw new ArgumentNullException(nameof(artist), "Cannot add null artist to database");
                    }

                    if (string.IsNullOrWhiteSpace(artist.ArtistName))
                    {
                        throw new ArgumentException("Artist must have a name", nameof(artist));
                    }

                    // Check if artist already exists
                    var existingArtist = await GetArtistByName(artist.ArtistName);
                    if (existingArtist != null)
                    {
                        return existingArtist.ArtistID;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            INSERT INTO artists (artistname, bio, createdat, updatedat, photoid)
                            VALUES (@artistName, @bio, @createdAt, @updatedAt, @photoID)";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@artistName"] = artist.ArtistName,
                            ["@bio"] = artist.Bio,
                            ["@createdAt"] = artist.CreatedAt != default ? artist.CreatedAt : DateTime.Now,
                            ["@updatedAt"] = artist.UpdatedAt != default ? artist.UpdatedAt : DateTime.Now,
                            ["@photoID"] = artist.PhotoID > 0 ? artist.PhotoID : null
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Get the inserted ID using SQLite's last_insert_rowid()
                        using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                        var result = await idCmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
                    }
                },
                $"Database operation: Add artist '{artist?.ArtistName ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateArtist(Artists artist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null || artist.ArtistID <= 0)
                    {
                        throw new ArgumentException("Cannot update null artist or artist with invalid ID", nameof(artist));
                    }

                    if (string.IsNullOrWhiteSpace(artist.ArtistName))
                    {
                        throw new ArgumentException("Artist must have a name", nameof(artist));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            UPDATE artists SET 
                                artistname = @artistName,
                                bio = @bio,
                                photoid = @photoID,
                                updatedat = @updatedAt
                            WHERE artistid = @artistID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@artistID"] = artist.ArtistID,
                            ["@artistName"] = artist.ArtistName,
                            ["@bio"] = artist.Bio,
                            ["@photoID"] = artist.PhotoID > 0 ? artist.PhotoID : null,
                            ["@updatedAt"] = DateTime.Now
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                $"Database operation: Update artist '{artist?.ArtistName ?? "Unknown"}' (ID: {artist?.ArtistID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteArtist(int artistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artistID <= 0)
                    {
                        throw new ArgumentException("Cannot delete artist with invalid ID", nameof(artistID));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        using var transaction = db.dbConn.BeginTransaction();
                        try
                        {
                            // First delete artist associations
                            await DeleteArtistRelationships(db, artistID, transaction);

                            // Then delete the artist
                            string query = "DELETE FROM artists WHERE artistid = @artistID";

                            var parameters = new Dictionary<string, object>
                            {
                                ["@artistID"] = artistID
                            };

                            using var cmd = db.CreateCommand(query, parameters);
                            cmd.Transaction = transaction;
                            await cmd.ExecuteNonQueryAsync();

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                },
                $"Database operation: Delete artist with ID {artistID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task DeleteArtistRelationships(DbConnection db, int artistID, SqliteTransaction transaction)
        {
            // Delete track-artist relationships
            string deleteTrackArtistQuery = "DELETE FROM trackartist WHERE artistid = @artistID";
            var parameters1 = new Dictionary<string, object>
            {
                ["@artistID"] = artistID
            };

            using var cmd1 = db.CreateCommand(deleteTrackArtistQuery, parameters1);
            cmd1.Transaction = transaction;
            await cmd1.ExecuteNonQueryAsync();

            // Update albums to set artistID to null (or 0 if your schema requires it)
            string updateAlbumsQuery = "UPDATE albums SET artistid = NULL WHERE artistid = @artistID";
            var parameters2 = new Dictionary<string, object>
            {
                ["@artistID"] = artistID
            };

            using var cmd2 = db.CreateCommand(updateAlbumsQuery, parameters2);
            cmd2.Transaction = transaction;
            await cmd2.ExecuteNonQueryAsync();
        }

        private Artists MapArtistFromReader(SqliteDataReader reader)
        {
            var artist = new Artists
            {
                ArtistID = reader.GetInt32("artistid"),
                ArtistName = reader.GetString("artistname"),
                Bio = reader.IsDBNull("bio") ? null : reader.GetString("bio"),
                CreatedAt = reader.GetDateTime("createdat"),
                UpdatedAt = reader.GetDateTime("updatedat")
            };

            // PhotoID might be null
            if (!reader.IsDBNull("photoid"))
            {
                artist.PhotoID = reader.GetInt32("photoid");
            }

            return artist;
        }
    }
}