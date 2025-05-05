using Npgsql;
using Playlist = OmegaPlayer.Features.Playlists.Models.Playlist;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playlists
{
    public class PlaylistRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public PlaylistRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Playlist> GetPlaylistById(int playlistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistID <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid playlist ID",
                            $"Attempted to get playlist with invalid ID: {playlistID}",
                            null,
                            false);
                        return null;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Playlists WHERE playlistID = @playlistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("playlistID", playlistID);

                            using (var reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    return new Playlist
                                    {
                                        PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
                                        ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                        Title = reader.GetString(reader.GetOrdinal("title")),
                                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
                                    };
                                }
                            }
                        }
                    }

                    return null;
                },
                $"Getting playlist with ID {playlistID}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<Playlist>> GetAllPlaylists()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var playlists = new List<Playlist>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Playlists";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var playlist = new Playlist
                                    {
                                        PlaylistID = reader.GetInt32(reader.GetOrdinal("playlistID")),
                                        ProfileID = reader.GetInt32(reader.GetOrdinal("profileID")),
                                        Title = reader.GetString(reader.GetOrdinal("title")),
                                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("createdAt")),
                                        UpdatedAt = reader.GetDateTime(reader.GetOrdinal("updatedAt"))
                                    };

                                    playlists.Add(playlist);
                                }
                            }
                        }
                    }

                    return playlists;
                },
                "Getting all playlists",
                new List<Playlist>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<int> AddPlaylist(Playlist playlist)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlist == null)
                    {
                        throw new ArgumentNullException(nameof(playlist), "Cannot add null playlist");
                    }

                    if (playlist.ProfileID <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(playlist));
                    }

                    if (string.IsNullOrWhiteSpace(playlist.Title))
                    {
                        playlist.Title = "Untitled Playlist";
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            INSERT INTO Playlists (profileID, title, createdAt, updatedAt)
                            VALUES (@profileID, @title, @createdAt, @updatedAt) RETURNING playlistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("profileID", playlist.ProfileID);
                            cmd.Parameters.AddWithValue("title", playlist.Title);
                            cmd.Parameters.AddWithValue("createdAt", playlist.CreatedAt);
                            cmd.Parameters.AddWithValue("updatedAt", playlist.UpdatedAt);

                            var playlistID = (int)cmd.ExecuteScalar();
                            return playlistID;
                        }
                    }
                },
                $"Adding playlist '{playlist?.Title ?? "Unknown"}' for profile {playlist?.ProfileID ?? 0}",
                -1,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task UpdatePlaylist(Playlist playlist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlist == null)
                    {
                        throw new ArgumentNullException(nameof(playlist), "Cannot update null playlist");
                    }

                    if (playlist.PlaylistID <= 0)
                    {
                        throw new ArgumentException("Invalid playlist ID", nameof(playlist));
                    }

                    if (playlist.ProfileID <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(playlist));
                    }

                    if (string.IsNullOrWhiteSpace(playlist.Title))
                    {
                        playlist.Title = "Untitled Playlist";
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            UPDATE Playlists SET 
                                profileID = @profileID,
                                title = @title,
                                updatedAt = @updatedAt
                            WHERE playlistID = @playlistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("playlistID", playlist.PlaylistID);
                            cmd.Parameters.AddWithValue("profileID", playlist.ProfileID);
                            cmd.Parameters.AddWithValue("title", playlist.Title);
                            cmd.Parameters.AddWithValue("updatedAt", playlist.UpdatedAt);

                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Updating playlist '{playlist?.Title ?? "Unknown"}' (ID: {playlist?.PlaylistID ?? 0})",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task DeletePlaylist(int playlistID)
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
                        string query = "DELETE FROM Playlists WHERE playlistID = @playlistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("playlistID", playlistID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Deleting playlist with ID {playlistID}",
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}