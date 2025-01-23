using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;
using OmegaPlayer.Features.Library.Models;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TracksRepository
    {
        public async Task<Tracks> GetTrackById(int trackID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Tracks WHERE trackID = @trackID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackID);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Tracks
                                {
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    Title = reader.GetString(reader.GetOrdinal("title")),
                                    AlbumID = reader.GetInt32(reader.GetOrdinal("albumID")),
                                    Duration = reader.GetTimeSpan(reader.GetOrdinal("duration")),
                                    ReleaseDate = reader.GetDateTime(reader.GetOrdinal("releaseDate")),
                                    TrackNumber = reader.GetInt32(reader.GetOrdinal("trackNumber")),
                                    FilePath = reader.GetString(reader.GetOrdinal("filePath")),
                                    Lyrics = reader.IsDBNull(reader.GetOrdinal("lyrics")) ? null : reader.GetString(reader.GetOrdinal("lyrics")),
                                    BitRate = reader.GetInt32(reader.GetOrdinal("bitRate")),
                                    FileSize = reader.GetInt32(reader.GetOrdinal("fileSize")),
                                    FileType = reader.GetString(reader.GetOrdinal("fileType")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt")),
                                    PlayCount = reader.GetInt32(reader.GetOrdinal("playCount")),
                                    CoverID = reader.GetInt32(reader.GetOrdinal("coverID")),
                                    GenreID = reader.IsDBNull(reader.GetOrdinal("genreID")) ? 0 : reader.GetInt32(reader.GetOrdinal("genreID"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception (implement a logger or use a logging library)
                Console.WriteLine($"An error occurred while fetching the track: {ex.Message}");
                throw; // Re-throw exception to be handled further up the call stack
            }

            return null; // Return null if no track is found
        }

        public async Task<Tracks> GetTrackByPath(string filePath)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Tracks WHERE filePath = @filePath";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("filePath", filePath);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Tracks
                                {
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    Title = reader.GetString(reader.GetOrdinal("title")),
                                    AlbumID = reader.GetInt32(reader.GetOrdinal("albumID")),
                                    Duration = reader.GetTimeSpan(reader.GetOrdinal("duration")),
                                    ReleaseDate = reader.GetDateTime(reader.GetOrdinal("releaseDate")),
                                    TrackNumber = reader.GetInt32(reader.GetOrdinal("trackNumber")),
                                    FilePath = reader.GetString(reader.GetOrdinal("filePath")),
                                    Lyrics = reader.IsDBNull(reader.GetOrdinal("lyrics")) ? null : reader.GetString(reader.GetOrdinal("lyrics")),
                                    BitRate = reader.GetInt32(reader.GetOrdinal("bitRate")),
                                    FileSize = reader.GetInt32(reader.GetOrdinal("fileSize")),
                                    FileType = reader.GetString(reader.GetOrdinal("fileType")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt")),
                                    PlayCount = reader.GetInt32(reader.GetOrdinal("playCount")),
                                    CoverID = reader.GetInt32(reader.GetOrdinal("coverID")),
                                    GenreID = reader.IsDBNull(reader.GetOrdinal("genreID")) ? 0 : reader.GetInt32(reader.GetOrdinal("genreID"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception (implement a logger or use a logging library)
                Console.WriteLine($"An error occurred while fetching the track: {ex.Message}");
                throw; // Re-throw exception to be handled further up the call stack
            }

            return null; // Return null if no track is found
        }

        public async Task<List<Tracks>> GetAllTracks()
        {
            var tracks = new List<Tracks>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Tracks";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var track = new Tracks
                                {
                                    TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                    Title = reader.GetString(reader.GetOrdinal("title")),
                                    AlbumID = reader.GetInt32(reader.GetOrdinal("albumID")),
                                    Duration = reader.GetTimeSpan(reader.GetOrdinal("duration")),
                                    ReleaseDate = reader.GetDateTime(reader.GetOrdinal("releaseDate")),
                                    TrackNumber = reader.GetInt32(reader.GetOrdinal("trackNumber")),
                                    FilePath = reader.GetString(reader.GetOrdinal("filePath")),
                                    Lyrics = reader.IsDBNull(reader.GetOrdinal("lyrics")) ? null : reader.GetString(reader.GetOrdinal("lyrics")),
                                    BitRate = reader.GetInt32(reader.GetOrdinal("bitRate")),
                                    FileSize = reader.GetInt32(reader.GetOrdinal("fileSize")),
                                    FileType = reader.GetString(reader.GetOrdinal("fileType")),
                                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                    UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt")),
                                    PlayCount = reader.GetInt32(reader.GetOrdinal("playCount")),
                                    CoverID = reader.GetInt32(reader.GetOrdinal("coverID")),
                                    GenreID = reader.IsDBNull(reader.GetOrdinal("genreID")) ? 0 : reader.GetInt32(reader.GetOrdinal("genreID"))
                                };

                                tracks.Add(track);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception (implement a logger or use a logging library)
                Console.WriteLine($"An error occurred while fetching all tracks: {ex.Message}");
                throw;
            }

            return tracks;
        }

        public async Task<int> AddTrack(Tracks track)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        INSERT INTO Tracks (title, albumID, duration, releaseDate, trackNumber, filePath, lyrics, bitRate, fileSize, fileType, createdAt, updatedAt, playCount, coverID, genreID)
                        VALUES (@title, @albumID, @duration, @releaseDate, @trackNumber, @filePath, @lyrics, @bitRate, @fileSize, @fileType, @createdAt, @updatedAt, @playCount, @coverID, @genreID) RETURNING trackID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("title", track.Title);
                        cmd.Parameters.AddWithValue("albumID", track.AlbumID);
                        cmd.Parameters.AddWithValue("duration", track.Duration);
                        cmd.Parameters.AddWithValue("releaseDate", track.ReleaseDate);
                        cmd.Parameters.AddWithValue("trackNumber", track.TrackNumber);
                        cmd.Parameters.AddWithValue("filePath", track.FilePath);
                        cmd.Parameters.AddWithValue("lyrics", track.Lyrics ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("bitRate", track.BitRate);
                        cmd.Parameters.AddWithValue("fileSize", track.FileSize);
                        cmd.Parameters.AddWithValue("fileType", track.FileType);
                        cmd.Parameters.AddWithValue("createdAt", track.CreatedAt);
                        cmd.Parameters.AddWithValue("updatedAt", track.UpdatedAt);
                        cmd.Parameters.AddWithValue("playCount", track.PlayCount);
                        cmd.Parameters.AddWithValue("coverID", track.CoverID);
                        cmd.Parameters.AddWithValue("genreID", track.GenreID);

                        var trackID = (int)cmd.ExecuteScalar();
                        return trackID;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception (implement a logger or use a logging library)
                Console.WriteLine($"An error occurred while adding the track: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateTrack(Tracks track)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                        UPDATE Tracks SET 
                            title = @title,
                            albumID = @albumID,
                            duration = @duration,
                            releaseDate = @releaseDate,
                            trackNumber = @trackNumber,
                            filePath = @filePath,
                            lyrics = @lyrics,
                            bitRate = @bitRate,
                            fileSize = @fileSize,
                            fileType = @fileType,
                            updatedAt = @updatedAt,
                            playCount = @playCount,
                            coverID = @coverID
                            genreID = @genreID
                        WHERE trackID = @trackID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", track.TrackID);
                        cmd.Parameters.AddWithValue("title", track.Title);
                        cmd.Parameters.AddWithValue("albumID", track.AlbumID);
                        cmd.Parameters.AddWithValue("duration", track.Duration);
                        cmd.Parameters.AddWithValue("releaseDate", track.ReleaseDate);
                        cmd.Parameters.AddWithValue("trackNumber", track.TrackNumber);
                        cmd.Parameters.AddWithValue("filePath", track.FilePath);
                        cmd.Parameters.AddWithValue("lyrics", track.Lyrics ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("bitRate", track.BitRate);
                        cmd.Parameters.AddWithValue("fileSize", track.FileSize);
                        cmd.Parameters.AddWithValue("fileType", track.FileType);
                        cmd.Parameters.AddWithValue("updatedAt", track.UpdatedAt);
                        cmd.Parameters.AddWithValue("playCount", track.PlayCount);
                        cmd.Parameters.AddWithValue("coverID", track.CoverID);
                        cmd.Parameters.AddWithValue("genreID", track.GenreID);

                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception (implement a logger or use a logging library)
                Console.WriteLine($"An error occurred while updating the track: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteTrack(int trackID)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Tracks WHERE trackID = @trackID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackID);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception (implement a logger or use a logging library)
                Console.WriteLine($"An error occurred while deleting the track: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateTrackLike(int trackId, bool isLiked)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"UPDATE Tracks SET is_liked = @isLiked WHERE trackID = @trackID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackId);
                        cmd.Parameters.AddWithValue("isLiked", isLiked);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating track like status: {ex.Message}");
                throw;
            }
        }

    }
}
