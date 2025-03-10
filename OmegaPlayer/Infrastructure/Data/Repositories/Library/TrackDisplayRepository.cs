using Npgsql;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Features.Library.Services;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TrackDisplayRepository
    {
        private readonly IMessenger _messenger;
        private readonly TracksService _tracksService;

        public TrackDisplayRepository(IMessenger messenger, TracksService tracksService)
        {
            _messenger = messenger;
            _tracksService = tracksService;
        }

        public async Task<List<TrackDisplayModel>> GetAllTracksWithMetadata(int profileId)
        {
            List<TrackDisplayModel> tracks = new List<TrackDisplayModel>();

            using (var db = new DbConnection())
            {
                string query = @"
                SELECT 
                    t.trackID, 
                    t.title, 
                    t.coverID, 
                    a.title AS albumTitle,
                    a.albumID,
                    t.duration, 
                    t.filePath, 
                    g.genreName AS genre, 
                    m.coverPath, 
                    t.releaseDate, 
                    t.BitRate,
                    t.FileType,
                    COALESCE(pc.playCount, 0) as playCount,
                    CASE WHEN l.trackID IS NOT NULL THEN true ELSE false END as isLiked
                FROM Tracks t
                LEFT JOIN Albums a ON t.albumID = a.albumID
                LEFT JOIN Genre g ON t.genreID = g.genreID
                LEFT JOIN Media m ON t.coverID = m.mediaID
                LEFT JOIN PlayCounts pc ON t.trackID = pc.trackID AND pc.profileID = @profileId
                LEFT JOIN Likes l ON t.trackID = l.trackID AND l.profileID = @profileId
                GROUP BY t.trackID, a.title, a.albumID, g.genreName, m.coverPath, pc.playCount, l.trackID";

                using (var cmd = new NpgsqlCommand(query, db.dbConn))
                {
                    cmd.Parameters.AddWithValue("@profileId", profileId); // Pass the profileId as a parameter

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var track = new TrackDisplayModel(_messenger, _tracksService)
                            {
                                TrackID = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                CoverID = reader.GetInt32(2),
                                AlbumTitle = reader.IsDBNull(3) ? null : reader.GetString(3),
                                AlbumID = reader.GetInt32(4),
                                Duration = reader.GetTimeSpan(5),
                                FilePath = reader.GetString(6),
                                Genre = reader.IsDBNull(7) ? null : reader.GetString(7),
                                CoverPath = reader.IsDBNull(8) ? null : reader.GetString(8),
                                ReleaseDate = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                                BitRate = reader.GetInt16(10),
                                FileType = reader.GetString(11),
                                PlayCount = reader.GetInt32(12),
                                IsLiked = reader.GetBoolean(13),
                                Artists = new List<Artists>() // Initialize the Artists list
                            };

                            tracks.Add(track);
                        }
                    }
                }

                // Now get the artists for each track
                string artistQuery = @"
                SELECT 
                    ta.trackID, 
                    ar.artistID, 
                    ar.artistName 
                FROM TrackArtist ta
                INNER JOIN Artists ar ON ta.artistID = ar.artistID";

                using (var cmd = new NpgsqlCommand(artistQuery, db.dbConn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int trackId = reader.GetInt32(0);
                            var artist = new Artists
                            {
                                ArtistID = reader.GetInt32(1),
                                ArtistName = reader.GetString(2)
                            };

                            // Find the corresponding track and add the artist to its list
                            var track = tracks.FirstOrDefault(t => t.TrackID == trackId);
                            track?.Artists.Add(artist);
                        }
                    }
                }
            }

            return tracks;
        }

        public async Task<List<TrackDisplayModel>> GetTracksWithMetadataAsync(int profileId, int pageNumber, int pageSize)
        {
            var tracks = new List<TrackDisplayModel>();

            string query = @"
            SELECT 
                t.trackID, 
                t.title, 
                t.coverID,  
                a.title AS albumTitle, 
                a.albumID,
                t.duration, 
                t.filePath, 
                g.genreName AS genre, 
                m.coverPath, 
                t.releaseDate,
                t.BitRate,
                t.FileType,
                COALESCE(pc.playCount, 0) as playCount,
                CASE WHEN l.trackID IS NOT NULL THEN true ELSE false END as isLiked
            FROM Tracks t
            LEFT JOIN Albums a ON t.albumID = a.albumID
            LEFT JOIN Genre g ON t.genreID = g.genreID
            LEFT JOIN Media m ON t.coverID = m.mediaID
            LEFT JOIN PlayCounts pc ON t.trackID = pc.trackID AND pc.profileID = @profileId
            LEFT JOIN Likes l ON t.trackID = l.trackID AND l.profileID = @profileId
            GROUP BY t.trackID, a.title, a.albumID, g.genreName, m.coverPath, pc.playCount, l.trackID
            LIMIT @pageSize OFFSET @offset";

            using (var db = new DbConnection())
            {
                using (var cmd = new NpgsqlCommand(query, db.dbConn))
                {
                    cmd.Parameters.AddWithValue("@profileId", profileId); // Pass the profileId as a parameter
                    cmd.Parameters.AddWithValue("@pageSize", pageSize);
                    cmd.Parameters.AddWithValue("@offset", (pageNumber - 1) * pageSize);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var track = new TrackDisplayModel(_messenger, _tracksService)
                            {
                                TrackID = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                CoverID = reader.GetInt32(2),
                                AlbumTitle = reader.IsDBNull(3) ? null : reader.GetString(3),
                                AlbumID = reader.GetInt32(4),
                                Duration = reader.GetTimeSpan(5),
                                FilePath = reader.GetString(6),
                                Genre = reader.IsDBNull(7) ? null : reader.GetString(7),
                                CoverPath = reader.IsDBNull(8) ? null : reader.GetString(8),
                                ReleaseDate = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                                BitRate = reader.GetInt16(10),
                                FileType = reader.GetString(11),
                                PlayCount = reader.GetInt32(12),
                                IsLiked = reader.GetBoolean(13),
                                Artists = new List<Artists>() // Initialize the Artists list
                            };

                            tracks.Add(track);
                        }
                    }
                }

                // Get artists for paginated tracks
                string artistQuery = @"
                SELECT 
                    ta.trackID, 
                    ar.artistID, 
                    ar.artistName 
                FROM TrackArtist ta
                INNER JOIN Artists ar ON ta.artistID = ar.artistID
                WHERE ta.trackID = ANY(@trackIds)";

                // Collect track IDs for current page as an array
                var trackIds = tracks.Select(t => t.TrackID).ToArray();

                using (var cmd = new NpgsqlCommand(artistQuery, db.dbConn))
                {
                    // Add the track IDs as an array parameter
                    cmd.Parameters.AddWithValue("@trackIds", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Integer, trackIds);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int trackId = reader.GetInt32(0);
                            var artist = new Artists
                            {
                                ArtistID = reader.GetInt32(1),
                                ArtistName = reader.GetString(2)
                            };

                            // Find the corresponding track and add the artist to its list
                            var track = tracks.FirstOrDefault(t => t.TrackID == trackId);
                            track?.Artists.Add(artist);
                        }
                    }
                }
            }

            return tracks;
        }

        public async Task<List<TrackDisplayModel>> GetTracksWithMetadataByIds(List<int> trackIds, int profileId)
        {
            List<TrackDisplayModel> tracks = new List<TrackDisplayModel>();

            using (var db = new DbConnection())
            {
                string query = @"
                    SELECT 
                    t.trackID, 
                    t.title, 
                    t.coverID, 
                    a.title AS albumTitle, 
                    a.albumID,
                    t.duration, 
                    t.filePath, 
                    g.genreName AS genre, 
                    m.coverPath, 
                    t.releaseDate,
                    t.BitRate,
                    t.FileType, 
                    COALESCE(pc.playCount, 0) as playCount,
                    CASE WHEN l.trackID IS NOT NULL THEN true ELSE false END as isLiked
                FROM Tracks t
                LEFT JOIN Albums a ON t.albumID = a.albumID
                LEFT JOIN Genre g ON t.genreID = g.genreID
                LEFT JOIN Media m ON t.coverID = m.mediaID
                LEFT JOIN PlayCounts pc ON t.trackID = pc.trackID AND pc.profileID = @profileId
                LEFT JOIN Likes l ON t.trackID = l.trackID AND l.profileID = @profileId
                WHERE t.trackID = ANY(@trackIds)
                GROUP BY t.trackID, a.title, a.albumID, g.genreName, m.coverPath, pc.playCount, l.trackID";

                using (var cmd = new NpgsqlCommand(query, db.dbConn))
                {
                    cmd.Parameters.AddWithValue("@profileId", profileId);
                    cmd.Parameters.AddWithValue("@trackIds", trackIds); // Use the list of track IDs

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var track = new TrackDisplayModel(_messenger, _tracksService)
                            {
                                TrackID = reader.GetInt32(0),
                                Title = reader.GetString(1),
                                CoverID = reader.GetInt32(2),
                                AlbumTitle = reader.IsDBNull(3) ? null : reader.GetString(3),
                                AlbumID = reader.GetInt32(4),
                                Duration = reader.GetTimeSpan(5),
                                FilePath = reader.GetString(6),
                                Genre = reader.IsDBNull(7) ? null : reader.GetString(7),
                                CoverPath = reader.IsDBNull(8) ? null : reader.GetString(8),
                                ReleaseDate = reader.IsDBNull(9) ? DateTime.MinValue : reader.GetDateTime(9),
                                BitRate = reader.GetInt16(10),
                                FileType = reader.GetString(11),
                                PlayCount = reader.GetInt32(12),
                                IsLiked = reader.GetBoolean(13),
                                Artists = new List<Artists>() // Initialize the Artists list
                            };

                            tracks.Add(track);
                        }
                    }
                }

                // Fetch artists for each track
                string artistQuery = @"
                SELECT 
                    ta.trackID, 
                    ar.artistID, 
                    ar.artistName 
                FROM TrackArtist ta
                INNER JOIN Artists ar ON ta.artistID = ar.artistID
                WHERE ta.trackID = ANY(@trackIds)"; // Match track IDs in the list

                using (var cmd = new NpgsqlCommand(artistQuery, db.dbConn))
                {
                    cmd.Parameters.AddWithValue("@trackIds", trackIds);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int trackId = reader.GetInt32(0);
                            var artist = new Artists
                            {
                                ArtistID = reader.GetInt32(1),
                                ArtistName = reader.GetString(2)
                            };

                            // Find the corresponding track and add the artist to its list
                            var track = tracks.FirstOrDefault(t => t.TrackID == trackId);
                            track?.Artists.Add(artist);
                        }
                    }
                }
            }

            return tracks;
        }
    }
}
