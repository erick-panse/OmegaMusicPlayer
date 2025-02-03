using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;

namespace OmegaPlayer.Features.Library.Services
{
    public class BlacklistedDirectoryService
    {
        private readonly BlacklistedDirectoryRepository _repository;

        public BlacklistedDirectoryService(BlacklistedDirectoryRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<BlacklistedDirectory>> GetBlacklistedDirectories(int profileId)
        {
            return await _repository.GetByProfile(profileId);
        }

        public async Task<int> AddBlacklistedDirectory(int profileId, string path)
        {
            var directory = new BlacklistedDirectory
            {
                ProfileID = profileId,
                Path = path
            };
            return await _repository.Add(directory);
        }

        public async Task RemoveBlacklistedDirectory(int blacklistId)
        {
            await _repository.Remove(blacklistId);
        }
    }
}