using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Data.Sqlite;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

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
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = @"
                        SELECT 
                            t.trackid, 
                            t.title, 
                            t.coverid, 
                            a.title AS albumtitle,
                            a.albumid,
                            t.duration, 
                            t.filepath, 
                            g.genrename AS genre, 
                            m.coverpath, 
                            t.releasedate, 
                            t.bitrate,
                            t.filetype,
                            t.createdat,
                            t.updatedat,
                            COALESCE(pc.playCount, 0) as playcount,
                            CASE WHEN l.trackid IS NOT NULL THEN 1 ELSE 0 END as isliked
                        FROM tracks t
                        LEFT JOIN albums a ON t.albumid = a.albumid
                        LEFT JOIN genre g ON t.genreid = g.genreid
                        LEFT JOIN media m ON t.coverid = m.mediaid
                        LEFT JOIN playcounts pc ON t.trackid = pc.trackid AND pc.profileid = @profileId
                        LEFT JOIN likes l ON t.trackid = l.trackid AND l.profileid = @profileId
                        GROUP BY t.trackid, a.title, a.albumid, g.genrename, m.coverpath, pc.playCount, l.trackid";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileId"] = profileId
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var track = new TrackDisplayModel(_messenger)
                            {
                                TrackID = reader.GetInt32("trackid"),
                                Title = reader.IsDBNull("title") ? null : reader.GetString("title"),
                                CoverID = reader.IsDBNull("coverid") ? 0 : reader.GetInt32("coverid"),
                                AlbumTitle = reader.IsDBNull("albumtitle") ? null : reader.GetString("albumtitle"),
                                AlbumID = reader.IsDBNull("albumid") ? 0 : reader.GetInt32("albumid"),
                                // Handle duration stored as ticks in SQLite
                                Duration = reader.IsDBNull("duration") ? TimeSpan.Zero : new TimeSpan(reader.GetInt64("duration")),
                                FilePath = reader.GetString("filepath"),
                                Genre = reader.IsDBNull("genre") ? null : reader.GetString("genre"),
                                CoverPath = reader.IsDBNull("coverpath") ? null : reader.GetString("coverpath"),
                                ReleaseDate = reader.IsDBNull("releasedate") ? DateTime.MinValue : reader.GetDateTime("releasedate"),
                                BitRate = reader.IsDBNull("bitrate") ? 0 : reader.GetInt32("bitrate"),
                                FileType = reader.GetString("filetype"),
                                FileCreatedDate = reader.IsDBNull("createdat") ? DateTime.MinValue : reader.GetDateTime("createdat"),
                                FileModifiedDate = reader.IsDBNull("updatedat") ? DateTime.MinValue : reader.GetDateTime("updatedat"),
                                PlayCount = reader.GetInt32("playcount"),
                                IsLiked = reader.GetInt32("isliked") == 1,
                                Artists = new List<Artists>() // Initialize the Artists list
                            };

                            tracks.Add(track);
                        }

                        // Close the first reader before opening another
                        reader.Close();

                        // Now get the artists for each track
                        string artistQuery = @"
                        SELECT 
                            ta.trackid, 
                            ar.artistid, 
                            ar.artistname 
                        FROM trackartist ta
                        INNER JOIN artists ar ON ta.artistid = ar.artistid";

                        using var artistCmd = db.CreateCommand(artistQuery);
                        using var artistReader = await artistCmd.ExecuteReaderAsync();

                        while (await artistReader.ReadAsync())
                        {
                            int trackId = artistReader.GetInt32("trackid");
                            var artist = new Artists
                            {
                                ArtistID = artistReader.GetInt32("artistid"),
                                ArtistName = artistReader.GetString("artistname")
                            };

                            // Find the corresponding track and add the artist to its list
                            var track = tracks.FirstOrDefault(t => t.TrackID == trackId);
                            track?.Artists.Add(artist);
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
                        // SQLite doesn't support PostgreSQL's ANY operator, so we need to use IN with a parameterized list
                        // Create comma-separated placeholder string for IN clause
                        var placeholders = string.Join(",", trackIds.Select((_, i) => $"@trackId{i}"));

                        string query = $@"
                            SELECT 
                            t.trackid, 
                            t.title, 
                            t.coverid, 
                            a.title AS albumtitle, 
                            a.albumid,
                            t.duration, 
                            t.filepath, 
                            g.genrename AS genre, 
                            m.coverpath, 
                            t.releasedate,
                            t.bitrate,
                            t.filetype, 
                            t.createdat,
                            t.updatedat,
                            COALESCE(pc.playCount, 0) as playcount,
                            CASE WHEN l.trackid IS NOT NULL THEN 1 ELSE 0 END as isliked
                        FROM tracks t
                        LEFT JOIN albums a ON t.albumid = a.albumid
                        LEFT JOIN genre g ON t.genreid = g.genreid
                        LEFT JOIN media m ON t.coverid = m.mediaid
                        LEFT JOIN playcounts pc ON t.trackid = pc.trackid AND pc.profileid = @profileId
                        LEFT JOIN likes l ON t.trackid = l.trackid AND l.profileid = @profileId
                        WHERE t.trackid IN ({placeholders})
                        GROUP BY t.trackid, a.title, a.albumid, g.genrename, m.coverpath, pc.playCount, l.trackid";

                        // Build parameters dictionary
                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileId"] = profileId
                        };

                        // Add track ID parameters
                        for (int i = 0; i < trackIds.Count; i++)
                        {
                            parameters[$"@trackId{i}"] = trackIds[i];
                        }

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var track = new TrackDisplayModel(_messenger)
                            {
                                TrackID = reader.GetInt32("trackid"),
                                Title = reader.IsDBNull("title") ? null : reader.GetString("title"),
                                CoverID = reader.IsDBNull("coverid") ? 0 : reader.GetInt32("coverid"),
                                AlbumTitle = reader.IsDBNull("albumtitle") ? null : reader.GetString("albumtitle"),
                                AlbumID = reader.IsDBNull("albumid") ? 0 : reader.GetInt32("albumid"),
                                // Handle duration stored as ticks in SQLite
                                Duration = reader.IsDBNull("duration") ? TimeSpan.Zero : new TimeSpan(reader.GetInt64("duration")),
                                FilePath = reader.GetString("filepath"),
                                Genre = reader.IsDBNull("genre") ? null : reader.GetString("genre"),
                                CoverPath = reader.IsDBNull("coverpath") ? null : reader.GetString("coverpath"),
                                ReleaseDate = reader.IsDBNull("releasedate") ? DateTime.MinValue : reader.GetDateTime("releasedate"),
                                BitRate = reader.IsDBNull("bitrate") ? 0 : reader.GetInt32("bitrate"),
                                FileType = reader.GetString("filetype"),
                                FileCreatedDate = reader.IsDBNull("createdat") ? DateTime.MinValue : reader.GetDateTime("createdat"),
                                FileModifiedDate = reader.IsDBNull("updatedat") ? DateTime.MinValue : reader.GetDateTime("updatedat"),
                                PlayCount = reader.GetInt32("playcount"),
                                IsLiked = reader.GetInt32("isliked") == 1,
                                Artists = new List<Artists>() // Initialize the Artists list
                            };

                            tracks.Add(track);
                        }

                        // Close the first reader before opening another
                        reader.Close();

                        // Skip artist query if no tracks found
                        if (tracks.Count == 0)
                        {
                            return tracks;
                        }

                        // Fetch artists for each track - use the same IN clause approach
                        string artistQuery = $@"
                        SELECT 
                            ta.trackid, 
                            ar.artistid, 
                            ar.artistname 
                        FROM trackartist ta
                        INNER JOIN artists ar ON ta.artistid = ar.artistid
                        WHERE ta.trackid IN ({placeholders})";

                        // Reuse the track ID parameters
                        var artistParameters = new Dictionary<string, object>();
                        for (int i = 0; i < trackIds.Count; i++)
                        {
                            artistParameters[$"@trackId{i}"] = trackIds[i];
                        }

                        using var artistCmd = db.CreateCommand(artistQuery, artistParameters);
                        using var artistReader = await artistCmd.ExecuteReaderAsync();

                        while (await artistReader.ReadAsync())
                        {
                            int trackId = artistReader.GetInt32("trackid");
                            var artist = new Artists
                            {
                                ArtistID = artistReader.GetInt32("artistid"),
                                ArtistName = artistReader.GetString("artistname")
                            };

                            // Find the corresponding track and add the artist to its list
                            var track = tracks.FirstOrDefault(t => t.TrackID == trackId);
                            track?.Artists.Add(artist);
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

        /// <summary>
        /// Gets tracks with metadata for a specific album
        /// </summary>
        public async Task<List<TrackDisplayModel>> GetTracksByAlbumId(int albumId, int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    List<TrackDisplayModel> tracks = new List<TrackDisplayModel>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                        SELECT 
                            t.trackid, 
                            t.title, 
                            t.coverid, 
                            a.title AS albumtitle,
                            a.albumid,
                            t.duration, 
                            t.filepath, 
                            g.genrename AS genre, 
                            m.coverpath, 
                            t.releasedate, 
                            t.bitrate,
                            t.filetype,
                            t.createdat,
                            t.updatedat,
                            t.tracknumber,
                            COALESCE(pc.playCount, 0) as playcount,
                            CASE WHEN l.trackid IS NOT NULL THEN 1 ELSE 0 END as isliked
                        FROM tracks t
                        LEFT JOIN albums a ON t.albumid = a.albumid
                        LEFT JOIN genre g ON t.genreid = g.genreid
                        LEFT JOIN media m ON t.coverid = m.mediaid
                        LEFT JOIN playcounts pc ON t.trackid = pc.trackid AND pc.profileid = @profileId
                        LEFT JOIN likes l ON t.trackid = l.trackid AND l.profileid = @profileId
                        WHERE t.albumid = @albumId
                        ORDER BY t.tracknumber, t.title";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@profileId"] = profileId,
                            ["@albumId"] = albumId
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        var trackIds = new List<int>();

                        while (await reader.ReadAsync())
                        {
                            var track = new TrackDisplayModel(_messenger)
                            {
                                TrackID = reader.GetInt32("trackid"),
                                Title = reader.IsDBNull("title") ? null : reader.GetString("title"),
                                CoverID = reader.IsDBNull("coverid") ? 0 : reader.GetInt32("coverid"),
                                AlbumTitle = reader.IsDBNull("albumtitle") ? null : reader.GetString("albumtitle"),
                                AlbumID = reader.IsDBNull("albumid") ? 0 : reader.GetInt32("albumid"),
                                Duration = reader.IsDBNull("duration") ? TimeSpan.Zero : new TimeSpan(reader.GetInt64("duration")),
                                FilePath = reader.GetString("filepath"),
                                Genre = reader.IsDBNull("genre") ? null : reader.GetString("genre"),
                                CoverPath = reader.IsDBNull("coverpath") ? null : reader.GetString("coverpath"),
                                ReleaseDate = reader.IsDBNull("releasedate") ? DateTime.MinValue : reader.GetDateTime("releasedate"),
                                BitRate = reader.IsDBNull("bitrate") ? 0 : reader.GetInt32("bitrate"),
                                FileType = reader.GetString("filetype"),
                                FileCreatedDate = reader.IsDBNull("createdat") ? DateTime.MinValue : reader.GetDateTime("createdat"),
                                FileModifiedDate = reader.IsDBNull("updatedat") ? DateTime.MinValue : reader.GetDateTime("updatedat"),
                                PlayCount = reader.GetInt32("playcount"),
                                IsLiked = reader.GetInt32("isliked") == 1,
                                Artists = new List<Artists>()
                            };

                            tracks.Add(track);
                            trackIds.Add(track.TrackID);
                        }

                        reader.Close();

                        // Get artists for all tracks if we have any tracks
                        if (trackIds.Count > 0)
                        {
                            var placeholders = string.Join(",", trackIds.Select((_, i) => $"@trackId{i}"));
                            string artistQuery = $@"
                            SELECT 
                                ta.trackid, 
                                ar.artistid, 
                                ar.artistname 
                            FROM trackartist ta
                            INNER JOIN artists ar ON ta.artistid = ar.artistid
                            WHERE ta.trackid IN ({placeholders})";

                            var artistParameters = new Dictionary<string, object>();
                            for (int i = 0; i < trackIds.Count; i++)
                            {
                                artistParameters[$"@trackId{i}"] = trackIds[i];
                            }

                            using var artistCmd = db.CreateCommand(artistQuery, artistParameters);
                            using var artistReader = await artistCmd.ExecuteReaderAsync();

                            while (await artistReader.ReadAsync())
                            {
                                int trackId = artistReader.GetInt32("trackid");
                                var artist = new Artists
                                {
                                    ArtistID = artistReader.GetInt32("artistid"),
                                    ArtistName = artistReader.GetString("artistname")
                                };

                                var track = tracks.FirstOrDefault(t => t.TrackID == trackId);
                                track?.Artists.Add(artist);
                            }
                        }
                    }

                    return tracks;
                },
                $"Getting tracks for album {albumId}, profile {profileId}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.Playback,
                false
            );
        }
    }
}