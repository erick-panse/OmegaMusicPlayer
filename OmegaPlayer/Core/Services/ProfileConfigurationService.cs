using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.UI;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OmegaPlayer.Features.Configuration.ViewModels;

namespace OmegaPlayer.Infrastructure.Services
{
    public class ProfileConfigurationService
    {
        private readonly ProfileConfigRepository _profileConfigRepository;
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache to improve performance and provide fallback
        private readonly ConcurrentDictionary<int, ProfileConfig> _configCache = new ConcurrentDictionary<int, ProfileConfig>();
        private readonly ConcurrentDictionary<int, DateTime> _configCacheTimes = new ConcurrentDictionary<int, DateTime>();
        private const int CACHE_EXPIRY_MINUTES = 5;

        public ProfileConfigurationService(
            ProfileConfigRepository profileConfigRepository,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _profileConfigRepository = profileConfigRepository;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;
        }

        /// <summary>
        /// Gets a profile configuration with caching and fallback to defaults.
        /// </summary>
        public async Task<ProfileConfig> GetProfileConfig(int profileId)
        {
            // Return cached config if it's still fresh
            if (_configCache.TryGetValue(profileId, out var cachedConfig) &&
                _configCacheTimes.TryGetValue(profileId, out var cacheTime) &&
                DateTime.Now.Subtract(cacheTime).TotalMinutes < CACHE_EXPIRY_MINUTES)
            {
                return cachedConfig;
            }

            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await _profileConfigRepository.GetProfileConfig(profileId);

                    if (config == null)
                    {
                        // Try to create default config if none exists
                        var id = await _profileConfigRepository.CreateProfileConfig(profileId);
                        if (id > 0)
                        {
                            config = await _profileConfigRepository.GetProfileConfig(profileId);
                        }
                    }

                    if (config != null)
                    {
                        // Update cache
                        _configCache[profileId] = config;
                        _configCacheTimes[profileId] = DateTime.Now;
                        return config;
                    }

                    // If we still got nothing, create default in memory and return it
                    return CreateDefaultConfig(profileId);
                },
                $"Getting configuration for profile {profileId}",
                _configCache.TryGetValue(profileId, out var fallbackConfig) ? fallbackConfig : CreateDefaultConfig(profileId),
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates a profile theme with error handling and proper messaging.
        /// </summary>
        public async Task UpdateProfileTheme(int profileId, ThemeConfiguration themeConfig)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetProfileConfig(profileId);
                    config.Theme = themeConfig.ToJson();
                    await _profileConfigRepository.UpdateProfileConfig(config);

                    // Update cache
                    _configCache[profileId] = config;
                    _configCacheTimes[profileId] = DateTime.Now;

                    // Apply theme immediately through ThemeService
                    var themeService = App.ServiceProvider.GetRequiredService<ThemeService>();

                    if (themeConfig.ThemeType == PresetTheme.Custom)
                    {
                        themeService.ApplyTheme(themeConfig.ToThemeColors());
                    }
                    else
                    {
                        themeService.ApplyPresetTheme(themeConfig.ThemeType);
                    }

                    _messenger.Send(new ThemeUpdatedMessage(themeConfig));
                },
                $"Updating theme for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates a profile configuration with error handling and cache management.
        /// </summary>
        public async Task UpdateProfileConfig(ProfileConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    try
                    {
                        // Update only the fields that are now in ProfileConfig
                        var existingConfig = await GetProfileConfig(config.ProfileID);
                        if (existingConfig != null)
                        {
                            existingConfig.LastVolume = config.LastVolume;
                            existingConfig.Theme = config.Theme;
                            existingConfig.DynamicPause = config.DynamicPause;
                            existingConfig.BlacklistDirectory = config.BlacklistDirectory;
                            existingConfig.ViewState = config.ViewState;
                            existingConfig.SortingState = config.SortingState;

                            await _profileConfigRepository.UpdateProfileConfig(existingConfig);

                            // Update cache
                            _configCache[config.ProfileID] = existingConfig;
                            _configCacheTimes[config.ProfileID] = DateTime.Now;
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            $"Error updating profile config for profile {config.ProfileID}",
                            ex.Message,
                            ex,
                            true);
                        throw;
                    }
                },
                $"Updating configuration for profile {config.ProfileID}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates playback settings with error handling.
        /// </summary>
        public async Task UpdatePlaybackSettings(int profileId, bool dynamicPause)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetProfileConfig(profileId);
                    config.DynamicPause = dynamicPause;
                    await _profileConfigRepository.UpdateProfileConfig(config);

                    // Update cache
                    _configCache[profileId] = config;
                    _configCacheTimes[profileId] = DateTime.Now;
                },
                $"Updating playback settings for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates volume settings with error handling.
        /// </summary>
        public async Task UpdateVolume(int profileId, int volume)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetProfileConfig(profileId);

                    // Skip update if value hasn't changed significantly
                    if (Math.Abs(config.LastVolume - volume) <= 1)
                    {
                        return;
                    }

                    config.LastVolume = volume;
                    await _profileConfigRepository.UpdateProfileConfig(config);

                    // Update cache
                    _configCache[profileId] = config;
                    _configCacheTimes[profileId] = DateTime.Now;
                },
                $"Updating volume for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates blacklist directories with error handling.
        /// </summary>
        public async Task UpdateBlacklist(int profileId, string[] blacklistDirs)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetProfileConfig(profileId);

                    // Validate and normalize paths
                    var normalizedPaths = NormalizeBlacklistPaths(blacklistDirs);

                    config.BlacklistDirectory = normalizedPaths;
                    await _profileConfigRepository.UpdateProfileConfig(config);

                    // Update cache
                    _configCache[profileId] = config;
                    _configCacheTimes[profileId] = DateTime.Now;

                    // Notify that blacklist has changed
                    _messenger.Send(new BlacklistChangedMessage());
                },
                $"Updating blacklist directories for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Adds a single blacklist directory with error handling.
        /// </summary>
        public async Task AddBlacklistDirectory(int profileId, string path)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        throw new ArgumentException("Blacklist path cannot be empty", nameof(path));
                    }

                    // Get current config
                    var config = await GetProfileConfig(profileId);
                    var currentBlacklist = config.BlacklistDirectory?.ToList() ?? new List<string>();

                    // Normalize path
                    string normalizedPath = NormalizePath(path);

                    // Check if path is already in the blacklist (case-insensitive)
                    bool alreadyExists = currentBlacklist.Any(p =>
                        string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));

                    if (!alreadyExists)
                    {
                        // Add to blacklist
                        currentBlacklist.Add(normalizedPath);
                        config.BlacklistDirectory = currentBlacklist.ToArray();

                        // Save updated config
                        await _profileConfigRepository.UpdateProfileConfig(config);

                        // Update cache
                        _configCache[profileId] = config;
                        _configCacheTimes[profileId] = DateTime.Now;

                        // Notify that blacklist has changed
                        _messenger.Send(new BlacklistChangedMessage());
                    }
                },
                $"Adding blacklist directory for profile {profileId}: {path}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Removes a single blacklist directory with error handling.
        /// </summary>
        public async Task RemoveBlacklistDirectory(int profileId, string path)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(path))
                    {
                        throw new ArgumentException("Blacklist path cannot be empty", nameof(path));
                    }

                    // Get current config
                    var config = await GetProfileConfig(profileId);
                    var currentBlacklist = config.BlacklistDirectory?.ToList() ?? new List<string>();

                    // Normalize path
                    string normalizedPath = NormalizePath(path);

                    // Find matching path (case-insensitive)
                    string pathToRemove = currentBlacklist.FirstOrDefault(p =>
                        string.Equals(p, normalizedPath, StringComparison.OrdinalIgnoreCase));

                    if (pathToRemove != null)
                    {
                        // Remove from blacklist
                        currentBlacklist.Remove(pathToRemove);
                        config.BlacklistDirectory = currentBlacklist.ToArray();

                        // Save updated config
                        await _profileConfigRepository.UpdateProfileConfig(config);

                        // Update cache
                        _configCache[profileId] = config;
                        _configCacheTimes[profileId] = DateTime.Now;

                        // Notify that blacklist has changed
                        _messenger.Send(new BlacklistChangedMessage());
                    }
                },
                $"Removing blacklist directory for profile {profileId}: {path}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Gets all blacklisted directories for a profile
        /// </summary>
        public async Task<string[]> GetBlacklistedDirectories(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetProfileConfig(profileId);

                    // Normalize and filter the blacklist
                    string[] blacklist = config.BlacklistDirectory ?? Array.Empty<string>();

                    // Filter out null or empty entries and normalize paths
                    return blacklist
                        .Where(path => !string.IsNullOrWhiteSpace(path))
                        .Select(NormalizePath)
                        .Distinct(StringComparer.OrdinalIgnoreCase) // Remove duplicates
                        .ToArray();
                },
                $"Getting blacklisted directories for profile {profileId}",
                Array.Empty<string>(), // Return empty array as fallback
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Normalize an array of blacklist paths
        /// </summary>
        private string[] NormalizeBlacklistPaths(string[] paths)
        {
            if (paths == null)
            {
                return Array.Empty<string>();
            }

            // Log input paths if debugging would be helpful
            if (paths.Length > 20)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Large blacklist detected",
                    $"Normalizing {paths.Length} blacklist paths. This could affect performance.",
                    null,
                    false);
            }

            return paths
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(NormalizePath)
                .Where(p => !string.IsNullOrWhiteSpace(p)) // Filter again after normalization
                .Distinct(StringComparer.OrdinalIgnoreCase) // Remove duplicates (case-insensitive)
                .ToArray();
        }

        /// <summary>
        /// Normalize a single path to ensure consistent format
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            try
            {
                // Attempt to get canonical path
                return Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (Exception ex)
            {
                // Log the error but don't throw
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error normalizing path",
                    $"Could not normalize path: {path}. Error: {ex.Message}",
                    ex,
                    false);

                // If unable to normalize, return trimmed path as fallback
                return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        /// <summary>
        /// Updates view and sort state with error handling.
        /// </summary>
        public async Task UpdateViewAndSortState(int profileId, string viewState, string sortingState)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetProfileConfig(profileId);
                    config.ViewState = viewState;
                    config.SortingState = sortingState;
                    await _profileConfigRepository.UpdateProfileConfig(config);

                    // Update cache
                    _configCache[profileId] = config;
                    _configCacheTimes[profileId] = DateTime.Now;
                },
                $"Updating view and sort state for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Updates equalizer presets with error handling.
        /// </summary>
        public async Task UpdateEqualizer(int profileId, string presets)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var config = await GetProfileConfig(profileId);
                    config.EqualizerPresets = presets;
                    await _profileConfigRepository.UpdateProfileConfig(config);

                    // Update cache
                    _configCache[profileId] = config;
                    _configCacheTimes[profileId] = DateTime.Now;
                },
                $"Updating equalizer presets for profile {profileId}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Creates a default configuration object in memory for fallback purposes.
        /// </summary>
        private ProfileConfig CreateDefaultConfig(int profileId)
        {
            _errorHandlingService.LogError(
                ErrorSeverity.NonCritical,
                $"Using default configuration for profile {profileId}",
                "Unable to retrieve or create profile configuration in database. Using default values.",
                null,
                true);

            return new ProfileConfig
            {
                ID = -1, // Sentinel value indicating this is a generated default
                ProfileID = profileId,
                EqualizerPresets = "{}",
                LastVolume = 50,
                Theme = "{}",
                DynamicPause = false,
                BlacklistDirectory = Array.Empty<string>(),
                ViewState = "{}",
                SortingState = "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
            };
        }

        /// <summary>
        /// Invalidates the cache for a specific profile, or all profiles if none specified.
        /// </summary>
        public void InvalidateCache(int? profileId = null)
        {
            if (profileId.HasValue)
            {
                _configCacheTimes[profileId.Value] = DateTime.MinValue;
            }
            else
            {
                foreach (var id in _configCacheTimes.Keys)
                {
                    _configCacheTimes[id] = DateTime.MinValue;
                }
            }
        }

        /// <summary>
        /// Emergency method to reset a profile to defaults in case of critical failures.
        /// </summary>
        public async Task ResetProfileToDefaults(int profileId)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var defaultConfig = CreateDefaultConfig(profileId);
                    await _profileConfigRepository.UpdateProfileConfig(defaultConfig);

                    // Update cache
                    _configCache[profileId] = defaultConfig;
                    _configCacheTimes[profileId] = DateTime.Now;

                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        $"Profile {profileId} configuration reset to defaults",
                        "The profile configuration has been reset to default values due to errors.",
                        null,
                        true);

                    // Notify UI components about the reset
                    _messenger.Send(new ThemeUpdatedMessage(ThemeConfiguration.FromJson(defaultConfig.Theme)));

                    // Notify that blacklist has changed (since we reset to empty)
                    _messenger.Send(new BlacklistChangedMessage());
                },
                $"Resetting profile {profileId} to defaults",
                ErrorSeverity.NonCritical);
        }
    }
}