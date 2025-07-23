using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace OmegaPlayer.Core.Services
{
    public class StateManagerService
    {
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly ProfileManager _profileManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ThemeService _themeService;
        private readonly AudioMonitorService _audioMonitorService;
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        private bool _isInitialized = false;
        private DateTime _lastStateSaveTime = DateTime.MinValue;
        private readonly TimeSpan _minimumSaveInterval = TimeSpan.FromSeconds(3); // Prevent excessive DB writes
        private Dictionary<string, ViewSortingState> _defaultSortingStates = null;

        public StateManagerService(
            ProfileConfigurationService profileConfigService,
            ProfileManager profileManager,
            IServiceProvider serviceProvider,
            ThemeService themeService,
            AudioMonitorService audioMonitorService,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _profileConfigService = profileConfigService;
            _profileManager = profileManager;
            _serviceProvider = serviceProvider;
            _themeService = themeService;
            _audioMonitorService = audioMonitorService;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;

            // Initialize default sorting states
            InitializeDefaultSortingStates();
        }

        /// <summary>
        /// Safely gets current profile ID, initializing if necessary.
        /// </summary>
        private async Task<int> GetCurrentProfileId()
        {
            try
            {
                var profile = await _profileManager.GetCurrentProfileAsync();
                return profile.ProfileID;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Failed to get current profile ID",
                    "Unable to determine the current profile. Some settings may not be saved or loaded correctly.",
                    ex,
                    true);

                return -1; // Sentinel value to indicate error
            }
        }

        /// <summary>
        /// Loads and applies application state with comprehensive error handling.
        /// </summary>
        public async Task LoadAndApplyState(bool profileSwitch = false)
        {
            if (_isInitialized && profileSwitch == false) return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profileId = await GetCurrentProfileId();
                    if (profileId < 0)
                    {
                        LoadDefaultState();
                        return;
                    }

                    var config = await _profileConfigService.GetProfileConfig(profileId);
                    if (config == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to load profile configuration",
                            "Using default state instead.",
                            null,
                            true);

                        LoadDefaultState();
                        return;
                    }

                    var mainVM = _serviceProvider.GetService<MainViewModel>();
                    var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
                    var trackQueueVM = _serviceProvider.GetService<TrackQueueViewModel>();

                    // If switching profiles, stop current playback first
                    if (profileSwitch && trackControlVM != null)
                    {
                        _errorHandlingService.SafeExecute(() => trackControlVM.StopPlayback(),
                            "Stopping playback before profile switch",
                            ErrorSeverity.Playback,
                            false);
                    }

                    // Load and apply state components with individual error handling
                    await LoadVolumeState(trackControlVM, config);
                    await LoadViewState(mainVM, config);
                    await LoadSortingState(mainVM, config);
                    await LoadThemeState(config);
                    await LoadDynamicPauseState(trackControlVM, config);
                    await LoadQueueState(trackControlVM, trackQueueVM);

                    // Navigate home if possible
                    if (mainVM != null)
                    {
                        await _errorHandlingService.SafeExecuteAsync(
                            async () => await mainVM.Navigate("Home"),
                            "Navigating to home after state load",
                            ErrorSeverity.NonCritical,
                            false);
                    }

                    _isInitialized = true;

                    // Notify components about state changes
                    _messenger.Send(new ProfileStateLoadedMessage(config.ProfileID));
                },
                "Loading and applying application state",
                ErrorSeverity.Critical);
        }

        /// <summary>
        /// Loads volume state with error handling.
        /// </summary>
        private async Task LoadVolumeState(TrackControlViewModel trackControlVM, ProfileConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                () =>
                {
                    if (trackControlVM != null && config.LastVolume > 0)
                    {
                        trackControlVM.TrackVolume = config.LastVolume / 100.0f;
                        trackControlVM.SetVolume();
                    }
                    return Task.CompletedTask;
                },
                "Loading volume state",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Loads view state with error handling.
        /// </summary>
        private async Task LoadViewState(MainViewModel mainVM, ProfileConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                () =>
                {
                    if (mainVM != null && !string.IsNullOrEmpty(config.ViewState))
                    {
                        try
                        {
                            var viewState = JsonSerializer.Deserialize<ViewState>(config.ViewState);
                            if (viewState != null)
                            {
                                if (Enum.TryParse<ViewType>(viewState.CurrentView, out var viewType))
                                {
                                    mainVM.CurrentViewType = viewType;
                                }

                                if (Enum.TryParse<ContentType>(viewState.ContentType, true, out var contentType))
                                {
                                    mainVM.CurrentContentType = contentType;
                                }
                            }
                        }
                        catch (JsonException ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Failed to parse view state",
                                "The view state JSON could not be parsed.",
                                ex,
                                false);
                        }
                    }
                    return Task.CompletedTask;
                },
                "Loading view state",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Loads sorting state with error handling.
        /// </summary>
        private async Task LoadSortingState(MainViewModel mainVM, ProfileConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                () =>
                {
                    if (mainVM != null)
                    {
                        if (!string.IsNullOrEmpty(config.SortingState))
                        {
                            try
                            {
                                var sortingStates = JsonSerializer.Deserialize<Dictionary<string, ViewSortingState>>(config.SortingState);
                                if (sortingStates != null)
                                {
                                    mainVM.SetSortingStates(sortingStates);
                                    mainVM.LoadSortStateForContentType(mainVM.CurrentContentType);
                                }
                            }
                            catch (Exception ex)
                            {
                                _errorHandlingService.LogError(
                                    ErrorSeverity.NonCritical,
                                    "Error loading sorting state",
                                    "The sorting state could not be loaded from the profile configuration.",
                                    ex,
                                    false);

                                LoadDefaultSortingState(mainVM);
                            }
                        }
                        else
                        {
                            LoadDefaultSortingState(mainVM);
                        }
                    }
                    return Task.CompletedTask;
                },
                "Loading sorting state",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Loads theme state with error handling.
        /// </summary>
        private async Task LoadThemeState(ProfileConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                () =>
                {
                    // Load theme
                    var themeConfig = ThemeConfiguration.FromJson(config.Theme);
                    if (themeConfig.ThemeType == PresetTheme.Custom)
                    {
                        _themeService.ApplyTheme(themeConfig.ToThemeColors());
                    }
                    else
                    {
                        _themeService.ApplyPresetTheme(themeConfig.ThemeType);
                    }
                    return Task.CompletedTask;
                },
                "Loading theme state",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Loads dynamic pause state with error handling.
        /// </summary>
        private async Task LoadDynamicPauseState(TrackControlViewModel trackControlVM, ProfileConfig config)
        {
            await _errorHandlingService.SafeExecuteAsync(
                () =>
                {
                    // Load dynamic pause setting
                    _audioMonitorService.EnableDynamicPause(config.DynamicPause);
                    if (trackControlVM != null)
                    {
                        trackControlVM.UpdateDynamicPause(config.DynamicPause);
                    }
                    return Task.CompletedTask;
                },
                "Loading dynamic pause state",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Loads queue state with error handling.
        /// </summary>
        private async Task LoadQueueState(TrackControlViewModel trackControlVM, TrackQueueViewModel trackQueueVM)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Load queue state and playback settings but skip if any of the components are not available
                    if (trackQueueVM == null) return;

                    await trackQueueVM.LoadLastPlayedQueue();

                    // Update UI elements for shuffle and repeat modes
                    if (trackControlVM != null)
                    {
                        await trackControlVM.UpdateTrackInfoWithIcons();
                    }
                },
                "Loading queue state",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Initializes default sorting states.
        /// </summary>
        private void InitializeDefaultSortingStates()
        {
            _defaultSortingStates = new Dictionary<string, ViewSortingState>
            {
                [ContentType.Library.ToString().ToLower()] = new ViewSortingState
                {
                    SortType = SortType.Name,
                    SortDirection = SortDirection.Ascending
                },
                [ContentType.Artist.ToString().ToLower()] = new ViewSortingState
                {
                    SortType = SortType.Name,
                    SortDirection = SortDirection.Ascending
                },
                [ContentType.Album.ToString().ToLower()] = new ViewSortingState
                {
                    SortType = SortType.Name,
                    SortDirection = SortDirection.Ascending
                },
                [ContentType.Genre.ToString().ToLower()] = new ViewSortingState
                {
                    SortType = SortType.Name,
                    SortDirection = SortDirection.Ascending
                },
                [ContentType.Folder.ToString().ToLower()] = new ViewSortingState
                {
                    SortType = SortType.Name,
                    SortDirection = SortDirection.Ascending
                }
            };
        }

        /// <summary>
        /// Loads default sorting state for a view model.
        /// </summary>
        private void LoadDefaultSortingState(MainViewModel mainVM)
        {
            if (mainVM == null) return;

            if (_defaultSortingStates == null)
            {
                InitializeDefaultSortingStates();
            }

            mainVM.SetSortingStates(_defaultSortingStates);
            mainVM.LoadSortStateForContentType(mainVM.CurrentContentType);
        }

        /// <summary>
        /// Loads default application state for when profile config is unavailable.
        /// </summary>
        private void LoadDefaultState()
        {
            _errorHandlingService.SafeExecuteAsync(
                () =>
                {
                    var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
                    var trackQueueVM = _serviceProvider.GetService<TrackQueueViewModel>();
                    var mainVM = _serviceProvider.GetService<MainViewModel>();

                    if (trackControlVM == null) return Task.CompletedTask;

                    // Stop any current playback
                    trackControlVM.StopPlayback();

                    // Set default volume
                    trackControlVM.TrackVolume = 0.5f;
                    trackControlVM.SetVolume();

                    // Reset queue state if available
                    if (trackQueueVM != null)
                    {
                        trackQueueVM.NowPlayingQueue.Clear();
                        trackQueueVM.CurrentTrack = null;
                    }

                    // Set default theme
                    _themeService.ApplyPresetTheme(PresetTheme.Dark);

                    // Set default view state
                    if (mainVM != null)
                    {
                        mainVM.CurrentViewType = ViewType.Card;
                        mainVM.CurrentContentType = ContentType.Home;
                        LoadDefaultSortingState(mainVM);
                    }

                    // Update UI elements
                    trackControlVM.UpdateMainIcons();

                    return Task.CompletedTask;
                },
                "Loading default application state",
                ErrorSeverity.NonCritical,
                true);
        }

        /// <summary>
        /// Saves volume state with error handling and throttling.
        /// </summary>
        public async Task SaveVolumeState(float volume)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profileId = await GetCurrentProfileId();
                    if (profileId < 0) return;

                    // Use the direct volume update method instead of getting config first
                    int volumeInt = (int)(volume * 100);
                    await _profileConfigService.UpdateVolume(profileId, volumeInt);
                },
                "Saving volume state",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Saves current application state with error handling and throttling.
        /// </summary>
        public async Task SaveCurrentState()
        {
            // Throttle save operations to prevent excessive database writes
            if (DateTime.Now - _lastStateSaveTime < _minimumSaveInterval)
            {
                return;
            }

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    _lastStateSaveTime = DateTime.Now;

                    var profileId = await GetCurrentProfileId();
                    if (profileId < 0) return;

                    var mainVM = _serviceProvider.GetService<MainViewModel>();
                    if (mainVM == null) return;

                    try
                    {
                        // Serialize view state
                        var viewState = new ViewState
                        {
                            CurrentView = mainVM.CurrentViewType.ToString(),
                            ContentType = mainVM.CurrentContentType.ToString()
                        };
                        string viewStateJson = JsonSerializer.Serialize(viewState);

                        // Serialize sorting states
                        var currentSortingStates = mainVM.GetSortingStates();
                        string sortingStateJson = JsonSerializer.Serialize(currentSortingStates);

                        // Use direct update method to avoid cache double-fetch
                        await _profileConfigService.UpdateViewAndSortState(profileId, viewStateJson, sortingStateJson);
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Error serializing application state",
                            "Failed to convert current application state to JSON format.",
                            ex,
                            false);
                    }
                },
                "Saving current application state",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Resets application state to defaults in case of corruption.
        /// </summary>
        public async Task ResetStateToDefaults()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    LoadDefaultState();

                    var profileId = await GetCurrentProfileId();
                    if (profileId < 0) return;

                    // Reset profile configuration to defaults
                    await _profileConfigService.ResetProfileToDefaults(profileId);

                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Application state reset to defaults",
                        "The application state has been reset to defaults due to corruption or errors.",
                        null,
                        true);
                },
                "Resetting application state to defaults",
                ErrorSeverity.NonCritical);
        }
    }

    public class ProfileStateLoadedMessage
    {
        public int ProfileId { get; }

        public ProfileStateLoadedMessage(int profileId)
        {
            ProfileId = profileId;
        }
    }

    public class ViewState
    {
        public string CurrentView { get; set; }
        public string ContentType { get; set; } = Features.Library.ViewModels.ContentType.Folder.ToString();
    }

    public class ViewSortingState
    {
        public SortType SortType { get; set; }
        public SortDirection SortDirection { get; set; }
    }
}