using System.Threading.Tasks;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Profile.Models;
using System.Linq;
using System;
using OmegaPlayer.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace OmegaPlayer.Core.Services
{
    public class ProfileManager
    {
        private readonly ProfileService _profileService;
        private readonly GlobalConfigurationService _globalConfigService;
        private readonly IServiceProvider _serviceProvider;

        public Profiles CurrentProfile { get; private set; }

        public ProfileManager(
            ProfileService profileService,
            GlobalConfigurationService globalConfigService,
            IServiceProvider serviceProvider)
        {
            _profileService = profileService;
            _globalConfigService = globalConfigService;
            _serviceProvider = serviceProvider;

            InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            var profiles = await _profileService.GetAllProfiles();
            var globalConfig = await _globalConfigService.GetGlobalConfig();

            if (!profiles.Any())
            {
                var defaultProfile = new Profiles
                {
                    ProfileName = "Profile1",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                var profileId = await _profileService.AddProfile(defaultProfile);
                defaultProfile.ProfileID = profileId;
                profiles.Add(defaultProfile);
            }

            if (globalConfig.LastUsedProfile.HasValue)
            {
                CurrentProfile = profiles.FirstOrDefault(p => p.ProfileID == globalConfig.LastUsedProfile.Value)
                    ?? profiles.First();
            }
            else
            {
                CurrentProfile = profiles.First();
                await _globalConfigService.UpdateLastUsedProfile(CurrentProfile.ProfileID);
            }
        }

        public async Task SwitchProfile(Profiles newProfile)
        {
            CurrentProfile = newProfile;
            await _globalConfigService.UpdateLastUsedProfile(newProfile.ProfileID);

            var _stateManager = _serviceProvider.GetService<StateManagerService>();
            if (_stateManager == null) return;

            // Load the new profile's state
            await _stateManager.LoadAndApplyState(true);
        }

    }
}