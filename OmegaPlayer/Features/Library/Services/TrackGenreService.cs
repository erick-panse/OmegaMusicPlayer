using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Linq;

namespace OmegaPlayer.Features.Library.Services
{
    public class TrackGenreService
    {
        private readonly TrackGenreRepository _trackGenreRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache for track-genre relationships to improve performance
        private readonly Dictionary<string, TrackGenre> _relationshipCache = new Dictionary<string, TrackGenre>();
        private readonly Dictionary<int, int> _trackToGenreCache = new Dictionary<int, int>();
        private readonly Dictionary<int, List<int>> _genreToTracksCache = new Dictionary<int, List<int>>();
        private const int MAX_CACHE_SIZE = 10000; // These are small objects, can cache more

        public TrackGenreService(
            TrackGenreRepository trackGenreRepository,
            IErrorHandlingService errorHandlingService)
        {
            _trackGenreRepository = trackGenreRepository;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<TrackGenre> GetTrackGenre(int trackID, int genreID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    string cacheKey = GetCacheKey(trackID, genreID);
                    if (_relationshipCache.TryGetValue(cacheKey, out var cachedRelationship))
                    {
                        return cachedRelationship;
                    }

                    var relationship = await _trackGenreRepository.GetTrackGenre(trackID, genreID);

                    if (relationship != null)
                    {
                        // Add to caches
                        AddToCache(relationship);
                    }

                    return relationship;
                },
                $"Fetching track-genre relationship: Track ID {trackID}, Genre ID {genreID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackGenre>> GetAllTrackGenres()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var relationships = await _trackGenreRepository.GetAllTrackGenres();

                    // Rebuild caches with complete data
                    _relationshipCache.Clear();
                    _trackToGenreCache.Clear();
                    _genreToTracksCache.Clear();

                    foreach (var relationship in relationships)
                    {
                        AddToCache(relationship);
                    }

                    return relationships;
                },
                "Fetching all track-genre relationships",
                new List<TrackGenre>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task AddTrackGenre(TrackGenre trackGenre)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackGenre == null)
                    {
                        throw new ArgumentNullException(nameof(trackGenre), "Cannot add null track-genre relationship");
                    }

                    // Check if relationship already exists in cache
                    string cacheKey = GetCacheKey(trackGenre.TrackID, trackGenre.GenreID);
                    if (_relationshipCache.ContainsKey(cacheKey))
                    {
                        // Relationship already exists, no need to add it again
                        return;
                    }

                    // Check if it exists in the database
                    var existingRelationship = await _trackGenreRepository.GetTrackGenre(
                        trackGenre.TrackID, trackGenre.GenreID);

                    if (existingRelationship != null)
                    {
                        // Already exists in database, just add to cache
                        AddToCache(existingRelationship);
                        return;
                    }

                    // Add to repository
                    await _trackGenreRepository.AddTrackGenre(trackGenre);

                    // Add to cache
                    AddToCache(trackGenre);
                },
                $"Adding track-genre relationship: Track ID {trackGenre?.TrackID}, Genre ID {trackGenre?.GenreID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteAllTrackGenresForTrack(int trackID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Remove from caches - get the genre for this track first
                    if (_trackToGenreCache.TryGetValue(trackID, out var genreId))
                    {
                        RemoveFromCache(trackID, genreId);
                    }

                    // Delete from repository
                    await _trackGenreRepository.DeleteAllTrackGenresForTrack(trackID);
                },
                $"Deleting all track-genre relationships for track ID {trackID}",
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> GetGenreByTrackId(int trackId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    if (_trackToGenreCache.TryGetValue(trackId, out var cachedGenreId))
                    {
                        return cachedGenreId;
                    }

                    // If not in cache, we need to fetch all relationships to ensure cache is up-to-date
                    var allRelationships = await GetAllTrackGenres();

                    // Now check again
                    return _trackToGenreCache.TryGetValue(trackId, out var genreId)
                        ? genreId
                        : 0; // 0 indicates no genre found
                },
                $"Getting genre for track ID {trackId}",
                0, // Return 0 (unknown genre) on failure
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<int>> GetTracksByGenreId(int genreId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    if (_genreToTracksCache.TryGetValue(genreId, out var cachedTracks))
                    {
                        return cachedTracks;
                    }

                    // If not in cache, need to fetch all relationships
                    var allRelationships = await GetAllTrackGenres();

                    // Now it should be in cache
                    return _genreToTracksCache.TryGetValue(genreId, out var tracks)
                        ? tracks
                        : new List<int>();
                },
                $"Getting tracks for genre ID {genreId}",
                new List<int>(),
                ErrorSeverity.NonCritical,
                false);
        }

        private string GetCacheKey(int trackId, int genreId)
        {
            return $"{trackId}:{genreId}";
        }

        private void AddToCache(TrackGenre relationship)
        {
            // Check if we need to manage cache size
            if (_relationshipCache.Count >= MAX_CACHE_SIZE)
            {
                // Clear half the cache when we hit the limit
                var keysToRemove = _relationshipCache.Keys.Take(_relationshipCache.Count / 2).ToList();
                foreach (var key in keysToRemove)
                {
                    _relationshipCache.Remove(key);
                }

                // Also clear the lookup caches as they're now incomplete
                _trackToGenreCache.Clear();
                _genreToTracksCache.Clear();
            }

            // Add to relationship cache
            string cacheKey = GetCacheKey(relationship.TrackID, relationship.GenreID);
            _relationshipCache[cacheKey] = relationship;

            // Add to track->genre cache (assuming one genre per track)
            _trackToGenreCache[relationship.TrackID] = relationship.GenreID;

            // Add to genre->tracks cache
            if (!_genreToTracksCache.TryGetValue(relationship.GenreID, out var tracks))
            {
                tracks = new List<int>();
                _genreToTracksCache[relationship.GenreID] = tracks;
            }
            if (!tracks.Contains(relationship.TrackID))
            {
                tracks.Add(relationship.TrackID);
            }
        }

        private void RemoveFromCache(int trackId, int genreId)
        {
            // Remove from relationship cache
            string cacheKey = GetCacheKey(trackId, genreId);
            _relationshipCache.Remove(cacheKey);

            // Remove from track->genre cache
            _trackToGenreCache.Remove(trackId);

            // Remove from genre->tracks cache
            if (_genreToTracksCache.TryGetValue(genreId, out var tracks))
            {
                tracks.Remove(trackId);
                if (tracks.Count == 0)
                {
                    _genreToTracksCache.Remove(genreId);
                }
            }
        }

        public void InvalidateCache()
        {
            _relationshipCache.Clear();
            _trackToGenreCache.Clear();
            _genreToTracksCache.Clear();
        }
    }
}