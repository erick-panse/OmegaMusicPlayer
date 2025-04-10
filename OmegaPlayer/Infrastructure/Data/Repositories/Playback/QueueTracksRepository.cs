using Npgsql;
using OmegaPlayer.Features.Playback.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playback
{
    public class QueueTracksRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public QueueTracksRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<QueueTracks>> GetTracksByQueueId(int queueID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (queueID <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid queue ID",
                            $"Attempted to get tracks with invalid queue ID: {queueID}",
                            null,
                            false);
                        return new List<QueueTracks>();
                    }

                    var trackList = new List<QueueTracks>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                        SELECT * FROM QueueTracks 
                        WHERE QueueID = @queueID 
                        ORDER BY TrackOrder";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("QueueID", queueID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (reader.Read())
                                {
                                    trackList.Add(new QueueTracks
                                    {
                                        QueueID = reader.GetInt32(reader.GetOrdinal("QueueID")),
                                        TrackID = reader.GetInt32(reader.GetOrdinal("TrackID")),
                                        TrackOrder = reader.GetInt32(reader.GetOrdinal("TrackOrder")),
                                        OriginalOrder = reader.GetInt32(reader.GetOrdinal("OriginalOrder"))
                                    });
                                }
                            }
                        }
                    }

                    return trackList;
                },
                $"Getting tracks for queue {queueID}",
                new List<QueueTracks>(),
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task AddTrackToQueue(List<QueueTracks> tracks)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (tracks == null || !tracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Empty tracks list provided",
                            "Attempted to add no tracks to queue",
                            null,
                            false);
                        return;
                    }

                    if (tracks.Any(t => t.QueueID <= 0))
                    {
                        throw new ArgumentException("One or more tracks has an invalid queue ID", nameof(tracks));
                    }

                    if (tracks.Any(t => t.TrackID <= 0))
                    {
                        throw new ArgumentException("One or more tracks has an invalid track ID", nameof(tracks));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        foreach (var track in tracks)
                        {
                            string query = @"
                            INSERT INTO QueueTracks (QueueID, TrackID, TrackOrder, OriginalOrder) 
                            VALUES (@QueueID, @TrackID, @TrackOrder, @OriginalOrder)";

                            using (var cmd = new NpgsqlCommand(query, db.dbConn))
                            {
                                cmd.Parameters.AddWithValue("QueueID", track.QueueID);
                                cmd.Parameters.AddWithValue("TrackID", track.TrackID);
                                cmd.Parameters.AddWithValue("TrackOrder", track.TrackOrder);
                                cmd.Parameters.AddWithValue("OriginalOrder", track.OriginalOrder);

                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        // Update LastModified in CurrentQueue
                        string updateQuery = @"
                        UPDATE CurrentQueue 
                        SET LastModified = CURRENT_TIMESTAMP 
                        WHERE QueueID = @QueueID";

                        using (var cmd = new NpgsqlCommand(updateQuery, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("QueueID", tracks.First().QueueID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Adding {tracks?.Count ?? 0} tracks to queue {tracks?.FirstOrDefault()?.QueueID ?? 0}",
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task RemoveTracksByQueueId(int queueID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (queueID <= 0)
                    {
                        throw new ArgumentException("Invalid queue ID", nameof(queueID));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "DELETE FROM QueueTracks WHERE QueueID = @QueueID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("QueueID", queueID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Removing all tracks from queue {queueID}",
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task RemoveAllTracksByQueueId(int queueID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (queueID <= 0)
                    {
                        throw new ArgumentException("Invalid queue ID", nameof(queueID));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "DELETE FROM QueueTracks WHERE QueueID = @QueueID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("QueueID", queueID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Removing all tracks from queue {queueID} (alternative method)",
                ErrorSeverity.Playback,
                false
            );
        }
    }
}