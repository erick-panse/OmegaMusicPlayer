using Npgsql;
using OmegaPlayer.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
{
    public class LikeRepository
    {
        public async Task<Like> GetLikeById(int likeID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Likes WHERE likeID = @likeID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("likeID", likeID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Like
                                {
                                    LikeID = reader.GetInt32(reader.GetOrdinal("likeID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching the Like by ID: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<Like>> GetAllLikes()
        {
            var likes = new List<Like>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Likes";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var like = new Like
                                {
                                    LikeID = reader.GetInt32(reader.GetOrdinal("likeID")),
                                    ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID"))
                                };

                                likes.Add(like);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching all Likes: {ex.Message}");
                throw;
            }

            return likes;
        }

        public async Task<int> AddLike(Like like)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO Likes (profileID, trackID)
                        VALUES (@profileID, @trackID) RETURNING likeID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("profileID", like.ProfileID);
                        cmd.Parameters.AddWithValue("trackID", like.TrackID);

                        var likeID = (int)cmd.ExecuteScalar();
                        return likeID;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding the Like: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteLike(int likeID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Likes WHERE likeID = @likeID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("likeID", likeID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the Like: {ex.Message}");
                throw;
            }
        }
    }
}
