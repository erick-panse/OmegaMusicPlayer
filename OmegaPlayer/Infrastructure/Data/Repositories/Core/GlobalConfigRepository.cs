using Npgsql;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class GlobalConfigRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        public GlobalConfigRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        public async Task<GlobalConfig> GetGlobalConfig()
        {
            return await _errorHandlingService.SafeExecuteAsync(async () => 
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM GlobalConfig LIMIT 1";
                    using var cmd = new NpgsqlCommand(query, db.dbConn);
                    using var reader = await cmd.ExecuteReaderAsync();

                    if (await reader.ReadAsync())
                    {
                        return new GlobalConfig
                        {
                            ID = reader.GetInt32(reader.GetOrdinal("ID")), 
                            LastUsedProfile = !reader.IsDBNull(reader.GetOrdinal("LastUsedProfile"))
                                ? reader.GetInt32(reader.GetOrdinal("LastUsedProfile"))
                                : null,
                            LanguagePreference = reader.GetString(reader.GetOrdinal("LanguagePreference"))
                        };
                    }
                    return null;
                }
            },
            "Error fetching global config",
            null,
            ErrorSeverity.Critical,
            false); // Don't show notification
        }

        public async Task UpdateGlobalConfig(GlobalConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                using (var db = new DbConnection())
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
                }
            },
            "Error updating global config",
            ErrorSeverity.Critical,
            false); // Don't show notification
            }

        public async Task<int> CreateDefaultGlobalConfig()
        {
            return await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                using (var db = new DbConnection())
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
            "Error creating default global config",
            0,
            ErrorSeverity.Critical,
            false); // Don't show notification
        }
    }
}