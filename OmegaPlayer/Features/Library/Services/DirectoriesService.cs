using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Linq;
using System.IO;
using CommunityToolkit.Mvvm.Messaging;

namespace OmegaPlayer.Features.Library.Services
{
    /// <summary>
    /// Service for managing music library directory  with robust error handling
    /// </summary>
    public class DirectoriesService
    {
        private readonly DirectoriesRepository _directoriesRepository;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        // In-memory cache for directories to provide fallback
        private List<Directories> _cachedDirectories = new();
        private DateTime _lastCacheTime = DateTime.MinValue;
        private const int CACHE_VALIDITY_MINUTES = 5;

        public DirectoriesService(
            DirectoriesRepository directoriesRepository,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _directoriesRepository = directoriesRepository;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;
        }

        /// <summary>
        /// Gets a directory by ID with error handling and fallback
        /// </summary>
        public async Task<Directories> GetDirectoryById(int dirID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Try to find in cache first for faster access
                    var cachedDir = _cachedDirectories.FirstOrDefault(d => d.DirID == dirID);
                    if (cachedDir != null)
                    {
                        return cachedDir;
                    }

                    return await _directoriesRepository.GetDirectoryById(dirID);
                },
                $"Fetching directory with ID {dirID}",
                null,
                ErrorSeverity.NonCritical
            );
        }

        /// <summary>
        /// Gets all directories with error handling and caching
        /// </summary>
        public async Task<List<Directories>> GetAllDirectories()
        {
            // If cache is valid, return cached directories
            if (DateTime.Now.Subtract(_lastCacheTime).TotalMinutes < CACHE_VALIDITY_MINUTES &&
                _cachedDirectories.Count > 0)
            {
                return _cachedDirectories;
            }

            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var directories = await _directoriesRepository.GetAllDirectories();

                    // Check for invalid directories
                    var invalidDirectories = directories
                        .Where(d => string.IsNullOrWhiteSpace(d.DirPath) || !Directory.Exists(d.DirPath))
                        .ToList();

                    if (invalidDirectories.Count > 0)
                    {
                        // Log warning about invalid directories
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Invalid music directories detected",
                            $"Found {invalidDirectories.Count} music directories that no longer exist or are invalid.",
                            null,
                            true);

                        // We keep invalid directories in the list because the user should be able to see and remove them
                    }

                    // Update cache
                    _cachedDirectories = new List<Directories>(directories);
                    _lastCacheTime = DateTime.Now;

                    return directories;
                },
                "Fetching all music directories",
                _cachedDirectories, // Return cached directories as fallback
                ErrorSeverity.NonCritical
            );
        }

        /// <summary>
        /// Adds a new directory with validation and error handling
        /// </summary>
        public async Task<int> AddDirectory(Directories directory)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Validate the directory
                    if (directory == null)
                    {
                        throw new ArgumentNullException(nameof(directory), "Directory cannot be null");
                    }

                    if (string.IsNullOrWhiteSpace(directory.DirPath))
                    {
                        throw new ArgumentException("Directory path cannot be empty", nameof(directory.DirPath));
                    }

                    // Check if directory exists
                    if (!Directory.Exists(directory.DirPath))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Directory not found",
                            $"The specified directory does not exist: {directory.DirPath}",
                            null,
                            true);

                        throw new DirectoryNotFoundException($"Directory not found: {directory.DirPath}");
                    }

                    // Verify directory is readable
                    try
                    {
                        Directory.GetFiles(directory.DirPath, "*", SearchOption.TopDirectoryOnly);
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Directory not accessible",
                            $"Cannot access the specified directory: {directory.DirPath}",
                            ex,
                            true);

                        throw new UnauthorizedAccessException($"Cannot access directory: {directory.DirPath}", ex);
                    }

                    // Check for duplicate (case-insensitive)
                    var allDirectories = await GetAllDirectories();
                    bool isDuplicate = allDirectories.Any(d =>
                        string.Equals(d.DirPath, directory.DirPath, StringComparison.OrdinalIgnoreCase));

                    if (isDuplicate)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Directory already exists",
                            $"The specified directory is already in your library: {directory.DirPath}",
                            null,
                            true);

                        throw new InvalidOperationException($"Directory already exists: {directory.DirPath}");
                    }

                    // Add directory
                    int dirId = await _directoriesRepository.AddDirectory(directory);

                    // Update cache
                    directory.DirID = dirId;
                    _cachedDirectories.Add(directory);
                    _lastCacheTime = DateTime.Now;

                    // Log success
                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "Music directory added",
                        $"Added directory to library: {directory.DirPath}",
                        null,
                        false); // Don't show notification - UI already shows success

                    return dirId;
                },
                $"Adding directory: {directory?.DirPath ?? "null"}",
                -1, // Return -1 as failure indicator
                ErrorSeverity.NonCritical
            );
        }

        /// <summary>
        /// Deletes a directory with error handling
        /// </summary>
        public async Task DeleteDirectory(int dirID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Find the directory in cache first
                    var directoryToRemove = _cachedDirectories.FirstOrDefault(d => d.DirID == dirID);

                    // If not in cache, try to get it from repository
                    if (directoryToRemove == null)
                    {
                        try
                        {
                            directoryToRemove = await _directoriesRepository.GetDirectoryById(dirID);
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error finding directory to delete",
                                $"Could not find directory with ID {dirID}",
                                ex,
                                false);
                        }
                    }

                    // Remember directory path for logging
                    string dirPath = directoryToRemove?.DirPath ?? $"ID: {dirID}";

                    // Delete from repository
                    await _directoriesRepository.DeleteDirectory(dirID);

                    // Update cache
                    if (directoryToRemove != null)
                    {
                        _cachedDirectories.Remove(directoryToRemove);
                    }

                    // Log success
                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "Music directory removed",
                        $"Removed directory from library: {dirPath}",
                        null,
                        false); // Don't show notification
                },
                $"Deleting directory with ID {dirID}",
                ErrorSeverity.NonCritical
            );
        }

        /// <summary>
        /// Validates accessibility of all directories and returns diagnostics
        /// </summary>
        public async Task<DirectoryDiagnostics> ValidateDirectories()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var directories = await GetAllDirectories();
                    var diagnostics = new DirectoryDiagnostics();

                    // Skip validation if no directories
                    if (directories == null || directories.Count == 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "No music directories to validate",
                            "No music directories have been added to the library yet.",
                            null,
                            false);

                        return diagnostics;
                    }

                    // Log validation start
                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "Validating music directories",
                        $"Checking accessibility of {directories.Count} music directories",
                        null,
                        false);

                    foreach (var dir in directories)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(dir.DirPath))
                            {
                                // Invalid directory path
                                diagnostics.MissingDirectories.Add(dir);
                                continue;
                            }

                            if (!Directory.Exists(dir.DirPath))
                            {
                                // Directory does not exist
                                diagnostics.MissingDirectories.Add(dir);
                                continue;
                            }

                            // Check if directory is accessible by getting files
                            Directory.GetFiles(dir.DirPath, "*", SearchOption.TopDirectoryOnly);

                            // Directory is valid and accessible
                            diagnostics.ValidDirectories.Add(dir);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            // Permission issues
                            diagnostics.InaccessibleDirectories.Add((dir, $"Permission denied: {ex.Message}"));
                        }
                        catch (IOException ex)
                        {
                            // IO issues (locked files, etc.)
                            diagnostics.InaccessibleDirectories.Add((dir, $"IO error: {ex.Message}"));
                        }
                        catch (Exception ex)
                        {
                            // Any other errors
                            diagnostics.InaccessibleDirectories.Add((dir, ex.Message));
                        }
                    }

                    // If we found problems, log them
                    if (diagnostics.HasIssues)
                    {
                        string issueDetails =
                            $"Missing directories: {diagnostics.MissingDirectories.Count}, " +
                            $"Inaccessible directories: {diagnostics.InaccessibleDirectories.Count}";

                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Music directory issues detected",
                            issueDetails,
                            null,
                            diagnostics.MissingDirectories.Count + diagnostics.InaccessibleDirectories.Count > 1);
                    }

                    return diagnostics;
                },
                "Validating music directories",
                new DirectoryDiagnostics(), // Return empty diagnostics as fallback
                ErrorSeverity.NonCritical
            );
        }

        /// <summary>
        /// Invalidates the directory cache
        /// </summary>
        public void InvalidateCache()
        {
            _lastCacheTime = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Diagnostic information about directory validation
    /// </summary>
    public class DirectoryDiagnostics
    {
        public List<Directories> ValidDirectories { get; } = new();
        public List<Directories> MissingDirectories { get; } = new();
        public List<(Directories Directory, string ErrorMessage)> InaccessibleDirectories { get; } = new();

        public bool HasIssues => MissingDirectories.Count > 0 || InaccessibleDirectories.Count > 0;
    }
}