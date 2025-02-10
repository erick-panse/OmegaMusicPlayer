using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services
{
    public class ProfileConfigurationService
    {
        private readonly ProfileConfigRepository _profileConfigRepository;
        private readonly IMessenger _messenger;

        public ProfileConfigurationService(ProfileConfigRepository profileConfigRepository, IMessenger messenger)
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

        public async Task UpdateProfileTheme(
            int profileId,
            string theme,
            string mainStartColor,
            string mainEndColor,
            string secondaryStartColor,
            string secondaryEndColor,
            string accentStartColor,
            string accentEndColor,
            string textStartColor,
            string textEndColor)
        {
            var config = await GetProfileConfig(profileId);
            config.Theme = theme;
            config.MainStartColor = mainStartColor;
            config.MainEndColor = mainEndColor;
            config.SecondaryStartColor = secondaryStartColor;
            config.SecondaryEndColor = secondaryEndColor;
            config.AccentStartColor = accentStartColor;
            config.AccentEndColor = accentEndColor;
            config.TextStartColor = textStartColor;
            config.TextEndColor = textEndColor;

            await _profileConfigRepository.UpdateProfileConfig(config);

            // Notify theme change
            var themeConfig = new ThemeConfiguration
            {
                ThemeType = theme switch
                {
                    "Light" => PresetTheme.Light,
                    "Dark" => PresetTheme.Dark,
                    _ => PresetTheme.Custom
                },
                MainStartColor = mainStartColor,
                MainEndColor = mainEndColor,
                SecondaryStartColor = secondaryStartColor,
                SecondaryEndColor = secondaryEndColor,
                AccentStartColor = accentStartColor,
                AccentEndColor = accentEndColor,
                TextStartColor = textStartColor,
                TextEndColor = textEndColor
            };

            _messenger.Send(new ThemeUpdatedMessage(themeConfig));
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

        public async Task UpdatePlaybackState(int profileId, int? trackId, int position, bool shuffle, string repeatMode)
        {
            var config = await GetProfileConfig(profileId);
            config.LastPlayedTrackID = trackId;
            config.LastPlayedPosition = position;
            config.ShuffleEnabled = shuffle;
            config.RepeatMode = repeatMode;
            await _profileConfigRepository.UpdateProfileConfig(config);
        }

        public async Task UpdateQueueState(int profileId, string queueState, string lastQueueState)
        {
            var config = await GetProfileConfig(profileId);
            config.QueueState = queueState;
            config.LastQueueState = lastQueueState;
            await _profileConfigRepository.UpdateProfileConfig(config);
        }

        public async Task UpdateViewAndSortState(int profileId, string viewState, string sortingState, string trackSortingState)
        {
            var config = await GetProfileConfig(profileId);
            config.ViewState = viewState;
            config.SortingState = sortingState;
            config.TrackSortingOrderState = trackSortingState;
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