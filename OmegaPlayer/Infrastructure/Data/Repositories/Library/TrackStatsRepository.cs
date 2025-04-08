using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TrackStatsRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackStatsRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<bool> IsTrackLiked(int trackId, int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
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
                },
                $"Checking if track {trackId} is liked by profile {profileId}",
                false,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<int> GetPlayCount(int trackId, int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
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
                },
                $"Getting play count for track {trackId}, profile {profileId}",
                0, // Default to 0 plays if there's an error
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task UpdateTrackLike(int trackId, int profileId, bool isLiked)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
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
                },
                $"{(isLiked ? "Liking" : "Unliking")} track {trackId} for profile {profileId}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task IncrementPlayCount(int trackId, int playCount, int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
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
                },
                $"Updating play count to {playCount} for track {trackId}, profile {profileId}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<(int TrackId, int PlayCount)>> GetMostPlayedTracks(int profileId, int limit = 10)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var results = new List<(int TrackId, int PlayCount)>();

                    using (var db = new DbConnection(_errorHandlingService))
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

                    return results;
                },
                $"Getting most played tracks for profile {profileId}",
                new List<(int, int)>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<int>> GetLikedTracks(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var results = new List<int>();

                    using (var db = new DbConnection(_errorHandlingService))
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

                    return results;
                },
                $"Getting liked tracks for profile {profileId}",
                new List<int>(),
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}