using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Configuration.ViewModels;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.Services
{
    /// <summary>
    /// Service for monitoring file system changes and triggering automatic rescans
    /// </summary>
    public class FileSystemWatcherService : IDisposable
    {
        private readonly DirectoryScannerService _scannerService;
        private readonly DirectoriesService _directoriesService;
        private readonly ProfileConfigurationService _profileConfigService;
        private readonly ProfileManager _profileManager;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        private readonly Dictionary<string, FileSystemWatcher> _watchers = new();
        private readonly Timer _debounceTimer;
        private readonly string[] _supportedFormats = { ".mp3", ".aac", ".m4a" };

        private const int DEBOUNCE_DELAY_MS = 2000; // Wait 2 seconds after last change
        private bool _hasChanges = false;
        private bool _isDisposed = false;

        public FileSystemWatcherService(
            DirectoryScannerService scannerService,
            DirectoriesService directoriesService,
            ProfileConfigurationService profileConfigService,
            ProfileManager profileManager,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _scannerService = scannerService;
            _directoriesService = directoriesService;
            _profileConfigService = profileConfigService;
            _profileManager = profileManager;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            // Create debounce timer
            _debounceTimer = new Timer(ProcessChanges, null, Timeout.Infinite, Timeout.Infinite);

            // Listen for directory configuration changes
            _messenger.Register<DirectoriesChangedMessage>(this, async (r, m) => await RestartWatchers());
            _messenger.Register<BlacklistChangedMessage>(this, async (r, m) => await RestartWatchers());
        }

        /// <summary>
        /// Start monitoring all configured directories
        /// </summary>
        public async Task StartWatching()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var directories = await _directoriesService.GetAllDirectories();
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    var config = await _profileConfigService.GetProfileConfig(profile.ProfileID);
                    var blacklist = config.BlacklistDirectory ?? Array.Empty<string>();

                    await StartWatchingDirectories(directories, blacklist);
                },
                "Starting file system monitoring",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Stop all file system watchers
        /// </summary>
        public void StopWatching()
        {
            foreach (var watcher in _watchers.Values)
            {
                try
                {
                    watcher.EnableRaisingEvents = false;
                    watcher.Dispose();
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Error stopping file watcher",
                        ex.Message,
                        ex,
                        false);
                }
            }

            _watchers.Clear();
            _hasChanges = false;
        }

        /// <summary>
        /// Restart watchers (called when configuration changes)
        /// </summary>
        private async Task RestartWatchers()
        {
            StopWatching();
            await Task.Delay(500); // Brief delay to let file operations settle
            await StartWatching();
        }

        private async Task StartWatchingDirectories(List<Directories> directories, string[] blacklist)
        {
            var normalizedBlacklist = blacklist
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(NormalizePath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var directory in directories)
            {
                if (string.IsNullOrEmpty(directory.DirPath) ||
                    !Directory.Exists(directory.DirPath) ||
                    IsPathBlacklisted(directory.DirPath, normalizedBlacklist))
                    continue;

                try
                {
                    var watcher = new FileSystemWatcher(directory.DirPath)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName |
                                     NotifyFilters.DirectoryName |
                                     NotifyFilters.LastWrite |
                                     NotifyFilters.CreationTime,
                        InternalBufferSize = 8 * 1024 * 1024 // 8MB buffer
                    };

                    // Subscribe to events
                    watcher.Created += OnFileSystemChanged;
                    watcher.Deleted += OnFileSystemChanged;
                    watcher.Renamed += OnFileSystemChanged;
                    watcher.Changed += OnFileSystemChanged;
                    watcher.Error += OnWatcherError;

                    watcher.EnableRaisingEvents = true;
                    _watchers[directory.DirPath] = watcher;

                    _errorHandlingService.LogInfo(
                        "Directory monitoring started",
                        $"Now monitoring: {directory.DirPath}",
                        false);
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Failed to start directory monitoring",
                        $"Could not monitor directory: {directory.DirPath}",
                        ex,
                        false);
                }
            }
        }

        private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
        {
            HandleFileSystemEvent(e.FullPath);
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _errorHandlingService.LogError(
                ErrorSeverity.NonCritical,
                "File system watcher error",
                "A file system monitoring error occurred. Some changes might not be detected.",
                e.GetException(),
                false);

            // Attempt to restart the problematic watcher
            if (sender is FileSystemWatcher watcher)
            {
                Task.Run(async () =>
                {
                    await Task.Delay(5000); // Wait 5 seconds
                    await RestartWatchers();
                });
            }
        }

        private void HandleFileSystemEvent(string fullPath)
        {
            if (_isDisposed) return;

            try
            {
                // Only care about music files and directories
                bool isMusicFile = _supportedFormats.Contains(Path.GetExtension(fullPath)?.ToLower());
                bool isDirectory = Directory.Exists(fullPath);

                if (!isMusicFile && !isDirectory) return;

                // Check if path is blacklisted
                var profile = _profileManager.GetCurrentProfileAsync().Result;
                var config = _profileConfigService.GetProfileConfig(profile.ProfileID).Result;
                var blacklist = config.BlacklistDirectory ?? Array.Empty<string>();

                if (IsPathBlacklisted(fullPath, blacklist.ToHashSet(StringComparer.OrdinalIgnoreCase)))
                    return;

                // Mark that we have changes and reset debounce timer
                _hasChanges = true;
                _debounceTimer.Change(DEBOUNCE_DELAY_MS, Timeout.Infinite);
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error handling file system change",
                    $"Error processing change for: {fullPath}",
                    ex,
                    false);
            }
        }

        private async void ProcessChanges(object state)
        {
            if (_isDisposed || !_hasChanges) return;

            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    _hasChanges = false; // Reset flag

                    _errorHandlingService.LogInfo(
                        "File system changes detected",
                        "Triggering library rescan due to file system changes...",
                        false);

                    // Trigger rescan
                    var directories = await _directoriesService.GetAllDirectories();
                    var profile = await _profileManager.GetCurrentProfileAsync();

                    // Force rescan by resetting last scan time
                    _scannerService.lastFullScanTime = DateTime.MinValue;

                    // Trigger the scan
                    await _scannerService.ScanDirectoriesAsync(directories, profile.ProfileID);
                },
                "Processing file system changes",
                ErrorSeverity.NonCritical,
                false);
        }

        private bool IsPathBlacklisted(string path, HashSet<string> blacklistedPaths)
        {
            if (blacklistedPaths.Count == 0) return false;

            string normalizedPath = NormalizePath(path);

            return blacklistedPaths.Any(blacklisted =>
                normalizedPath.StartsWith(blacklisted, StringComparison.OrdinalIgnoreCase));
        }

        private string NormalizePath(string path)
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            _isDisposed = true;
            StopWatching();
            _debounceTimer?.Dispose();
            _messenger.Unregister<DirectoriesChangedMessage>(this);
            _messenger.Unregister<BlacklistChangedMessage>(this);
        }
    }
}