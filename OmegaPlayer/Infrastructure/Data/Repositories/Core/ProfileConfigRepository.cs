using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class ProfileConfigRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache for profile configurations to provide fallback
        private readonly ConcurrentDictionary<int, ProfileConfig> _configCache = new ConcurrentDictionary<int, ProfileConfig>();

        public readonly string DefaultTheme = 
            "{\"ThemeType\":2," +
            "\"MainStartColor\":null,\"MainEndColor\":null," +
            "\"SecondaryStartColor\":null,\"SecondaryEndColor\":null," +
            "\"AccentStartColor\":null,\"AccentEndColor\":null," +
            "\"TextStartColor\":null,\"TextEndColor\":null}";

        public readonly string DefaultSortingState =
            "{\"home\": {\"SortType\": 0, \"SortDirection\": 0}, \"album\": {\"SortType\": 0, \"SortDirection\": 0}, \"genre\": {\"SortType\": 0, \"SortDirection\": 0}," +
            " \"artist\": {\"SortType\": 0, \"SortDirection\": 0}, \"config\": {\"SortType\": 0, \"SortDirection\": 0}, \"folder\": {\"SortType\": 0, \"SortDirection\": 0}," +
            " \"details\": {\"SortType\": 0, \"SortDirection\": 0}, \"library\": {\"SortType\": 0, \"SortDirection\": 0}, \"playlist\": {\"SortType\": 0, \"SortDirection\": 0}}";

        public readonly string DefaultViewState = "{\"LibraryViewType\":\"Card\",\"DetailsViewType\":\"Image\",\"ContentType\":\"Library\"}";

        public ProfileConfigRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
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
                    using var context = _contextFactory.CreateDbContext();

                    var configEntity = await context.ProfileConfigs
                        .AsNoTracking() // Better performance for read-only operations
                        .FirstOrDefaultAsync(pc => pc.ProfileId == profileId);

                    if (configEntity != null)
                    {
                        var config = new ProfileConfig
                        {
                            ID = configEntity.Id,
                            ProfileID = configEntity.ProfileId ?? profileId,
                            EqualizerPresets = configEntity.EqualizerPresets ?? "{}",
                            LastVolume = configEntity.LastVolume,
                            Theme = configEntity.Theme ?? DefaultTheme,
                            DynamicPause = configEntity.DynamicPause,
                            BlacklistDirectory = configEntity.BlacklistDirectory ?? Array.Empty<string>(),
                            ViewState = configEntity.ViewState ?? DefaultViewState,
                            SortingState = configEntity.SortingState ?? DefaultSortingState
                        };

                        // Cache the retrieved configuration
                        _configCache[profileId] = config;
                        return config;
                    }

                    // If no config exists, create a default one
                    var createdId = await CreateProfileConfig(profileId);
                    if (createdId > 0)
                    {
                        return await GetProfileConfig(profileId); // Recursive call to get the newly created config
                    }

                    return null;
                },
                $"Fetching configuration for profile {profileId}",
                _configCache.TryGetValue(profileId, out var cachedConfig) ? cachedConfig : null,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Creates a new profile configuration record in the database.
        /// </summary>
        public async Task<int> CreateProfileConfig(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    // Check if config already exists
                    var existingConfig = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == profileId);

                    if (existingConfig != null)
                    {
                        return existingConfig.Id; // Return existing ID
                    }

                    // Create new config with default values
                    var newConfigEntity = new Entities.ProfileConfig
                    {
                        ProfileId = profileId,
                        EqualizerPresets = "{}",
                        LastVolume = 50,
                        Theme = DefaultTheme,
                        DynamicPause = false,
                        BlacklistDirectory = Array.Empty<string>(),
                        ViewState = DefaultViewState,
                        SortingState = DefaultSortingState
                    };

                    context.ProfileConfigs.Add(newConfigEntity);
                    await context.SaveChangesAsync();

                    // Create and cache the Core model
                    var config = new ProfileConfig
                    {
                        ID = newConfigEntity.Id,
                        ProfileID = profileId,
                        EqualizerPresets = newConfigEntity.EqualizerPresets,
                        LastVolume = newConfigEntity.LastVolume,
                        Theme = newConfigEntity.Theme,
                        DynamicPause = newConfigEntity.DynamicPause,
                        BlacklistDirectory = Array.Empty<string>(),
                        ViewState = newConfigEntity.ViewState,
                        SortingState = newConfigEntity.SortingState
                    };

                    _configCache[profileId] = config;
                    return newConfigEntity.Id;
                },
                $"Creating configuration for profile {profileId}",
                -1, // Return -1 to indicate failure
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Updates an existing profile configuration in the database.
        /// </summary>
        public async Task UpdateProfileConfig(ProfileConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var existingConfig = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == config.ProfileID);

                    if (existingConfig != null)
                    {
                        // Update existing entity
                        existingConfig.EqualizerPresets = config.EqualizerPresets ?? "{}";
                        existingConfig.LastVolume = config.LastVolume;
                        existingConfig.Theme = config.Theme ?? DefaultTheme;
                        existingConfig.DynamicPause = config.DynamicPause;
                        existingConfig.BlacklistDirectory = config.BlacklistDirectory;
                        existingConfig.ViewState = config.ViewState ?? DefaultViewState;
                        existingConfig.SortingState = config.SortingState ?? DefaultSortingState;

                        await context.SaveChangesAsync();

                        // Update the cache
                        _configCache[config.ProfileID] = new ProfileConfig
                        {
                            ID = existingConfig.Id,
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
                    else
                    {
                        // Create new config if it doesn't exist
                        await CreateProfileConfig(config.ProfileID);
                        await UpdateProfileConfig(config); // Recursive call to update the newly created config
                    }
                },
                $"Updating configuration for profile {config.ProfileID}",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Clears the cache for a specific profile or all profiles
        /// </summary>
        public void ClearCache(int? profileId = null)
        {
            if (profileId.HasValue)
            {
                _configCache.TryRemove(profileId.Value, out _);
            }
            else
            {
                _configCache.Clear();
            }
        }
    }
}