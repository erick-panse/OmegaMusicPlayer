using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TrackStatsRepository
    {
        public async Task<bool> IsTrackLiked(int trackId, int profileId)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT 1 FROM Likes WHERE trackID = @trackID AND profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackId);
                        cmd.Parameters.AddWithValue("profileID", profileId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            return reader.HasRows;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking if track is liked: {ex.Message}");
                throw;
            }
        }

        public async Task<int> GetPlayCount(int trackId, int profileId)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT playCount FROM PlayCounts WHERE trackID = @trackID AND profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackId);
                        cmd.Parameters.AddWithValue("profileID", profileId);

                        var result = await cmd.ExecuteScalarAsync();
                        return result != null ? Convert.ToInt32(result) : 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting play count: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateTrackLike(int trackId, int profileId, bool isLiked)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = isLiked
                        ? "INSERT INTO Likes (trackID, profileID) VALUES (@trackID, @profileID) ON CONFLICT DO NOTHING"
                        : "DELETE FROM Likes WHERE trackID = @trackID AND profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackId);
                        cmd.Parameters.AddWithValue("profileID", profileId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating track like status: {ex.Message}");
                throw;
            }
        }

        public async Task IncrementPlayCount(int trackId, int playCount, int profileId)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO PlayCounts (trackID, profileID, playCount, lastPlayed)
                        VALUES (@trackID, @profileID, @playCount, CURRENT_TIMESTAMP)
                        ON CONFLICT (trackID, profileID) 
                        DO UPDATE SET 
                            playCount = @playCount,
                            lastPlayed = CURRENT_TIMESTAMP";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackId);
                        cmd.Parameters.AddWithValue("playCount", playCount);
                        cmd.Parameters.AddWithValue("profileID", profileId);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error incrementing play count: {ex.Message}");
                throw;
            }
        }

        public async Task<List<(int TrackId, int PlayCount)>> GetMostPlayedTracks(int profileId, int limit = 10)
        {
            var results = new List<(int TrackId, int PlayCount)>();
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        SELECT trackID, playCount 
                        FROM PlayCounts 
                        WHERE profileID = @profileID 
                        ORDER BY playCount DESC 
                        LIMIT @limit";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("profileID", profileId);
                        cmd.Parameters.AddWithValue("limit", limit);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add((
                                    reader.GetInt32(reader.GetOrdinal("trackID")),
                                    reader.GetInt32(reader.GetOrdinal("playCount"))
                                ));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting most played tracks: {ex.Message}");
                throw;
            }
            return results;
        }

        public async Task<List<int>> GetLikedTracks(int profileId)
        {
            var results = new List<int>();
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT trackID FROM Likes WHERE profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("profileID", profileId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add(reader.GetInt32(0));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting liked tracks: {ex.Message}");
                throw;
            }
            return results;
        }
    }
}