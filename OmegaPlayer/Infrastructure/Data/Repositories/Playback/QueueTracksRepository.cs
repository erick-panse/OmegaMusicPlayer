using Microsoft.Data.Sqlite;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Playback.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

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
                        // Use lowercase table and column names for Entity Framework compatibility
                        string query = @"
                        SELECT queueid, trackid, trackorder, originalorder FROM queuetracks 
                        WHERE queueid = @queueID 
                        ORDER BY trackorder";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@queueID"] = queueID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            trackList.Add(new QueueTracks
                            {
                                QueueID = reader.GetInt32("queueid"),
                                TrackID = reader.GetInt32("trackid"),
                                TrackOrder = reader.GetInt32("trackorder"),
                                OriginalOrder = reader.GetInt32("originalorder")
                            });
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
                        // Use synchronous transaction for proper SQLite typing
                        using var transaction = db.dbConn.BeginTransaction();
                        try
                        {
                            // First, remove all existing tracks within the transaction
                            string deleteQuery = "DELETE FROM queuetracks WHERE queueid = @QueueID";
                            var deleteParameters = new Dictionary<string, object>
                            {
                                ["@QueueID"] = tracks.First().QueueID
                            };

                            using var deleteCmd = db.CreateCommand(deleteQuery, deleteParameters);
                            deleteCmd.Transaction = transaction;
                            await deleteCmd.ExecuteNonQueryAsync();

                            // Then insert all new tracks in batch
                            foreach (var track in tracks)
                            {
                                string insertQuery = @"
                                INSERT INTO queuetracks (queueid, trackid, trackorder, originalorder) 
                                VALUES (@QueueID, @TrackID, @TrackOrder, @OriginalOrder)";

                                var insertParameters = new Dictionary<string, object>
                                {
                                    ["@QueueID"] = track.QueueID,
                                    ["@TrackID"] = track.TrackID,
                                    ["@TrackOrder"] = track.TrackOrder,
                                    ["@OriginalOrder"] = track.OriginalOrder
                                };

                                using var insertCmd = db.CreateCommand(insertQuery, insertParameters);
                                insertCmd.Transaction = transaction;
                                await insertCmd.ExecuteNonQueryAsync();
                            }

                            // Update LastModified in CurrentQueue
                            string updateQuery = @"
                            UPDATE currentqueue 
                            SET lastmodified = datetime('now') 
                            WHERE queueid = @QueueID";

                            var updateParameters = new Dictionary<string, object>
                            {
                                ["@QueueID"] = tracks.First().QueueID
                            };

                            using var updateCmd = db.CreateCommand(updateQuery, updateParameters);
                            updateCmd.Transaction = transaction;
                            await updateCmd.ExecuteNonQueryAsync();

                            // Commit the transaction if everything succeeded
                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            // Roll back the transaction if anything fails
                            transaction.Rollback();
                            _errorHandlingService.LogError(
                                ErrorSeverity.Playback,
                                "Failed to update queue tracks",
                                $"Error occurred while updating tracks for queue {tracks.First().QueueID}",
                                ex,
                                false);
                            throw; // Rethrow to be handled by the SafeExecuteAsync wrapper
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
                        string query = "DELETE FROM queuetracks WHERE queueid = @QueueID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@QueueID"] = queueID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                $"Removing all tracks from queue {queueID}",
                ErrorSeverity.Playback,
                false
            );
        }
    }
}