using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

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
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = "SELECT 1 FROM likes WHERE trackid = @trackID AND profileid = @profileID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@trackID"] = trackId,
                            ["@profileID"] = profileId
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();
                        return reader.HasRows;
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
                        string query = "SELECT playcount FROM playcounts WHERE trackid = @trackID AND profileid = @profileID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@trackID"] = trackId,
                            ["@profileID"] = profileId
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        var result = await cmd.ExecuteScalarAsync();
                        return result != null ? Convert.ToInt32(result) : 0;
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
                        if (isLiked)
                        {
                            // SQLite uses INSERT OR IGNORE instead of ON CONFLICT DO NOTHING
                            string insertQuery = "INSERT OR IGNORE INTO likes (trackid, profileid, likedat) VALUES (@trackID, @profileID, @likedAt)";

                            var insertParameters = new Dictionary<string, object>
                            {
                                ["@trackID"] = trackId,
                                ["@profileID"] = profileId,
                                ["@likedAt"] = DateTime.UtcNow
                            };

                            using var cmd = db.CreateCommand(insertQuery, insertParameters);
                            await cmd.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            string deleteQuery = "DELETE FROM likes WHERE trackid = @trackID AND profileid = @profileID";

                            var deleteParameters = new Dictionary<string, object>
                            {
                                ["@trackID"] = trackId,
                                ["@profileID"] = profileId
                            };

                            using var cmd = db.CreateCommand(deleteQuery, deleteParameters);
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
                        // SQLite uses INSERT OR REPLACE instead of ON CONFLICT ... DO UPDATE
                        string query = @"
                            INSERT OR REPLACE INTO playcounts (trackid, profileid, playcount, lastplayed)
                            VALUES (@trackID, @profileID, @playCount, @lastPlayed)";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@trackID"] = trackId,
                            ["@playCount"] = playCount,
                            ["@profileID"] = profileId,
                            ["@lastPlayed"] = DateTime.UtcNow
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
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
                            SELECT trackid, playcount 
                            FROM playcounts 
                            WHERE profileid = @profileID 
                            ORDER BY playcount DESC 
                            LIMIT @limit";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileID"] = profileId,
                            ["@limit"] = limit
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            results.Add((
                                reader.GetInt32("trackid"),
                                reader.GetInt32("playcounts")
                            ));
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
                        string query = "SELECT trackid FROM likes WHERE profileid = @profileID ORDER BY likedat DESC";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileID"] = profileId
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            results.Add(reader.GetInt32("trackid"));
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