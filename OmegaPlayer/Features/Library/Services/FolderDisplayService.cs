using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Services;

namespace OmegaPlayer.Features.Library.Services
{
    public class FolderDisplayService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly StandardImageService _standardImageService;
        private readonly ProfileConfigurationService _profileConfigurationService;
        private readonly ProfileManager _profileManager;
        private readonly IErrorHandlingService _errorHandlingService;

        public FolderDisplayService(
            AllTracksRepository allTracksRepository,
            StandardImageService standardImageService,
            ProfileConfigurationService profileConfigurationService,
            ProfileManager profileManager,
            IErrorHandlingService errorHandlingService)
        {
            _allTracksRepository = allTracksRepository;
            _standardImageService = standardImageService;
            _profileConfigurationService = profileConfigurationService;
            _profileManager = profileManager;
            _errorHandlingService = errorHandlingService;
        }

        private async Task<int> GetCurrentProfileId()
        {
            try
            {
                var profile = await _profileManager.GetCurrentProfileAsync();
                return profile.ProfileID;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to get current profile ID",
                    "Will use default profile ID of 1",
                    ex,
                    false);
                return 1; // Fallback to default profile ID
            }
        }

        public async Task<List<FolderDisplayModel>> GetAllFoldersAsync()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var allTracks = _allTracksRepository.AllTracks;

                    // If no tracks are available, return cached folders or empty list
                    if (allTracks == null || !allTracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No tracks available for folder display",
                            "Cannot create folder view without available tracks",
                            null,
                            false);
                        return new List<FolderDisplayModel>();
                    }

                    var blacklistedPaths = await _profileConfigurationService.GetBlacklistedDirectories(await GetCurrentProfileId());

                    // Extract paths from blacklisted directories into a HashSet for efficient lookup
                    var blacklistedPathsSet = blacklistedPaths
                        .Select(b => b.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // Group tracks by folder path with null safety
                    var folderGroups = allTracks
                        .Where(t => !string.IsNullOrEmpty(t.FilePath) && File.Exists(t.FilePath))
                        .GroupBy(t => Path.GetDirectoryName(t.FilePath));

                    var folders = new List<FolderDisplayModel>();

                    foreach (var group in folderGroups)
                    {
                        // Skip null groups (should not happen with the filter above, but just in case)
                        if (group.Key == null) continue;

                        // Normalize the folder path for comparison with blacklisted paths
                        var normalizedPath = group.Key.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        // Check if this folder path is blacklisted
                        if (blacklistedPathsSet.Contains(normalizedPath))
                            continue; // Skip this folder as it's blacklisted

                        var folderTracks = group.ToList();

                        var folderModel = new FolderDisplayModel
                        {
                            FolderPath = group.Key,
                            FolderName = Path.GetFileName(group.Key) ?? "Unknown Folder",
                            TrackIDs = folderTracks.Select(t => t.TrackID).ToList(),
                            TotalDuration = TimeSpan.FromTicks(folderTracks.Sum(t => t.Duration.Ticks))
                        };

                        folders.Add(folderModel);
                    }

                    return folders;
                },
                "Getting all folders for display",
                new List<FolderDisplayModel>(),
                ErrorSeverity.NonCritical, 
                false
            );
        }

        public async Task<List<TrackDisplayModel>> GetFolderTracksAsync(string folderPath)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Validate input
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        throw new ArgumentException("Folder path cannot be null or empty", nameof(folderPath));
                    }

                    // Get tracks from repository
                    var allTracks = _allTracksRepository.AllTracks;

                    // Return matching tracks
                    var folderTracks = allTracks
                        .Where(t => !string.IsNullOrEmpty(t.FilePath) &&
                               Path.GetDirectoryName(t.FilePath) == folderPath)
                        .ToList();

                    return folderTracks;
                },
                $"Getting tracks for folder: {folderPath}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical, 
                false
            );
        }

        public async Task LoadFolderCoverAsync(FolderDisplayModel folder, string coverPath, string size = "low", bool isVisible = false)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Validate inputs
                    if (folder == null)
                    {
                        throw new ArgumentNullException(nameof(folder), "Folder cannot be null");
                    }

                    if (string.IsNullOrEmpty(coverPath))
                    {
                        return; // No cover to load, exit silently
                    }

                    // Load image based on requested size
                    switch (size.ToLower())
                    {
                        case "low":
                            folder.Cover = await _standardImageService.LoadLowQualityAsync(coverPath, isVisible);
                            break;
                        case "medium":
                            folder.Cover = await _standardImageService.LoadMediumQualityAsync(coverPath, isVisible);
                            break;
                        case "high":
                            folder.Cover = await _standardImageService.LoadHighQualityAsync(coverPath, isVisible);
                            break;
                        case "detail":
                            folder.Cover = await _standardImageService.LoadDetailQualityAsync(coverPath, isVisible);
                            break;
                        default:
                            folder.Cover = await _standardImageService.LoadLowQualityAsync(coverPath, isVisible);
                            break;
                    }

                    folder.CoverSize = size;
                },
                $"Loading folder cover from path: {coverPath}",
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Loads folder cover asynchronously only if it's visible (optimized version)
        /// </summary>
        public async Task LoadFolderCoverIfVisibleAsync(FolderDisplayModel folder, string coverPath, bool isVisible, string size = "low")
        {
            // Only load if the folder is actually visible
            if (!isVisible)
            {
                // Still notify the service about the visibility state for cache management
                if (!string.IsNullOrEmpty(coverPath))
                {
                    await _standardImageService.NotifyImageVisible(coverPath, false);
                }
                return;
            }

            await LoadFolderCoverAsync(folder, coverPath, size, isVisible);
        }
    }
}