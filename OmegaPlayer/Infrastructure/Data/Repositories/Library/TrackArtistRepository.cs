using Npgsql;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TrackArtistRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackArtistRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<TrackArtist> GetTrackArtist(int trackID, int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0 || artistID <= 0)
                    {
                        return null;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM TrackArtist WHERE trackID = @trackID AND artistID = @artistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);
                            cmd.Parameters.AddWithValue("artistID", artistID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return new TrackArtist
                                    {
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        ArtistID = reader.GetInt32(reader.GetOrdinal("artistID"))
                                    };
                                }
                            }
                        }
                    }
                    return null;
                },
                $"Database operation: Get track-artist relationship: Track ID {trackID}, Artist ID {artistID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackArtist>> GetAllTrackArtists()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var trackArtists = new List<TrackArtist>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM TrackArtist";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var trackArtist = new TrackArtist
                                    {
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        ArtistID = reader.GetInt32(reader.GetOrdinal("artistID"))
                                    };

                                    trackArtists.Add(trackArtist);
                                }
                            }
                        }
                    }

                    return trackArtists;
                },
                "Database operation: Get all track-artist relationships",
                new List<TrackArtist>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<List<TrackArtist>> GetTrackArtistsByTrackId(int trackID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0)
                    {
                        return new List<TrackArtist>();
                    }

                    var trackArtists = new List<TrackArtist>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM TrackArtist WHERE trackID = @trackID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var trackArtist = new TrackArtist
                                    {
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        ArtistID = reader.GetInt32(reader.GetOrdinal("artistID"))
                                    };

                                    trackArtists.Add(trackArtist);
                                }
                            }
                        }
                    }

                    return trackArtists;
                },
                $"Database operation: Get track-artist relationships for track ID {trackID}",
                new List<TrackArtist>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<List<TrackArtist>> GetTrackArtistsByArtistId(int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artistID <= 0)
                    {
                        return new List<TrackArtist>();
                    }

                    var trackArtists = new List<TrackArtist>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM TrackArtist WHERE artistID = @artistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("artistID", artistID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var trackArtist = new TrackArtist
                                    {
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        ArtistID = reader.GetInt32(reader.GetOrdinal("artistID"))
                                    };

                                    trackArtists.Add(trackArtist);
                                }
                            }
                        }
                    }

                    return trackArtists;
                },
                $"Database operation: Get track-artist relationships for artist ID {artistID}",
                new List<TrackArtist>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task AddTrackArtist(TrackArtist trackArtist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackArtist == null)
                    {
                        throw new ArgumentNullException(nameof(trackArtist), "Cannot add null track-artist relationship");
                    }

                    if (trackArtist.TrackID <= 0 || trackArtist.ArtistID <= 0)
                    {
                        throw new ArgumentException("TrackID and ArtistID must be valid", nameof(trackArtist));
                    }

                    // Check if relationship already exists to avoid duplicates
                    var existingRelationship = await GetTrackArtist(trackArtist.TrackID, trackArtist.ArtistID);
                    if (existingRelationship != null)
                    {
                        return; // Relationship already exists, no need to add
                    }

                    // Verify track and artist exist
                    bool trackAndArtistExist = await VerifyTrackAndArtistExistAsync(
                        trackArtist.TrackID, trackArtist.ArtistID);

                    if (!trackAndArtistExist)
                    {
                        throw new InvalidOperationException(
                            $"Cannot create relationship: Track ID {trackArtist.TrackID} " +
                            $"or Artist ID {trackArtist.ArtistID} does not exist");
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "INSERT INTO TrackArtist (trackID, artistID) VALUES (@trackID, @artistID)";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackArtist.TrackID);
                            cmd.Parameters.AddWithValue("artistID", trackArtist.ArtistID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Add track-artist relationship: Track ID {trackArtist?.TrackID}, Artist ID {trackArtist?.ArtistID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteTrackArtist(int trackID, int artistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0 || artistID <= 0)
                    {
                        throw new ArgumentException("TrackID and ArtistID must be valid");
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "DELETE FROM TrackArtist WHERE trackID = @trackID AND artistID = @artistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);
                            cmd.Parameters.AddWithValue("artistID", artistID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Delete track-artist relationship: Track ID {trackID}, Artist ID {artistID}",
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task DeleteAllTrackArtistsForTrack(int trackID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0)
                    {
                        throw new ArgumentException("TrackID must be valid");
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "DELETE FROM TrackArtist WHERE trackID = @trackID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Delete all track-artist relationships for track ID {trackID}",
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task DeleteAllTrackArtistsForArtist(int artistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artistID <= 0)
                    {
                        throw new ArgumentException("ArtistID must be valid");
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "DELETE FROM TrackArtist WHERE artistID = @artistID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("artistID", artistID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Delete all track-artist relationships for artist ID {artistID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task<bool> VerifyTrackAndArtistExistAsync(int trackID, int artistID)
        {
            try
            {
                using (var db = new DbConnection(_errorHandlingService))
                {
                    // Check if track exists
                    string trackQuery = "SELECT COUNT(*) FROM Tracks WHERE trackID = @trackID";
                    using (var cmd = new NpgsqlCommand(trackQuery, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackID);
                        int trackCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        if (trackCount == 0)
                        {
                            return false;
                        }
                    }

                    // Check if artist exists
                    string artistQuery = "SELECT COUNT(*) FROM Artists WHERE artistID = @artistID";
                    using (var cmd = new NpgsqlCommand(artistQuery, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("artistID", artistID);
                        int artistCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        return artistCount > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error verifying track and artist existence",
                    $"Failed to verify if track ID {trackID} and artist ID {artistID} exist: {ex.Message}",
                    ex,
                    false);
                return false;
            }
        }
    }
}