using Npgsql;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class GenresRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public GenresRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Genres> GetGenreByName(string genreName)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(genreName))
                    {
                        return await GetOrCreateUnknownGenre();
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Genre WHERE genreName = @genreName";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("genreName", genreName);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return new Genres
                                    {
                                        GenreID = reader.GetInt32(reader.GetOrdinal("genreID")),
                                        GenreName = reader.GetString(reader.GetOrdinal("genreName"))
                                    };
                                }
                            }
                        }
                    }
                    return null;
                },
                $"Database operation: Get genre by name '{genreName}'",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Genres> GetGenreById(int genreID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Genre WHERE genreID = @genreID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("genreID", genreID);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return new Genres
                                    {
                                        GenreID = reader.GetInt32(reader.GetOrdinal("genreID")),
                                        GenreName = reader.GetString(reader.GetOrdinal("genreName"))
                                    };
                                }
                            }
                        }
                    }
                    return null;
                },
                $"Database operation: Get genre with ID {genreID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Genres>> GetAllGenres()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var genres = new List<Genres>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Genre ORDER BY genreName";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                while (await reader.ReadAsync())
                                {
                                    var genre = new Genres
                                    {
                                        GenreID = reader.GetInt32(reader.GetOrdinal("genreID")),
                                        GenreName = reader.GetString(reader.GetOrdinal("genreName"))
                                    };
                                    genres.Add(genre);
                                }
                            }
                        }
                    }

                    // If no genres exist, create the Unknown genre
                    if (genres.Count == 0)
                    {
                        var unknownGenre = await GetOrCreateUnknownGenre();
                        if (unknownGenre != null)
                        {
                            genres.Add(unknownGenre);
                        }
                    }

                    return genres;
                },
                "Database operation: Get all genres",
                new List<Genres>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> AddGenre(Genres genre)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genre == null)
                    {
                        throw new ArgumentNullException(nameof(genre), "Cannot add null genre to database");
                    }

                    // If genre name is empty or null, use "Unknown"
                    string genreName = !string.IsNullOrWhiteSpace(genre.GenreName) ?
                        genre.GenreName : "Unknown";

                    // Check if genre already exists to avoid duplicates
                    var existingGenre = await GetGenreByName(genreName);
                    if (existingGenre != null)
                    {
                        return existingGenre.GenreID;
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            INSERT INTO Genre (genreName)
                            VALUES (@genreName)
                            RETURNING genreID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("genreName", genreName);
                            var genreID = (int)await cmd.ExecuteScalarAsync();
                            return genreID;
                        }
                    }
                },
                $"Database operation: Add genre '{genre?.GenreName ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateGenre(Genres genre)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genre == null || genre.GenreID <= 0)
                    {
                        throw new ArgumentException("Cannot update null genre or genre with invalid ID", nameof(genre));
                    }

                    // If attempting to update to empty name, use "Unknown"
                    string genreName = !string.IsNullOrWhiteSpace(genre.GenreName) ?
                        genre.GenreName : "Unknown";

                    // Don't allow renaming the "Unknown" genre to something else
                    var currentGenre = await GetGenreById(genre.GenreID);
                    if (currentGenre != null && currentGenre.GenreName == "Unknown" &&
                        genreName != "Unknown")
                    {
                        throw new InvalidOperationException("Cannot rename the 'Unknown' genre");
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            UPDATE Genre SET 
                                genreName = @genreName
                            WHERE genreID = @genreID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("genreID", genre.GenreID);
                            cmd.Parameters.AddWithValue("genreName", genreName);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Update genre '{genre?.GenreName ?? "Unknown"}' (ID: {genre?.GenreID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteGenre(int genreID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genreID <= 0)
                    {
                        throw new ArgumentException("Cannot delete genre with invalid ID", nameof(genreID));
                    }

                    // Check if this is the "Unknown" genre - don't allow deletion
                    var genre = await GetGenreById(genreID);
                    if (genre != null && genre.GenreName == "Unknown")
                    {
                        throw new InvalidOperationException("Cannot delete the 'Unknown' genre");
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // First get the unknown genre ID for reassignment
                        var unknownGenre = await GetOrCreateUnknownGenre();
                        int unknownGenreId = unknownGenre?.GenreID ?? 0;

                        // Update track-genre relationships to point to unknown genre
                        string updateTrackGenreQuery = @"
                            UPDATE TrackGenre 
                            SET genreID = @unknownGenreId 
                            WHERE genreID = @genreID";

                        using (var cmd = new NpgsqlCommand(updateTrackGenreQuery, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("unknownGenreId", unknownGenreId);
                            cmd.Parameters.AddWithValue("genreID", genreID);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Update tracks to set their genreID to unknown genre
                        string updateTracksQuery = @"
                            UPDATE Tracks 
                            SET genreID = @unknownGenreId 
                            WHERE genreID = @genreID";

                        using (var cmd = new NpgsqlCommand(updateTracksQuery, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("unknownGenreId", unknownGenreId);
                            cmd.Parameters.AddWithValue("genreID", genreID);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        // Then delete the genre
                        string query = "DELETE FROM Genre WHERE genreID = @genreID";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            cmd.Parameters.AddWithValue("genreID", genreID);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                },
                $"Database operation: Delete genre with ID {genreID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task<Genres> GetOrCreateUnknownGenre()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Try to get the "Unknown" genre
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM Genre WHERE genreName = 'Unknown'";

                        using (var cmd = new NpgsqlCommand(query, db.dbConn))
                        {
                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    return new Genres
                                    {
                                        GenreID = reader.GetInt32(reader.GetOrdinal("genreID")),
                                        GenreName = reader.GetString(reader.GetOrdinal("genreName"))
                                    };
                                }
                            }
                        }

                        // If it doesn't exist, create it
                        string insertQuery = @"
                            INSERT INTO Genre (genreName)
                            VALUES ('Unknown')
                            RETURNING genreID";

                        using (var cmd = new NpgsqlCommand(insertQuery, db.dbConn))
                        {
                            var genreID = (int)await cmd.ExecuteScalarAsync();
                            return new Genres
                            {
                                GenreID = genreID,
                                GenreName = "Unknown"
                            };
                        }
                    }
                },
                "Database operation: Get or create Unknown genre",
                null,
                ErrorSeverity.NonCritical,
                false);
        }
    }
}