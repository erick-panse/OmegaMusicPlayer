using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
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
        private readonly IErrorHandlingService _errorHandlingService;

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

        // Cancellation support
        private CancellationTokenSource _loadCancellationSource;

        // Task completion tracking
        private TaskCompletionSource<bool> _initializationComplete;
        private bool _isInitialized = false;

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
            IErrorHandlingService errorHandlingService
            )
        {
            _trackDisplayRepository = trackDisplayRepository;
            _albumRepository = albumRepository;
            _artistsRepository = artistsRepository;
            _genresRepository = genresRepository;
            _profileConfigurationService = profileConfigurationService;
            _profileManager = profileManager;
            _errorHandlingService = errorHandlingService;

            // Initialize empty collections
            _allTracks = new ReadOnlyCollection<TrackDisplayModel>(new List<TrackDisplayModel>());
            _allAlbums = new ReadOnlyCollection<Albums>(new List<Albums>());
            _allArtists = new ReadOnlyCollection<Artists>(new List<Artists>());
            _allGenres = new ReadOnlyCollection<Genres>(new List<Genres>());

            // Setup initialization tracking
            _initializationComplete = new TaskCompletionSource<bool>();

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
                await LoadTracks();
                _initializationComplete.TrySetResult(true);
                _isInitialized = true;
            }
            catch (OperationCanceledException)
            {
                // Silently handle cancellation without error
                _initializationComplete.TrySetCanceled();
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Failed to initialize repository",
                    "AllTracksRepository initialization failed",
                    ex,
                    true);
                _initializationComplete.TrySetException(ex);
            }
        }

        /// <summary>
        /// Waits for initialization to complete before allowing operations
        /// </summary>
        public async Task InitializeAsync(TimeSpan? timeout = null)
        {
            if (_isInitialized) return;

            if (timeout.HasValue)
            {
                var timeoutTask = Task.Delay(timeout.Value);
                var completedTask = await Task.WhenAny(_initializationComplete.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new TimeoutException("Repository initialization timed out");
                }
            }
            else
            {
                await _initializationComplete.Task;
            }
        }

        /// <summary>
        /// Extension method that properly handles cancellation without error notifications
        /// </summary>
        private async Task<T> ExecuteWithCancellationAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            Func<T> fallbackProvider,
            string operationName,
            CancellationToken cancellationToken)
        {
            try
            {
                // Check cancellation before starting
                cancellationToken.ThrowIfCancellationRequested();

                // Execute the operation
                return await operation(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // For cancellations, just return the fallback without error logging
                return fallbackProvider();
            }
            catch (Exception ex)
            {
                // For actual errors, use error handling service
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    $"Failed during {operationName}",
                    $"Operation failed: {operationName}",
                    ex,
                    true);

                return fallbackProvider();
            }
        }

        public async Task LoadTracks(CancellationToken? externalToken = null)
        {
            // Cancel any existing load operation
            if (_loadCancellationSource != null)
            {
                _loadCancellationSource.Cancel();
                _loadCancellationSource.Dispose();
            }

            // Create a new cancellation source
            _loadCancellationSource = new CancellationTokenSource();
            var cancellationToken = externalToken.HasValue
                ? CancellationTokenSource.CreateLinkedTokenSource(externalToken.Value, _loadCancellationSource.Token).Token
                : _loadCancellationSource.Token;

            try
            {
                // Check cancellation before starting
                cancellationToken.ThrowIfCancellationRequested();

                var currentProfile = await _profileManager.GetCurrentProfileAsync();

                if (currentProfile == null)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "Failed to initialize profile manager",
                        "Current profile is null, cannot load tracks",
                        null,
                        true);

                    // Use cached data as fallback
                    UseTracksCache();
                    return;
                }

                // Check cancellation
                cancellationToken.ThrowIfCancellationRequested();

                // Load tracks with blacklist validation
                var loadedTracks = await ValidateBlacklist(currentProfile.ProfileID, cancellationToken);

                // Thread-safe update of property
                lock (_cacheLock)
                {
                    _allTracks = new ReadOnlyCollection<TrackDisplayModel>(loadedTracks);
                    _cachedAllTracks = new List<TrackDisplayModel>(loadedTracks);
                }

                // Check cancellation
                cancellationToken.ThrowIfCancellationRequested();

                // Load related data
                await Task.WhenAll(
                    LoadAlbumsAsync(cancellationToken),
                    LoadArtistsAsync(cancellationToken),
                    LoadGenresAsync(cancellationToken)
                );

                _isInitialized = true;
            }
            catch (OperationCanceledException)
            {
                // Silently handle cancellation, with no error logging
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Failed to load tracks",
                    "An unexpected error occurred while loading tracks. Using cached data if available.",
                    ex,
                    true);

                // Use cached data as fallback
                UseTracksCache();
            }
        }

        private async Task LoadAlbumsAsync(CancellationToken cancellationToken)
        {
            var loadedAlbums = await GetAlbumsForProfile(cancellationToken);

            // Thread-safe update of properties
            lock (_cacheLock)
            {
                _allAlbums = new ReadOnlyCollection<Albums>(loadedAlbums);
                _cachedAllAlbums = new List<Albums>(loadedAlbums);
            }
        }

        private async Task LoadArtistsAsync(CancellationToken cancellationToken)
        {
            var loadedArtists = await GetArtistsForProfile(cancellationToken);

            // Thread-safe update of properties
            lock (_cacheLock)
            {
                _allArtists = new ReadOnlyCollection<Artists>(loadedArtists);
                _cachedAllArtists = new List<Artists>(loadedArtists);
            }
        }

        private async Task LoadGenresAsync(CancellationToken cancellationToken)
        {
            var loadedGenres = await GetGenresForProfile(cancellationToken);

            // Thread-safe update of properties
            lock (_cacheLock)
            {
                _allGenres = new ReadOnlyCollection<Genres>(loadedGenres);
                _cachedAllGenres = new List<Genres>(loadedGenres);
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

        public async Task<List<TrackDisplayModel>> ValidateBlacklist(int profileId, CancellationToken cancellationToken = default)
        {
            return await ExecuteWithCancellationAsync(
                async (token) =>
                {
                    var allTracksToValidate = await _trackDisplayRepository.GetAllTracksWithMetadata(profileId);

                    // Check cancellation
                    token.ThrowIfCancellationRequested();

                    if (allTracksToValidate == null || !allTracksToValidate.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No tracks found",
                            $"No tracks found for profile {profileId}.",
                            null,
                            true);
                        return new List<TrackDisplayModel>();
                    }

                    var blacklistedPaths = await _profileConfigurationService.GetBlacklistedDirectories(profileId);

                    // Check cancellation
                    token.ThrowIfCancellationRequested();

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
                        // Check cancellation periodically (every 100 tracks)
                        if (filteredTracks.Count % 100 == 0 && token.IsCancellationRequested)
                        {
                            token.ThrowIfCancellationRequested();
                        }

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
                () => _cachedAllTracks, // Return cached tracks on cancellation or error
                $"Validating tracks against blacklist for profile {profileId}",
                cancellationToken
            );
        }

        public async Task<List<Albums>> GetAlbumsForProfile(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithCancellationAsync(
                async (token) =>
                {
                    var tempAllAlbums = await _albumRepository.GetAllAlbums();

                    // Check cancellation
                    token.ThrowIfCancellationRequested();

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
                () => _cachedAllAlbums, // Return cached albums on cancellation or error
                "Getting albums for current profile",
                cancellationToken
            );
        }

        public async Task<List<Artists>> GetArtistsForProfile(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithCancellationAsync(
                async (token) =>
                {
                    var tempAllArtists = await _artistsRepository.GetAllArtists();

                    // Check cancellation
                    token.ThrowIfCancellationRequested();

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
                () => _cachedAllArtists, // Return cached artists on cancellation or error
                "Getting artists for current profile",
                cancellationToken
            );
        }

        public async Task<List<Genres>> GetGenresForProfile(CancellationToken cancellationToken = default)
        {
            return await ExecuteWithCancellationAsync(
                async (token) =>
                {
                    var tempAllGenres = await _genresRepository.GetAllGenres();

                    // Check cancellation
                    token.ThrowIfCancellationRequested();

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
                () => _cachedAllGenres, // Return cached genres on cancellation or error
                "Getting genres for current profile",
                cancellationToken
            );
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
            _loadCancellationSource?.Cancel();
            _loadCancellationSource?.Dispose();
            _loadCancellationSource = null;
        }
    }
}