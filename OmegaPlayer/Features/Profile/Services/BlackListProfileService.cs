using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Profile;

namespace OmegaPlayer.Features.Profile.Services
{
    public class BlackListProfileService
    {
        private readonly BlackListProfileRepository _blackListProfileRepository;

        public BlackListProfileService(BlackListProfileRepository blackListProfileRepository)
        {
            _blackListProfileRepository = blackListProfileRepository;
        }

        public async Task<BlackListProfile> GetBlackListProfile(int blackListID, int profileID)
        {
            try
            {
                return await _blackListProfileRepository.GetBlackListProfile(blackListID, profileID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching BlackListProfile: {ex.Message}");
                throw;
            }
        }

        public async Task<List<BlackListProfile>> GetAllBlackListProfiles()
        {
            try
            {
                return await _blackListProfileRepository.GetAllBlackListProfiles();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all BlackListProfiles: {ex.Message}");
                throw;
            }
        }

        public async Task AddBlackListProfile(BlackListProfile blackListProfile)
        {
            try
            {
                await _blackListProfileRepository.AddBlackListProfile(blackListProfile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding BlackListProfile: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteBlackListProfile(int blackListID, int profileID)
        {
            try
            {
                await _blackListProfileRepository.DeleteBlackListProfile(blackListID, profileID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting BlackListProfile: {ex.Message}");
                throw;
            }
        }
    }
}
