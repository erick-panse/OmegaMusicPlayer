using Npgsql;
using OmegaPlayer.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
{
    public class MediaRepository
    {
        public async Task<Media> GetMediaById(int mediaID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Media WHERE mediaID = @mediaID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("mediaID", mediaID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Media
                                {
                                    MediaID = reader.GetInt32(reader.GetOrdinal("mediaID")),
                                    CoverPath = reader.GetString(reader.GetOrdinal("coverPath")),
                                    MediaType = reader.GetString(reader.GetOrdinal("mediaType"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching the Media by ID: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<Media>> GetAllMedia()
        {
            var mediaList = new List<Media>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Media";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var media = new Media
                                {
                                    MediaID = reader.GetInt32(reader.GetOrdinal("mediaID")),
                                    CoverPath = reader.GetString(reader.GetOrdinal("coverPath")),
                                    MediaType = reader.GetString(reader.GetOrdinal("mediaType"))
                                };

                                mediaList.Add(media);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching all Media: {ex.Message}");
                throw;
            }

            return mediaList;
        }

        public async Task<int> AddMedia(Media media)
        {
            try
            {
                using (var db = new DbConnection())
                {

                    string query = @"
                        INSERT INTO Media (mediaType)
                        VALUES (@MediaType) RETURNING mediaID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("mediatype", media.MediaType);

                        var mediaID = (int)await cmd.ExecuteScalarAsync();
                        return mediaID;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while adding the Media: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateMedia(Media media)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        UPDATE Media SET 
                            coverpath = @coverPath,
                            mediaType = @mediaType
                        WHERE mediaID = @mediaID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("mediaID", media.MediaID);
                        cmd.Parameters.AddWithValue("coverpath", media.CoverPath);
                        cmd.Parameters.AddWithValue("mediatype", media.MediaType);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while updating the Media: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateMediaFilePath(int mediaId, string coverPath)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                UPDATE Media
                SET coverpath = @coverPath
                WHERE mediaID = @mediaID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("coverPath", coverPath);
                        cmd.Parameters.AddWithValue("mediaID", mediaId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle exceptions
                Console.WriteLine($"An error occurred while updating the media file path: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteMedia(int mediaID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Media WHERE mediaID = @mediaID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("mediaID", mediaID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while deleting the Media: {ex.Message}");
                throw;
            }
        }
    }
}
