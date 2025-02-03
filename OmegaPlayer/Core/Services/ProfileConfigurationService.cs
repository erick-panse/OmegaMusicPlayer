using OmegaPlayer.Core.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services
{
    public class ProfileConfigurationService
    {
        private readonly ProfileConfigRepository _profileConfigRepository;

        public ProfileConfigurationService(ProfileConfigRepository profileConfigRepository)
        {
            _profileConfigRepository = profileConfigRepository;
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

        public async Task UpdateProfileTheme(int profileId, string theme, string mainColor, string secondaryColor)
        {
            var config = await GetProfileConfig(profileId);
            config.Theme = theme;
            config.MainColor = mainColor;
            config.SecondaryColor = secondaryColor;
            await _profileConfigRepository.UpdateProfileConfig(config);
        }

        public async Task UpdatePlaybackSettings(int profileId, float speed, bool dynamicPause, string outputDevice)
        {
            var config = await GetProfileConfig(profileId);
            config.DefaultPlaybackSpeed = speed;
            config.DynamicPause = dynamicPause;
            config.OutputDevice = outputDevice;
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