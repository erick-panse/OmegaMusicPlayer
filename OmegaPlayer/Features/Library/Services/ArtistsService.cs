using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Collections.Concurrent;
using System.Linq;

namespace OmegaPlayer.Features.Library.Services
{
    public class ArtistsService
    {
        private readonly ArtistsRepository _artistsRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache to improve performance during bulk operations
        private readonly ConcurrentDictionary<int, Artists> _idCache = new ConcurrentDictionary<int, Artists>();
        private readonly ConcurrentDictionary<string, Artists> _nameCache = new ConcurrentDictionary<string, Artists>(StringComparer.OrdinalIgnoreCase);
        private const int MAX_CACHE_SIZE = 500;
        private DateTime _lastCacheCleanup = DateTime.Now;

        public ArtistsService(
            ArtistsRepository artistsRepository,
            IErrorHandlingService errorHandlingService)
        {
            _artistsRepository = artistsRepository;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Artists> GetArtistById(int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    if (_idCache.TryGetValue(artistID, out var cachedArtist))
                    {
                        return cachedArtist;
                    }

                    var artist = await _artistsRepository.GetArtistById(artistID);

                    // Add to cache if not null
                    if (artist != null)
                    {
                        AddToCache(artist);
                    }

                    return artist;
                },
                $"Fetching artist with ID {artistID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Artists> GetArtistByName(string artistName)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(artistName))
                    {
                        return null;
                    }

                    // Check cache first
                    if (_nameCache.TryGetValue(artistName, out var cachedArtist))
                    {
                        return cachedArtist;
                    }

                    var artist = await _artistsRepository.GetArtistByName(artistName);

                    // Add to cache if not null
                    if (artist != null)
                    {
                        AddToCache(artist);
                    }

                    return artist;
                },
                $"Fetching artist by name: {artistName}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Artists>> GetAllArtists()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var artists = await _artistsRepository.GetAllArtists();

                    // Rebuild cache with results
                    InvalidateCache();

                    // Add to cache up to max size
                    foreach (var artist in artists.Take(MAX_CACHE_SIZE))
                    {
                        AddToCache(artist);
                    }

                    return artists;
                },
                "Fetching all artists",
                new List<Artists>(), // Return empty list on failure
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> AddArtist(Artists artist)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null)
                    {
                        throw new ArgumentNullException(nameof(artist), "Cannot add null artist");
                    }

                    // Check if an artist with same name already exists in cache
                    if (!string.IsNullOrEmpty(artist.ArtistName) &&
                        _nameCache.TryGetValue(artist.ArtistName, out var existingArtist))
                    {
                        return existingArtist.ArtistID;
                    }

                    int artistId = await _artistsRepository.AddArtist(artist);
                    artist.ArtistID = artistId;

                    // Add to cache
                    AddToCache(artist);

                    return artistId;
                },
                $"Adding artist: {artist?.ArtistName ?? "Unknown"}",
                -1, // Return -1 on failure
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateArtist(Artists artist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null)
                    {
                        throw new ArgumentNullException(nameof(artist), "Cannot update null artist");
                    }

                    await _artistsRepository.UpdateArtist(artist);

                    // Update cache
                    if (artist.ArtistID > 0)
                    {
                        // If the name changed, remove old name from cache
                        if (_idCache.TryGetValue(artist.ArtistID, out var oldArtist) &&
                            oldArtist.ArtistName != artist.ArtistName)
                        {
                            _nameCache.TryRemove(oldArtist.ArtistName, out _);
                        }

                        AddToCache(artist);
                    }
                },
                $"Updating artist: {artist?.ArtistName ?? "Unknown"} (ID: {artist?.ArtistID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteArtist(int artistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Remove from cache
                    if (_idCache.TryRemove(artistID, out var artist) && artist != null)
                    {
                        _nameCache.TryRemove(artist.ArtistName, out _);
                    }

                    await _artistsRepository.DeleteArtist(artistID);
                },
                $"Deleting artist with ID {artistID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private void AddToCache(Artists artist)
        {
            // Manage cache size
            if (_idCache.Count >= MAX_CACHE_SIZE)
            {
                // Only clean up at most once per minute to avoid excessive cleaning
                if ((DateTime.Now - _lastCacheCleanup).TotalMinutes >= 1)
                {
                    CleanupCache();
                    _lastCacheCleanup = DateTime.Now;
                }
            }

            // Add/update in caches
            _idCache[artist.ArtistID] = artist;

            if (!string.IsNullOrEmpty(artist.ArtistName))
            {
                _nameCache[artist.ArtistName] = artist;
            }
        }

        private void CleanupCache()
        {
            // Simple strategy: remove half the entries
            var keysToRemove = _idCache.Keys.Take(_idCache.Count / 2).ToList();
            foreach (var key in keysToRemove)
            {
                if (_idCache.TryRemove(key, out var artist) && artist != null)
                {
                    _nameCache.TryRemove(artist.ArtistName, out _);
                }
            }
        }

        public void InvalidateCache()
        {
            _idCache.Clear();
            _nameCache.Clear();
        }
    }
}