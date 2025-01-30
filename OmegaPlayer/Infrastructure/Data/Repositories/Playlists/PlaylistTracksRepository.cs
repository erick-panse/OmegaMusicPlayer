using Npgsql;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Playlists.Models;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playlists
{
    public class PlaylistTracksRepository
    {
        public async Task<List<PlaylistTracks>> GetPlaylistTrack(int playlistID)
        {
            var playlistTracks = new List<PlaylistTracks>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM PlaylistTracks WHERE playlistID = @playlistID ORDER BY trackOrder";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("playlistID", playlistID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (await reader.ReadAsync())
                            {
                                playlistTracks.Add(new PlaylistTracks
                                {
                                    PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    TrackOrder = reader.GetInt32(reader.GetOrdinal("trackOrder"))
                                });
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

            return playlistTracks;
        }

        public async Task<List<PlaylistTracks>> GetAllPlaylistTracks()
        {
            var playlistTracks = new List<PlaylistTracks>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM PlaylistTracks ORDER BY playlistID, trackOrder";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var playlistTrack = new PlaylistTracks
                                {
                                    PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
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
                    string query = "INSERT INTO PlaylistTracks (playlistID, trackID, trackOrder) VALUES (@playlistID, @trackID, @trackOrder)";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("playlistID", playlistTrack.PlaylistID);
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

        public async Task DeletePlaylistTrack(int playlistID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM PlaylistTracks WHERE playlistID = @playlistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("playlistID", playlistID);
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
