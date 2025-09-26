using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Messages;
using OmegaMusicPlayer.Features.Profile.Models;
using OmegaMusicPlayer.Features.Profile.Services;
using OmegaMusicPlayer.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Core.Services
{
    public class ProfileManager
    {
        private readonly ProfileService _profileService;
        private readonly GlobalConfigurationService _globalConfigService;
        private readonly LocalizationService _localizationService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        // Thread-safety and state tracking
        private Profiles _currentProfile;
        private bool _isInitialized = false;
        private Task<Profiles> _currentInitTask = null;
        private readonly List<Profiles> _cachedProfiles = new List<Profiles>();
        private readonly SemaphoreSlim _profileSwitchLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1, 1);

        public ProfileManager(
            ProfileService profileService,
            GlobalConfigurationService globalConfigService,
            LocalizationService localizationService,
            IServiceProvider serviceProvider,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _profileService = profileService;
            _globalConfigService = globalConfigService;
            _localizationService = localizationService;
            _serviceProvider = serviceProvider;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            // Register for profile change messages to invalidate cache
            _messenger.Register<ProfileChangedMessage>(this, (r, m) => UpdateProfile());
        }

        public async void UpdateProfile()
        {
            // Load profile info
            _currentProfile = await _profileService.GetProfileById(_currentProfile.ProfileID);
        }

        /// <summary>
        /// Gets the current profile, ensuring initialization has occurred.
        /// Always returns a valid profile object, never null.
        /// </summary>
        public async Task<Profiles> GetCurrentProfileAsync()
        {
            // Fast path - already initialized
            if (_isInitialized && _currentProfile != null)
                return _currentProfile;

            // Acquire semaphore to coordinate initialization
            await _initSemaphore.WaitAsync();
            try
            {
                // Check again after acquiring semaphore
                if (_isInitialized && _currentProfile != null)
                    return _currentProfile;

                // If there's already an initialization task, wait for it
                if (_currentInitTask != null)
                {
                    var task = _currentInitTask;
                    _initSemaphore.Release();

                    try
                    {
                        return await task;
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Profile initialization failed from existing task",
                            "Using emergency profile due to initialization error.",
                            ex,
                            true);
                        return CreateEmergencyProfile();
                    }
                }

                // Start new initialization task
                _currentInitTask = InitializeProfileAsync();

                try
                {
                    var result = await _currentInitTask;
                    return result;
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Profile initialization failed",
                        "Using emergency profile due to initialization error.",
                        ex,
                        true);
                    return CreateEmergencyProfile();
                }
                finally
                {
                    _currentInitTask = null;
                }
            }
            finally
            {
                if (_initSemaphore.CurrentCount == 0)
                    _initSemaphore.Release();
            }
        }

        /// <summary>
        /// Internal method that handles the actual profile initialization.
        /// </summary>
        private async Task<Profiles> InitializeProfileAsync()
        {
            try
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
                        _currentProfile = profiles.FirstOrDefault(p => p.ProfileID == globalConfig.LastUsedProfile.Value);

                        // If last used profile no longer exists, use the first available profile
                        if (_currentProfile == null)
                        {
                            _currentProfile = profiles.First();
                            await _globalConfigService.UpdateLastUsedProfile(_currentProfile.ProfileID);
                        }
                    }
                    else
                    {
                        // If no last used profile is set, use the first available profile
                        _currentProfile = profiles.First();
                        await _globalConfigService.UpdateLastUsedProfile(_currentProfile.ProfileID);
                    }
                }
                catch (Exception ex)
                {
                    // If we fail to get global config, fall back to first profile
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Failed to retrieve last used profile",
                        "Using the first available profile instead.",
                        ex,
                        true);

                    if (profiles.Any())
                    {
                        _currentProfile = profiles.First();
                    }
                    else
                    {
                        // If still no profiles, use emergency profile
                        _currentProfile = CreateEmergencyProfile();
                    }
                }

                // Mark as initialized
                _isInitialized = true;

                // If we still don't have a profile after all that, use emergency profile
                if (_currentProfile == null)
                {
                    _currentProfile = CreateEmergencyProfile();
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Failed to initialize profile",
                        "Using emergency profile after initialization failure.",
                        null,
                        false);
                }

                return _currentProfile;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Profile initialization failed unexpectedly",
                    "The profile manager failed to initialize profiles.",
                    ex,
                    true);

                // Use emergency profile on any failure
                _currentProfile = CreateEmergencyProfile();
                return _currentProfile;
            }
        }


        /// <summary>
        /// Switches to a different profile with proper error handling.
        /// Thread-safe with semaphore protection.
        /// </summary>
        public async Task SwitchProfile(Profiles newProfile)
        {
            // Ensure initialization completes first
            await GetCurrentProfileAsync();

            // Acquire lock to ensure only one profile switch happens at a time
            await _profileSwitchLock.WaitAsync();

            try
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
                                await stateManager.SaveSortState();
                            }
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Failed to save current profile state",
                                "The current profile state could not be saved before switching profiles.",
                                ex,
                                false);
                        }

                        // Update current profile
                        _currentProfile = newProfile;

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

                        // Notify subscribers about profile config changes
                        _messenger.Send(new ProfileConfigChangedMessage(newProfile.ProfileID));
                    },
                    _localizationService["SwitchProfileError"] + newProfile?.ProfileName ?? "Unknown",
                    ErrorSeverity.NonCritical,
                    true);
            }
            finally
            {
                // Always release the lock
                _profileSwitchLock.Release();
            }
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
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
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
                ErrorSeverity.NonCritical,
                false);
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
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _errorHandlingService.LogError(
                ErrorSeverity.NonCritical,
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
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Resets to a stable state in case of critical failure.
        /// </summary>
        public async Task ResetToStableState()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Reset initialization state
                    _isInitialized = false;
                    _currentProfile = null;
                    _cachedProfiles.Clear();

                    // Use the public method which handles all initialization properly
                    await GetCurrentProfileAsync();

                    // Reset state manager
                    var stateManager = _serviceProvider.GetService<StateManagerService>();
                    if (stateManager != null)
                    {
                        await stateManager.LoadAndApplyState(true);
                    }

                    _errorHandlingService.LogInfo(
                        "Profile manager reset to stable state",
                        "The profile manager has been reset to a stable state after a critical failure.");
                },
                "Resetting profile manager to stable state",
                ErrorSeverity.NonCritical,
                false);
        }
    }

    /// <summary>
    /// Message for notifying that a profile's configuration has changed
    /// </summary>
    public class ProfileConfigChangedMessage
    {
        public int ProfileId { get; }

        public ProfileConfigChangedMessage(int profileId)
        {
            ProfileId = profileId;
        }
    }
}