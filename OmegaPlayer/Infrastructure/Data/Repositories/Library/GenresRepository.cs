using Npgsql;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class GenresRepository
    {
        public async Task<Genres> GetGenreByName(string genreID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Genre WHERE genreName = @genreName";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("genreName", genreID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Genres
                                {
                                    GenreID = reader.GetInt32(reader.GetOrdinal("genreID")),
                                    GenreName = reader.GetString(reader.GetOrdinal("genreName"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching the genre: {ex.Message}");
                throw;
            }
            return null;
        }

        public async Task<List<Genres>> GetAllGenres()
        {
            var genres = new List<Genres>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Genre";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var genre = new Genres
                                {
                                    GenreID = reader.GetInt32(reader.GetOrdinal("genreID")),
                                    GenreName = reader.GetString(reader.GetOrdinal("genreName"))
                                };

                                genres.Add(genre);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching all genres: {ex.Message}");
                throw;
            }
            return genres;
        }

        public async Task<int> AddGenre(Genres genre)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO Genre (genreName)
                        VALUES (@genreName) RETURNING genreID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("genreName", genre.GenreName);

                        var genreID = (int)cmd.ExecuteScalar();
                        return genreID;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while adding the genre: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateGenre(Genres genre)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        UPDATE Genre SET 
                            genreName = @genreName
                        WHERE genreID = @genreID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("genreID", genre.GenreID);
                        cmd.Parameters.AddWithValue("genreName", genre.GenreName);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while updating the genre: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteGenre(int genreID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Genre WHERE genreID = @genreID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("genreID", genreID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while deleting the genre: {ex.Message}");
                throw;
            }
        }
    }
}
