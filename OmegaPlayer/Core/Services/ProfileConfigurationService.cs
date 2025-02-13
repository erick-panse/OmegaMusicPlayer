using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Playback.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.UI;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services
{
    public class ProfileConfigurationService
    {
        private readonly ProfileConfigRepository _profileConfigRepository;
        private readonly IMessenger _messenger;

        public ProfileConfigurationService(
            ProfileConfigRepository profileConfigRepository,
            IMessenger messenger)
        {
            _profileConfigRepository = profileConfigRepository;
            _messenger = messenger;
        }

        public async Task<ProfileConfig> GetProfileConfig(int profileId)
        {
            var config = await _profileConfigRepository.GetProfileConfig(profileId);
            if (config == null)
            {
                var id = await _profileConfigRepository.CreateProfileConfig(profileId);
                config = await _profileConfigRepository.GetProfileConfig(profileId);
            }
            return config;
        }

        public async Task UpdateProfileTheme(int profileId, ThemeConfiguration themeConfig)
        {
            var config = await GetProfileConfig(profileId);
            config.Theme = themeConfig.ToJson();
            await _profileConfigRepository.UpdateProfileConfig(config);

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
        }

        public async Task UpdateProfileConfig(ProfileConfig config)
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
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating profile config: {ex.Message}");
                throw;
            }
        }

        public async Task UpdatePlaybackSettings(int profileId, bool dynamicPause)
        {
            var config = await GetProfileConfig(profileId);
            config.DynamicPause = dynamicPause;
            await _profileConfigRepository.UpdateProfileConfig(config);
        }

        public async Task UpdateVolume(int profileId, int volume)
        {
            var config = await GetProfileConfig(profileId);
            config.LastVolume = volume;
            await _profileConfigRepository.UpdateProfileConfig(config);
        }

        public async Task UpdateBlacklist(int profileId, string[] blacklistDirs)
        {
            var config = await GetProfileConfig(profileId);
            config.BlacklistDirectory = blacklistDirs;
            await _profileConfigRepository.UpdateProfileConfig(config);
        }

        public async Task UpdateViewAndSortState(int profileId, string viewState, string sortingState)
        {
            var config = await GetProfileConfig(profileId);
            config.ViewState = viewState;
            config.SortingState = sortingState;
            await _profileConfigRepository.UpdateProfileConfig(config);
        }

        public async Task UpdateEqualizer(int profileId, string presets)
        {
            var config = await GetProfileConfig(profileId);
            config.EqualizerPresets = presets;
            await _profileConfigRepository.UpdateProfileConfig(config);
        }
    }
}