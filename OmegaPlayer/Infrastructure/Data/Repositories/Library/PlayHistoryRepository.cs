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
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = @"
                            SELECT historyid, profileid, trackid, playedat 
                            FROM playhistory 
                            WHERE profileid = @profileId 
                            ORDER BY playedat DESC 
                            LIMIT @maxHistory";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileId"] = profileId,
                            ["@maxHistory"] = MAX_HISTORY_PER_PROFILE
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            history.Add(new PlayHistory
                            {
                                HistoryID = reader.GetInt32("historyid"),
                                ProfileID = reader.GetInt32("profileid"),
                                TrackID = reader.GetInt32("trackid"),
                                PlayedAt = reader.GetDateTime("playedat")
                            });
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
                        using var transaction = db.dbConn.BeginTransaction();
                        try
                        {
                            // First check and maintain history size limit
                            // SQLite doesn't support complex OFFSET in DELETE, so we'll use a different approach
                            string cleanupQuery = @"
                                DELETE FROM playhistory 
                                WHERE profileid = @profileId 
                                AND historyid NOT IN (
                                    SELECT historyid 
                                    FROM playhistory 
                                    WHERE profileid = @profileId 
                                    ORDER BY playedat DESC 
                                    LIMIT @maxHistory
                                )";

                            var cleanupParameters = new Dictionary<string, object>
                            {
                                ["@profileId"] = profileId,
                                ["@maxHistory"] = MAX_HISTORY_PER_PROFILE - 1 // Leave room for the new entry
                            };

                            using var cleanupCmd = db.CreateCommand(cleanupQuery, cleanupParameters);
                            cleanupCmd.Transaction = transaction;
                            await cleanupCmd.ExecuteNonQueryAsync();

                            // Add new history entry
                            string insertQuery = @"
                                INSERT INTO playhistory (profileid, trackid, playedat)
                                VALUES (@profileId, @trackId, @playedAt)";

                            var insertParameters = new Dictionary<string, object>
                            {
                                ["@profileId"] = profileId,
                                ["@trackId"] = trackId,
                                ["@playedAt"] = DateTime.UtcNow
                            };

                            using var insertCmd = db.CreateCommand(insertQuery, insertParameters);
                            insertCmd.Transaction = transaction;
                            await insertCmd.ExecuteNonQueryAsync();

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw;
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
                        string query = "DELETE FROM playhistory WHERE profileid = @profileId";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileId"] = profileId
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                $"Clearing play history for profile {profileId}",
                ErrorSeverity.NonCritical
            );
        }
    }
}