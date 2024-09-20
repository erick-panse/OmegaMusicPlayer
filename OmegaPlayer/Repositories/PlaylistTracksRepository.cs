using Npgsql;
using OmegaPlayer.Models;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
{
    public class PlaylistTracksRepository
    {
        public async Task<PlaylistTracks> GetPlaylistTrack(int playlistID, int profileID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM PlaylistTracks WHERE playlistID = @playlistID AND profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("playlistID", playlistID);
                        cmd.Parameters.AddWithValue("profileID", profileID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new PlaylistTracks
                                {
                                    PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    TrackOrder = reader.GetInt32(reader.GetOrdinal("trackOrder"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching the PlaylistTrack: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<PlaylistTracks>> GetAllPlaylistTracks()
        {
            var playlistTracks = new List<PlaylistTracks>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM PlaylistTracks";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var playlistTrack = new PlaylistTracks
                                {
                                    PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    TrackOrder = reader.GetInt32(reader.GetOrdinal("trackOrder"))
                                };

                                playlistTracks.Add(playlistTrack);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching all PlaylistTracks: {ex.Message}");
                throw;
            }

            return playlistTracks;
        }

        public async Task AddPlaylistTrack(PlaylistTracks playlistTrack)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "INSERT INTO PlaylistTracks (playlistID, profileID, trackID, trackOrder) VALUES (@playlistID, @profileID, @trackID, @trackOrder)";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("playlistID", playlistTrack.PlaylistID);
                        cmd.Parameters.AddWithValue("profileID", playlistTrack.ProfileID);
                        cmd.Parameters.AddWithValue("trackID", playlistTrack.TrackID);
                        cmd.Parameters.AddWithValue("trackOrder", playlistTrack.TrackOrder);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding the PlaylistTrack: {ex.Message}");
                throw;
            }
        }

        public async Task DeletePlaylistTrack(int playlistID, int profileID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM PlaylistTracks WHERE playlistID = @playlistID AND profileID = @profileID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("playlistID", playlistID);
                        cmd.Parameters.AddWithValue("profileID", profileID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the PlaylistTrack: {ex.Message}");
                throw;
            }
        }
    }
}
