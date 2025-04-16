using OmegaPlayer.Features.Library.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Infrastructure.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace OmegaPlayer.Features.Library.Services
{
    public class DirectoryScannerService
    {
        private readonly TracksService _trackService;
        private readonly TrackMetadataService _trackDataService;
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        // Scanning status tracking
        private bool _isScanningInProgress = false;
        private DateTime _lastFullScanTime = DateTime.MinValue;
        private const int MIN_SCAN_INTERVAL_MINUTES = 15;

        // File formats we support
        private readonly string[] _supportedFormats = { ".mp3", ".wav", ".flac", ".ogg", ".aac", ".m4a" };

        public DirectoryScannerService(
            TracksService trackService,
            TrackMetadataService trackDataService,
            ProfileConfigurationService profileConfigService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _trackService = trackService;
            _trackDataService = trackDataService;
            _profileConfigService = profileConfigService;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;
        }

        /// <summary>
        /// Scans all directories for music files, respecting blacklists
        /// </summary>
        public async Task ScanDirectoriesAsync(List<Directories> directories, int profileId)
        {
            // Prevent concurrent scanning
            if (_isScanningInProgress)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Directory scan already in progress",
                    "Please wait for the current scan to complete before starting another scan.",
                    null,
                    true);
                return;
            }

            // Respect minimum scan interval to prevent excessive scanning
            if (DateTime.Now.Subtract(_lastFullScanTime).TotalMinutes < MIN_SCAN_INTERVAL_MINUTES)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Scan requested too soon",
                    $"Library scan was performed less than {MIN_SCAN_INTERVAL_MINUTES} minutes ago. Skipping scan.",
                    null,
                    false);
                return;
            }

            _isScanningInProgress = true;

            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                try
                {
                    // Get blacklisted directories from profile config
                    var config = await _profileConfigService.GetProfileConfig(profileId);
                    var blacklistedPaths = config.BlacklistDirectory ?? Array.Empty<string>();

                    _messenger.Send(new LibraryScanStartedMessage());
                    int processedFiles = 0;
                    int addedFiles = 0;
                    int updatedFiles = 0;

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

                    _messenger.Send(new LibraryScanCompletedMessage(processedFiles, addedFiles, updatedFiles));
                    _lastFullScanTime = DateTime.Now;
                }
                finally
                {
                    _isScanningInProgress = false;
                }
            }, "Scanning music directories", ErrorSeverity.NonCritical);
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
        /// Checks if a path is blacklisted
        /// </summary>
        private bool IsPathBlacklisted(string path, HashSet<string> blacklistedPaths)
        {
            if (blacklistedPaths.Count == 0)
                return false;

            string normalizedPath = NormalizePath(path);

            // Check if this path or any parent path is in the blacklist
            while (!string.IsNullOrEmpty(normalizedPath))
            {
                if (blacklistedPaths.Contains(normalizedPath))
                    return true;

                // Move up to parent directory
                var parentInfo = Directory.GetParent(normalizedPath);
                if (parentInfo == null)
                    break;

                normalizedPath = NormalizePath(parentInfo.FullName);
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

        /// <summary>
        /// Conducts a fresh scan for a specified directory
        /// </summary>
        public async Task ScanSpecificDirectoryAsync(string directoryPath, int profileId)
        {
            if (_isScanningInProgress)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Scan already in progress",
                    "Please wait for the current scan to complete.",
                    null,
                    true);
                return;
            }

            _isScanningInProgress = true;

            await _errorHandlingService.SafeExecuteAsync(async () =>
            {
                try
                {
                    // Get blacklisted directories
                    var config = await _profileConfigService.GetProfileConfig(profileId);
                    var blacklistedPaths = config.BlacklistDirectory ?? Array.Empty<string>();

                    // Normalize blacklisted paths
                    var normalizedBlacklist = blacklistedPaths
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Select(p => NormalizePath(p))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    // Don't scan if the directory itself is blacklisted
                    if (!IsPathBlacklisted(directoryPath, normalizedBlacklist))
                    {
                        _messenger.Send(new LibraryScanStartedMessage());

                        var results = await ScanDirectoryAsync(directoryPath, normalizedBlacklist, profileId);

                        _messenger.Send(new LibraryScanCompletedMessage(
                            results.ProcessedFiles,
                            results.AddedFiles,
                            results.UpdatedFiles));
                    }
                    else
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Directory is blacklisted",
                            $"The directory {directoryPath} is blacklisted and will not be scanned.",
                            null,
                            true);
                    }
                }
                finally
                {
                    _isScanningInProgress = false;
                }
            }, $"Scanning directory: {directoryPath}", ErrorSeverity.NonCritical);
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

        public LibraryScanCompletedMessage(int processedFiles, int addedFiles, int updatedFiles)
        {
            ProcessedFiles = processedFiles;
            AddedFiles = addedFiles;
            UpdatedFiles = updatedFiles;
        }
    }
}