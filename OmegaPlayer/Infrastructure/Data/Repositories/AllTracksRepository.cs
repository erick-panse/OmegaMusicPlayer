using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Core.Services;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    public class AllTracksRepository : IDisposable
    {
        private readonly TrackDisplayRepository _trackDisplayRepository;
        private readonly AlbumRepository _albumRepository;
        private readonly ArtistsRepository _artistsRepository;
        private readonly GenresRepository _genresRepository;
        private readonly ProfileManager _profileManager;
        private readonly ProfileConfigurationService _profileConfigurationService;
        private readonly LocalizationService _localizationService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        // Cached data collections with thread safety
        private readonly object _cacheLock = new object();
        private List<TrackDisplayModel> _cachedAllTracks = new List<TrackDisplayModel>();
        private List<Albums> _cachedAllAlbums = new List<Albums>();
        private List<Artists> _cachedAllArtists = new List<Artists>();
        private List<Genres> _cachedAllGenres = new List<Genres>();

        // Use ReadOnlyCollection for public properties to prevent external modification
        private ReadOnlyCollection<TrackDisplayModel> _allTracks;
        private ReadOnlyCollection<Albums> _allAlbums;
        private ReadOnlyCollection<Artists> _allArtists;
        private ReadOnlyCollection<Genres> _allGenres;

        // Initialization status tracking
        private bool _isInitialized = false;

        // Cache validation flags
        private bool _trackCacheValid = false;
        private bool _albumCacheValid = false;
        private bool _artistCacheValid = false;
        private bool _genreCacheValid = false;

        // Semaphore for synchronizing track loading operations
        private SemaphoreSlim _loadingSemaphore = new SemaphoreSlim(1, 1);
        private Task<bool> _currentLoadingTask = null;

        public ReadOnlyCollection<TrackDisplayModel> AllTracks => _allTracks;
        public ReadOnlyCollection<Albums> AllAlbums => _allAlbums;
        public ReadOnlyCollection<Artists> AllArtists => _allArtists;
        public ReadOnlyCollection<Genres> AllGenres => _allGenres;

        public AllTracksRepository(
            TrackDisplayRepository trackDisplayRepository,
            AlbumRepository albumRepository,
            ArtistsRepository artistsRepository,
            GenresRepository genresRepository,
            ProfileConfigurationService profileConfigurationService,
            ProfileManager profileManager,
            LocalizationService localizationService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _trackDisplayRepository = trackDisplayRepository;
            _albumRepository = albumRepository;
            _artistsRepository = artistsRepository;
            _genresRepository = genresRepository;
            _profileConfigurationService = profileConfigurationService;
            _profileManager = profileManager;
            _localizationService = localizationService;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            // Initialize empty collections
            _allTracks = new ReadOnlyCollection<TrackDisplayModel>(new List<TrackDisplayModel>());
            _allAlbums = new ReadOnlyCollection<Albums>(new List<Albums>());
            _allArtists = new ReadOnlyCollection<Artists>(new List<Artists>());
            _allGenres = new ReadOnlyCollection<Genres>(new List<Genres>());

            // Initialize data asynchronously
            InitializeAsync();
        }

        /// <summary>
        /// Safer initialization approach that doesn't block constructor
        /// </summary>
        private async void InitializeAsync()
        {
            try
            {
                await LoadTracksInternal();
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Failed to initialize repository",
                    "AllTracksRepository initialization failed",
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Invalidates all caches, forcing a reload from the database on next access
        /// </summary>
        public void InvalidateAllCaches()
        {
            lock (_cacheLock)
            {
                _trackCacheValid = false;
                _albumCacheValid = false;
                _artistCacheValid = false;
                _genreCacheValid = false;

                _errorHandlingService.LogInfo(
                    "All caches invalidated",
                    "Repository will reload data from database on next access");
            }

            // Notify services and ViewModels to invalidate their caches
            _messenger.Send(new AllTracksInvalidatedMessage());
        }

        /// <summary>
        /// Public method that ensures track loading is synchronized across multiple callers
        /// with cache-first approach
        /// </summary>
        public async Task<bool> LoadTracks(bool forceRefresh = false)
        {
            try
            {
                if (_isInitialized && _trackCacheValid && !forceRefresh && _cachedAllTracks.Any())
                {
                    return true;
                }

                // If we're already loading tracks, return the existing task
                if (_currentLoadingTask != null && !_currentLoadingTask.IsCompleted)
                {
                    return await _currentLoadingTask;
                }

                // Try to enter the semaphore but don't wait if it's already taken
                if (await _loadingSemaphore.WaitAsync(0))
                {
                    try
                    {
                        // Double-check cache after acquiring semaphore
                        // (another thread may have finished loading while we were waiting)
                        if (_isInitialized && _trackCacheValid && !forceRefresh && _cachedAllTracks.Any())
                        {
                            return true;
                        }

                        // We got the semaphore, create a new loading task
                        var taskCompletionSource = new TaskCompletionSource<bool>();
                        _currentLoadingTask = taskCompletionSource.Task;

                        // Start the actual loading operation
                        await LoadTracksInternal();

                        // Mark cache as valid after successful load
                        lock (_cacheLock)
                        {
                            _trackCacheValid = true;
                        }

                        // Signal completion
                        taskCompletionSource.TrySetResult(true);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        // Mark the task as failed
                        if (_currentLoadingTask is Task<bool> task &&
                            task.Status == TaskStatus.WaitingForActivation)
                        {
                            ((TaskCompletionSource<bool>)task.AsyncState).TrySetException(ex);
                        }

                        // Log the error
                        _errorHandlingService.LogError(
                            ErrorSeverity.Critical,
                            _localizationService["ErrorLoadingTracks"],
                            _localizationService["ErrorLoadingTracksDetails"],
                            ex,
                            true);

                        // If we failed but have cached data, return true anyway
                        lock (_cacheLock)
                        {
                            return _cachedAllTracks.Any();
                        }
                    }
                    finally
                    {
                        // Always release the semaphore
                        _loadingSemaphore.Release();
                    }
                }
                else
                {
                    // If we couldn't get the semaphore, wait for the current loading task
                    if (_currentLoadingTask != null)
                    {
                        return await _currentLoadingTask;
                    }

                    // If there's no current task but we couldn't get the semaphore,
                    // something went wrong; return based on cached data state
                    lock (_cacheLock)
                    {
                        return _cachedAllTracks.Any();
                    }
                }
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    _localizationService["ErrorLoadingTracks"],
                    _localizationService["ErrorLoadingTracksDetails"],
                    ex,
                    true);

                // Return based on cached data state
                lock (_cacheLock)
                {
                    return _cachedAllTracks.Any();
                }
            }
        }

        /// <summary>
        /// Internal implementation of track loading
        /// </summary>
        private async Task LoadTracksInternal()
        {
            try
            {
                var currentProfile = await _profileManager.GetCurrentProfileAsync();

                if (currentProfile == null)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "Failed to initialize profile manager",
                        "Current profile is null, cannot load tracks",
                        null,
                        false);

                    // Use cached data as fallback
                    UseTracksCache();
                    return;
                }

                // Load tracks with blacklist validation
                var loadedTracks = await ValidateBlacklist(currentProfile.ProfileID);

                // Thread-safe update of property
                lock (_cacheLock)
                {
                    _allTracks = new ReadOnlyCollection<TrackDisplayModel>(loadedTracks);
                    _cachedAllTracks = new List<TrackDisplayModel>(loadedTracks);
                    _trackCacheValid = true;
                }

                // Load related data
                await Task.WhenAll(
                    LoadAlbumsAsync(),
                    LoadArtistsAsync(),
                    LoadGenresAsync()
                );

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    _localizationService["ErrorLoadingTracks"],
                    _localizationService["ErrorLoadingTracksDetails"],
                    ex,
                    false);

                // Use cached data as fallback
                UseTracksCache();
            }
        }

        private async Task LoadAlbumsAsync()
        {
            if (_albumCacheValid && _cachedAllAlbums.Any())
            {
                return;
            }

            var loadedAlbums = await GetAlbumsForProfile();

            // Thread-safe update of properties
            lock (_cacheLock)
            {
                _allAlbums = new ReadOnlyCollection<Albums>(loadedAlbums);
                _cachedAllAlbums = new List<Albums>(loadedAlbums);
                _albumCacheValid = true;
            }
        }

        private async Task LoadArtistsAsync()
        {
            if (_artistCacheValid && _cachedAllArtists.Any())
            {
                return;
            }

            var loadedArtists = await GetArtistsForProfile();

            // Thread-safe update of properties
            lock (_cacheLock)
            {
                _allArtists = new ReadOnlyCollection<Artists>(loadedArtists);
                _cachedAllArtists = new List<Artists>(loadedArtists);
                _artistCacheValid = true;
            }
        }

        private async Task LoadGenresAsync()
        {
            if (_genreCacheValid && _cachedAllGenres.Any())
            {
                return;
            }

            var loadedGenres = await GetGenresForProfile();

            // Thread-safe update of properties
            lock (_cacheLock)
            {
                _allGenres = new ReadOnlyCollection<Genres>(loadedGenres);
                _cachedAllGenres = new List<Genres>(loadedGenres);
                _genreCacheValid = true;
            }
        }

        private void UseTracksCache()
        {
            lock (_cacheLock)
            {
                // Use cached data if available
                if (_cachedAllTracks.Any())
                {
                    _allTracks = new ReadOnlyCollection<TrackDisplayModel>(_cachedAllTracks);
                    _allAlbums = new ReadOnlyCollection<Albums>(_cachedAllAlbums);
                    _allArtists = new ReadOnlyCollection<Artists>(_cachedAllArtists);
                    _allGenres = new ReadOnlyCollection<Genres>(_cachedAllGenres);
                }
                // Otherwise keep the empty default collections
            }
        }

        public async Task<List<TrackDisplayModel>> ValidateBlacklist(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var allTracksToValidate = await _trackDisplayRepository.GetAllTracksWithMetadata(profileId);

                    if (allTracksToValidate == null || !allTracksToValidate.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No tracks found",
                            $"No tracks found for profile {profileId}.",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

                    var blacklistedPaths = await _profileConfigurationService.GetBlacklistedDirectories(profileId);

                    // Skip blacklist validation if no blacklisted paths
                    if (blacklistedPaths == null || !blacklistedPaths.Any())
                    {
                        return allTracksToValidate;
                    }

                    // Normalize blacklisted paths for comparison
                    var normalizedBlacklist = blacklistedPaths
                        .Where(p => !string.IsNullOrEmpty(p))
                        .Select(p => NormalizePath(p))
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var filteredTracks = new List<TrackDisplayModel>();

                    // Efficiently check each track against blacklist, including subfolders
                    foreach (var track in allTracksToValidate)
                    {
                        if (string.IsNullOrEmpty(track.FilePath))
                        {
                            continue; // Skip tracks with no path
                        }

                        // Get directory and check if it or any parent is blacklisted
                        var trackDirectory = Path.GetDirectoryName(track.FilePath);
                        if (trackDirectory == null || IsInBlacklistedFolder(trackDirectory, normalizedBlacklist))
                        {
                            continue; // Skip this track
                        }

                        filteredTracks.Add(track);
                    }

                    return filteredTracks;
                },
                $"Validating tracks against blacklist for profile {profileId}",
                _cachedAllTracks ?? new List<TrackDisplayModel>(), // Return cached tracks on error
                ErrorSeverity.Playback,
                false);
        }

        public async Task<List<Albums>> GetAlbumsForProfile()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var tempAllAlbums = await _albumRepository.GetAllAlbums();

                    if (tempAllAlbums == null || !tempAllAlbums.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No albums found",
                            "No albums found in the database.",
                            null,
                            false);
                        return new List<Albums>();
                    }

                    // Track safety - ensure AllTracks is not null and has items
                    if (_allTracks == null || !_allTracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No tracks available for album filtering",
                            "Cannot filter albums without available tracks.",
                            null,
                            false);
                        return tempAllAlbums; // Return all albums if we can't filter
                    }

                    var trackAlbumIds = _allTracks
                        .Where(t => t.AlbumID > 0) // Filter out invalid AlbumIDs
                        .Select(t => t.AlbumID)
                        .ToHashSet(); // Get unique AlbumIds from tracks

                    return tempAllAlbums
                        .Where(album => trackAlbumIds.Contains(album.AlbumID))
                        .ToList(); // Filter only albums present in tracks
                },
                "Getting albums for current profile",
                _cachedAllAlbums ?? new List<Albums>(), // Return cached albums on error
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Artists>> GetArtistsForProfile()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var tempAllArtists = await _artistsRepository.GetAllArtists();

                    if (tempAllArtists == null || !tempAllArtists.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No artists found",
                            "No artists found in the database.",
                            null,
                            false);
                        return new List<Artists>();
                    }

                    // Track safety - ensure AllTracks is not null and has items
                    if (_allTracks == null || !_allTracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No tracks available for artist filtering",
                            "Cannot filter artists without available tracks.",
                            null,
                            false);
                        return tempAllArtists; // Return all artists if we can't filter
                    }

                    // Add safety check for Artists collection
                    var trackArtistIds = _allTracks
                        .Where(t => t.Artists != null) // Filter out null Artists collections
                        .SelectMany(t => t.Artists?.Select(a => a.ArtistID) ?? Enumerable.Empty<int>())
                        .Where(id => id > 0) // Filter out invalid ArtistIDs
                        .ToHashSet(); // Extract all artist IDs from tracks

                    return tempAllArtists
                        .Where(artist => trackArtistIds.Contains(artist.ArtistID))
                        .ToList(); // Filter only artists present in tracks
                },
                "Getting artists for current profile",
                _cachedAllArtists ?? new List<Artists>(), // Return cached artists on error
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Genres>> GetGenresForProfile()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var tempAllGenres = await _genresRepository.GetAllGenres();

                    if (tempAllGenres == null || !tempAllGenres.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No genres found",
                            "No genres found in the database.",
                            null,
                            false);
                        return new List<Genres>();
                    }

                    // Track safety - ensure AllTracks is not null and has items
                    if (_allTracks == null || !_allTracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No tracks available for genre filtering",
                            "Cannot filter genres without available tracks.",
                            null,
                            false);
                        return tempAllGenres; // Return all genres if we can't filter
                    }

                    var trackGenres = _allTracks
                        .Where(t => !string.IsNullOrEmpty(t.Genre)) // Filter out null or empty genres
                        .Select(t => t.Genre)
                        .ToHashSet(); // Get unique Genres from tracks

                    return tempAllGenres
                        .Where(genre => trackGenres.Contains(genre.GenreName))
                        .ToList(); // Filter only genres present in tracks
                },
                "Getting genres for current profile",
                _cachedAllGenres ?? new List<Genres>(), // Return cached genres on error,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Correctly determines if a folder path is blacklisted or is within a blacklisted folder
        /// </summary>
        private bool IsInBlacklistedFolder(string folderPath, HashSet<string> blacklistedPaths)
        {
            if (string.IsNullOrEmpty(folderPath) || blacklistedPaths == null || !blacklistedPaths.Any())
            {
                return false;
            }

            // Normalize the path for consistent comparison
            string normalizedPath = NormalizePath(folderPath);

            // First try direct matching (performance optimization)
            if (blacklistedPaths.Contains(normalizedPath))
            {
                return true;
            }

            // Get path segments for the folder path
            string[] folderSegments = GetPathSegments(normalizedPath);

            // Check if this folder is within any blacklisted path
            foreach (var blacklistedPath in blacklistedPaths)
            {
                string normalizedBlacklistedPath = NormalizePath(blacklistedPath);
                string[] blacklistSegments = GetPathSegments(normalizedBlacklistedPath);

                // If the blacklist path has more segments than the folder path,
                // it can't be a parent of the folder path
                if (blacklistSegments.Length > folderSegments.Length)
                {
                    continue;
                }

                // Check if all segments of the blacklisted path match the folder path
                bool isMatch = true;
                for (int i = 0; i < blacklistSegments.Length; i++)
                {
                    if (!string.Equals(blacklistSegments[i], folderSegments[i], StringComparison.OrdinalIgnoreCase))
                    {
                        isMatch = false;
                        break;
                    }
                }

                if (isMatch)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Normalizes a path for consistent comparison
        /// </summary>
        private string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                // Convert all separators to the platform-specific separator
                path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                // Remove trailing separators
                path = path.TrimEnd(Path.DirectorySeparatorChar);

                // Handle special case of drive root (e.g., "C:")
                if (path.Length == 2 && path[1] == ':')
                {
                    return path + Path.DirectorySeparatorChar;
                }

                // Try to get full path to resolve any ".." or "."
                // Only if path exists (to avoid exceptions)
                if (Directory.Exists(path))
                {
                    try
                    {
                        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);
                    }
                    catch
                    {
                        // Fall back to original normalized path if GetFullPath fails
                        return path;
                    }
                }

                return path;
            }
            catch
            {
                // If any normalization fails, return the original path
                return path;
            }
        }

        /// <summary>
        /// Splits a path into its segments for proper path comparison
        /// </summary>
        private string[] GetPathSegments(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Array.Empty<string>();
            }

            // Split by directory separator
            return path.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>
        /// Releases resources and cancels pending operations
        /// </summary>
        public void Dispose()
        {
            _loadingSemaphore?.Dispose();
            _loadingSemaphore = null;
        }
    }
}