using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Profile;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Profile.Services
{
    public class ProfileService
    {
        private readonly ProfileRepository _profileRepository;

        public ProfileService(ProfileRepository profileRepository)
        {
            _profileRepository = profileRepository;
        }

        public async Task<Profiles> GetProfileById(int profileID)
        {
            try
            {
                return await _profileRepository.GetProfileById(profileID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching Profile by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<List<Profiles>> GetAllProfiles()
        {
            try
            {
                return await _profileRepository.GetAllProfiles();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all Profiles: {ex.Message}");
                throw;
            }
        }

        public async Task<int> AddProfile(Profiles profile)
        {
            try
            {
                return await _profileRepository.AddProfile(profile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding Profile: {ex.Message}");
                throw;
            }
        }

        public async Task UpdateProfile(Profiles profile)
        {
            try
            {
                await _profileRepository.UpdateProfile(profile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating Profile: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteProfile(int profileID)
        {
            try
            {
                await _profileRepository.DeleteProfile(profileID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting Profile: {ex.Message}");
                throw;
            }
        }
    }
}
