using Npgsql;
using System.Collections.Generic;
using System;
using System.Linq;
using OmegaPlayer.Models;
using System.Threading.Tasks;

namespace OmegaPlayer.Repositories
{
    public class TrackDisplayRepository
    {
        
        public async Task<List<TrackDisplayModel>> GetAllTracksWithMetadata()
        {
            List<TrackDisplayModel> tracks = new List<TrackDisplayModel>();

            using (var db = new DbConnection())
            {
                string query = @"
            SELECT 
                t.trackID, 
                t.title, 
                a.title AS albumTitle, 
                string_agg(ar.artistName, ', ') AS artists, -- Combines multiple artists into a string
                t.duration, 
                t.filePath, 
                g.genreName AS genre, 
                m.coverPath, 
                t.releaseDate, 
                t.playCount
            FROM Tracks t
            LEFT JOIN Albums a ON t.albumID = a.albumID
            LEFT JOIN TrackArtist ta ON t.trackID = ta.trackID
            LEFT JOIN Artists ar ON ta.artistID = ar.artistID
            LEFT JOIN Genre g ON t.genreID = g.genreID
            LEFT JOIN Media m ON t.coverID = m.mediaID
            GROUP BY t.trackID, a.title, g.genreName, m.coverPath";

                using (var cmd = new NpgsqlCommand(query, db.dbConn))
                {
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tracks.Add(new TrackDisplayModel
                            {
                                TrackID = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                AlbumTitle = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Artists = reader.GetString(3).Split(',').ToList(), // Splits multiple artists
                                Duration = reader.GetTimeSpan(4),
                                FilePath = reader.GetString(5),
                                Genre = reader.IsDBNull(6) ? null : reader.GetString(6),
                                CoverPath = reader.IsDBNull(7) ? null : reader.GetString(7),
                                ReleaseDate = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                                PlayCount = reader.GetInt32(9)
                            });
                        }
                    }
                }
            }

            return tracks;
        }

        public async Task<List<TrackDisplayModel>> GetTracksWithMetadataAsync(int pageNumber, int pageSize)
        {
            var tracks = new List<TrackDisplayModel>();

            string query = @"SELECT 
                t.trackID, 
                t.title, 
                a.title AS albumTitle, 
                string_agg(ar.artistName, ', ') AS artists, -- Combines multiple artists into a string
                t.duration, 
                t.filePath, 
                g.genreName AS genre, 
                m.coverPath, 
                t.releaseDate, 
                t.playCount
            FROM Tracks t
            LEFT JOIN Albums a ON t.albumID = a.albumID
            LEFT JOIN TrackArtist ta ON t.trackID = ta.trackID
            LEFT JOIN Artists ar ON ta.artistID = ar.artistID
            LEFT JOIN Genre g ON t.genreID = g.genreID
            LEFT JOIN Media m ON t.coverID = m.mediaID

            GROUP BY t.trackID, a.title, g.genreName, m.coverPath
            LIMIT @pageSize OFFSET @offset";

            using (var db = new DbConnection())
            {
                using (var cmd = new NpgsqlCommand(query, db.dbConn))
                {
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);
                    cmd.Parameters.AddWithValue("@offset", (pageNumber - 1) * pageSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var track = new TrackDisplayModel
                            {
                                TrackID = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                AlbumTitle = reader.IsDBNull(2) ? null : reader.GetString(2),
                                Artists = reader.GetString(3).Split(',').ToList(), // Splits multiple artists
                                Duration = reader.GetTimeSpan(4),
                                FilePath = reader.GetString(5),
                                Genre = reader.IsDBNull(6) ? null : reader.GetString(6),
                                CoverPath = reader.IsDBNull(7) ? null : reader.GetString(7),
                                ReleaseDate = reader.IsDBNull(8) ? DateTime.MinValue : reader.GetDateTime(8),
                                PlayCount = reader.GetInt32(9)
                            };
                            tracks.Add(track);
                        }
                    }
                }
            }

            return tracks;
        }
    }
}
