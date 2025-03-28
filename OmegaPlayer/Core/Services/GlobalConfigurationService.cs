using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services
{
    public class GlobalConfigurationService
    {
        private readonly GlobalConfigRepository _globalConfigRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache for improved performance and resilience
        private GlobalConfig _cachedConfig = null;
        private DateTime _lastCacheTime = DateTime.MinValue;
        private const int CACHE_EXPIRY_MINUTES = 5;

        public GlobalConfigurationService(
            GlobalConfigRepository globalConfigRepository,
            IErrorHandlingService errorHandlingService)
        {
            _globalConfigRepository = globalConfigRepository;
            _errorHandlingService = errorHandlingService;
        }

        /// <summary>
        /// Gets the global configuration with caching and fallback to default values.
        /// </summary>
        public async Task<GlobalConfig> GetGlobalConfig()
        {
            // Return cached config if it's still fresh
            if (_cachedConfig != null &&
                DateTime.Now.Subtract(_lastCacheTime).TotalMinutes < CACHE_EXPIRY_MINUTES)
            {
                return _cachedConfig;
            }

            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await _globalConfigRepository.GetGlobalConfig();

                    if (config == null)
                    {
                        // Try to create default config if none exists
                        var id = await _globalConfigRepository.CreateDefaultGlobalConfig();
                        if (id > 0)
                        {
                            config = await _globalConfigRepository.GetGlobalConfig();
                        }
                    }

                    if (config != null)
                    {
                        // Update cache
                        _cachedConfig = config;
                        _lastCacheTime = DateTime.Now;
                        return config;
                    }

                    // If we still got nothing, create default in memory
                    return CreateDefaultConfig();
                },
                "Getting global configuration",
                _cachedConfig ?? CreateDefaultConfig(),
                ErrorSeverity.Critical);
        }

        /// <summary>
        /// Updates the last used profile with error handling.
        /// </summary>
        public async Task UpdateLastUsedProfile(int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetGlobalConfig();

                    // Skip update if value hasn't changed
                    if (config.LastUsedProfile == profileId)
                    {
                        return;
                    }

                    config.LastUsedProfile = profileId;
                    await _globalConfigRepository.UpdateGlobalConfig(config);

                    // Update cache
                    _cachedConfig = config;
                    _lastCacheTime = DateTime.Now;
                },
                $"Updating last used profile to {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates the language preference with error handling.
        /// </summary>
        public async Task UpdateLanguage(string language)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetGlobalConfig();

                    // Skip update if value hasn't changed
                    if (config.LanguagePreference == language)
                    {
                        return;
                    }

                    config.LanguagePreference = language;
                    await _globalConfigRepository.UpdateGlobalConfig(config);

                    // Update cache
                    _cachedConfig = config;
                    _lastCacheTime = DateTime.Now;
                },
                $"Updating language preference to {language}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Creates a default configuration for fallback.
        /// </summary>
        private GlobalConfig CreateDefaultConfig()
        {
            _errorHandlingService.LogError(
                ErrorSeverity.Critical,
                "Using default global configuration",
                "Unable to retrieve or create global configuration in database. Using default values.",
                null,
                true);

            return new GlobalConfig
            {
                ID = -1, // Sentinel value indicating this is a generated default
                LastUsedProfile = null,
                LanguagePreference = "en"
            };
        }

        /// <summary>
        /// Forces cache refresh for when changes are made from other parts of the application.
        /// </summary>
        public void InvalidateCache()
        {
            _lastCacheTime = DateTime.MinValue;
        }
    }
}