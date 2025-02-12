using System.Text.Json;
using OmegaPlayer.Infrastructure.Services;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Library.ViewModels;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Features.Shell.ViewModels;
using OmegaPlayer.UI;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;

namespace OmegaPlayer.Core.Services
{
    public class StateManagerService
    {
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly ProfileManager _profileManager;
        private readonly IServiceProvider _serviceProvider;
        private bool _isInitialized = false;

        public StateManagerService(
            ProfileConfigurationService profileConfigService,
            ProfileManager profileManager,
            IServiceProvider serviceProvider)
        {
            _profileConfigService = profileConfigService;
            _profileManager = profileManager;
            _serviceProvider = serviceProvider;
        }

        private async Task<int> GetCurrentProfileId()
        {
            await _profileManager.InitializeAsync();
            return _profileManager.CurrentProfile.ProfileID;
        }

        public async Task LoadAndApplyState()
        {
            if (_isInitialized) return;

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
                    }
                }

                // Load sorting state for all views
                if (!string.IsNullOrEmpty(config.SortingState) && mainVM != null)
                {
                    try
                    {
                        var sortingStates = JsonSerializer.Deserialize<Dictionary<string, ViewSortingState>>(config.SortingState);
                        if (sortingStates != null)
                        {
                            mainVM.SetSortingStates(sortingStates);

                            // Load initial view's sort state
                            mainVM.LoadSortStateForView("library");
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

                // Load queue state last
                if (trackQueueVM != null)
                {
                    await trackQueueVM.LoadLastPlayedQueue();
                }

                _isInitialized = true;
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
                ["library"] = new ViewSortingState
                {
                    SortType = SortType.Name,
                    SortDirection = SortDirection.Ascending
                },
                ["artists"] = new ViewSortingState
                {
                    SortType = SortType.Name,
                    SortDirection = SortDirection.Ascending
                },
                ["albums"] = new ViewSortingState
                {
                    SortType = SortType.Name,
                    SortDirection = SortDirection.Ascending
                },
                ["genres"] = new ViewSortingState
                {
                    SortType = SortType.Name,
                    SortDirection = SortDirection.Ascending
                }
            };

            mainVM.SetSortingStates(defaultSortingStates);
            mainVM.LoadSortStateForView("library");
        }

        private void LoadDefaultState()
        {
            var mainVM = _serviceProvider.GetService<MainViewModel>();
            var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();

            if (mainVM == null) return;

            LoadDefaultSortingState(mainVM);

            mainVM.CurrentViewType = ViewType.Card;


            if (trackControlVM == null) return;

            trackControlVM.TrackVolume = 0.5f;
            trackControlVM.SetVolume();


            _isInitialized = true;
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
                        CurrentView = mainVM.CurrentViewType.ToString()
                    };
                    config.ViewState = JsonSerializer.Serialize(viewState);

                    // Save sorting states for all views
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

    public class ViewState
    {
        public string CurrentView { get; set; }
    }

    public class ViewSortingState
    {
        public SortType SortType { get; set; }
        public SortDirection SortDirection { get; set; }
    }
}