using Microsoft.Data.Sqlite;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class GlobalConfigRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;
        private GlobalConfig _cachedConfig = null;

        public GlobalConfigRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        /// <summary>
        /// Retrieves the global configuration settings from the database.
        /// Falls back to cached config or default config on failure.
        /// </summary>
        public async Task<GlobalConfig> GetGlobalConfig()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = "SELECT id, lastusedprofile, languagepreference FROM globalconfig LIMIT 1";

                        using var cmd = db.CreateCommand(query);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            var config = new GlobalConfig
                            {
                                ID = reader.GetInt32("id"),
                                LastUsedProfile = !reader.IsDBNull("lastusedprofile")
                                    ? reader.GetInt32("lastusedprofile")
                                    : null,
                                LanguagePreference = reader.GetString("languagepreference")
                            };

                            // Cache config for fallback in case of later failures
                            _cachedConfig = config;
                            return config;
                        }
                        return null;
                    }
                },
                "Fetching global configuration",
                _cachedConfig,
                ErrorSeverity.Critical);
        }

        /// <summary>
        /// Updates the global configuration settings in the database.
        /// </summary>
        public async Task UpdateGlobalConfig(GlobalConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            UPDATE globalconfig SET 
                                lastusedprofile = @LastUsedProfile,
                                languagepreference = @LanguagePreference
                            WHERE id = @ID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@ID"] = config.ID,
                            ["@LastUsedProfile"] = config.LastUsedProfile,
                            ["@LanguagePreference"] = config.LanguagePreference
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Update cache on successful write
                        _cachedConfig = new GlobalConfig
                        {
                            ID = config.ID,
                            LastUsedProfile = config.LastUsedProfile,
                            LanguagePreference = config.LanguagePreference
                        };
                    }
                },
                "Updating global configuration",
                ErrorSeverity.Critical);
        }

        /// <summary>
        /// Creates a default global configuration record in the database.
        /// </summary>
        public async Task<int> CreateDefaultGlobalConfig()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        // SQLite doesn't support RETURNING clause, so we'll use INSERT then get last_insert_rowid()
                        string query = @"
                            INSERT INTO globalconfig (languagepreference)
                            VALUES (@LanguagePreference)";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@LanguagePreference"] = "en"
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Get the inserted ID using SQLite's last_insert_rowid()
                        using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                        var result = await idCmd.ExecuteScalarAsync();
                        return Convert.ToInt32(result);
                    }
                },
                "Creating default global configuration",
                -1,  // Return -1 as error value
                ErrorSeverity.Critical);
        }
    }
}