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
    public class DirectoriesRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        // Query constants to avoid SQL injection and improve maintainability
        // Updated to use lowercase table/column names for Entity Framework compatibility
        private const string SQL_GET_DIRECTORY_BY_ID = "SELECT dirid, dirpath FROM directories WHERE dirid = @dirID";
        private const string SQL_GET_ALL_DIRECTORIES = "SELECT dirid, dirpath FROM directories ORDER BY dirpath";
        private const string SQL_INSERT_DIRECTORY = "INSERT INTO directories (dirpath) VALUES (@dirPath)";
        private const string SQL_DELETE_DIRECTORY = "DELETE FROM directories WHERE dirid = @dirID";
        private const string SQL_CHECK_PATH_EXISTS = "SELECT COUNT(*) FROM directories WHERE dirpath LIKE @dirPath COLLATE NOCASE";

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
                        var parameters = new Dictionary<string, object>
                        {
                            ["@dirID"] = dirID
                        };

                        using var cmd = db.CreateCommand(SQL_GET_DIRECTORY_BY_ID, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            return new Directories
                            {
                                DirID = reader.GetInt32("dirid"),
                                DirPath = reader.GetString("dirpath")
                            };
                        }
                        return null;
                    }
                },
                $"Getting directory with ID {dirID}",
                null,
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
                        using var cmd = db.CreateCommand(SQL_GET_ALL_DIRECTORIES);
                        using var reader = await cmd.ExecuteReaderAsync();

                        while (await reader.ReadAsync())
                        {
                            var directory = new Directories
                            {
                                DirID = reader.GetInt32("dirid"),
                                DirPath = reader.GetString("dirpath")
                            };

                            directories.Add(directory);
                        }
                    }

                    return directories;
                },
                "Getting all directories",
                new List<Directories>(),
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

                        var parameters = new Dictionary<string, object>
                        {
                            ["@dirPath"] = directory.DirPath
                        };

                        using var cmd = db.CreateCommand(SQL_INSERT_DIRECTORY, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Get the inserted ID using SQLite's last_insert_rowid()
                        using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                        var result = await idCmd.ExecuteScalarAsync();
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
                        var parameters = new Dictionary<string, object>
                        {
                            ["@dirID"] = dirID
                        };

                        using var cmd = db.CreateCommand(SQL_DELETE_DIRECTORY, parameters);
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
                // Use LIKE with COLLATE NOCASE for case-insensitive comparison in SQLite
                var parameters = new Dictionary<string, object>
                {
                    ["@dirPath"] = dirPath
                };

                using var cmd = db.CreateCommand(SQL_CHECK_PATH_EXISTS, parameters);
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