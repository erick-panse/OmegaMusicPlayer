using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class GlobalConfigRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;
        private GlobalConfig _cachedConfig = null;

        public GlobalConfigRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
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
                    using var context = _contextFactory.CreateDbContext();

                    var configEntity = await context.GlobalConfigs
                        .AsNoTracking() // Better performance for read-only operations
                        .FirstOrDefaultAsync();

                    if (configEntity != null)
                    {
                        var config = new GlobalConfig
                        {
                            ID = configEntity.Id,
                            LastUsedProfile = configEntity.LastUsedProfile,
                            LanguagePreference = configEntity.LanguagePreference ?? "en"
                        };

                        // Cache config for fallback in case of later failures
                        _cachedConfig = config;
                        return config;
                    }

                    // If no config exists, create one
                    var defaultConfigId = await CreateDefaultGlobalConfig();
                    if (defaultConfigId > 0)
                    {
                        return await GetGlobalConfig(); // Recursive call to get the newly created config
                    }

                    return null;
                },
                "Fetching global configuration",
                _cachedConfig,
                ErrorSeverity.Critical,
                false);
        }

        /// <summary>
        /// Updates the global configuration settings in the database.
        /// </summary>
        public async Task UpdateGlobalConfig(GlobalConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var existingConfig = await context.GlobalConfigs
                        .FirstOrDefaultAsync(gc => gc.Id == config.ID);

                    if (existingConfig != null)
                    {
                        existingConfig.LastUsedProfile = config.LastUsedProfile;
                        existingConfig.LanguagePreference = config.LanguagePreference ?? "en";

                        await context.SaveChangesAsync();

                        // Update cache on successful write
                        _cachedConfig = new GlobalConfig
                        {
                            ID = config.ID,
                            LastUsedProfile = config.LastUsedProfile,
                            LanguagePreference = config.LanguagePreference
                        };
                    }
                    else
                    {
                        // If config doesn't exist, create it
                        var newEntity = new OmegaPlayer.Infrastructure.Data.Entities.GlobalConfig
                        {
                            LastUsedProfile = config.LastUsedProfile,
                            LanguagePreference = config.LanguagePreference ?? "en"
                        };

                        context.GlobalConfigs.Add(newEntity);
                        await context.SaveChangesAsync();

                        // Update cache with the new config
                        _cachedConfig = new GlobalConfig
                        {
                            ID = newEntity.Id,
                            LastUsedProfile = newEntity.LastUsedProfile,
                            LanguagePreference = newEntity.LanguagePreference
                        };
                    }
                },
                "Updating global configuration",
                ErrorSeverity.Critical,
                false);
        }

        /// <summary>
        /// Creates a default global configuration record in the database.
        /// </summary>
        public async Task<int> CreateDefaultGlobalConfig()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    // Check if a config already exists
                    var existingConfig = await context.GlobalConfigs.AnyAsync();
                    if (existingConfig)
                    {
                        // Return the ID of the existing config
                        var existing = await context.GlobalConfigs.FirstAsync();
                        return existing.Id;
                    }

                    var newConfig = new OmegaPlayer.Infrastructure.Data.Entities.GlobalConfig
                    {
                        LanguagePreference = "en",
                        LastUsedProfile = null
                    };

                    context.GlobalConfigs.Add(newConfig);
                    await context.SaveChangesAsync();

                    return newConfig.Id;
                },
                "Creating default global configuration",
                -1,  // Return -1 as error value
                ErrorSeverity.Critical,
                false);
        }

        /// <summary>
        /// Updates the last used profile in the global configuration
        /// </summary>
        public async Task UpdateLastUsedProfile(int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var config = await context.GlobalConfigs.FirstOrDefaultAsync();
                    if (config != null)
                    {
                        config.LastUsedProfile = profileId;
                        await context.SaveChangesAsync();

                        // Update cache
                        if (_cachedConfig != null)
                        {
                            _cachedConfig.LastUsedProfile = profileId;
                        }
                    }
                },
                "Updating last used profile",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Updates the language preference in the global configuration
        /// </summary>
        public async Task UpdateLanguagePreference(string languageCode)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var config = await context.GlobalConfigs.FirstOrDefaultAsync();
                    if (config != null)
                    {
                        config.LanguagePreference = languageCode ?? "en";
                        await context.SaveChangesAsync();

                        // Update cache
                        if (_cachedConfig != null)
                        {
                            _cachedConfig.LanguagePreference = languageCode;
                        }
                    }
                },
                "Updating language preference",
                ErrorSeverity.NonCritical,
                false);
        }
    }
}