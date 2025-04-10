using Npgsql;
using OmegaPlayer.Features.Playback.Models;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playback
{
    public class CurrentQueueRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public CurrentQueueRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<CurrentQueue> GetCurrentQueueByProfileId(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (profileId <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid profile ID",
                            $"Attempted to get queue with invalid profile ID: {profileId}",
                            null,
                            false);
                        return null;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                        SELECT * FROM CurrentQueue
                        WHERE ProfileID = @profileId";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("@profileId", profileId);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return new CurrentQueue
                                    {
                                        QueueID = reader.GetInt32(reader.GetOrdinal("QueueID")),
                                        ProfileID = reader.GetInt32(reader.GetOrdinal("ProfileID")),
                                        CurrentTrackOrder = reader.IsDBNull(reader.GetOrdinal("currentTrackOrder"))
                                            ? -1 : reader.GetInt32(reader.GetOrdinal("currentTrackOrder")),
                                        IsShuffled = reader.GetBoolean(reader.GetOrdinal("IsShuffled")),
                                        RepeatMode = reader.GetString(reader.GetOrdinal("RepeatMode")),
                                        LastModified = reader.GetDateTime(reader.GetOrdinal("LastModified"))
                                    };
                                }
                            }
                        }
                    }

                    return null;
                },
                $"Getting current queue for profile {profileId}",
                null,
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task<int> CreateCurrentQueue(CurrentQueue currentQueue)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (currentQueue == null)
                    {
                        throw new ArgumentNullException(nameof(currentQueue), "Cannot create a null queue");
                    }

                    if (currentQueue.ProfileID <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(currentQueue));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                        INSERT INTO CurrentQueue 
                        (ProfileID, CurrentTrackOrder, IsShuffled, RepeatMode) 
                        VALUES (@ProfileID, @CurrentTrackOrder, @IsShuffled, @RepeatMode) 
                        RETURNING QueueID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("ProfileID", currentQueue.ProfileID);
                            cmd.Parameters.AddWithValue("CurrentTrackOrder", currentQueue.CurrentTrackOrder);
                            cmd.Parameters.AddWithValue("IsShuffled", currentQueue.IsShuffled);
                            cmd.Parameters.AddWithValue("RepeatMode", currentQueue.RepeatMode);

                            return (int)await cmd.ExecuteScalarAsync();
                        }
                    }
                },
                $"Creating new queue for profile {currentQueue?.ProfileID ?? 0}",
                -1,
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task UpdateCurrentTrackOrder(CurrentQueue currentQueue)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (currentQueue == null)
                    {
                        throw new ArgumentNullException(nameof(currentQueue), "Cannot update a null queue");
                    }

                    if (currentQueue.QueueID <= 0)
                    {
                        throw new ArgumentException("Invalid queue ID", nameof(currentQueue));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                        UPDATE CurrentQueue SET 
                        CurrentTrackOrder = @CurrentTrackOrder,
                        IsShuffled = @IsShuffled,
                        RepeatMode = @RepeatMode,
                        LastModified = CURRENT_TIMESTAMP
                        WHERE QueueID = @QueueID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("QueueID", currentQueue.QueueID);
                            cmd.Parameters.AddWithValue("CurrentTrackOrder", currentQueue.CurrentTrackOrder);
                            cmd.Parameters.AddWithValue("IsShuffled", currentQueue.IsShuffled);
                            cmd.Parameters.AddWithValue("RepeatMode", currentQueue.RepeatMode);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Updating queue {currentQueue?.QueueID ?? 0} with track order {currentQueue?.CurrentTrackOrder ?? 0}",
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task DeleteQueueById(int queueID)
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
                        string query = "DELETE FROM CurrentQueue WHERE QueueID = @QueueID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("QueueID", queueID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Deleting queue with ID {queueID}",
                ErrorSeverity.Playback,
                false
            );
        }
    }
}