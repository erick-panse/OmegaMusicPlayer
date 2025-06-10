using Microsoft.Data.Sqlite;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TracksRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public TracksRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Tracks> GetTrackById(int trackID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = @"SELECT trackid, title, albumid, duration, releasedate, tracknumber, 
                                       filepath, lyrics, bitrate, filesize, filetype, createdat, updatedat, 
                                       coverid, genreid FROM tracks WHERE trackid = @trackID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@trackID"] = trackID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return MapTrackFromReader(reader);
                        }
                    }
                    return null;
                },
                $"Database operation: Get track with ID {trackID}",
                null,
                ErrorSeverity.Playback,
                false);
        }

        public async Task<Tracks> GetTrackByPath(string filePath)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        return null;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"SELECT trackid, title, albumid, duration, releasedate, tracknumber, 
                                       filepath, lyrics, bitrate, filesize, filetype, createdat, updatedat, 
                                       coverid, genreid FROM tracks WHERE filepath = @filePath";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@filePath"] = filePath
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return MapTrackFromReader(reader);
                        }
                    }
                    return null;
                },
                $"Database operation: Get track by path {Path.GetFileName(filePath)}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Tracks>> GetAllTracks()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var tracks = new List<Tracks>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"SELECT trackid, title, albumid, duration, releasedate, tracknumber, 
                                       filepath, lyrics, bitrate, filesize, filetype, createdat, updatedat, 
                                       coverid, genreid FROM tracks ORDER BY title";

                        using var cmd = db.CreateCommand(query);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var track = MapTrackFromReader(reader);
                            tracks.Add(track);
                        }
                    }

                    return tracks;
                },
                "Database operation: Get all tracks",
                new List<Tracks>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> AddTrack(Tracks track)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null)
                    {
                        throw new ArgumentNullException(nameof(track), "Cannot add null track to database");
                    }

                    // Validate essential track properties
                    if (string.IsNullOrEmpty(track.FilePath))
                    {
                        throw new ArgumentException("Track must have a file path", nameof(track));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            INSERT INTO tracks (title, albumid, duration, releasedate, tracknumber, filepath, lyrics, 
                                              bitrate, filesize, filetype, createdat, updatedat, coverid, genreid)
                            VALUES (@title, @albumID, @duration, @releaseDate, @trackNumber, @filePath, @lyrics, 
                                   @bitRate, @fileSize, @fileType, @createdAt, @updatedAt, @coverID, @genreID)";

                        var parameters = BuildTrackParameters(track);

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Get the inserted ID using SQLite's last_insert_rowid()
                        using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                        var result = await idCmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
                    }
                },
                $"Database operation: Add track {track?.Title ?? "Unknown"}",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateTrack(Tracks track)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null || track.TrackID <= 0)
                    {
                        throw new ArgumentException("Cannot update null track or track with invalid ID", nameof(track));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            UPDATE tracks SET 
                                title = @title,
                                albumid = @albumID,
                                duration = @duration,
                                releasedate = @releaseDate,
                                tracknumber = @trackNumber,
                                filepath = @filePath,
                                lyrics = @lyrics,
                                bitrate = @bitRate,
                                filesize = @fileSize,
                                filetype = @fileType,
                                updatedat = @updatedAt,
                                coverid = @coverID,
                                genreid = @genreID
                            WHERE trackid = @trackID";

                        var parameters = BuildTrackParameters(track);
                        parameters["@trackID"] = track.TrackID;

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
                    }
                },
                $"Database operation: Update track {track?.Title ?? "Unknown"} (ID: {track?.TrackID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteTrack(int trackID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0)
                    {
                        throw new ArgumentException("Cannot delete track with invalid ID", nameof(trackID));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        using var transaction = db.dbConn.BeginTransaction();
                        try
                        {
                            // First delete any associated data that might cause foreign key constraints
                            await DeleteTrackRelationships(db, trackID, transaction);

                            // Then delete the track
                            string query = "DELETE FROM tracks WHERE trackid = @trackID";

                            var parameters = new Dictionary<string, object>
                            {
                                ["@trackID"] = trackID
                            };

                            using var cmd = db.CreateCommand(query, parameters);
                            cmd.Transaction = transaction;
                            await cmd.ExecuteNonQueryAsync();

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                },
                $"Database operation: Delete track with ID {trackID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task DeleteTrackRelationships(DbConnection db, int trackID, SqliteTransaction transaction)
        {
            // Delete track-artist relationships
            string deleteTrackArtistQuery = "DELETE FROM trackartist WHERE trackid = @trackID";
            var parameters1 = new Dictionary<string, object>
            {
                ["@trackID"] = trackID
            };

            using var cmd1 = db.CreateCommand(deleteTrackArtistQuery, parameters1);
            cmd1.Transaction = transaction;
            await cmd1.ExecuteNonQueryAsync();

            // Delete track-genre relationships
            string deleteTrackGenreQuery = "DELETE FROM trackgenre WHERE trackid = @trackID";
            var parameters2 = new Dictionary<string, object>
            {
                ["@trackID"] = trackID
            };

            using var cmd2 = db.CreateCommand(deleteTrackGenreQuery, parameters2);
            cmd2.Transaction = transaction;
            await cmd2.ExecuteNonQueryAsync();

            // Delete playlist-track relationships if they exist
            string deletePlaylistTracksQuery = "DELETE FROM playlisttracks WHERE trackid = @trackID";
            var parameters3 = new Dictionary<string, object>
            {
                ["@trackID"] = trackID
            };

            using var cmd3 = db.CreateCommand(deletePlaylistTracksQuery, parameters3);
            cmd3.Transaction = transaction;
            await cmd3.ExecuteNonQueryAsync();

            // Delete from play history if exists
            string deletePlayHistoryQuery = "DELETE FROM playhistory WHERE trackid = @trackID";
            var parameters4 = new Dictionary<string, object>
            {
                ["@trackID"] = trackID
            };

            using var cmd4 = db.CreateCommand(deletePlayHistoryQuery, parameters4);
            cmd4.Transaction = transaction;
            await cmd4.ExecuteNonQueryAsync();

            // Delete from queue if exists
            string deleteQueueTracksQuery = "DELETE FROM queuetracks WHERE trackid = @trackID";
            var parameters5 = new Dictionary<string, object>
            {
                ["@trackID"] = trackID
            };

            using var cmd5 = db.CreateCommand(deleteQueueTracksQuery, parameters5);
            cmd5.Transaction = transaction;
            await cmd5.ExecuteNonQueryAsync();

            // Delete from play counts if exists
            string deletePlayCountsQuery = "DELETE FROM playcounts WHERE trackid = @trackID";
            var parameters6 = new Dictionary<string, object>
            {
                ["@trackID"] = trackID
            };

            using var cmd6 = db.CreateCommand(deletePlayCountsQuery, parameters6);
            cmd6.Transaction = transaction;
            await cmd6.ExecuteNonQueryAsync();

            // Delete from likes if exists
            string deleteLikesQuery = "DELETE FROM likes WHERE trackid = @trackID";
            var parameters7 = new Dictionary<string, object>
            {
                ["@trackID"] = trackID
            };

            using var cmd7 = db.CreateCommand(deleteLikesQuery, parameters7);
            cmd7.Transaction = transaction;
            await cmd7.ExecuteNonQueryAsync();
        }

        private Dictionary<string, object> BuildTrackParameters(Tracks track)
        {
            // Ensure required fields have default values
            var title = track.Title ?? Path.GetFileNameWithoutExtension(track.FilePath);
            var fileType = track.FileType ?? Path.GetExtension(track.FilePath)?.TrimStart('.');
            var createdAt = track.CreatedAt != default ? track.CreatedAt : DateTime.Now;
            var updatedAt = DateTime.Now; // Always update the timestamp

            return new Dictionary<string, object>
            {
                ["@title"] = title,
                ["@albumID"] = track.AlbumID > 0 ? track.AlbumID : null,
                ["@duration"] = track.Duration.Ticks > 0 ? track.Duration.Ticks : 0L, // Store TimeSpan as ticks for SQLite
                ["@releaseDate"] = track.ReleaseDate != default ? track.ReleaseDate : new DateTime(1900, 1, 1),
                ["@trackNumber"] = track.TrackNumber,
                ["@filePath"] = track.FilePath,
                ["@lyrics"] = track.Lyrics,
                ["@bitRate"] = track.BitRate,
                ["@fileSize"] = track.FileSize,
                ["@fileType"] = fileType,
                ["@createdAt"] = createdAt,
                ["@updatedAt"] = updatedAt,
                ["@coverID"] = track.CoverID > 0 ? track.CoverID : null,
                ["@genreID"] = track.GenreID > 0 ? track.GenreID : null
            };
        }

        private Tracks MapTrackFromReader(SqliteDataReader reader)
        {
            return new Tracks
            {
                TrackID = reader.GetInt32("trackid"),
                Title = reader.GetString("title"),
                AlbumID = reader.IsDBNull("albumid") ? 0 : reader.GetInt32("albumid"),
                // Convert ticks back to TimeSpan for SQLite
                Duration = reader.IsDBNull("duration") ? TimeSpan.Zero : new TimeSpan(reader.GetInt64("duration")),
                ReleaseDate = reader.GetDateTime("releasedate"),
                TrackNumber = reader.GetInt32("tracknumber"),
                FilePath = reader.GetString("filepath"),
                Lyrics = reader.IsDBNull("lyrics") ? null : reader.GetString("lyrics"),
                BitRate = reader.GetInt32("bitrate"),
                FileSize = reader.IsDBNull("filesize") ? 0 : reader.GetInt32("filesize"),
                FileType = reader.GetString("filetype"),
                CreatedAt = reader.GetDateTime("createdat"),
                UpdatedAt = reader.GetDateTime("updatedat"),
                CoverID = reader.IsDBNull("coverid") ? 0 : reader.GetInt32("coverid"),
                GenreID = reader.IsDBNull("genreid") ? 0 : reader.GetInt32("genreid")
            };
        }
    }
}