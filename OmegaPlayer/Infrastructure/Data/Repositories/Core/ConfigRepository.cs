using Npgsql;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Core
{
    public class ConfigRepository
    {
        // Get a specific config setting by ID
        public async Task<Config> GetConfigById(int configId)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Config WHERE ConfigId = @configId";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("configId", configId);

                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Config
                                {
                                    ConfigId = reader.GetInt32(reader.GetOrdinal("ConfigId")),
                                    DefaultPlaybackSpeed = reader.GetString(reader.GetOrdinal("DefaultPlaybackSpeed")),
                                    EqualizerPresets = reader.GetString(reader.GetOrdinal("EqualizerPresets")),
                                    ReplayGain = reader.GetBoolean(reader.GetOrdinal("ReplayGain")),
                                    Theme = reader.GetString(reader.GetOrdinal("Theme")),
                                    MainColor = reader.GetString(reader.GetOrdinal("MainColor")),
                                    SecondaryColor = reader.GetString(reader.GetOrdinal("SecondaryColor")),
                                    LayoutSettings = reader.GetString(reader.GetOrdinal("LayoutSettings")),
                                    FontSize = reader.GetInt32(reader.GetOrdinal("FontSize")),
                                    StylePreferences = reader.GetString(reader.GetOrdinal("StylePreferences")),
                                    ShowAlbumArt = reader.GetBoolean(reader.GetOrdinal("ShowAlbumArt")),
                                    EnableAnimations = reader.GetBoolean(reader.GetOrdinal("EnableAnimations")),
                                    OutputDevice = reader.GetString(reader.GetOrdinal("OutputDevice")),
                                    DynamicPause = reader.GetBoolean(reader.GetOrdinal("DynamicPause")),
                                    AutoRescanInterval = reader.GetInt32(reader.GetOrdinal("AutoRescanInterval")),
                                    IncludeFileTypes = reader.GetString(reader.GetOrdinal("IncludeFileTypes")),
                                    ExcludeFileTypes = reader.GetString(reader.GetOrdinal("ExcludeFileTypes")),
                                    SortingOrderState = reader.GetString(reader.GetOrdinal("SortingOrderState")),
                                    SortPlaylistsState = reader.GetString(reader.GetOrdinal("SortPlaylistsState")),
                                    SortType = reader.GetString(reader.GetOrdinal("SortType")),
                                    AutoPlayNext = reader.GetBoolean(reader.GetOrdinal("AutoPlayNext")),
                                    SaveQueue = reader.GetBoolean(reader.GetOrdinal("SaveQueue")),
                                    ShuffleState = reader.GetBoolean(reader.GetOrdinal("ShuffleState")),
                                    RepeatMode = reader.GetString(reader.GetOrdinal("RepeatMode")),
                                    EnableTrackChangeNotifications = reader.GetBoolean(reader.GetOrdinal("EnableTrackChangeNotifications")),
                                    NotificationSettings = reader.GetString(reader.GetOrdinal("NotificationSettings")),
                                    LastUsedProfile = reader.IsDBNull(reader.GetOrdinal("LastUsedProfile")) ? null : reader.GetInt32(reader.GetOrdinal("LastUsedProfile")),
                                    AutoLogin = reader.GetBoolean(reader.GetOrdinal("AutoLogin")),
                                    LanguagePreference = reader.GetString(reader.GetOrdinal("LanguagePreference")),
                                    Volume = reader.GetInt32(reader.GetOrdinal("Volume"))
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching the config: {ex.Message}");
                throw;
            }
            return null;
        }

        // Get all config entries (if needed)
        public async Task<List<Config>> GetAllConfigs()
        {
            var configs = new List<Config>();

            try
            {
                using (var db = new DbConnection())
                {
                    string query = "SELECT * FROM Config";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var config = new Config
                                {
                                    ConfigId = reader.GetInt32(reader.GetOrdinal("ConfigId")),
                                    DefaultPlaybackSpeed = reader.GetString(reader.GetOrdinal("DefaultPlaybackSpeed")),
                                    EqualizerPresets = reader.GetString(reader.GetOrdinal("EqualizerPresets")),
                                    ReplayGain = reader.GetBoolean(reader.GetOrdinal("ReplayGain")),
                                    Theme = reader.GetString(reader.GetOrdinal("Theme")),
                                    MainColor = reader.GetString(reader.GetOrdinal("MainColor")),
                                    SecondaryColor = reader.GetString(reader.GetOrdinal("SecondaryColor")),
                                    LayoutSettings = reader.GetString(reader.GetOrdinal("LayoutSettings")),
                                    FontSize = reader.GetInt32(reader.GetOrdinal("FontSize")),
                                    StylePreferences = reader.GetString(reader.GetOrdinal("StylePreferences")),
                                    ShowAlbumArt = reader.GetBoolean(reader.GetOrdinal("ShowAlbumArt")),
                                    EnableAnimations = reader.GetBoolean(reader.GetOrdinal("EnableAnimations")),
                                    OutputDevice = reader.GetString(reader.GetOrdinal("OutputDevice")),
                                    DynamicPause = reader.GetBoolean(reader.GetOrdinal("DynamicPause")),
                                    AutoRescanInterval = reader.GetInt32(reader.GetOrdinal("AutoRescanInterval")),
                                    IncludeFileTypes = reader.GetString(reader.GetOrdinal("IncludeFileTypes")),
                                    ExcludeFileTypes = reader.GetString(reader.GetOrdinal("ExcludeFileTypes")),
                                    SortingOrderState = reader.GetString(reader.GetOrdinal("SortingOrderState")),
                                    SortPlaylistsState = reader.GetString(reader.GetOrdinal("SortPlaylistsState")),
                                    SortType = reader.GetString(reader.GetOrdinal("SortType")),
                                    AutoPlayNext = reader.GetBoolean(reader.GetOrdinal("AutoPlayNext")),
                                    SaveQueue = reader.GetBoolean(reader.GetOrdinal("SaveQueue")),
                                    ShuffleState = reader.GetBoolean(reader.GetOrdinal("ShuffleState")),
                                    RepeatMode = reader.GetString(reader.GetOrdinal("RepeatMode")),
                                    EnableTrackChangeNotifications = reader.GetBoolean(reader.GetOrdinal("EnableTrackChangeNotifications")),
                                    NotificationSettings = reader.GetString(reader.GetOrdinal("NotificationSettings")),
                                    LastUsedProfile = reader.IsDBNull(reader.GetOrdinal("LastUsedProfile")) ? null : reader.GetInt32(reader.GetOrdinal("LastUsedProfile")),
                                    AutoLogin = reader.GetBoolean(reader.GetOrdinal("AutoLogin")),
                                    LanguagePreference = reader.GetString(reader.GetOrdinal("LanguagePreference")),
                                    Volume = reader.GetFloat(reader.GetOrdinal("Volume"))
                                };

                                configs.Add(config);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while fetching all configs: {ex.Message}");
                throw;
            }
            return configs;
        }

        // Add a new config entry
        public async Task<int> AddConfig(Config config)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                    INSERT INTO Config 
                        (DefaultPlaybackSpeed, EqualizerPresets, ReplayGain, Theme, MainColor, SecondaryColor, LayoutSettings, FontSize, StylePreferences, 
                         ShowAlbumArt, EnableAnimations, OutputDevice, DynamicPause, AutoRescanInterval, IncludeFileTypes, ExcludeFileTypes, 
                         SortingOrderState, SortPlaylistsState, SortType, AutoPlayNext, SaveQueue, ShuffleState, RepeatMode, EnableTrackChangeNotifications, 
                         NotificationSettings, LastUsedProfile, AutoLogin, LanguagePreference, Volume)
                    VALUES 
                        (@DefaultPlaybackSpeed, @EqualizerPresets, @ReplayGain, @Theme, @MainColor, @SecondaryColor, @LayoutSettings, @FontSize, @StylePreferences, 
                         @ShowAlbumArt, @EnableAnimations, @OutputDevice, @DynamicPause, @AutoRescanInterval, @IncludeFileTypes, @ExcludeFileTypes, 
                         @SortingOrderState, @SortPlaylistsState, @SortType, @AutoPlayNext, @SaveQueue, @ShuffleState, @RepeatMode, @EnableTrackChangeNotifications, 
                         @NotificationSettings, @LastUsedProfile, @AutoLogin, @LanguagePreference, @Volume)
                    RETURNING ConfigId";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("DefaultPlaybackSpeed", config.DefaultPlaybackSpeed);
                        cmd.Parameters.AddWithValue("EqualizerPresets", config.EqualizerPresets);
                        cmd.Parameters.AddWithValue("ReplayGain", config.ReplayGain);
                        cmd.Parameters.AddWithValue("Theme", config.Theme);
                        cmd.Parameters.AddWithValue("MainColor", config.MainColor);
                        cmd.Parameters.AddWithValue("SecondaryColor", config.SecondaryColor);
                        cmd.Parameters.AddWithValue("LayoutSettings", config.LayoutSettings);
                        cmd.Parameters.AddWithValue("FontSize", config.FontSize);
                        cmd.Parameters.AddWithValue("StylePreferences", config.StylePreferences ?? string.Empty);
                        cmd.Parameters.AddWithValue("ShowAlbumArt", config.ShowAlbumArt);
                        cmd.Parameters.AddWithValue("EnableAnimations", config.EnableAnimations);
                        cmd.Parameters.AddWithValue("OutputDevice", config.OutputDevice ?? string.Empty);
                        cmd.Parameters.AddWithValue("DynamicPause", config.DynamicPause);
                        cmd.Parameters.AddWithValue("AutoRescanInterval", config.AutoRescanInterval);
                        cmd.Parameters.AddWithValue("IncludeFileTypes", config.IncludeFileTypes ?? string.Empty);
                        cmd.Parameters.AddWithValue("ExcludeFileTypes", config.ExcludeFileTypes ?? string.Empty);
                        cmd.Parameters.AddWithValue("SortingOrderState", config.SortingOrderState);
                        cmd.Parameters.AddWithValue("SortPlaylistsState", config.SortPlaylistsState);
                        cmd.Parameters.AddWithValue("SortType", config.SortType);
                        cmd.Parameters.AddWithValue("AutoPlayNext", config.AutoPlayNext);
                        cmd.Parameters.AddWithValue("SaveQueue", config.SaveQueue);
                        cmd.Parameters.AddWithValue("ShuffleState", config.ShuffleState);
                        cmd.Parameters.AddWithValue("RepeatMode", config.RepeatMode);
                        cmd.Parameters.AddWithValue("EnableTrackChangeNotifications", config.EnableTrackChangeNotifications);
                        cmd.Parameters.AddWithValue("NotificationSettings", config.NotificationSettings ?? string.Empty);
                        cmd.Parameters.AddWithValue("LastUsedProfile", config.LastUsedProfile ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("AutoLogin", config.AutoLogin);
                        cmd.Parameters.AddWithValue("LanguagePreference", config.LanguagePreference);
                        cmd.Parameters.AddWithValue("Volume", config.Volume);

                        var configId = (int)cmd.ExecuteScalar();
                        return configId;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while adding the config: {ex.Message}");
                throw;
            }
        }

        // Update an existing config entry
        public async Task UpdateConfig(Config config)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                    UPDATE Config SET 
                        DefaultPlaybackSpeed = @DefaultPlaybackSpeed, 
                        EqualizerPresets = @EqualizerPresets, 
                        ReplayGain = @ReplayGain, 
                        Theme = @Theme, 
                        MainColor = @MainColor, 
                        SecondaryColor = @SecondaryColor, 
                        LayoutSettings = @LayoutSettings, 
                        FontSize = @FontSize, 
                        StylePreferences = @StylePreferences, 
                        ShowAlbumArt = @ShowAlbumArt, 
                        EnableAnimations = @EnableAnimations, 
                        OutputDevice = @OutputDevice, 
                        DynamicPause = @DynamicPause, 
                        AutoRescanInterval = @AutoRescanInterval, 
                        IncludeFileTypes = @IncludeFileTypes, 
                        ExcludeFileTypes = @ExcludeFileTypes, 
                        SortingOrderState = @SortingOrderState, 
                        SortPlaylistsState = @SortPlaylistsState, 
                        SortType = @SortType, 
                        AutoPlayNext = @AutoPlayNext, 
                        SaveQueue = @SaveQueue, 
                        ShuffleState = @ShuffleState, 
                        RepeatMode = @RepeatMode, 
                        EnableTrackChangeNotifications = @EnableTrackChangeNotifications, 
                        NotificationSettings = @NotificationSettings, 
                        LastUsedProfile = @LastUsedProfile, 
                        AutoLogin = @AutoLogin, 
                        LanguagePreference = @LanguagePreference, 
                        Volume = @Volume
                    WHERE ConfigId = @ConfigId";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("ConfigId", config.ConfigId);
                        cmd.Parameters.AddWithValue("DefaultPlaybackSpeed", config.DefaultPlaybackSpeed);
                        cmd.Parameters.AddWithValue("EqualizerPresets", config.EqualizerPresets);
                        cmd.Parameters.AddWithValue("ReplayGain", config.ReplayGain);
                        cmd.Parameters.AddWithValue("Theme", config.Theme);
                        cmd.Parameters.AddWithValue("MainColor", config.MainColor);
                        cmd.Parameters.AddWithValue("SecondaryColor", config.SecondaryColor);
                        cmd.Parameters.AddWithValue("LayoutSettings", config.LayoutSettings);
                        cmd.Parameters.AddWithValue("FontSize", config.FontSize);
                        cmd.Parameters.AddWithValue("StylePreferences", config.StylePreferences ?? string.Empty);
                        cmd.Parameters.AddWithValue("ShowAlbumArt", config.ShowAlbumArt);
                        cmd.Parameters.AddWithValue("EnableAnimations", config.EnableAnimations);
                        cmd.Parameters.AddWithValue("OutputDevice", config.OutputDevice ?? string.Empty);
                        cmd.Parameters.AddWithValue("DynamicPause", config.DynamicPause);
                        cmd.Parameters.AddWithValue("AutoRescanInterval", config.AutoRescanInterval);
                        cmd.Parameters.AddWithValue("IncludeFileTypes", config.IncludeFileTypes ?? string.Empty);
                        cmd.Parameters.AddWithValue("ExcludeFileTypes", config.ExcludeFileTypes ?? string.Empty);
                        cmd.Parameters.AddWithValue("SortingOrderState", config.SortingOrderState);
                        cmd.Parameters.AddWithValue("SortPlaylistsState", config.SortPlaylistsState);
                        cmd.Parameters.AddWithValue("SortType", config.SortType);
                        cmd.Parameters.AddWithValue("AutoPlayNext", config.AutoPlayNext);
                        cmd.Parameters.AddWithValue("SaveQueue", config.SaveQueue);
                        cmd.Parameters.AddWithValue("ShuffleState", config.ShuffleState);
                        cmd.Parameters.AddWithValue("RepeatMode", config.RepeatMode);
                        cmd.Parameters.AddWithValue("EnableTrackChangeNotifications", config.EnableTrackChangeNotifications);
                        cmd.Parameters.AddWithValue("NotificationSettings", config.NotificationSettings ?? string.Empty);
                        cmd.Parameters.AddWithValue("LastUsedProfile", config.LastUsedProfile ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("AutoLogin", config.AutoLogin);
                        cmd.Parameters.AddWithValue("LanguagePreference", config.LanguagePreference);
                        cmd.Parameters.AddWithValue("Volume", config.Volume);


                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while updating the config: {ex.Message}");
                throw;
            }
        }

        // Method to update the Volume
        public async Task UpdateVolume(int configId, float volume)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = @"
                    UPDATE Config 
                    SET Volume = @Volume 
                    WHERE ConfigId = @ConfigId";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("ConfigId", configId);
                        cmd.Parameters.AddWithValue("Volume", volume);

                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
                Console.WriteLine($"An error occurred while updating the volume: {ex.Message}");
                throw;
            }
        }

        // Delete a config entry
        public async Task DeleteConfig(int configId)
        {
            try
            {
                using (var db = new DbConnection())
                {
                    string query = "DELETE FROM Config WHERE ConfigId = @configId";

                    using (var cmd = new NpgsqlCommand(query, db.dbConn))
                    {
                        cmd.Parameters.AddWithValue("configId", configId);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred while deleting the config: {ex.Message}");
                throw;
            }
        }
    }
}
