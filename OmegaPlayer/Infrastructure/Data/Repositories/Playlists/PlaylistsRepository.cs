using Npgsql;
using Playlist = OmegaPlayer.Features.Playlists.Models.Playlists;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playlists
{
    public class PlaylistRepository
    {
        public async Task<Playlist> GetPlaylistById(int playlistID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Playlists WHERE playlistID = @playlistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("playlistID", playlistID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Playlist
                                {
                                    PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    Title = reader.GetString(reader.GetOrdinal("title")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching the Playlist by ID: {ex.Message}");
                throw;
            }
            return null;
        }

        public async Task<List<Playlist>> GetAllPlaylists()
        {
            var playlists = new List<Playlist>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Playlists";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var playlist = new Playlist
                                {
                                    PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    Title = reader.GetString(reader.GetOrdinal("title")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
                                };

                                playlists.Add(playlist);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching all Playlists: {ex.Message}");
                throw;
            }
            return playlists;
        }

        public async Task<int> AddPlaylist(Playlist playlist)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO Playlists (profileID, title, createdAt, updatedAt)
                        VALUES (@profileID, @title, @createdAt, @updatedAt) RETURNING playlistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("profileID", playlist.ProfileID);
                        cmd.Parameters.AddWithValue("title", playlist.Title);
                        cmd.Parameters.AddWithValue("createdAt", playlist.CreatedAt);
                        cmd.Parameters.AddWithValue("updatedAt", playlist.UpdatedAt);

                        var playlistID = (int)cmd.ExecuteScalar();
                        return playlistID;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding the Playlist: {ex.Message}");
                throw;
            }
        }

        public async Task UpdatePlaylist(Playlist playlist)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        UPDATE Playlists SET 
                            profileID = @profileID,
                            title = @title,
                            updatedAt = @updatedAt
                        WHERE playlistID = @playlistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("playlistID", playlist.PlaylistID);
                        cmd.Parameters.AddWithValue("profileID", playlist.ProfileID);
                        cmd.Parameters.AddWithValue("title", playlist.Title);
                        cmd.Parameters.AddWithValue("createdAt", playlist.CreatedAt);
                        cmd.Parameters.AddWithValue("updatedAt", playlist.UpdatedAt);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while updating the Playlist: {ex.Message}");
                throw;
            }
        }

        public async Task DeletePlaylist(int playlistID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Playlists WHERE playlistID = @playlistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("playlistID", playlistID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the Playlist: {ex.Message}");
                throw;
            }
        }
    }
}
