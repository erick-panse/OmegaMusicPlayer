using Npgsql;
using OmegaPlayer.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
{
    public class ArtistsRepository
    {
        public async Task<Artists> GetArtistById(int artistID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Artists WHERE artistID = @artistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("artistID", artistID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Artists
                                {
                                    ArtistID = reader.GetInt32(reader.GetOrdinal("artistID")),
                                    ArtistName = reader.GetString(reader.GetOrdinal("artistName")),
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
                // Log exception
                Console.WriteLine($"An error occurred while fetching the artist by ID: {ex.Message}");
                throw;
            }
            return null;
        }

        public async Task<List<Artists>> GetArtistByName(string artistName)
        {
            var artists = new List<Artists>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Artists WHERE artistName = @artistName";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("artistName", artistName);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var artist = new Artists
                                {
                                    ArtistID = reader.GetInt32(reader.GetOrdinal("artistID")),
                                    ArtistName = reader.GetString(reader.GetOrdinal("artistName")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
                                };
                                artists.Add(artist);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching the artist by Name: {ex.Message}");
                throw;
            }
            return artists;
        }

        public async Task<List<Artists>> GetAllArtists()
        {
            var artists = new List<Artists>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Artists";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var artist = new Artists
                                {
                                    ArtistID = reader.GetInt32(reader.GetOrdinal("artistID")),
                                    ArtistName = reader.GetString(reader.GetOrdinal("artistName")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
                                };

                                artists.Add(artist);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching all artists: {ex.Message}");
                throw;
            }
            return artists;
        }

        public async Task<int> AddArtist(Artists artist)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                    INSERT INTO Artists (artistName, createdAt, updatedAt)
                    VALUES (@artistName, @createdAt, @updatedAt)
                    RETURNING artistID"; // Add RETURNING clause to fetch the generated ID

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        // Add parameters
                        cmd.Parameters.AddWithValue("artistName", artist.ArtistName);
                        cmd.Parameters.AddWithValue("createdAt", artist.CreatedAt);
                        cmd.Parameters.AddWithValue("updatedAt", artist.UpdatedAt);

                        // Use ExecuteScalar to get the returned artistID
                        var artistID = (int)cmd.ExecuteScalar();
                        return artistID;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while adding the artist: {ex.Message}");
                throw;
            }
        }
    

        public async Task UpdateArtist(Artists artist)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        UPDATE Artists SET 
                            artistName = @artistName,
                            updatedAt = @updatedAt
                        WHERE artistID = @artistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("artistID", artist.ArtistID);
                        cmd.Parameters.AddWithValue("artistName", artist.ArtistName);
                        cmd.Parameters.AddWithValue("updatedAt", artist.UpdatedAt);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while updating the artist: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteArtist(int artistID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Artists WHERE artistID = @artistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("artistID", artistID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while deleting the artist: {ex.Message}");
                throw;
            }
        }
    }
}
