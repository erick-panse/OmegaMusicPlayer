using Npgsql;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System;
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
                        string query = "SELECT * FROM GlobalConfig LIMIT 1";
                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            var config = new GlobalConfig
                            {
                                ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                LastUsedProfile = !reader.IsDBNull(reader.GetOrdinal("LastUsedProfile"))
                                    ? reader.GetInt32(reader.GetOrdinal("LastUsedProfile"))
                                    : null,
                                LanguagePreference = reader.GetString(reader.GetOrdinal("LanguagePreference"))
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
                            UPDATE GlobalConfig SET 
                                LastUsedProfile = @LastUsedProfile,
                                LanguagePreference = @LanguagePreference
                            WHERE ID = @ID";

                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        cmd.Parameters.AddWithValue("ID", config.ID);
                        cmd.Parameters.AddWithValue("LastUsedProfile", config.LastUsedProfile ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("LanguagePreference", config.LanguagePreference);

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
                        string query = @"
                            INSERT INTO GlobalConfig (LanguagePreference)
                            VALUES (@LanguagePreference)
                            RETURNING ID";

                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        cmd.Parameters.AddWithValue("LanguagePreference", "en");

                        return (int)await cmd.ExecuteScalarAsync();
                    }
                },
                "Creating default global configuration",
                -1,  // Return -1 as error value
                ErrorSeverity.Critical);
        }
    }
}