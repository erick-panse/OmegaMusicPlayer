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
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = "SELECT genreid, genrename FROM genre WHERE genrename = @genreName";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@genreName"] = genreName
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return new Genres
                            {
                                GenreID = reader.GetInt32("genreid"),
                                GenreName = reader.GetString("genrename")
                            };
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
                        string query = "SELECT genreid, genrename FROM genre WHERE genreid = @genreID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@genreID"] = genreID
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return new Genres
                            {
                                GenreID = reader.GetInt32("genreid"),
                                GenreName = reader.GetString("genrename")
                            };
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
                        string query = "SELECT genreid, genrename FROM genre ORDER BY genrename";

                        using var cmd = db.CreateCommand(query);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var genre = new Genres
                            {
                                GenreID = reader.GetInt32("genreid"),
                                GenreName = reader.GetString("genrename")
                            };
                            genres.Add(genre);
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
                        string query = "INSERT INTO genre (genrename) VALUES (@genreName)";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@genreName"] = genreName
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Get the inserted ID using SQLite's last_insert_rowid()
                        using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                        var result = await idCmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
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
                        string query = "UPDATE genre SET genrename = @genreName WHERE genreid = @genreID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@genreID"] = genre.GenreID,
                            ["@genreName"] = genreName
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();
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
                        using var transaction = db.dbConn.BeginTransaction();
                        try
                        {
                            // First get the unknown genre ID for reassignment
                            var unknownGenre = await GetOrCreateUnknownGenre();
                            int unknownGenreId = unknownGenre?.GenreID ?? 0;

                            // Update track-genre relationships to point to unknown genre
                            string updateTrackGenreQuery = @"
                                UPDATE trackgenre 
                                SET genreid = @unknownGenreId 
                                WHERE genreid = @genreID";

                            var parameters1 = new Dictionary<string, object>
                            {
                                ["@unknownGenreId"] = unknownGenreId,
                                ["@genreID"] = genreID
                            };

                            using var cmd1 = db.CreateCommand(updateTrackGenreQuery, parameters1);
                            cmd1.Transaction = transaction;
                            await cmd1.ExecuteNonQueryAsync();

                            // Update tracks to set their genreID to unknown genre
                            string updateTracksQuery = @"
                                UPDATE tracks 
                                SET genreid = @unknownGenreId 
                                WHERE genreid = @genreID";

                            var parameters2 = new Dictionary<string, object>
                            {
                                ["@unknownGenreId"] = unknownGenreId,
                                ["@genreID"] = genreID
                            };

                            using var cmd2 = db.CreateCommand(updateTracksQuery, parameters2);
                            cmd2.Transaction = transaction;
                            await cmd2.ExecuteNonQueryAsync();

                            // Then delete the genre
                            string deleteQuery = "DELETE FROM genre WHERE genreid = @genreID";

                            var parameters3 = new Dictionary<string, object>
                            {
                                ["@genreID"] = genreID
                            };

                            using var cmd3 = db.CreateCommand(deleteQuery, parameters3);
                            cmd3.Transaction = transaction;
                            await cmd3.ExecuteNonQueryAsync();

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw;
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
                        string query = "SELECT genreid, genrename FROM genre WHERE genrename = 'Unknown'";

                        using var cmd = db.CreateCommand(query);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return new Genres
                            {
                                GenreID = reader.GetInt32("genreid"),
                                GenreName = reader.GetString("genrename")
                            };
                        }

                        // Close the reader before executing another command
                        reader.Close();

                        // If it doesn't exist, create it
                        string insertQuery = "INSERT INTO genre (genrename) VALUES ('Unknown')";

                        using var insertCmd = db.CreateCommand(insertQuery);
                        await insertCmd.ExecuteNonQueryAsync();

                        // Get the inserted ID
                        using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                        var result = await idCmd.ExecuteScalarAsync();
                        var genreID = Convert.ToInt32(result);

                        return new Genres
                        {
                            GenreID = genreID,
                            GenreName = "Unknown"
                        };
                    }
                },
                "Database operation: Get or create Unknown genre",
                null,
                ErrorSeverity.NonCritical,
                false);
        }
    }
}