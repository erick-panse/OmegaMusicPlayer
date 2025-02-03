using OmegaPlayer.Core.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services
{
    public class GlobalConfigurationService
    {
        private readonly GlobalConfigRepository _globalConfigRepository;

        public GlobalConfigurationService(GlobalConfigRepository globalConfigRepository)
        {
            _globalConfigRepository = globalConfigRepository;
        }

        public async Task<GlobalConfig> GetGlobalConfig()
        {
            var config = await _globalConfigRepository.GetGlobalConfig();
            if (config == null)
            {
                var id = await _globalConfigRepository.CreateDefaultGlobalConfig();
                config = await _globalConfigRepository.GetGlobalConfig();
            }
            return config;
        }

        public async Task UpdateLastUsedProfile(int profileId)
        {
            var config = await GetGlobalConfig();
            config.LastUsedProfile = profileId;
            await _globalConfigRepository.UpdateGlobalConfig(config);
        }

        public async Task UpdateLanguage(string language)
        {
            var config = await GetGlobalConfig();
            config.LanguagePreference = language;
            await _globalConfigRepository.UpdateGlobalConfig(config);
        }

    }
}