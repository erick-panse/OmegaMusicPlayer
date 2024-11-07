using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using OmegaPlayer.Features.Library.Models;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class AlbumRepository
    {
        public async Task<Albums> GetAlbumById(int albumID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Albums WHERE albumID = @albumID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("albumID", albumID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Albums
                                {
                                    AlbumID = reader.GetInt32(reader.GetOrdinal("albumID")),
                                    Title = reader.GetString(reader.GetOrdinal("title")),
                                    ArtistID = reader.GetInt32(reader.GetOrdinal("artistID")),
                                    ReleaseDate = reader.GetDateTime(reader.GetOrdinal("releaseDate")),
                                    DiscNumber = reader.GetInt32(reader.GetOrdinal("discNumber")),
                                    TrackCounter = reader.GetInt32(reader.GetOrdinal("trackCounter")),
                                    CoverID = reader.GetInt32(reader.GetOrdinal("coverID")),
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
                Console.WriteLine($"An error occurred while fetching the Album by ID: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<Albums> GetAlbumByTitle(string title, int artistID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Albums WHERE title = @title AND ArtistID = @artistID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("title", title);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Albums
                                {
                                    AlbumID = reader.GetInt32(reader.GetOrdinal("albumID")),
                                    Title = reader.GetString(reader.GetOrdinal("title")),
                                    ArtistID = reader.GetInt32(reader.GetOrdinal("artistID")),
                                    ReleaseDate = reader.GetDateTime(reader.GetOrdinal("releaseDate")),
                                    DiscNumber = reader.GetInt32(reader.GetOrdinal("discNumber")),
                                    TrackCounter = reader.GetInt32(reader.GetOrdinal("trackCounter")),
                                    CoverID = reader.GetInt32(reader.GetOrdinal("coverID")),
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
                Console.WriteLine($"An error occurred while fetching the Album by ID: {ex.Message}");
                throw;
            }

            return null;
        }

        public async Task<List<Albums>> GetAllAlbums()
        {
            var albums = new List<Albums>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Albums";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var album = new Albums
                                {
                                    AlbumID = reader.GetInt32(reader.GetOrdinal("albumID")),
                                    Title = reader.GetString(reader.GetOrdinal("title")),
                                    ArtistID = reader.GetInt32(reader.GetOrdinal("artistID")),
                                    ReleaseDate = reader.GetDateTime(reader.GetOrdinal("releaseDate")),
                                    DiscNumber = reader.GetInt32(reader.GetOrdinal("discNumber")),
                                    TrackCounter = reader.GetInt32(reader.GetOrdinal("trackCounter")),
                                    CoverID = reader.GetInt32(reader.GetOrdinal("coverID")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
                                };

                                albums.Add(album);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while fetching all Albums: {ex.Message}");
                throw;
            }

            return albums;
        }

        public async Task<int> AddAlbum(Albums album)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO Albums (title, artistID, releaseDate, discNumber, trackCounter, coverID, createdAt, updatedAt)
                        VALUES (@title, @artistID, @releaseDate, @discNumber, @trackCounter, @coverID, @createdAt, @updatedAt) RETURNING albumID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("title", album.Title);
                        cmd.Parameters.AddWithValue("artistID", album.ArtistID);
                        cmd.Parameters.AddWithValue("releaseDate", album.ReleaseDate);
                        cmd.Parameters.AddWithValue("discNumber", album.DiscNumber);
                        cmd.Parameters.AddWithValue("trackCounter", album.TrackCounter);
                        cmd.Parameters.AddWithValue("coverID", album.CoverID);
                        cmd.Parameters.AddWithValue("createdAt", album.CreatedAt);
                        cmd.Parameters.AddWithValue("updatedAt", album.UpdatedAt);

                        var albumID = (int)cmd.ExecuteScalar();
                        return albumID;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while adding the Album: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateAlbum(Albums album)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        UPDATE Albums SET 
                            title = @title,
                            artistID = @artistID,
                            releaseDate = @releaseDate,
                            discNumber = @discNumber,
                            trackCounter = @trackCounter,
                            coverID = @coverID,
                            updatedAt = @updatedAt
                        WHERE albumID = @albumID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("albumID", album.AlbumID);
                        cmd.Parameters.AddWithValue("title", album.Title);
                        cmd.Parameters.AddWithValue("artistID", album.ArtistID);
                        cmd.Parameters.AddWithValue("releaseDate", album.ReleaseDate);
                        cmd.Parameters.AddWithValue("discNumber", album.DiscNumber);
                        cmd.Parameters.AddWithValue("trackCounter", album.TrackCounter);
                        cmd.Parameters.AddWithValue("coverID", album.CoverID);
                        cmd.Parameters.AddWithValue("updatedAt", album.UpdatedAt);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while updating the Album: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteAlbum(int albumID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Albums WHERE albumID = @albumID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("albumID", albumID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while deleting the Album: {ex.Message}");
                throw;
            }
        }
    }

}
