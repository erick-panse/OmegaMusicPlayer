using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
{
    public class ConfigService
    {
        private readonly ConfigRepository _configRepository;

        public ConfigService(ConfigRepository configRepository)
        {
            _configRepository = configRepository;
        }

        public async Task<Config> GetConfigById(int configID)
        {
            try
            {
                return await _configRepository.GetConfigById(configID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching config by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Config>> GetAllConfigs()
        {
            try
            {
                return await _configRepository.GetAllConfigs();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all configs: {ex.Message}");
                throw;
            }
        }

        public async Task<int> AddConfig(Config config)
        {
            try
            {
                return await _configRepository.AddConfig(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding config: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateConfig(Config config)
        {
            try
            {
                await _configRepository.UpdateConfig(config);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating config: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteConfig(int configID)
        {
            try
            {
                await _configRepository.DeleteConfig(configID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting config: {ex.Message}");
                throw;
            }
        }
    }
}
