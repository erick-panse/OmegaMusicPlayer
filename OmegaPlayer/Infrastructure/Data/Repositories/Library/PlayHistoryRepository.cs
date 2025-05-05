using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class PlayHistoryRepository
    {
        private const int MAX_HISTORY_PER_PROFILE = 100;
        private readonly IErrorHandlingService _errorHandlingService;

        public PlayHistoryRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<PlayHistory>> GetRecentlyPlayed(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var history = new List<PlayHistory>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            SELECT * FROM PlayHistory 
                            WHERE ProfileID = @profileId 
                            ORDER BY PlayedAt DESC 
                            LIMIT @maxHistory";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("profileId", profileId);
                            cmd.Parameters.AddWithValue("maxHistory", MAX_HISTORY_PER_PROFILE);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    history.Add(new PlayHistory
                                    {
                                        HistoryID = reader.GetInt32(reader.GetOrdinal("HistoryID")),
                                        ProfileID = reader.GetInt32(reader.GetOrdinal("ProfileID")),
                                        TrackID = reader.GetInt32(reader.GetOrdinal("TrackID")),
                                        PlayedAt = reader.GetDateTime(reader.GetOrdinal("PlayedAt"))
                                    });
                                }
                            }
                        }
                    }

                    return history;
                },
                $"Getting recently played tracks for profile {profileId}",
                new List<PlayHistory>(),
                ErrorSeverity.NonCritical
            );
        }

        public async Task AddToHistory(int profileId, int trackId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // First check and maintain history size limit
                        string cleanupQuery = @"
                            DELETE FROM PlayHistory 
                            WHERE HistoryID IN (
                                SELECT HistoryID 
                                FROM PlayHistory 
                                WHERE ProfileID = @profileId 
                                ORDER BY PlayedAt DESC 
                                OFFSET @maxHistory
                            )";

                        using (var cmd = new NpgsqlCommand(cleanupQuery, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("profileId", profileId);
                            cmd.Parameters.AddWithValue("maxHistory", MAX_HISTORY_PER_PROFILE);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Add new history entry
                        string insertQuery = @"
                            INSERT INTO PlayHistory (ProfileID, TrackID, PlayedAt)
                            VALUES (@profileId, @trackId, @playedAt)";

                        using (var cmd = new NpgsqlCommand(insertQuery, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("profileId", profileId);
                            cmd.Parameters.AddWithValue("trackId", trackId);
                            cmd.Parameters.AddWithValue("playedAt", DateTime.UtcNow);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Adding track {trackId} to play history for profile {profileId}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task ClearHistory(int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "DELETE FROM PlayHistory WHERE ProfileID = @profileId";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("profileId", profileId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Clearing play history for profile {profileId}",
                ErrorSeverity.NonCritical
            );
        }
    }
}