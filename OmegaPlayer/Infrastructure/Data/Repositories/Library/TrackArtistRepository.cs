using Npgsql;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TrackArtistRepository
    {
        public async Task<TrackArtist> GetTrackArtist(int trackID, int artistID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM TrackArtist WHERE trackID = @trackID AND artistID = @artistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackID);
                        cmd.Parameters.AddWithValue("artistID", artistID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new TrackArtist
                                {
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    ArtistID = reader.GetInt32(reader.GetOrdinal("artistID"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching the TrackArtist: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<TrackArtist>> GetAllTrackArtists()
        {
            var trackArtists = new List<TrackArtist>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM TrackArtist";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var trackArtist = new TrackArtist
                                {
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    ArtistID = reader.GetInt32(reader.GetOrdinal("artistID"))
                                };

                                trackArtists.Add(trackArtist);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching all TrackArtists: {ex.Message}");
                throw;
            }

            return trackArtists;
        }

        public async Task AddTrackArtist(TrackArtist trackArtist)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "INSERT INTO TrackArtist (trackID, artistID) VALUES (@trackID, @artistID)";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackArtist.TrackID);
                        cmd.Parameters.AddWithValue("artistID", trackArtist.ArtistID);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding the TrackArtist: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteTrackArtist(int trackID, int artistID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM TrackArtist WHERE trackID = @trackID AND artistID = @artistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackID);
                        cmd.Parameters.AddWithValue("artistID", artistID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the TrackArtist: {ex.Message}");
                throw;
            }
        }
    }
}
