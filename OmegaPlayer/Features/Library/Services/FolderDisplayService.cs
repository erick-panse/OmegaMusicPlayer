using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Core.Services;

namespace OmegaPlayer.Features.Library.Services
{
    public class FolderDisplayService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly StandardImageService _standardImageService;
        private readonly BlacklistedDirectoryService _blacklistService;
        private readonly ProfileManager _profileManager;

        public FolderDisplayService(
            AllTracksRepository allTracksRepository,
            StandardImageService standardImageService,
            BlacklistedDirectoryService blacklistService,
            ProfileManager profileManager)
        {
            _allTracksRepository = allTracksRepository;
            _standardImageService = standardImageService;
            _blacklistService = blacklistService;
            _profileManager = profileManager;
        }

        private async Task<int> GetCurrentProfileId()
        {
            await _profileManager.InitializeAsync();
            return _profileManager.CurrentProfile.ProfileID;
        }

        public async Task<List<FolderDisplayModel>> GetAllFoldersAsync()
        {
            var allTracks = _allTracksRepository.AllTracks;
            var blacklistedPaths = await _blacklistService.GetBlacklistedDirectories(await GetCurrentProfileId());

            // Extract paths from blacklisted directories into a HashSet for efficient lookup
            var blacklistedPathsSet = blacklistedPaths
                .Select(b => b.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Group tracks by folder path
            var folderGroups = allTracks
                .GroupBy(t => Path.GetDirectoryName(t.FilePath));

            var folders = new List<FolderDisplayModel>();

            foreach (var group in folderGroups)
            {
                // Normalize the folder path for comparison with blacklisted paths
                var normalizedPath = group.Key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                // Check if this folder path is blacklisted
                if (blacklistedPathsSet.Contains(normalizedPath))
                    continue; // Skip this folder as it's blacklisted

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

        public async Task LoadFolderCoverAsync(FolderDisplayModel folder, string coverPath, string size = "low")
        {
            try
            {
                if (string.IsNullOrEmpty(coverPath)) return;

                switch (size.ToLower())
                {
                    case "low":
                        folder.Cover = await _standardImageService.LoadLowQualityAsync(coverPath);
                        break;
                    case "medium":
                        folder.Cover = await _standardImageService.LoadMediumQualityAsync(coverPath);
                        break;
                    case "high":
                        folder.Cover = await _standardImageService.LoadHighQualityAsync(coverPath);
                        break;
                    case "detail":
                        folder.Cover = await _standardImageService.LoadDetailQualityAsync(coverPath);
                        break;
                    default:
                        folder.Cover = await _standardImageService.LoadLowQualityAsync(coverPath);
                        break;
                }

                folder.CoverSize = size;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading folder cover: {ex.Message}");
            }
        }

    }
}