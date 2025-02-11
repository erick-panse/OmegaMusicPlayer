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

                var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
                var mainVM = _serviceProvider.GetService<MainViewModel>();
                var trackQueueVM = _serviceProvider.GetService<TrackQueueViewModel>();

                // Load volume state first
                if (trackControlVM != null)
                {
                    // Convert LastVolume from percentage to float (0-1)
                    float volume = config.LastVolume / 100.0f;
                    trackControlVM.TrackVolume = volume;
                    trackControlVM.SetVolume(); // Ensure volume is applied to audio system
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

                // Load sorting state - enhanced to ensure proper order
                if (!string.IsNullOrEmpty(config.SortingState))
                {
                    var sortingState = JsonSerializer.Deserialize<SortingState>(config.SortingState);
                    if (mainVM != null && sortingState != null)
                    {
                        // First update the internal state
                        mainVM.SortDirection = sortingState.SortDirection;
                        mainVM.SelectedSortType = sortingState.SortType;

                        // Then update the display text in the correct order
                        mainVM.SelectedSortTypeText = MapSortTypeToDisplayText(sortingState.SortType);
                        mainVM.SelectedSortDirectionText = sortingState.SortDirection == SortDirection.Ascending ? "A-Z" : "Z-A";

                        // Ensure sorting is properly applied
                        await Task.Delay(100); // Small delay to ensure UI is ready
                        mainVM.UpdateSorting();
                    }
                }

                // Load queue state last (includes shuffle, repeat mode, and current track)
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

        private string MapSortTypeToDisplayText(SortType sortType)
        {
            return sortType switch
            {
                SortType.Name => "Name",
                SortType.Artist => "Artist",
                SortType.Album => "Album",
                SortType.Duration => "Duration",
                SortType.Genre => "Genre",
                SortType.ReleaseDate => "Release Date",
                _ => "Name"
            };
        }

        private void LoadDefaultState()
        {
            var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
            var mainVM = _serviceProvider.GetService<MainViewModel>();

            if (mainVM != null)
            {
                mainVM.CurrentViewType = ViewType.Card;
                mainVM.SelectedSortType = SortType.Name;
                mainVM.SortDirection = SortDirection.Ascending;
                mainVM.SelectedSortTypeText = "Name";
                mainVM.SelectedSortDirectionText = "A-Z";
                mainVM.UpdateSorting();
            }

            if (trackControlVM != null)
            {
                trackControlVM.TrackVolume = 0.5f;
                trackControlVM.SetVolume();
            }

            _isInitialized = true;
        }

        public async Task SaveCurrentState()
        {
            try
            {
                var profileId = await GetCurrentProfileId();
                var config = await _profileConfigService.GetProfileConfig(profileId);
                if (config == null) return;

                var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
                var mainVM = _serviceProvider.GetService<MainViewModel>();

                // Save volume as a percentage (0-100)
                if (trackControlVM != null)
                {
                    config.LastVolume = (int)(trackControlVM.TrackVolume * 100);
                }

                if (mainVM != null)
                {
                    // Save view state
                    var viewState = new ViewState
                    {
                        CurrentView = mainVM.CurrentViewType.ToString()
                    };
                    config.ViewState = JsonSerializer.Serialize(viewState);

                    // Save sorting state
                    var sortingState = new SortingState
                    {
                        SortType = mainVM.SelectedSortType,
                        SortDirection = mainVM.SortDirection
                    };
                    config.SortingState = JsonSerializer.Serialize(sortingState);
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

    public class SortingState
    {
        public SortType SortType { get; set; }
        public SortDirection SortDirection { get; set; }
    }
}