using Avalonia.Controls;
using Npgsql;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Data;
using System.Linq;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class DirectoriesRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        // Query constants to avoid SQL injection and improve maintainability
        private const string SQL_GET_DIRECTORY_BY_ID = "SELECT * FROM Directories WHERE dirID = @dirID";
        private const string SQL_GET_ALL_DIRECTORIES = "SELECT * FROM Directories ORDER BY dirPath";
        private const string SQL_INSERT_DIRECTORY = "INSERT INTO Directories (dirPath) VALUES (@dirPath) RETURNING dirID";
        private const string SQL_DELETE_DIRECTORY = "DELETE FROM Directories WHERE dirID = @dirID";

        public DirectoriesRepository(IErrorHandlingService errorHandlingService = null)
        {
            _errorHandlingService = errorHandlingService;
        }

        /// <summary>
        /// Gets a directory by ID with error handling
        /// </summary>
        public async Task<Directories> GetDirectoryById(int dirID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        using var cmd = new NpgsqlCommand(SQL_GET_DIRECTORY_BY_ID, db.dbConn);
                        cmd.Parameters.AddWithValue("dirID", dirID);

                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync())
                        {
                            return new Directories
                            {
                                DirID = reader.GetInt32(reader.GetOrdinal("dirID")),
                                DirPath = reader.GetString(reader.GetOrdinal("dirPath"))
                            };
                        }
                        return null;
                    }
                },
                $"Getting directory with ID {dirID}",
                null, // Return null as fallback
                ErrorSeverity.NonCritical
            );
        }

        /// <summary>
        /// Gets all directories with error handling
        /// </summary>
        public async Task<List<Directories>> GetAllDirectories()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var directories = new List<Directories>();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        using var cmd = new NpgsqlCommand(SQL_GET_ALL_DIRECTORIES, db.dbConn);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var directory = new Directories
                            {
                                DirID = reader.GetInt32(reader.GetOrdinal("dirID")),
                                DirPath = reader.GetString(reader.GetOrdinal("dirPath"))
                            };

                            directories.Add(directory);
                        }
                    }

                    return directories;
                },
                "Getting all directories",
                new List<Directories>(), // Return empty list as fallback
                ErrorSeverity.NonCritical
            );
        }

        /// <summary>
        /// Adds a directory with error handling
        /// </summary>
        public async Task<int> AddDirectory(Directories directory)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (directory == null || string.IsNullOrWhiteSpace(directory.DirPath))
                    {
                        throw new ArgumentException("Directory path cannot be null or empty");
                    }

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // Check for duplicate first to avoid unique constraint violations
                        if (await DirectoryPathExistsAsync(db, directory.DirPath))
                        {
                            throw new InvalidOperationException($"Directory path already exists: {directory.DirPath}");
                        }

                        using var cmd = new NpgsqlCommand(SQL_INSERT_DIRECTORY, db.dbConn);
                        cmd.Parameters.AddWithValue("dirPath", directory.DirPath);

                        var result = await cmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
                    }
                },
                $"Adding directory: {directory?.DirPath ?? "null"}",
                -1, // Return -1 on error
                ErrorSeverity.NonCritical
            );
        }

        /// <summary>
        /// Deletes a directory with error handling
        /// </summary>
        public async Task DeleteDirectory(int dirID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        using var cmd = new NpgsqlCommand(SQL_DELETE_DIRECTORY, db.dbConn);
                        cmd.Parameters.AddWithValue("dirID", dirID);

                        int rowsAffected = await cmd.ExecuteNonQueryAsync();

                        if (rowsAffected == 0)
                        {
                            _errorHandlingService?.LogError(
                                ErrorSeverity.NonCritical,
                                "Directory not found",
                                $"Directory with ID {dirID} was not found in the database.",
                                null,
                                false);
                        }
                    }
                },
                $"Deleting directory with ID {dirID}",
                ErrorSeverity.NonCritical
            );
        }

        /// <summary>
        /// Checks if a directory path already exists (case-insensitive)
        /// </summary>
        private async Task<bool> DirectoryPathExistsAsync(DbConnection db, string dirPath)
        {
            try
            {
                // Use ILIKE for case-insensitive comparison in PostgreSQL
                const string SQL_CHECK_PATH_EXISTS = "SELECT COUNT(*) FROM Directories WHERE dirPath ILIKE @dirPath";

                using var cmd = new NpgsqlCommand(SQL_CHECK_PATH_EXISTS, db.dbConn);
                cmd.Parameters.AddWithValue("dirPath", dirPath);

                var result = await cmd.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
            catch (Exception ex)
            {
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error checking directory path existence",
                    ex.Message,
                    ex,
                    false);

                // Assume it doesn't exist if there's an error
                return false;
            }
        }
    }
}