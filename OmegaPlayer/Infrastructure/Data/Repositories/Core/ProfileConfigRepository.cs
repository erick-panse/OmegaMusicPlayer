using Microsoft.Data.Sqlite;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Text.Json;
using System.Threading.Tasks;

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
                        // Use lowercase table and column names to match Entity Framework conventions
                        string query = @"SELECT id, profileid, equalizerpresets, lastvolume, theme, dynamicpause, 
                                        blacklistdirectory, viewstate, sortingstate 
                                        FROM profileconfig WHERE profileid = @ProfileID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@ProfileID"] = profileId
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        using var reader = await cmd.ExecuteReaderAsync();

                        if (await reader.ReadAsync())
                        {
                            var config = new ProfileConfig
                            {
                                ID = reader.GetInt32("id"),
                                ProfileID = reader.GetInt32("profileid"),
                                EqualizerPresets = !reader.IsDBNull("equalizerpresets") ? reader.GetString("equalizerpresets") : "{}",
                                LastVolume = reader.GetInt32("lastvolume"),
                                Theme = !reader.IsDBNull("theme") ? reader.GetString("theme") : "dark",
                                DynamicPause = reader.GetBoolean("dynamicpause"),
                                // Handle blacklist directory as comma-separated string
                                BlacklistDirectory = ParseBlacklistDirectory( !reader.IsDBNull("blacklistdirectory") ? reader.GetString("blacklistdirectory") : ""),
                                ViewState = !reader.IsDBNull("viewstate") ? reader.GetString("viewstate") : "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}",
                                SortingState = !reader.IsDBNull("sortingstate") ? reader.GetString("sortingstate") : "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
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
                    // Use ProfileConfig's default values to create new config
                    var config = new ProfileConfig
                    {
                        ProfileID = profileId
                    };

                    using (var db = new DbConnection(_errorHandlingService))
                    {
                        string query = @"
                            INSERT INTO profileconfig (profileid, equalizerpresets, lastvolume, theme, dynamicpause, 
                                                     blacklistdirectory, viewstate, sortingstate)
                            VALUES (@ProfileID, @EqualizerPresets, @LastVolume, @Theme, @DynamicPause, 
                                   @BlacklistDirectory, @ViewState, @SortingState)";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@ProfileID"] = profileId,
                            ["@EqualizerPresets"] = config.EqualizerPresets,
                            ["@LastVolume"] = config.LastVolume,
                            ["@Theme"] = config.Theme,
                            ["@DynamicPause"] = config.DynamicPause,
                            ["@BlacklistDirectory"] = SerializeBlacklistDirectory(config.BlacklistDirectory),
                            ["@ViewState"] = config.ViewState,
                            ["@SortingState"] = config.SortingState
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Get the inserted ID using SQLite's last_insert_rowid()
                        using var idCmd = db.CreateCommand("SELECT last_insert_rowid()");
                        var result = await idCmd.ExecuteScalarAsync();
                        var id = Convert.ToInt32(result);

                        // Update the config object and cache it
                        config.ID = id;
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
                            UPDATE profileconfig SET 
                                equalizerpresets = @EqualizerPresets,
                                lastvolume = @LastVolume,
                                theme = @Theme,
                                dynamicpause = @DynamicPause,
                                blacklistdirectory = @BlacklistDirectory,
                                viewstate = @ViewState,
                                sortingstate = @SortingState
                            WHERE profileid = @ProfileID";

                        var parameters = new Dictionary<string, object>
                        {
                            ["@ProfileID"] = config.ProfileID,
                            ["@EqualizerPresets"] = config.EqualizerPresets ?? "{}",
                            ["@LastVolume"] = config.LastVolume,
                            ["@Theme"] = config.Theme ?? "dark",
                            ["@DynamicPause"] = config.DynamicPause,
                            ["@BlacklistDirectory"] = SerializeBlacklistDirectory(config.BlacklistDirectory),
                            ["@ViewState"] = config.ViewState ?? "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}",
                            ["@SortingState"] = config.SortingState ?? "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
                        };

                        using var cmd = db.CreateCommand(query, parameters);
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
                        string query = "DELETE FROM profileconfig WHERE profileid = @ProfileID";
                        var parameters = new Dictionary<string, object>
                        {
                            ["@ProfileID"] = profileId
                        };

                        using var cmd = db.CreateCommand(query, parameters);
                        await cmd.ExecuteNonQueryAsync();

                        // Remove from cache
                        _configCache.TryRemove(profileId, out _);
                    }
                },
                $"Deleting configuration for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        #region Helper Methods

        /// <summary>
        /// Parses blacklist directory string into array for SQLite
        /// </summary>
        private string[] ParseBlacklistDirectory(string blacklistString)
        {
            if (string.IsNullOrEmpty(blacklistString))
                return Array.Empty<string>();

            try
            {
                // Try to parse as JSON first (for future compatibility)
                if (blacklistString.StartsWith("["))
                {
                    return JsonSerializer.Deserialize<string[]>(blacklistString) ?? Array.Empty<string>();
                }

                // Fall back to comma-separated parsing
                return blacklistString.Split(',', StringSplitOptions.RemoveEmptyEntries);
            }
            catch
            {
                // If parsing fails, return empty array
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Serializes blacklist directory array to string for SQLite
        /// </summary>
        private string SerializeBlacklistDirectory(string[] blacklistArray)
        {
            if (blacklistArray == null || blacklistArray.Length == 0)
                return "";

            try
            {
                // Store as JSON for better structure
                return JsonSerializer.Serialize(blacklistArray);
            }
            catch
            {
                // Fall back to comma-separated if JSON fails
                return string.Join(",", blacklistArray);
            }
        }

        #endregion
    }
}