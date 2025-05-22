using Npgsql;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TrackDisplayRepository
    {
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackDisplayRepository(
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<TrackDisplayModel>> GetAllTracksWithMetadata(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    List<TrackDisplayModel> tracks = new List<TrackDisplayModel>();

                    using (var db = new DbConnection(_errorHandlingService))
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
                            t.CreatedAt,
                            t.UpdatedAt,
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
                            cmd.Parameters.AddWithValue("@profileId", profileId);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var track = new TrackDisplayModel(_messenger)
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
                                        FileCreatedDate = reader.IsDBNull(12) ? DateTime.MinValue : reader.GetDateTime(12),
                                        FileModifiedDate = reader.IsDBNull(13) ? DateTime.MinValue : reader.GetDateTime(13),
                                        PlayCount = reader.GetInt32(14),
                                        IsLiked = reader.GetBoolean(15),
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
                },
                $"Retrieving all tracks with metadata for profile {profileId}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task<List<TrackDisplayModel>> GetTracksWithMetadataByIds(List<int> trackIds, int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Validate input
                    if (trackIds == null || !trackIds.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Empty track IDs list provided",
                            "Attempted to get tracks with an empty or null track ID list.",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

                    List<TrackDisplayModel> tracks = new List<TrackDisplayModel>();

                    using (var db = new DbConnection(_errorHandlingService))
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
                            t.CreatedAt,
                            t.UpdatedAt,
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
                                    var track = new TrackDisplayModel(_messenger)
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
                                        FileCreatedDate = reader.IsDBNull(12) ? DateTime.MinValue : reader.GetDateTime(12),
                                        FileModifiedDate = reader.IsDBNull(13) ? DateTime.MinValue : reader.GetDateTime(13),
                                        PlayCount = reader.GetInt32(14),
                                        IsLiked = reader.GetBoolean(15),
                                        Artists = new List<Artists>() // Initialize the Artists list
                                    };

                                    tracks.Add(track);
                                }
                            }
                        }

                        // Skip artist query if no tracks found
                        if (tracks.Count == 0)
                        {
                            return tracks;
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
                },
                $"Getting tracks by IDs ({trackIds?.Count ?? 0} tracks) for profile {profileId}",
                new List<TrackDisplayModel>(), 
                ErrorSeverity.Playback,
                false
            );
        }
    }
}