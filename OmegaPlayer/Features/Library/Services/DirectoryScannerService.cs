using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Configuration.ViewModels;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.API;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Services.Database;
using OmegaPlayer.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.Services
{
    public class DirectoryScannerService
    {
        private readonly TracksService _trackService;
        private readonly TrackMetadataService _trackDataService;
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly LibraryMaintenanceService _maintenanceService;
        private readonly DeezerService _deezerService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        // Scanning status tracking
        public bool isScanningInProgress = false;
        public DateTime lastFullScanTime = DateTime.MinValue;
        private const int MIN_SCAN_INTERVAL_MINUTES = 2;

        // File formats we support
        private readonly string[] _supportedFormats = { ".mp3", ".wav", ".flac", ".ogg", ".aac", ".m4a" };

        public bool IsWatchingEnabled { get; private set; } = true;

        public DirectoryScannerService(
            TracksService trackService,
            TrackMetadataService trackDataService,
            ProfileConfigurationService profileConfigService,
            AllTracksRepository allTracksRepository,
            LibraryMaintenanceService maintenanceService,
            DeezerService deezerService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _trackService = trackService;
            _trackDataService = trackDataService;
            _profileConfigService = profileConfigService;
            _allTracksRepository = allTracksRepository;
            _maintenanceService = maintenanceService;
            _deezerService = deezerService;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            // Register for directory and blacklist change messages to trigger automatic rescans
            _messenger.Register<DirectoriesChangedMessage>(this, async (r, m) => await HandleDirectoriesChanged());
            _messenger.Register<BlacklistChangedMessage>(this, async (r, m) => await HandleBlacklistChanged());
        }

        /// <summary>
        /// Handles directory changes by triggering a rescan
        /// </summary>
        private async Task HandleDirectoriesChanged()
        {
            if (isScanningInProgress) return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    _errorHandlingService.LogInfo(
                        "Directory configuration changed",
                        "Triggering library rescan due to directory changes...");

                    // Get current profile and directories
                    var profileManager = App.ServiceProvider.GetService<ProfileManager>();
                    var directoriesService = App.ServiceProvider.GetService<DirectoriesService>();

                    if (profileManager != null && directoriesService != null)
                    {
                        var currentProfile = await profileManager.GetCurrentProfileAsync();
                        var directories = await directoriesService.GetAllDirectories();

                        // Overwrite the lastFullScanTime to rescan now
                        lastFullScanTime = DateTime.MinValue;

                        // Trigger rescan with current directories
                        await ScanDirectoriesAsync(directories, currentProfile.ProfileID);
                    }
                    else
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Unable to trigger rescan",
                            "Required services not available for automatic rescan after directory changes.",
                            null,
                            false);
                    }
                },
                "Handling directory configuration changes",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Handles blacklist changes by triggering a rescan
        /// </summary>
        private async Task HandleBlacklistChanged()
        {
            if (isScanningInProgress) return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    _errorHandlingService.LogInfo(
                        "Blacklist configuration changed",
                        "Triggering library rescan due to blacklist changes...");

                    // Get current profile and directories
                    var profileManager = App.ServiceProvider.GetService<ProfileManager>();
                    var directoriesService = App.ServiceProvider.GetService<DirectoriesService>();

                    if (profileManager != null && directoriesService != null)
                    {
                        var currentProfile = await profileManager.GetCurrentProfileAsync();
                        var directories = await directoriesService.GetAllDirectories();

                        // Overwrite the lastFullScanTime to rescan now
                        lastFullScanTime = DateTime.MinValue;

                        // Trigger rescan with updated blacklist
                        await ScanDirectoriesAsync(directories, currentProfile.ProfileID);
                    }
                    else
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Unable to trigger rescan",
                            "Required services not available for automatic rescan after blacklist changes.",
                            null,
                            false);
                    }
                },
                "Handling blacklist configuration changes",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Scans all directories for music files, respecting blacklists
        /// </summary>
        public async Task ScanDirectoriesAsync(List<Directories> directories, int profileId)
        {
            // Prevent concurrent scanning
            if (isScanningInProgress)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Directory scan already in progress",
                    "Please wait for the current scan to complete before starting another scan.",
                    null,
                    false);
                return;
            }

            // Respect minimum scan interval to prevent excessive scanning
            if (DateTime.Now.Subtract(lastFullScanTime).TotalMinutes < MIN_SCAN_INTERVAL_MINUTES)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Scan requested too soon",
                    $"Library scan was performed less than {MIN_SCAN_INTERVAL_MINUTES} minutes ago. Skipping scan.",
                    null,
                    false);
                return;
            }

            isScanningInProgress = true;

            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                // Get blacklisted directories from profile config
                var config = await _profileConfigService.GetProfileConfig(profileId);
                var blacklistedPaths = config.BlacklistDirectory ?? Array.Empty<string>();

                // Wait for _allTracksRepository to load to notify user correctly
                await _allTracksRepository.LoadTracks();
                var allTracksCount = _allTracksRepository.AllTracks.Count;

                _messenger.Send(new LibraryScanStartedMessage());
                int processedFiles = 0;
                int addedFiles = 0;
                int updatedFiles = 0;
                int removedFiles = 0;

                // Normalize blacklisted paths for comparison
                var normalizedBlacklist = blacklistedPaths
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Select(p => p.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var directory in directories)
                {
                    // Scan this directory if it's not blacklisted
                    if (!normalizedBlacklist.Contains(NormalizePath(directory.DirPath)))
                    {
                        var scanResults = await ScanDirectoryAsync(directory.DirPath, normalizedBlacklist, profileId);
                        processedFiles += scanResults.ProcessedFiles;
                        addedFiles += scanResults.AddedFiles;
                        updatedFiles += scanResults.UpdatedFiles;
                    }
                }

                if (allTracksCount > processedFiles)
                {
                    // Number of tracks decreased
                    removedFiles = allTracksCount - processedFiles;
                }
                else if (addedFiles == 0 && allTracksCount < processedFiles)
                {
                    // Number of tracks increased without adding new tracks (a folder was removed from blacklist)
                    addedFiles = processedFiles - allTracksCount;
                }

                bool forceMaintenance = addedFiles > 0 || updatedFiles > 0 || removedFiles != 0 ? true : false;

                // If we successfully added/ updated/ removed tracks, run maintenance afterwards
                if (forceMaintenance)
                {
                    // Mark that metadata was updated so maintenance can run again
                    LibraryMaintenanceService.MarkMetadataUpdated();
                }

                try
                {
                    // Always call but only force when added/ updated/ removed tracks
                    await _maintenanceService.PerformLibraryMaintenance(forceMaintenance);
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Background maintenance failed",
                        "Library maintenance failed to run after track population",
                        ex,
                        false);
                }

                _messenger.Send(new LibraryScanCompletedMessage(processedFiles, addedFiles, updatedFiles, removedFiles));
                lastFullScanTime = DateTime.Now;

                // Fetch Artist Photo from deezer
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Small delay to let the maintenance complete
                        await Task.Delay(2000);
                        await FetchArtistDataFromDeezer();
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Background artist data fetch failed",
                            "Failed to fetch artist data from Deezer in background",
                            ex,
                            false);
                    }
                });

                isScanningInProgress = false;

            }, "Scanning music directories", ErrorSeverity.NonCritical);
        }

        /// <summary>
        /// Fetches artist photos and biographies from Deezer for artists without complete data
        /// </summary>
        private async Task FetchArtistDataFromDeezer(CancellationToken cancellationToken = default)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    _errorHandlingService.LogInfo(
                        "Starting Deezer artist data fetch",
                        "Fetching missing artist photos and biographies from Deezer...",
                        false);

                    // Use the batch fetch method from DeezerService
                    var artistsUpdated = await _deezerService.FetchMissingArtistData(cancellationToken, 300);

                    if (artistsUpdated > 0)
                    {
                        _errorHandlingService.LogInfo(
                            "Deezer fetch completed successfully",
                            $"Successfully updated data for {artistsUpdated} artists from Deezer.",
                            false);

                        // Invalidate caches to force reload
                        _allTracksRepository.InvalidateAllCaches();

                        // Trigger reload
                        await _allTracksRepository.LoadTracks(forceRefresh: true);
                    }
                    else
                    {
                        _errorHandlingService.LogInfo(
                            "Deezer fetch completed",
                            "No new artist data was found or needed.",
                            false);
                    }
                },
                "Fetching artist data from Deezer",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Scans a single directory and its subdirectories
        /// </summary>
        private async Task<ScanResults> ScanDirectoryAsync(string path, HashSet<string> blacklistedPaths, int profileId)
        {
            var results = new ScanResults();

            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                if (!Directory.Exists(path))
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Directory not found",
                        $"The directory {path} does not exist or is not accessible.",
                        null,
                        false);
                    return;
                }

                // Get all files in directory first (non-recursive)
                var files = GetMusicFilesInDirectory(path);

                // Process each music file in the current directory
                foreach (var file in files)
                {
                    results.ProcessedFiles++;

                    try
                    {
                        var fileInfo = new FileInfo(file);

                        // Check if file exists in database already
                        var track = await _trackService.GetTrackByPath(file);

                        if (track == null)
                        {
                            // New track - add it
                            await _trackDataService.PopulateTrackMetadata(file);
                            results.AddedFiles++;
                        }
                        else if (fileInfo.LastWriteTime > track.UpdatedAt)
                        {
                            // Track exists but file has been modified - update it
                            await _trackDataService.UpdateTrackMetadata(file);
                            results.UpdatedFiles++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Error processing music file",
                            $"Failed to process {file}: {ex.Message}",
                            ex,
                            false);

                        // Continue with next file
                    }
                }

                // Now scan subdirectories if they're not blacklisted
                try
                {
                    var subdirectories = Directory.GetDirectories(path);

                    foreach (var subdir in subdirectories)
                    {
                        // Skip blacklisted subdirectories
                        if (!IsPathBlacklisted(subdir, blacklistedPaths))
                        {
                            var subResults = await ScanDirectoryAsync(subdir, blacklistedPaths, profileId);
                            results.ProcessedFiles += subResults.ProcessedFiles;
                            results.AddedFiles += subResults.AddedFiles;
                            results.UpdatedFiles += subResults.UpdatedFiles;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Error scanning subdirectories",
                        $"Failed to scan subdirectories in {path}: {ex.Message}",
                        ex,
                        false);
                }
            }, $"Scanning directory: {path}", ErrorSeverity.NonCritical);

            return results;
        }

        /// <summary>
        /// Gets music files in a directory (non-recursive)
        /// </summary>
        private List<string> GetMusicFilesInDirectory(string path)
        {
            var files = new List<string>();

            try
            {
                // Get all files
                var allFiles = Directory.GetFiles(path);

                // Filter for supported music file extensions
                files = allFiles
                    .Where(f => _supportedFormats.Contains(Path.GetExtension(f).ToLower()))
                    .ToList();
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error getting files in directory",
                    $"Failed to get files in {path}: {ex.Message}",
                    ex,
                    false);
            }

            return files;
        }

        /// <summary>
        /// Enable or disable automatic file system watching
        /// </summary>
        public void SetFileSystemWatching(bool enabled)
        {
            IsWatchingEnabled = enabled;

            _errorHandlingService.LogInfo(
                "File system watching updated",
                $"Automatic file monitoring {(enabled ? "enabled" : "disabled")}",
                false);
        }

        /// <summary>
        /// Checks if a path is blacklisted
        /// </summary>
        private bool IsPathBlacklisted(string path, HashSet<string> blacklistedPaths)
        {
            if (blacklistedPaths.Count == 0)
                return false;

            string normalizedPath = NormalizePath(path);

            // Check if this path or any parent path is in the blacklist
            foreach (var blacklistedPath in blacklistedPaths)
            {
                if (normalizedPath.ToLower().Contains(blacklistedPath.ToLower()))
                    return true;

                var normalizedBlacklistedPath = NormalizePath(blacklistedPath);

                if (normalizedPath.ToLower().Contains(normalizedBlacklistedPath.ToLower()))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Normalizes path for consistent blacklist comparison
        /// </summary>
        private string NormalizePath(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    /// <summary>
    /// Results from a directory scan operation
    /// </summary>
    public class ScanResults
    {
        public int ProcessedFiles { get; set; }
        public int AddedFiles { get; set; }
        public int UpdatedFiles { get; set; }
    }

    /// <summary>
    /// Message sent when a library scan starts
    /// </summary>
    public class LibraryScanStartedMessage
    {
        public DateTime StartTime { get; } = DateTime.Now;
    }

    /// <summary>
    /// Message sent when a library scan completes
    /// </summary>
    public class LibraryScanCompletedMessage
    {
        public DateTime EndTime { get; } = DateTime.Now;
        public int ProcessedFiles { get; }
        public int AddedFiles { get; }
        public int UpdatedFiles { get; }
        public int RemovedFiles { get; }

        public LibraryScanCompletedMessage(int processedFiles, int addedFiles, int updatedFiles, int removedFiles)
        {
            ProcessedFiles = processedFiles;
            AddedFiles = addedFiles;
            UpdatedFiles = updatedFiles;
            RemovedFiles = removedFiles;
        }
    }
}