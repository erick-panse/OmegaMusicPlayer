using Npgsql;
using OmegaPlayer.Core.Models;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class GlobalConfigRepository
    {
        public async Task<GlobalConfig> GetGlobalConfig()
        {
            try
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching global config: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateGlobalConfig(GlobalConfig config)
        {
            try
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating global config: {ex.Message}");
                throw;
            }
        }

        public async Task<int> CreateDefaultGlobalConfig()
        {
            try
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating default global config: {ex.Message}");
                throw;
            }
        }
    }

}