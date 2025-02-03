using System.Threading.Tasks;
using OmegaPlayer.Features.Profile.Services;
using OmegaPlayer.Features.Profile.Models;
using System.Linq;
using System;
using OmegaPlayer.Infrastructure.Services;

namespace OmegaPlayer.Core.Services
{
    public class ProfileManager
    {
        private readonly ProfileService _profileService;
        private readonly GlobalConfigurationService _globalConfigService;
        private readonly ProfileConfigurationService _profileConfigService;
        private Profiles _currentProfile;

        public Profiles CurrentProfile => _currentProfile;

        public ProfileManager(
            ProfileService profileService,
            GlobalConfigurationService globalConfigService,
            ProfileConfigurationService profileConfigService)
        {
            _profileService = profileService;
            _globalConfigService = globalConfigService;
            _profileConfigService = profileConfigService;

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
                _currentProfile = profiles.FirstOrDefault(p => p.ProfileID == globalConfig.LastUsedProfile.Value)
                    ?? profiles.First();
            }
            else
            {
                _currentProfile = profiles.First();
                await _globalConfigService.UpdateLastUsedProfile(_currentProfile.ProfileID);
            }

            var config = await _profileConfigService.GetProfileConfig(_currentProfile.ProfileID);
        }

        public async Task SwitchProfile(Profiles newProfile)
        {
            _currentProfile = newProfile;
            await _globalConfigService.UpdateLastUsedProfile(newProfile.ProfileID);
            var config = await _profileConfigService.GetProfileConfig(newProfile.ProfileID);
        }
    }
}