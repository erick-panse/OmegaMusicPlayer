using Microsoft.Data.Sqlite;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

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
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = "SELECT trackid, genreid FROM trackgenre WHERE trackid = @trackID AND genreid = @genreID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@trackID"] = trackID,
                            ["@genreID"] = genreID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return new TrackGenre
                            {
                                TrackID = reader.GetInt32("trackid"),
                                GenreID = reader.GetInt32("genreid")
                            };
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
                        string query = "SELECT trackid, genreid FROM trackgenre";

                        using var cmd = db.CreateCommand(query);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var trackGenre = new TrackGenre
                            {
                                TrackID = reader.GetInt32("trackid"),
                                GenreID = reader.GetInt32("genreid")
                            };

                            trackGenres.Add(trackGenre);
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
                        string query = "SELECT trackid, genreid FROM trackgenre WHERE trackid = @trackID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@trackID"] = trackID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var trackGenre = new TrackGenre
                            {
                                TrackID = reader.GetInt32("trackid"),
                                GenreID = reader.GetInt32("genreid")
                            };

                            trackGenres.Add(trackGenre);
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
                        string query = "SELECT trackid, genreid FROM trackgenre WHERE genreid = @genreID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@genreID"] = genreID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var trackGenre = new TrackGenre
                            {
                                TrackID = reader.GetInt32("trackid"),
                                GenreID = reader.GetInt32("genreid")
                            };

                            trackGenres.Add(trackGenre);
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
                        string query = "INSERT INTO trackgenre (trackid, genreid) VALUES (@trackID, @genreID)";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@trackID"] = trackGenre.TrackID,
                            ["@genreID"] = trackGenre.GenreID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
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
                        string query = "DELETE FROM trackgenre WHERE trackid = @trackID AND genreid = @genreID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@trackID"] = trackID,
                            ["@genreID"] = genreID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
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
                        string query = "DELETE FROM trackgenre WHERE trackid = @trackID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@trackID"] = trackID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
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
                            UPDATE trackgenre 
                            SET genreid = @unknownGenreId 
                            WHERE genreid = @genreID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@unknownGenreId"] = unknownGenreId,
                            ["@genreID"] = genreID
                        };

                        using var cmd = db.CreateCommand(updateQuery, parameters);
                        await cmd.ExecuteNonQueryAsync();
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
                    string trackQuery = "SELECT COUNT(*) FROM tracks WHERE trackid = @trackID";
                    var trackParameters = new Dictionary<string, object>
                    {
                        ["@trackID"] = trackID
                    };

                    using var trackCmd = db.CreateCommand(trackQuery, trackParameters);
                    int trackCount = Convert.ToInt32(await trackCmd.ExecuteScalarAsync());
                    if (trackCount == 0)
                    {
                        return false;
                    }

                    // Check if genre exists
                    string genreQuery = "SELECT COUNT(*) FROM genre WHERE genreid = @genreID";
                    var genreParameters = new Dictionary<string, object>
                    {
                        ["@genreID"] = genreID
                    };

                    using var genreCmd = db.CreateCommand(genreQuery, genreParameters);
                    int genreCount = Convert.ToInt32(await genreCmd.ExecuteScalarAsync());
                    return genreCount > 0;
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
                    string query = "DELETE FROM trackgenre WHERE trackid = @trackID";

                    var parameters = new Dictionary<string, object>
                    {
                        ["@trackID"] = trackID
                    };

                    using var cmd = db.CreateCommand(query, parameters);
                    await cmd.ExecuteNonQueryAsync();
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
                    string query = "SELECT genreid FROM genre WHERE genrename = 'Unknown'";

                    using var cmd = db.CreateCommand(query);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        return Convert.ToInt32(result);
                    }

                    // If it doesn't exist, create it
                    string insertQuery = "INSERT INTO genre (genrename) VALUES ('Unknown')";

                    using var insertCmd = db.CreateCommand(insertQuery);
                    await insertCmd.ExecuteNonQueryAsync();

                    // Get the inserted ID using SQLite's last_insert_rowid()
                    using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                    var insertResult = await idCmd.ExecuteScalarAsync();
                    return Convert.ToInt32(insertResult);
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