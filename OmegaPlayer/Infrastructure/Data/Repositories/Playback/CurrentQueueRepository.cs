using Npgsql;
using OmegaPlayer.Features.Playback.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playback
{
    public class CurrentQueueRepository
    {
        public async Task<CurrentQueue> GetCurrentQueueByProfileId(int profileId)
        {
            try
            {
                using (var db = new DbConnection())
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching the queue: {ex.Message}");
                throw;
            }
            return null;
        }

        public async Task<int> CreateCurrentQueue(CurrentQueue currentQueue)
        {
            try
            {
                using (var db = new DbConnection())
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating CurrentQueue: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateCurrentTrackOrder(CurrentQueue currentQueue)
        {
            try
            {
                using (var db = new DbConnection())
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating CurrentTrackOrder: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteQueueById(int queueID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE * FROM CurrentQueue WHERE QueueID = @QueueID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("QueueID", queueID);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while removing tracks from QueueTracks: {ex.Message}");
                throw;
            }
        }
    }
}
