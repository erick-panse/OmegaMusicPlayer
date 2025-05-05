using Npgsql;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.IO;

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
                        string query = "SELECT * FROM Tracks WHERE trackID = @trackID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return MapTrackFromReader(reader);
                                }
                            }
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
                        string query = "SELECT * FROM Tracks WHERE filePath = @filePath";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("filePath", filePath);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return MapTrackFromReader(reader);
                                }
                            }
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
                        string query = "SELECT * FROM Tracks";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var track = MapTrackFromReader(reader);
                                    tracks.Add(track);
                                }
                            }
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
                            INSERT INTO Tracks (title, albumID, duration, releaseDate, trackNumber, filePath, lyrics, 
                                               bitRate, fileSize, fileType, createdAt, updatedAt, coverID, genreID)
                            VALUES (@title, @albumID, @duration, @releaseDate, @trackNumber, @filePath, @lyrics, 
                                   @bitRate, @fileSize, @fileType, @createdAt, @updatedAt, @coverID, @genreID) 
                            RETURNING trackID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            AddTrackParameters(cmd, track);

                            var trackID = (int)await cmd.ExecuteScalarAsync();
                            return trackID;
                        }
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
                                coverID = @coverID,
                                genreID = @genreID
                            WHERE trackID = @trackID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", track.TrackID);
                            AddTrackParameters(cmd, track);

                            await cmd.ExecuteNonQueryAsync();
                        }
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
                        // First delete any associated data that might cause foreign key constraints
                        await DeleteTrackRelationships(db, trackID);

                        // Then delete the track
                        string query = "DELETE FROM Tracks WHERE trackID = @trackID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Delete track with ID {trackID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task DeleteTrackRelationships(DbConnection db, int trackID)
        {
            // Delete track-artist relationships
            string deleteTrackArtistQuery = "DELETE FROM TrackArtist WHERE trackID = @trackID";
            using (var cmd = new NpgsqlCommand(deleteTrackArtistQuery, db.dbConn))
            {
                cmd.Parameters.AddWithValue("trackID", trackID);
                await cmd.ExecuteNonQueryAsync();
            }

            // Delete track-genre relationships
            string deleteTrackGenreQuery = "DELETE FROM TrackGenre WHERE trackID = @trackID";
            using (var cmd = new NpgsqlCommand(deleteTrackGenreQuery, db.dbConn))
            {
                cmd.Parameters.AddWithValue("trackID", trackID);
                await cmd.ExecuteNonQueryAsync();
            }

            // Delete playlist-track relationships if they exist
            string deletePlaylistTracksQuery = "DELETE FROM PlaylistTracks WHERE trackID = @trackID";
            using (var cmd = new NpgsqlCommand(deletePlaylistTracksQuery, db.dbConn))
            {
                cmd.Parameters.AddWithValue("trackID", trackID);
                await cmd.ExecuteNonQueryAsync();
            }

            // Delete from play history if exists
            string deletePlayHistoryQuery = "DELETE FROM PlayHistory WHERE trackID = @trackID";
            using (var cmd = new NpgsqlCommand(deletePlayHistoryQuery, db.dbConn))
            {
                cmd.Parameters.AddWithValue("trackID", trackID);
                await cmd.ExecuteNonQueryAsync();
            }

            // Delete from queue if exists
            string deleteQueueTracksQuery = "DELETE FROM QueueTracks WHERE trackID = @trackID";
            using (var cmd = new NpgsqlCommand(deleteQueueTracksQuery, db.dbConn))
            {
                cmd.Parameters.AddWithValue("trackID", trackID);
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private void AddTrackParameters(NpgsqlCommand cmd, Tracks track)
        {
            cmd.Parameters.AddWithValue("title", track.Title ?? Path.GetFileNameWithoutExtension(track.FilePath));
            cmd.Parameters.AddWithValue("albumID", track.AlbumID);
            cmd.Parameters.AddWithValue("duration", track.Duration);
            cmd.Parameters.AddWithValue("releaseDate", track.ReleaseDate);
            cmd.Parameters.AddWithValue("trackNumber", track.TrackNumber);
            cmd.Parameters.AddWithValue("filePath", track.FilePath);
            cmd.Parameters.AddWithValue("lyrics", track.Lyrics ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("bitRate", track.BitRate);
            cmd.Parameters.AddWithValue("fileSize", track.FileSize);
            cmd.Parameters.AddWithValue("fileType", track.FileType ?? Path.GetExtension(track.FilePath)?.TrimStart('.'));
            cmd.Parameters.AddWithValue("createdAt", track.CreatedAt);
            cmd.Parameters.AddWithValue("updatedAt", track.UpdatedAt);
            cmd.Parameters.AddWithValue("coverID", track.CoverID);
            cmd.Parameters.AddWithValue("genreID", track.GenreID);
        }

        private Tracks MapTrackFromReader(NpgsqlDataReader reader)
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
                CoverID = reader.GetInt32(reader.GetOrdinal("coverID")),
                GenreID = reader.IsDBNull(reader.GetOrdinal("genreID")) ? 0 : reader.GetInt32(reader.GetOrdinal("genreID"))
            };
        }
    }
}