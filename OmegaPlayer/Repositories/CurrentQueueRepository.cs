using Npgsql;
using OmegaPlayer.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
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
                                    ? -1 : reader.GetInt32(reader.GetOrdinal("currentTrackOrder"))
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
                    string query = @"INSERT INTO CurrentQueue (ProfileID, CurrentTrackOrder) 
                                 VALUES (@ProfileID, @CurrentTrackOrder) RETURNING QueueID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("ProfileID", currentQueue.ProfileID);
                        cmd.Parameters.AddWithValue("CurrentTrackOrder", currentQueue.CurrentTrackOrder);

                        return (int)await cmd.ExecuteScalarAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while creating CurrentQueue: {ex.Message}");
                throw;
            }
        }

        public async Task SaveQueue(CurrentQueue queue, List<QueueTracks> tracks)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    using (var transaction = db.dbConn.BeginTransaction())
                    {
                        string insertQueue = @"
                INSERT INTO CurrentQueue (ProfileID, CurrentTrackOrder)
                VALUES (@profileId, @currentTrackOrder)
                ON CONFLICT (QueueID) DO UPDATE
                SET CurrentTrackOrder = @currentTrackOrder
                RETURNING QueueID";

                        using (var cmd = new NpgsqlCommand(insertQueue, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("@profileId", queue.ProfileID);
                            cmd.Parameters.AddWithValue("@currentTrackOrder", queue.CurrentTrackOrder);

                            queue.QueueID = (int)await cmd.ExecuteScalarAsync();
                        }

                        string insertQueueTrack = @"
                INSERT INTO QueueTracks (QueueID, TrackID, TrackOrder)
                VALUES (@queueId, @trackId, @trackOrder)
                ON CONFLICT (QueueID, TrackOrder) DO NOTHING";

                        foreach (var track in tracks)
                        {
                            using (var cmd = new NpgsqlCommand(insertQueueTrack, db.dbConn))
                            {
                                cmd.Parameters.AddWithValue("@queueId", queue.QueueID);
                                cmd.Parameters.AddWithValue("@trackId", track.TrackID);
                                cmd.Parameters.AddWithValue("@trackOrder", track.TrackOrder);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving the queue: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateCurrentQueue(CurrentQueue currentQueue)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"UPDATE CurrentQueue SET CurrentTrackOrder = @CurrentTrackOrder 
                                 WHERE ProfileID = @ProfileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("ProfileID", currentQueue.ProfileID);
                        cmd.Parameters.AddWithValue("CurrentTrackOrder", currentQueue.CurrentTrackOrder);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while updating CurrentQueue: {ex.Message}");
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
