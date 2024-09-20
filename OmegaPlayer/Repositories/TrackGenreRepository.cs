using Npgsql;
using OmegaPlayer.Models;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
{
    public class TrackGenreRepository
    {
        public async Task<TrackGenre> GetTrackGenre(int trackID, int genreID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM TrackGenre WHERE trackID = @trackID AND genreID = @genreID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackID);
                        cmd.Parameters.AddWithValue("genreID", genreID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new TrackGenre
                                {
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    GenreID = reader.GetInt32(reader.GetOrdinal("genreID"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching the TrackGenre: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<TrackGenre>> GetAllTrackGenres()
        {
            var trackGenres = new List<TrackGenre>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM TrackGenre";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var trackGenre = new TrackGenre
                                {
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    GenreID = reader.GetInt32(reader.GetOrdinal("genreID"))
                                };

                                trackGenres.Add(trackGenre);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching all TrackGenres: {ex.Message}");
                throw;
            }

            return trackGenres;
        }

        public async Task AddTrackGenre(TrackGenre trackGenre)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "INSERT INTO TrackGenre (trackID, genreID) VALUES (@trackID, @genreID)";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackGenre.TrackID);
                        cmd.Parameters.AddWithValue("genreID", trackGenre.GenreID);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding the TrackGenre: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteTrackGenre(int trackID, int genreID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM TrackGenre WHERE trackID = @trackID AND genreID = @genreID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackID);
                        cmd.Parameters.AddWithValue("genreID", genreID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the TrackGenre: {ex.Message}");
                throw;
            }
        }
    }
}
