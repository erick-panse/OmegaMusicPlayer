using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Profile;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Profile.Services
{
    public class UserActivityService
    {
        private readonly UserActivityRepository _userActivityRepository;

        public UserActivityService(UserActivityRepository userActivityRepository)
        {
            _userActivityRepository = userActivityRepository;
        }

        public async Task<UserActivity> GetUserActivityById(int userActivityID)
        {
            try
            {
                return await _userActivityRepository.GetUserActivityById(userActivityID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching UserActivity by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<List<UserActivity>> GetAllUserActivities()
        {
            try
            {
                return await _userActivityRepository.GetAllUserActivities();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all UserActivities: {ex.Message}");
                throw;
            }
        }

        public async Task AddUserActivity(UserActivity userActivity)
        {
            try
            {
                await _userActivityRepository.AddUserActivity(userActivity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding UserActivity: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteUserActivity(int userActivityID)
        {
            try
            {
                await _userActivityRepository.DeleteUserActivity(userActivityID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting UserActivity: {ex.Message}");
                throw;
            }
        }
    }
}
