using Npgsql;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TrackGenreRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackGenreRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<TrackGenre> GetTrackGenre(int trackID, int genreID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0 || genreID <= 0)
                    {
                        return null;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM TrackGenre WHERE trackID = @trackID AND genreID = @genreID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);
                            cmd.Parameters.AddWithValue("genreID", genreID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return new TrackGenre
                                    {
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        GenreID = reader.GetInt32(reader.GetOrdinal("genreID"))
                                    };
                                }
                            }
                        }
                    }
                    return null;
                },
                $"Database operation: Get track-genre relationship: Track ID {trackID}, Genre ID {genreID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackGenre>> GetAllTrackGenres()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var trackGenres = new List<TrackGenre>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM TrackGenre";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var trackGenre = new TrackGenre
                                    {
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        GenreID = reader.GetInt32(reader.GetOrdinal("genreID"))
                                    };

                                    trackGenres.Add(trackGenre);
                                }
                            }
                        }
                    }

                    return trackGenres;
                },
                "Database operation: Get all track-genre relationships",
                new List<TrackGenre>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<List<TrackGenre>> GetTrackGenresByTrackId(int trackID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0)
                    {
                        return new List<TrackGenre>();
                    }

                    var trackGenres = new List<TrackGenre>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM TrackGenre WHERE trackID = @trackID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var trackGenre = new TrackGenre
                                    {
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        GenreID = reader.GetInt32(reader.GetOrdinal("genreID"))
                                    };

                                    trackGenres.Add(trackGenre);
                                }
                            }
                        }
                    }

                    return trackGenres;
                },
                $"Database operation: Get track-genre relationships for track ID {trackID}",
                new List<TrackGenre>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<List<TrackGenre>> GetTrackGenresByGenreId(int genreID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genreID <= 0)
                    {
                        return new List<TrackGenre>();
                    }

                    var trackGenres = new List<TrackGenre>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM TrackGenre WHERE genreID = @genreID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("genreID", genreID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var trackGenre = new TrackGenre
                                    {
                                        TrackID = reader.GetInt32(reader.GetOrdinal("trackID")),
                                        GenreID = reader.GetInt32(reader.GetOrdinal("genreID"))
                                    };

                                    trackGenres.Add(trackGenre);
                                }
                            }
                        }
                    }

                    return trackGenres;
                },
                $"Database operation: Get track-genre relationships for genre ID {genreID}",
                new List<TrackGenre>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task AddTrackGenre(TrackGenre trackGenre)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackGenre == null)
                    {
                        throw new ArgumentNullException(nameof(trackGenre), "Cannot add null track-genre relationship");
                    }

                    if (trackGenre.TrackID <= 0 || trackGenre.GenreID <= 0)
                    {
                        throw new ArgumentException("TrackID and GenreID must be valid", nameof(trackGenre));
                    }

                    // Check if relationship already exists to avoid duplicates
                    var existingRelationship = await GetTrackGenre(trackGenre.TrackID, trackGenre.GenreID);
                    if (existingRelationship != null)
                    {
                        return; // Relationship already exists, no need to add
                    }

                    // Verify track and genre exist
                    bool trackAndGenreExist = await VerifyTrackAndGenreExistAsync(
                        trackGenre.TrackID, trackGenre.GenreID);

                    if (!trackAndGenreExist)
                    {
                        throw new InvalidOperationException(
                            $"Cannot create relationship: Track ID {trackGenre.TrackID} " +
                            $"or Genre ID {trackGenre.GenreID} does not exist");
                    }

                    // Remove any existing genre associations for this track (assuming a track can only have one genre)
                    await DeleteExistingTrackGenres(trackGenre.TrackID);

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "INSERT INTO TrackGenre (trackID, genreID) VALUES (@trackID, @genreID)";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackGenre.TrackID);
                            cmd.Parameters.AddWithValue("genreID", trackGenre.GenreID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Add track-genre relationship: Track ID {trackGenre?.TrackID}, Genre ID {trackGenre?.GenreID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteTrackGenre(int trackID, int genreID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0 || genreID <= 0)
                    {
                        throw new ArgumentException("TrackID and GenreID must be valid");
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "DELETE FROM TrackGenre WHERE trackID = @trackID AND genreID = @genreID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);
                            cmd.Parameters.AddWithValue("genreID", genreID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Delete track-genre relationship: Track ID {trackID}, Genre ID {genreID}",
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task DeleteAllTrackGenresForTrack(int trackID)
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
                        string query = "DELETE FROM TrackGenre WHERE trackID = @trackID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("trackID", trackID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Delete all track-genre relationships for track ID {trackID}",
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task DeleteAllTrackGenresForGenre(int genreID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genreID <= 0)
                    {
                        throw new ArgumentException("GenreID must be valid");
                    }

                    // Get "Unknown" genre
                    int unknownGenreId = await GetUnknownGenreId();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // If the provided genreID is the "Unknown" genre, don't delete anything
                        if (genreID == unknownGenreId)
                        {
                            return;
                        }

                        // Update track-genre relationships to point to unknown genre
                        string updateQuery = @"
                            UPDATE TrackGenre 
                            SET genreID = @unknownGenreId 
                            WHERE genreID = @genreID";

                        using (var cmd = new NpgsqlCommand(updateQuery, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("unknownGenreId", unknownGenreId);
                            cmd.Parameters.AddWithValue("genreID", genreID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Reassign track-genre relationships from genre ID {genreID} to Unknown genre",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task<bool> VerifyTrackAndGenreExistAsync(int trackID, int genreID)
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

                    // Check if genre exists
                    string genreQuery = "SELECT COUNT(*) FROM Genre WHERE genreID = @genreID";
                    using (var cmd = new NpgsqlCommand(genreQuery, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("genreID", genreID);
                        int genreCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                        return genreCount > 0;
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error verifying track and genre existence",
                    $"Failed to verify if track ID {trackID} and genre ID {genreID} exist: {ex.Message}",
                    ex,
                    false);
                return false;
            }
        }

        private async Task DeleteExistingTrackGenres(int trackID)
        {
            try
            {
                using (var db = new DbConnection(_errorHandlingService))
                {
                    string query = "DELETE FROM TrackGenre WHERE trackID = @trackID";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("trackID", trackID);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error deleting existing track-genre relationships",
                    $"Failed to delete existing genre associations for track ID {trackID}: {ex.Message}",
                    ex,
                    false);
            }
        }

        private async Task<int> GetUnknownGenreId()
        {
            try
            {
                using (var db = new DbConnection(_errorHandlingService))
                {
                    // Try to get the "Unknown" genre
                    string query = "SELECT genreID FROM Genre WHERE genreName = 'Unknown'";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        var result = await cmd.ExecuteScalarAsync();
                        if (result != null && result != DBNull.Value)
                        {
                            return Convert.ToInt32(result);
                        }
                    }

                    // If it doesn't exist, create it
                    string insertQuery = @"
                        INSERT INTO Genre (genreName)
                        VALUES ('Unknown')
                        RETURNING genreID";

                    using (var cmd = new NpgsqlCommand(insertQuery, db.dbConn))
                    {
                        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error getting Unknown genre",
                    $"Failed to get or create Unknown genre: {ex.Message}",
                    ex,
                    false);
                return 0; // Return 0 as fallback
            }
        }
    }
}