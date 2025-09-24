using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services
{
    public class GlobalConfigurationService
    {
        private readonly GlobalConfigRepository _globalConfigRepository;
        private readonly LocalizationService _localizationService;
        private readonly IErrorHandlingService _errorHandlingService;

        // Thread synchronization
        private readonly SemaphoreSlim _getConfigLock = new SemaphoreSlim(1, 1);
        private Task<GlobalConfig> _pendingGetConfigTask = null;

        // Local cache
        private GlobalConfig _cachedConfig = null;

        public GlobalConfigurationService(
            GlobalConfigRepository globalConfigRepository,
            LocalizationService localizationService,
            IErrorHandlingService errorHandlingService)
        {
            _globalConfigRepository = globalConfigRepository;
            _localizationService = localizationService;
            _errorHandlingService = errorHandlingService;
        }

        /// <summary>
        /// Gets or creates the global configuration with protection against concurrent calls
        /// </summary>
        public async Task<GlobalConfig> GetGlobalConfig()
        {
            // Fast path - return cached config if available
            if (_cachedConfig != null)
            {
                return _cachedConfig;
            }

            // Use a semaphore to prevent multiple concurrent initializations
            await _getConfigLock.WaitAsync();
            try
            {
                // Check cache again after acquiring lock
                if (_cachedConfig != null)
                {
                    return _cachedConfig;
                }

                // If there's already a task in progress, wait for it
                if (_pendingGetConfigTask != null)
                {
                    var currentTask = _pendingGetConfigTask;
                    // Release lock while waiting
                    _getConfigLock.Release();

                    try
                    {
                        // Wait for the other task to complete
                        var result = await currentTask;
                        return result;
                    }
                    catch
                    {
                        // If that task failed, re-acquire lock and try again
                        await _getConfigLock.WaitAsync();
                        // Re-check cache
                        if (_cachedConfig != null)
                        {
                            return _cachedConfig;
                        }
                    }
                }

                // Start a new task to get the config
                _pendingGetConfigTask = GetOrCreateConfigAsync();

                try
                {
                    // Wait for task to complete
                    var config = await _pendingGetConfigTask;
                    // Cache the result
                    _cachedConfig = config;
                    return config;
                }
                finally
                {
                    // Clear the pending task reference
                    _pendingGetConfigTask = null;
                }
            }
            finally
            {
                // Make sure to release the lock
                if (_getConfigLock.CurrentCount == 0)
                {
                    _getConfigLock.Release();
                }
            }
        }

        /// <summary>
        /// Internal method to get or create global config
        /// </summary>
        private async Task<GlobalConfig> GetOrCreateConfigAsync()
        {
            try
            {
                var config = await _globalConfigRepository.GetGlobalConfig();

                // If no config exists, create one
                if (config == null)
                {
                    // Create a default config
                    var configId = await _globalConfigRepository.CreateDefaultGlobalConfig();

                    if (configId > 0)
                    {
                        // Get the newly created config
                        config = await _globalConfigRepository.GetGlobalConfig();
                    }
                }

                return config;
            }
            catch (Exception ex)
            {
                // Log the error - NonCritical to avoid triggering recovery during startup
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error getting global configuration",
                    "Failed to retrieve or create global configuration. Using a temporary default.",
                    ex,
                    false);

                // Return a temporary default config
                return new GlobalConfig
                {
                    ID = -1,
                    LastUsedProfile = null,
                    LanguagePreference = "en",
                    WindowWidth = 1440,
                    WindowHeight = 760,
                    WindowX = null,
                    WindowY = null,
                    IsWindowMaximized = false
                };
            }
        }

        /// <summary>
        /// Updates the last used profile in global config
        /// </summary>
        public async Task UpdateLastUsedProfile(int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetGlobalConfig();

                    // Skip update if we have a temporary config
                    if (config == null || config.ID <= 0)
                    {
                        return;
                    }

                    config.LastUsedProfile = profileId;
                    await _globalConfigRepository.UpdateGlobalConfig(config);

                    // Update cache
                    _cachedConfig = config;
                },
                $"Updating last used profile to {profileId}",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Updates language preference in global config
        /// </summary>
        public async Task UpdateLanguage(string language)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetGlobalConfig();

                    // Skip update if we have a temporary config
                    if (config == null || config.ID <= 0)
                    {
                        return;
                    }

                    config.LanguagePreference = language;
                    await _globalConfigRepository.UpdateGlobalConfig(config);

                    // Update cache
                    _cachedConfig = config;
                },
                _localizationService["ErrorChangingLanguage"],
                ErrorSeverity.NonCritical,
                true);
        }

        /// <summary>
        /// Updates window state in global config
        /// </summary>
        public void UpdateWindowState(int width, int height, int? x, int? y, bool isMaximized)
        {
            _globalConfigRepository.UpdateWindowState(Math.Max(width, 955), Math.Max(height, 650), x, y, isMaximized);
        }

        /// <summary>
        /// Gets the saved window state for application startup
        /// </summary>
        public async Task<(int width, int height, int? x, int? y, bool isMaximized)> GetWindowState()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetGlobalConfig();

                    return (
                        Math.Max(config.WindowWidth, 955),  // Ensure minimum width
                        Math.Max(config.WindowHeight, 650), // Ensure minimum height
                        config.WindowX,
                        config.WindowY,
                        config.IsWindowMaximized
                    );
                },
                "Getting window state",
                (1440, 760, null, null, false), // Default values
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Invalidates the cached config, forcing a reload from database
        /// </summary>
        public void InvalidateCache()
        {
            _cachedConfig = null;
        }
    }
}