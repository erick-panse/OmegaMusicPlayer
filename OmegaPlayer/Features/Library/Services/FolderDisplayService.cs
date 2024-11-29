using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Services.Cache;
using OmegaPlayer.Infrastructure.Data.Repositories;

namespace OmegaPlayer.Features.Library.Services
{
    public class FolderDisplayService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly ImageCacheService _imageCacheService;

        public FolderDisplayService(
            AllTracksRepository allTracksRepository,
            ImageCacheService imageCacheService)
        {
            _allTracksRepository = allTracksRepository;
            _imageCacheService = imageCacheService;
        }

        public async Task<List<FolderDisplayModel>> GetFoldersPageAsync(int page, int pageSize)
        {
            var allTracks = _allTracksRepository.AllTracks;

            // Group tracks by folder path
            var folderGroups = allTracks
                .GroupBy(t => Path.GetDirectoryName(t.FilePath))
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            var folders = new List<FolderDisplayModel>();

            foreach (var group in folderGroups)
            {
                var folderTracks = group.ToList();

                var folderModel = new FolderDisplayModel
                {
                    FolderPath = group.Key,
                    FolderName = Path.GetFileName(group.Key),
                    TrackIDs = folderTracks.Select(t => t.TrackID).ToList(),
                    TotalDuration = TimeSpan.FromTicks(folderTracks.Sum(t => t.Duration.Ticks))
                };

                folders.Add(folderModel);

                // Load thumbnail for the first track in the folder
                var firstTrack = folderTracks.FirstOrDefault();
                if (firstTrack != null && !string.IsNullOrEmpty(firstTrack.CoverPath))
                {
                    await LoadFolderCoverAsync(folderModel, firstTrack.CoverPath, "low");
                }
            }

            return folders;
        }

        public async Task<List<TrackDisplayModel>> GetFolderTracksAsync(string folderPath)
        {
            return _allTracksRepository.AllTracks
                .Where(t => Path.GetDirectoryName(t.FilePath) == folderPath)
                .ToList();
        }

        private async Task LoadFolderCoverAsync(FolderDisplayModel folder, string coverPath, string size = "low")
        {
            try
            {
                if (string.IsNullOrEmpty(coverPath)) return;

                int coverSize = size == "high" ? 160 : 110;
                folder.Cover = await _imageCacheService.LoadThumbnailAsync(coverPath, coverSize, coverSize);
                folder.CoverSize = size;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading folder cover: {ex.Message}");
            }
        }

        public async Task LoadHighResFolderCoverAsync(FolderDisplayModel folder, string coverPath)
        {
            await LoadFolderCoverAsync(folder, coverPath, "high");
        }
    }
}