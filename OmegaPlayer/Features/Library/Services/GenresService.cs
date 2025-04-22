using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Features.Library.Services
{
    public class GenresService
    {
        private readonly GenresRepository _genresRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache for efficient lookups - genres are typically few and static
        private readonly Dictionary<string, Genres> _nameCache = new Dictionary<string, Genres>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Genres> _idCache = new Dictionary<int, Genres>();
        private bool _isCacheInitialized = false;

        public GenresService(
            GenresRepository genresRepository,
            IErrorHandlingService errorHandlingService)
        {
            _genresRepository = genresRepository;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Genres> GetGenreByName(string genreName)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Initialize cache if needed
                    await EnsureCacheInitializedAsync();

                    // Handle null/empty genre name by returning "Unknown" genre
                    if (string.IsNullOrWhiteSpace(genreName))
                    {
                        return await GetOrCreateUnknownGenreAsync();
                    }

                    // Check cache first
                    if (_nameCache.TryGetValue(genreName, out var cachedGenre))
                    {
                        return cachedGenre;
                    }

                    // Try to get from repository
                    var genre = await _genresRepository.GetGenreByName(genreName);

                    if (genre != null)
                    {
                        // Add to cache
                        _nameCache[genreName] = genre;
                        _idCache[genre.GenreID] = genre;
                        return genre;
                    }

                    // Genre doesn't exist in database, return null
                    return null;
                },
                $"Fetching genre by name: {genreName}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Genres>> GetAllGenres()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var genres = await _genresRepository.GetAllGenres();

                    // Rebuild the cache with all genres
                    _nameCache.Clear();
                    _idCache.Clear();

                    foreach (var genre in genres)
                    {
                        _nameCache[genre.GenreName] = genre;
                        _idCache[genre.GenreID] = genre;
                    }

                    _isCacheInitialized = true;

                    return genres;
                },
                "Fetching all genres",
                new List<Genres>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> AddGenre(Genres genre)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genre == null)
                    {
                        throw new ArgumentNullException(nameof(genre), "Cannot add null genre");
                    }

                    if (string.IsNullOrWhiteSpace(genre.GenreName))
                    {
                        genre.GenreName = "Unknown";
                    }

                    // Check if the genre already exists in cache
                    if (_nameCache.TryGetValue(genre.GenreName, out var existingGenre))
                    {
                        return existingGenre.GenreID;
                    }

                    // Ensure cache is initialized
                    await EnsureCacheInitializedAsync();

                    // Add to repository
                    int genreId = await _genresRepository.AddGenre(genre);
                    genre.GenreID = genreId;

                    // Add to cache
                    _nameCache[genre.GenreName] = genre;
                    _idCache[genre.GenreID] = genre;

                    return genreId;
                },
                $"Adding genre: {genre?.GenreName ?? "Unknown"}",
                -1, // Return -1 on failure
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateGenre(Genres genre)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genre == null)
                    {
                        throw new ArgumentNullException(nameof(genre), "Cannot update null genre");
                    }

                    if (string.IsNullOrWhiteSpace(genre.GenreName))
                    {
                        genre.GenreName = "Unknown";
                    }

                    // Ensure cache is initialized
                    await EnsureCacheInitializedAsync();

                    // Update repository
                    await _genresRepository.UpdateGenre(genre);

                    // Check if name has changed - if so, update cache accordingly
                    if (_idCache.TryGetValue(genre.GenreID, out var oldGenre) &&
                        oldGenre.GenreName != genre.GenreName)
                    {
                        _nameCache.Remove(oldGenre.GenreName);
                    }

                    // Update cache
                    _nameCache[genre.GenreName] = genre;
                    _idCache[genre.GenreID] = genre;
                },
                $"Updating genre: {genre?.GenreName ?? "Unknown"} (ID: {genre?.GenreID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteGenre(int genreID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Ensure cache is initialized
                    await EnsureCacheInitializedAsync();

                    // Remove from cache
                    if (_idCache.TryGetValue(genreID, out var genre))
                    {
                        _idCache.Remove(genreID);
                        _nameCache.Remove(genre.GenreName);
                    }

                    // Delete from repository
                    await _genresRepository.DeleteGenre(genreID);
                },
                $"Deleting genre with ID {genreID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task EnsureCacheInitializedAsync()
        {
            if (!_isCacheInitialized)
            {
                await GetAllGenres();

                // Make sure we have the "Unknown" genre
                await GetOrCreateUnknownGenreAsync();
            }
        }

        private async Task<Genres> GetOrCreateUnknownGenreAsync()
        {
            // Check if "Unknown" genre exists in cache
            if (_nameCache.TryGetValue("Unknown", out var unknownGenre))
            {
                return unknownGenre;
            }

            // Try to get from repository
            unknownGenre = await _genresRepository.GetGenreByName("Unknown");

            if (unknownGenre == null)
            {
                // Create "Unknown" genre
                unknownGenre = new Genres { GenreName = "Unknown" };
                int id = await _genresRepository.AddGenre(unknownGenre);
                unknownGenre.GenreID = id;
            }

            // Add to cache
            _nameCache["Unknown"] = unknownGenre;
            _idCache[unknownGenre.GenreID] = unknownGenre;

            return unknownGenre;
        }

        public void InvalidateCache()
        {
            _nameCache.Clear();
            _idCache.Clear();
            _isCacheInitialized = false;
        }
    }
}