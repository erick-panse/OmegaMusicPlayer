using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class ProfileConfigRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache for profile configurations to provide fallback
        private readonly ConcurrentDictionary<int, ProfileConfig> _configCache = new ConcurrentDictionary<int, ProfileConfig>();

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
                            Theme = configEntity.Theme ?? "dark",
                            DynamicPause = configEntity.DynamicPause,
                            BlacklistDirectory = configEntity.BlacklistDirectory ?? Array.Empty<string>(),
                            ViewState = configEntity.ViewState ?? "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}",
                            SortingState = configEntity.SortingState ?? "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
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
                    using var context = _contextFactory.CreateDbContext();

                    // Check if config already exists
                    var existingConfig = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == profileId);

                    if (existingConfig != null)
                    {
                        return existingConfig.Id; // Return existing ID
                    }

                    // Create new config with default values
                    var newConfigEntity = new OmegaPlayer.Infrastructure.Data.Entities.ProfileConfig
                    {
                        ProfileId = profileId,
                        EqualizerPresets = "{}",
                        LastVolume = 50,
                        Theme = "dark",
                        DynamicPause = true,
                        BlacklistDirectory = Array.Empty<string>(),
                        ViewState = "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}",
                        SortingState = "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
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
                    using var context = _contextFactory.CreateDbContext();

                    var existingConfig = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == config.ProfileID);

                    if (existingConfig != null)
                    {
                        // Update existing entity
                        existingConfig.EqualizerPresets = config.EqualizerPresets ?? "{}";
                        existingConfig.LastVolume = config.LastVolume;
                        existingConfig.Theme = config.Theme ?? "dark";
                        existingConfig.DynamicPause = config.DynamicPause;
                        existingConfig.BlacklistDirectory = config.BlacklistDirectory;
                        existingConfig.ViewState = config.ViewState ?? "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}";
                        existingConfig.SortingState = config.SortingState ?? "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}";

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
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates only the theme for a profile configuration
        /// </summary>
        public async Task UpdateProfileTheme(int profileId, string theme)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var existingConfig = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == profileId);

                    if (existingConfig != null)
                    {
                        existingConfig.Theme = theme ?? "dark";
                        await context.SaveChangesAsync();

                        // Update cache
                        if (_configCache.TryGetValue(profileId, out var cachedConfig))
                        {
                            cachedConfig.Theme = theme;
                        }
                    }
                },
                $"Updating theme for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates only the volume for a profile configuration
        /// </summary>
        public async Task UpdateProfileVolume(int profileId, int volume)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var existingConfig = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == profileId);

                    if (existingConfig != null)
                    {
                        existingConfig.LastVolume = Math.Max(0, Math.Min(100, volume)); // Clamp between 0-100
                        await context.SaveChangesAsync();

                        // Update cache
                        if (_configCache.TryGetValue(profileId, out var cachedConfig))
                        {
                            cachedConfig.LastVolume = existingConfig.LastVolume;
                        }
                    }
                },
                $"Updating volume for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates only the view state for a profile configuration
        /// </summary>
        public async Task UpdateProfileViewState(int profileId, string viewState)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var existingConfig = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == profileId);

                    if (existingConfig != null)
                    {
                        existingConfig.ViewState = viewState ?? "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}";
                        await context.SaveChangesAsync();

                        // Update cache
                        if (_configCache.TryGetValue(profileId, out var cachedConfig))
                        {
                            cachedConfig.ViewState = viewState;
                        }
                    }
                },
                $"Updating view state for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates only the sorting state for a profile configuration
        /// </summary>
        public async Task UpdateProfileSortingState(int profileId, string sortingState)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var existingConfig = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == profileId);

                    if (existingConfig != null)
                    {
                        existingConfig.SortingState = sortingState ?? "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}";
                        await context.SaveChangesAsync();

                        // Update cache
                        if (_configCache.TryGetValue(profileId, out var cachedConfig))
                        {
                            cachedConfig.SortingState = sortingState;
                        }
                    }
                },
                $"Updating sorting state for profile {profileId}",
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
                    using var context = _contextFactory.CreateDbContext();

                    var configToDelete = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == profileId);

                    if (configToDelete != null)
                    {
                        context.ProfileConfigs.Remove(configToDelete);
                        await context.SaveChangesAsync();

                        // Remove from cache
                        _configCache.TryRemove(profileId, out _);
                    }
                },
                $"Deleting configuration for profile {profileId}",
                ErrorSeverity.NonCritical);
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

        /// <summary>
        /// Gets all profile configurations (useful for bulk operations)
        /// </summary>
        public async Task<System.Collections.Generic.List<ProfileConfig>> GetAllProfileConfigs()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var configs = await context.ProfileConfigs
                        .AsNoTracking()
                        .ToListAsync();

                    return configs.Select(entity => new ProfileConfig
                    {
                        ID = entity.Id,
                        ProfileID = entity.ProfileId ?? 0,
                        EqualizerPresets = entity.EqualizerPresets ?? "{}",
                        LastVolume = entity.LastVolume,
                        Theme = entity.Theme ?? "dark",
                        DynamicPause = entity.DynamicPause,
                        BlacklistDirectory = entity.BlacklistDirectory ?? Array.Empty<string>(),
                        ViewState = entity.ViewState ?? "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}",
                        SortingState = entity.SortingState ?? "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
                    }).ToList();
                },
                "Getting all profile configurations",
                new System.Collections.Generic.List<ProfileConfig>(),
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates only the blacklist directories for a profile configuration
        /// </summary>
        public async Task UpdateProfileBlacklistDirectories(int profileId, string[] blacklistDirectories)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var existingConfig = await context.ProfileConfigs
                        .FirstOrDefaultAsync(pc => pc.ProfileId == profileId);

                    if (existingConfig != null)
                    {
                        existingConfig.BlacklistDirectory = blacklistDirectories;
                        await context.SaveChangesAsync();

                        // Update cache
                        if (_configCache.TryGetValue(profileId, out var cachedConfig))
                        {
                            cachedConfig.BlacklistDirectory = blacklistDirectories;
                        }
                    }
                },
                $"Updating blacklist directories for profile {profileId}",
                ErrorSeverity.NonCritical);
        }
    }
}