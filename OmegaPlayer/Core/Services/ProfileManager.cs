using System.Threading.Tasks;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Profile.Models;
using System.Linq;
using System;
using OmegaPlayer.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;

namespace OmegaPlayer.Core.Services
{
    public class ProfileManager
    {
        private readonly ProfileService _profileService;
        private readonly GlobalConfigurationService _globalConfigService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        public Profiles CurrentProfile { get; private set; }

        private bool _isInitialized = false;
        private readonly object _initLock = new object();
        private readonly List<Profiles> _cachedProfiles = new List<Profiles>();

        public ProfileManager(
            ProfileService profileService,
            GlobalConfigurationService globalConfigService,
            IServiceProvider serviceProvider,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _profileService = profileService;
            _globalConfigService = globalConfigService;
            _serviceProvider = serviceProvider;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;
        }

        /// <summary>
        /// Initializes the profile manager and loads the current profile safely.
        /// This method is thread-safe and can be called multiple times.
        /// </summary>
        public async Task InitializeAsync()
        {
            // Return immediately if already initialized, avoiding redundant calls
            if (_isInitialized && CurrentProfile != null)
                return;

            // Use a lock to prevent race conditions during initialization
            lock (_initLock)
            {
                if (_isInitialized && CurrentProfile != null)
                    return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Load all available profiles
                    var profiles = await _profileService.GetAllProfiles();
                    if (profiles == null || !profiles.Any())
                    {
                        // Create a default profile if no profiles exist
                        await CreateDefaultProfile();
                        profiles = await _profileService.GetAllProfiles();
                    }

                    _cachedProfiles.Clear();
                    _cachedProfiles.AddRange(profiles);

                    try
                    {
                        // Get global config to determine last used profile
                        var globalConfig = await _globalConfigService.GetGlobalConfig();

                        if (globalConfig.LastUsedProfile.HasValue)
                        {
                            // Try to find the last used profile
                            CurrentProfile = profiles.FirstOrDefault(p => p.ProfileID == globalConfig.LastUsedProfile.Value);

                            // If last used profile no longer exists, use the first available profile
                            if (CurrentProfile == null)
                            {
                                CurrentProfile = profiles.First();
                                await _globalConfigService.UpdateLastUsedProfile(CurrentProfile.ProfileID);
                            }
                        }
                        else
                        {
                            // If no last used profile is set, use the first available profile
                            CurrentProfile = profiles.First();
                            await _globalConfigService.UpdateLastUsedProfile(CurrentProfile.ProfileID);
                        }
                    }
                    catch (Exception ex)
                    {
                        // If we fail to get global config, fall back to first profile
                        _errorHandlingService.LogError(
                            ErrorSeverity.Critical,
                            "Failed to retrieve last used profile",
                            "Using the first available profile instead.",
                            ex,
                            true);

                        CurrentProfile = profiles.First();
                    }

                    // Mark as initialized once we have a valid current profile
                    lock (_initLock)
                    {
                        _isInitialized = true;
                    }
                },
                "Initializing profile manager",
                ErrorSeverity.Critical);

            // If initialization failed but we have cached profiles, use the first one
            if (CurrentProfile == null && _cachedProfiles.Any())
            {
                CurrentProfile = _cachedProfiles.First();
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Using fallback profile after initialization failure",
                    "The profile manager could not initialize properly but recovered using a cached profile.",
                    null,
                    true);

                lock (_initLock)
                {
                    _isInitialized = true;
                }
            }
            // If we still don't have a profile, create an in-memory fallback
            else if (CurrentProfile == null)
            {
                CurrentProfile = CreateEmergencyProfile();
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Created emergency profile after initialization failure",
                    "The profile manager created an in-memory profile because no profiles could be loaded or created.",
                    null,
                    true);

                lock (_initLock)
                {
                    _isInitialized = true;
                }
            }
        }

        /// <summary>
        /// Switches to a different profile with proper error handling.
        /// </summary>
        public async Task SwitchProfile(Profiles newProfile)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (newProfile == null)
                        throw new ArgumentNullException(nameof(newProfile), "Cannot switch to a null profile");

                    // Save current profile state before switching
                    try
                    {
                        var stateManager = _serviceProvider.GetService<StateManagerService>();
                        if (stateManager != null)
                        {
                            await stateManager.SaveCurrentState();
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to save current profile state",
                            "The current profile state could not be saved before switching profiles.",
                            ex,
                            false); // Don't show notification for this intermediate error
                    }

                    // Update current profile
                    CurrentProfile = newProfile;

                    // Update last used profile in global config
                    await _globalConfigService.UpdateLastUsedProfile(newProfile.ProfileID);

                    // Load the new profile's state
                    var _stateManager = _serviceProvider.GetService<StateManagerService>();
                    if (_stateManager != null)
                    {
                        await _stateManager.LoadAndApplyState(true);
                    }

                    // Notify subscribers about profile change
                    _messenger.Send(new ProfileStateLoadedMessage(newProfile.ProfileID));
                },
                $"Switching to profile {newProfile?.ProfileName ?? "Unknown"}",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Creates a default profile when none exists.
        /// </summary>
        private async Task CreateDefaultProfile()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var defaultProfile = new Profiles
                    {
                        ProfileName = "Default",
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    var profileId = await _profileService.AddProfile(defaultProfile);
                    defaultProfile.ProfileID = profileId;

                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "Created default profile",
                        "No profiles were found, so a default profile was created.",
                        null,
                        true);

                    // Add to cached profiles
                    _cachedProfiles.Add(defaultProfile);
                },
                "Creating default profile",
                ErrorSeverity.Critical);
        }

        /// <summary>
        /// Creates an emergency in-memory profile as a last resort fallback.
        /// </summary>
        private Profiles CreateEmergencyProfile()
        {
            var emergencyProfile = new Profiles
            {
                ProfileID = -1, // Sentinel value indicating this is a generated emergency profile
                ProfileName = "Emergency Profile",
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            _errorHandlingService.LogError(
                ErrorSeverity.Critical,
                "Created emergency in-memory profile",
                "Unable to load or create profiles from the database. Using an in-memory profile as a last resort.",
                null,
                true);

            return emergencyProfile;
        }

        /// <summary>
        /// Reloads the profile list from the database.
        /// </summary>
        public async Task RefreshProfilesAsync()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profiles = await _profileService.GetAllProfiles();
                    if (profiles != null && profiles.Any())
                    {
                        _cachedProfiles.Clear();
                        _cachedProfiles.AddRange(profiles);
                    }
                },
                "Refreshing profile list",
                ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Resets to a stable state in case of critical failure.
        /// </summary>
        public async Task ResetToStableState()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Try to refresh profiles
                    await RefreshProfilesAsync();

                    // If we have profiles, use the first one
                    if (_cachedProfiles.Any())
                    {
                        CurrentProfile = _cachedProfiles.First();
                        await _globalConfigService.UpdateLastUsedProfile(CurrentProfile.ProfileID);
                    }
                    else
                    {
                        // If we still don't have profiles, create a default one
                        await CreateDefaultProfile();
                        var profiles = await _profileService.GetAllProfiles();
                        if (profiles.Any())
                        {
                            CurrentProfile = profiles.First();
                            await _globalConfigService.UpdateLastUsedProfile(CurrentProfile.ProfileID);
                        }
                        else
                        {
                            // As a last resort, use an emergency profile
                            CurrentProfile = CreateEmergencyProfile();
                        }
                    }

                    // Reset state manager
                    var stateManager = _serviceProvider.GetService<StateManagerService>();
                    if (stateManager != null)
                    {
                        await stateManager.LoadAndApplyState(true);
                    }

                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "Profile manager reset to stable state",
                        "The profile manager has been reset to a stable state after a critical failure.",
                        null,
                        true);
                },
                "Resetting profile manager to stable state",
                ErrorSeverity.Critical);
        }
    }
}