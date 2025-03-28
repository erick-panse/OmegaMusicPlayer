using Npgsql;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using OmegaPlayer.Features.Library.Models;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class ProfileConfigRepository
    {
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache for profile configurations to provide fallback
        private readonly ConcurrentDictionary<int, ProfileConfig> _configCache = new ConcurrentDictionary<int, ProfileConfig>();

        public ProfileConfigRepository(IErrorHandlingService errorHandlingService)
        {
            _errorHandlingService = errorHandlingService;
        }

        /// <summary>
        /// Retrieves the profile configuration for a specific profile.
        /// Falls back to cached config or creates a default one if needed.
        /// </summary>
        public async Task<ProfileConfig> GetProfileConfig(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "SELECT * FROM ProfileConfig WHERE ProfileID = @ProfileID";
                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        cmd.Parameters.AddWithValue("ProfileID", profileId);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            var config = new ProfileConfig
                            {
                                ID = reader.GetInt32(reader.GetOrdinal("ID")),
                                ProfileID = reader.GetInt32(reader.GetOrdinal("ProfileID")),
                                EqualizerPresets = !reader.IsDBNull(reader.GetOrdinal("EqualizerPresets")) ? reader.GetString(reader.GetOrdinal("EqualizerPresets")) : "{}",
                                LastVolume = reader.GetInt32(reader.GetOrdinal("LastVolume")),
                                Theme = !reader.IsDBNull(reader.GetOrdinal("Theme")) ? reader.GetString(reader.GetOrdinal("Theme")) : "{}",
                                DynamicPause = reader.GetBoolean(reader.GetOrdinal("DynamicPause")),
                                BlacklistDirectory = !reader.IsDBNull(reader.GetOrdinal("BlacklistDirectory")) ? (string[])reader.GetValue(reader.GetOrdinal("BlacklistDirectory")) : Array.Empty<string>(),
                                ViewState = !reader.IsDBNull(reader.GetOrdinal("ViewState")) ? reader.GetString(reader.GetOrdinal("ViewState")) : "{\"tracks\": \"grid\"}",
                                SortingState = !reader.IsDBNull(reader.GetOrdinal("SortingState")) ? reader.GetString(reader.GetOrdinal("SortingState")) : "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
                            };

                            // Cache the retrieved configuration
                            _configCache[profileId] = config;
                            return config;
                        }
                        return null;
                    }
                },
                $"Fetching configuration for profile {profileId}",
                _configCache.TryGetValue(profileId, out var cachedConfig) ? cachedConfig : null,
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Creates a new profile configuration record in the database.
        /// </summary>
        public async Task<int> CreateProfileConfig(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // use ProfileConfig's default values to create new config
                    var config = new ProfileConfig();

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            INSERT INTO ProfileConfig (ProfileID, EqualizerPresets, LastVolume, Theme, DynamicPause, BlacklistDirectory, ViewState, SortingState)
                            VALUES (@ProfileID, @EqualizerPresets, @LastVolume, @Theme, @DynamicPause, @BlacklistDirectory, @ViewState, @SortingState)
                            RETURNING ID";

                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        cmd.Parameters.AddWithValue("ProfileID", profileId);
                        cmd.Parameters.AddWithValue("EqualizerPresets", NpgsqlTypes.NpgsqlDbType.Jsonb, config.EqualizerPresets);
                        cmd.Parameters.AddWithValue("LastVolume", config.LastVolume);
                        cmd.Parameters.AddWithValue("Theme", NpgsqlTypes.NpgsqlDbType.Jsonb, config.Theme);
                        cmd.Parameters.AddWithValue("DynamicPause", config.DynamicPause);
                        cmd.Parameters.AddWithValue("BlacklistDirectory", NpgsqlTypes.NpgsqlDbType.Jsonb, config.BlacklistDirectory);
                        cmd.Parameters.AddWithValue("ViewState", NpgsqlTypes.NpgsqlDbType.Jsonb, config.ViewState);
                        cmd.Parameters.AddWithValue("SortingState", NpgsqlTypes.NpgsqlDbType.Jsonb, config.SortingState);

                        var id = (int)await cmd.ExecuteScalarAsync();

                        // Cache the created configuration
                        config.ProfileID = id;
                        _configCache[profileId] = config;

                        return id;
                    }
                },
                $"Creating configuration for profile {profileId}",
                -1, // Return -1 to indicate failure
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates an existing profile configuration in the database.
        /// </summary>
        public async Task UpdateProfileConfig(ProfileConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            UPDATE ProfileConfig SET 
                                EqualizerPresets = @EqualizerPresets,
                                LastVolume = @LastVolume,
                                Theme = @Theme,
                                DynamicPause = @DynamicPause,
                                BlacklistDirectory = @BlacklistDirectory,
                                ViewState = @ViewState,
                                SortingState = @SortingState
                            WHERE ProfileID = @ProfileID";

                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        cmd.Parameters.AddWithValue("ProfileID", config.ProfileID);
                        cmd.Parameters.AddWithValue("EqualizerPresets", NpgsqlTypes.NpgsqlDbType.Jsonb, config.EqualizerPresets ?? "{}");
                        cmd.Parameters.AddWithValue("LastVolume", config.LastVolume);
                        cmd.Parameters.AddWithValue("Theme", NpgsqlTypes.NpgsqlDbType.Jsonb, config.Theme ?? "{}");
                        cmd.Parameters.AddWithValue("DynamicPause", config.DynamicPause);
                        cmd.Parameters.AddWithValue("BlacklistDirectory", config.BlacklistDirectory);
                        cmd.Parameters.AddWithValue("ViewState", NpgsqlTypes.NpgsqlDbType.Jsonb, config.ViewState ?? "{}");
                        cmd.Parameters.AddWithValue("SortingState", NpgsqlTypes.NpgsqlDbType.Jsonb, config.SortingState ?? "{}");

                        await cmd.ExecuteNonQueryAsync();

                        // Update the cache
                        _configCache[config.ProfileID] = new ProfileConfig
                        {
                            ID = config.ID,
                            ProfileID = config.ProfileID,
                            EqualizerPresets = config.EqualizerPresets,
                            LastVolume = config.LastVolume,
                            Theme = config.Theme,
                            DynamicPause = config.DynamicPause,
                            BlacklistDirectory = config.BlacklistDirectory,
                            ViewState = config.ViewState,
                            SortingState = config.SortingState
                        };
                    }
                },
                $"Updating configuration for profile {config.ProfileID}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Deletes a profile configuration from the database.
        /// </summary>
        public async Task DeleteProfileConfig(int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = "DELETE FROM ProfileConfig WHERE ProfileID = @ProfileID";
                        using var cmd = new NpgsqlCommand(query, db.dbConn);
                        cmd.Parameters.AddWithValue("ProfileID", profileId);
                        await cmd.ExecuteNonQueryAsync();

                        // Remove from cache
                        _configCache.TryRemove(profileId, out _);
                    }
                },
                $"Deleting configuration for profile {profileId}",
                ErrorSeverity.NonCritical);
        }
    }
}