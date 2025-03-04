using System.Text.Json;
using OmegaPlayer.Infrastructure.Services;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Features.Playback.Services;

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
        private bool _isInitialized = false;

        public StateManagerService(
            ProfileConfigurationService profileConfigService,
            ProfileManager profileManager,
            IServiceProvider serviceProvider,
            ThemeService themeService,
            AudioMonitorService audioMonitorService,
            IMessenger messenger)
        {
            _profileConfigService = profileConfigService;
            _profileManager = profileManager;
            _serviceProvider = serviceProvider;
            _themeService = themeService;
            _audioMonitorService = audioMonitorService;
            _messenger = messenger;
        }

        private async Task<int> GetCurrentProfileId()
        {
            await _profileManager.InitializeAsync();
            return _profileManager.CurrentProfile.ProfileID;
        }

        public async Task LoadAndApplyState(bool profileSwitch = false)
        {
            if (_isInitialized && profileSwitch == false) return;

            try
            {
                var config = await _profileConfigService.GetProfileConfig(await GetCurrentProfileId());
                if (config == null)
                {
                    LoadDefaultState();
                    return;
                }

                var mainVM = _serviceProvider.GetService<MainViewModel>();
                var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
                var trackQueueVM = _serviceProvider.GetService<TrackQueueViewModel>();

                // If switching profiles, stop current playback first
                if (profileSwitch && trackControlVM != null)
                {
                    trackControlVM.StopPlayback();
                }

                // Load volume state
                if (trackControlVM != null && config.LastVolume > 0)
                {
                    trackControlVM.TrackVolume = config.LastVolume / 100.0f;
                    trackControlVM.SetVolume();
                }

                // Load view state
                if (!string.IsNullOrEmpty(config.ViewState))
                {
                    var viewState = JsonSerializer.Deserialize<ViewState>(config.ViewState);
                    if (mainVM != null && viewState != null)
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

                // Load sorting state for all content types
                if (!string.IsNullOrEmpty(config.SortingState) && mainVM != null)
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
                        Console.WriteLine($"Error loading sorting state: {ex.Message}");
                        LoadDefaultSortingState(mainVM);
                    }
                }
                else
                {
                    LoadDefaultSortingState(mainVM);
                }

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

                // Load dynamic pause setting
                _audioMonitorService.EnableDynamicPause(config.DynamicPause);
                if (trackControlVM != null)
                {
                    trackControlVM.UpdateDynamicPause(config.DynamicPause);
                }

                // Load queue state and playback settings
                if (trackQueueVM != null)
                {
                    await trackQueueVM.LoadLastPlayedQueue();

                    // Update UI elements for shuffle and repeat modes
                    if (trackControlVM != null)
                    {
                        trackControlVM.UpdateMainIcons();
                        await trackControlVM.UpdateTrackInfo();
                    }
                }

                if (mainVM != null)
                {
                    await mainVM.Navigate("Home");
                }
                    _isInitialized = true;

                // Notify components about state changes
                _messenger.Send(new ProfileStateLoadedMessage(config.ProfileID));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading state: {ex.Message}");
                LoadDefaultState();
            }
        }

        private void LoadDefaultSortingState(MainViewModel? mainVM)
        {
            if (mainVM == null) return;

            var defaultSortingStates = new Dictionary<string, ViewSortingState>
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

            mainVM.SetSortingStates(defaultSortingStates);
            mainVM.LoadSortStateForContentType(mainVM.CurrentContentType);
        }

        private void LoadDefaultState()
        {
            var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
            var trackQueueVM = _serviceProvider.GetService<TrackQueueViewModel>();

            if (trackControlVM == null) return;

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

            // Update UI elements
            trackControlVM.UpdateMainIcons();
        }

        public async Task SaveVolumeState(float volume)
        {
            try
            {
                var profileId = await GetCurrentProfileId();
                var config = await _profileConfigService.GetProfileConfig(profileId);
                if (config == null) return;

                config.LastVolume = (int)(volume * 100);
                await _profileConfigService.UpdateProfileConfig(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving volume state: {ex.Message}");
            }
        }

        public async Task SaveCurrentState()
        {
            try
            {
                var profileId = await GetCurrentProfileId();
                var config = await _profileConfigService.GetProfileConfig(profileId);
                if (config == null) return;

                var mainVM = _serviceProvider.GetService<MainViewModel>();

                if (mainVM != null)
                {
                    // Save view state
                    var viewState = new ViewState
                    {
                        CurrentView = mainVM.CurrentViewType.ToString(),
                        ContentType = mainVM.CurrentContentType.ToString()
                    };
                    config.ViewState = JsonSerializer.Serialize(viewState);

                    // Save sorting states for all content types
                    var currentSortingStates = mainVM.GetSortingStates();
                    config.SortingState = JsonSerializer.Serialize(currentSortingStates);
                }

                await _profileConfigService.UpdateProfileConfig(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving state: {ex.Message}");
            }
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