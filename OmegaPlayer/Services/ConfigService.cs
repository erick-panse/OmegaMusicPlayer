using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;

namespace OmegaPlayer.Services
{
    public class ConfigService
    {
        private readonly ConfigRepository _configRepository;

        public ConfigService(ConfigRepository configRepository)
        {
            _configRepository = configRepository;
        }

        // Fetch a configuration by its ID
        public async Task<Config> GetConfigById(int configId)
        {
            return await _configRepository.GetConfigById(configId);
        }

        // Fetch all configurations
        public async Task<List<Config>> GetAllConfigs()
        {
            return await _configRepository.GetAllConfigs();
        }

        // Add a new configuration
        public async Task<int> AddConfig(Config config)
        {
            return await _configRepository.AddConfig(config);
        }

        // Update an existing configuration
        public async Task UpdateConfig(Config config)
        {
            await _configRepository.UpdateConfig(config);
        }

        // Delete a configuration by its ID
        public async Task DeleteConfig(int configId)
        {
            await _configRepository.DeleteConfig(configId);
        }

        // Custom logic: Fetch last used profile's configuration
        public async Task<Config> GetLastUsedProfileConfig()
        {
            var configs = await _configRepository.GetAllConfigs();
            return configs?.FirstOrDefault(c => c.LastUsedProfile.HasValue);
        }

        // Method to update only the Volume
        public async Task UpdateVolume(int configId, int volume)
        {
            try
            {
                await _configRepository.UpdateVolume(configId, volume);
            }
            catch (Exception ex)
            {
                // Log exception or handle it accordingly
                Console.WriteLine($"An error occurred while updating the volume in the service: {ex.Message}");
                throw;
            }
        }
        // Custom logic: Save queue settings
        public async Task SaveQueueSettings(int configId, bool saveQueue)
        {
            var config = await _configRepository.GetConfigById(configId);
            if (config != null)
            {
                config.SaveQueue = saveQueue;
                await _configRepository.UpdateConfig(config);
            }
        }

        // Custom logic: Update playback settings
        public async Task UpdatePlaybackSettings(int configId, string playbackSpeed, string outputDevice)
        {
            var config = await _configRepository.GetConfigById(configId);
            if (config != null)
            {
                config.DefaultPlaybackSpeed = playbackSpeed;
                config.OutputDevice = outputDevice;
                await _configRepository.UpdateConfig(config);
            }
        }
    }
}
