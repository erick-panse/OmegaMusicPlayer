using OmegaPlayer.Models;
using OmegaPlayer.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OmegaPlayer.Services
{
    public class BlackListService
    {
        private readonly BlackListRepository _blackListRepository;

        public BlackListService(BlackListRepository blackListRepository)
        {
            _blackListRepository = blackListRepository;
        }

        public async Task<BlackList> GetBlackListById(int blackListID)
        {
            try
            {
                return await _blackListRepository.GetBlackListById(blackListID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching BlackList by ID: {ex.Message}");
                throw;
            }
        }

        public async Task<List<BlackList>> GetAllBlackLists()
        {
            try
            {
                return await _blackListRepository.GetAllBlackLists();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching all BlackLists: {ex.Message}");
                throw;
            }
        }

        public async Task<int> AddBlackList(BlackList blackList)
        {
            try
            {
                return await _blackListRepository.AddBlackList(blackList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding BlackList: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteBlackList(int blackListID)
        {
            try
            {
                await _blackListRepository.DeleteBlackList(blackListID);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting BlackList: {ex.Message}");
                throw;
            }
        }
    }
}
