using Npgsql;
using OmegaPlayer.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
{
    public class QueueTracksRepository
    {
        public async Task<List<QueueTracks>> GetTracksByQueueId(int queueID)
        {
            var trackList = new List<QueueTracks>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM QueueTracks WHERE QueueID = @queueID ORDER BY TrackOrder";

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
                                    TrackOrder = reader.GetInt32(reader.GetOrdinal("TrackOrder"))
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching tracks from QueueTracks: {ex.Message}");
                throw;
            }

            return trackList;
        }

        public async Task AddTrackToQueue(List<QueueTracks> tracks)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    foreach (var track in tracks)
                    {
                        string query = @"INSERT INTO QueueTracks (QueueID, TrackID, TrackOrder) 
                                     VALUES (@QueueID, @TrackID, @TrackOrder)";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("QueueID", track.QueueID);
                            cmd.Parameters.AddWithValue("TrackID", track.TrackID);
                            cmd.Parameters.AddWithValue("TrackOrder", track.TrackOrder);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding tracks to QueueTracks: {ex.Message}");
                throw;
            }
        }

        public async Task RemoveTracksByQueueId(int queueID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM QueueTracks WHERE QueueID = @QueueID";

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

        public async Task RemoveAllTracksByQueueId(int queueID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE * FROM QueueTracks WHERE QueueID = @QueueID";

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
