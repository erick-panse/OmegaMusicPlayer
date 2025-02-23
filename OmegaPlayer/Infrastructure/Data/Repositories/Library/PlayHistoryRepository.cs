using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class PlayHistoryRepository
    {
        private const int MAX_HISTORY_PER_PROFILE = 100;

        public async Task<List<PlayHistory>> GetRecentlyPlayed(int profileId)
        {
            var history = new List<PlayHistory>();

            try
            {
                using (var db = new DbConnection())
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching play history: {ex.Message}");
                throw;
            }

            return history;
        }

        public async Task AddToHistory(int profileId, int trackId)
        {
            try
            {
                using (var db = new DbConnection())
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding to play history: {ex.Message}");
                throw;
            }
        }

        public async Task ClearHistory(int profileId)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM PlayHistory WHERE ProfileID = @profileId";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("profileId", profileId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing play history: {ex.Message}");
                throw;
            }
        }
    }
}