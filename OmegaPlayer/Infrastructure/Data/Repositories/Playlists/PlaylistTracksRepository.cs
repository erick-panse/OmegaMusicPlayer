using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playlists
{
    public class PlaylistTracksRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public PlaylistTracksRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<PlaylistTracks>> GetPlaylistTrack(int playlistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistID <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid playlist ID",
                            $"Attempted to get tracks for invalid playlist ID: {playlistID}",
                            null,
                            false);
                        return new List<PlaylistTracks>();
                    }

                    var playlistTracks = new List<PlaylistTracks>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM PlaylistTracks WHERE playlistID = @playlistID ORDER BY trackOrder";

                        using (var cmd = new SqliteCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("playlistID", playlistID);

                            using (var reader = cmd.ExecuteReader())
                            {
                                while (await reader.ReadAsync())
                                {
                                    playlistTracks.Add(new PlaylistTracks
                                    {
                                        PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        TrackOrder = reader.GetInt32(reader.GetOrdinal("trackOrder"))
                                    });
                                }
                            }
                        }
                    }

                    return playlistTracks;
                },
                $"Getting tracks for playlist {playlistID}",
                new List<PlaylistTracks>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<PlaylistTracks>> GetAllPlaylistTracks()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var playlistTracks = new List<PlaylistTracks>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM PlaylistTracks ORDER BY playlistID, trackOrder";

                        using (var cmd = new SqliteCommand(query, db.dbConn))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var playlistTrack = new PlaylistTracks
                                    {
                                        PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        TrackOrder = reader.GetInt32(reader.GetOrdinal("trackOrder"))
                                    };

                                    playlistTracks.Add(playlistTrack);
                                }
                            }
                        }
                    }

                    return playlistTracks;
                },
                "Getting all playlist tracks",
                new List<PlaylistTracks>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task AddPlaylistTrack(PlaylistTracks playlistTrack)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistTrack == null)
                    {
                        throw new ArgumentNullException(nameof(playlistTrack), "Cannot add null playlist track");
                    }

                    if (playlistTrack.PlaylistID <= 0)
                    {
                        throw new ArgumentException("Invalid playlist ID", nameof(playlistTrack));
                    }

                    if (playlistTrack.TrackID <= 0)
                    {
                        throw new ArgumentException("Invalid track ID", nameof(playlistTrack));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "INSERT INTO PlaylistTracks (playlistID, trackID, trackOrder) VALUES (@playlistID, @trackID, @trackOrder)";

                        using (var cmd = new SqliteCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("playlistID", playlistTrack.PlaylistID);
                            cmd.Parameters.AddWithValue("trackID", playlistTrack.TrackID);
                            cmd.Parameters.AddWithValue("trackOrder", playlistTrack.TrackOrder);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Adding track {playlistTrack?.TrackID ?? 0} to playlist {playlistTrack?.PlaylistID ?? 0}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task DeletePlaylistTrack(int playlistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistID <= 0)
                    {
                        throw new ArgumentException("Invalid playlist ID", nameof(playlistID));
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "DELETE FROM PlaylistTracks WHERE playlistID = @playlistID";

                        using (var cmd = new SqliteCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("playlistID", playlistID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Deleting all tracks from playlist {playlistID}",
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}