using Npgsql;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

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
                        string query = "SELECT * FROM Artists WHERE artistID = @artistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("artistID", artistID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return MapArtistFromReader(reader);
                                }
                            }
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
                        string query = "SELECT * FROM Artists WHERE artistName = @artistName";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("artistName", artistName);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return MapArtistFromReader(reader);
                                }
                            }
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
                        string query = "SELECT * FROM Artists ORDER BY artistName";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var artist = MapArtistFromReader(reader);
                                    artists.Add(artist);
                                }
                            }
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
                            INSERT INTO Artists (artistName, bio, createdAt, updatedAt)
                            VALUES (@artistName, @bio, @createdAt, @updatedAt)
                            RETURNING artistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("artistName", artist.ArtistName);
                            cmd.Parameters.AddWithValue("bio", artist.Bio ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("createdAt", artist.CreatedAt);
                            cmd.Parameters.AddWithValue("updatedAt", artist.UpdatedAt);

                            var artistID = (int)await cmd.ExecuteScalarAsync();
                            return artistID;
                        }
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
                            UPDATE Artists SET 
                                artistName = @artistName,
                                bio = @bio,
                                photoID = @photoID,
                                updatedAt = @updatedAt
                            WHERE artistID = @artistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("artistID", artist.ArtistID);
                            cmd.Parameters.AddWithValue("artistName", artist.ArtistName);
                            cmd.Parameters.AddWithValue("bio", artist.Bio ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("photoID", artist.PhotoID);
                            cmd.Parameters.AddWithValue("updatedAt", DateTime.Now);

                            await cmd.ExecuteNonQueryAsync();
                        }
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
                        // First delete artist associations
                        await DeleteArtistRelationships(db, artistID);

                        // Then delete the artist
                        string query = "DELETE FROM Artists WHERE artistID = @artistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("artistID", artistID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Delete artist with ID {artistID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task DeleteArtistRelationships(DbConnection db, int artistID)
        {
            // Delete track-artist relationships
            string deleteTrackArtistQuery = "DELETE FROM TrackArtist WHERE artistID = @artistID";
            using (var cmd = new NpgsqlCommand(deleteTrackArtistQuery, db.dbConn))
            {
                cmd.Parameters.AddWithValue("artistID", artistID);
                await cmd.ExecuteNonQueryAsync();
            }

            // Update albums to set artistID to unknown (0)
            string updateAlbumsQuery = "UPDATE Albums SET artistID = 0 WHERE artistID = @artistID";
            using (var cmd = new NpgsqlCommand(updateAlbumsQuery, db.dbConn))
            {
                cmd.Parameters.AddWithValue("artistID", artistID);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private Artists MapArtistFromReader(NpgsqlDataReader reader)
        {
            var artist = new Artists
            {
                ArtistID = reader.GetInt32(reader.GetOrdinal("artistID")),
                ArtistName = reader.GetString(reader.GetOrdinal("artistName")),
                Bio = reader.IsDBNull(reader.GetOrdinal("bio")) ? null : reader.GetString(reader.GetOrdinal("bio")),
                CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
            };

            // PhotoID might be null
            if (!reader.IsDBNull(reader.GetOrdinal("photoID")))
            {
                artist.PhotoID = reader.GetInt32(reader.GetOrdinal("photoID"));
            }

            return artist;
        }
    }

    public static class NpgsqlDataReaderExtensions
    {
        public static bool HasColumn(this NpgsqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}