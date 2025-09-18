using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.Services
{
    public class TrackArtistService
    {
        private readonly TrackArtistRepository _trackArtistRepository;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        // Cache for track-artist relationships to improve performance
        private readonly Dictionary<string, TrackArtist> _relationshipCache = new Dictionary<string, TrackArtist>();
        private readonly Dictionary<int, List<int>> _trackToArtistsCache = new Dictionary<int, List<int>>();
        private readonly Dictionary<int, List<int>> _artistToTracksCache = new Dictionary<int, List<int>>();
        private const int MAX_CACHE_SIZE = 2000; // These are small objects, can cache more

        public TrackArtistService(
            TrackArtistRepository trackArtistRepository,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _trackArtistRepository = trackArtistRepository;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            _messenger.Register<AllTracksInvalidatedMessage>(this, (r, m) => InvalidateCache());
        }

        public async Task<TrackArtist> GetTrackArtist(int trackID, int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    string cacheKey = GetCacheKey(trackID, artistID);
                    if (_relationshipCache.TryGetValue(cacheKey, out var cachedRelationship))
                    {
                        return cachedRelationship;
                    }

                    var relationship = await _trackArtistRepository.GetTrackArtist(trackID, artistID);

                    if (relationship != null)
                    {
                        // Add to caches
                        AddToCache(relationship);
                    }

                    return relationship;
                },
                $"Fetching track-artist relationship: Track ID {trackID}, Artist ID {artistID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackArtist>> GetAllTrackArtists()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var relationships = await _trackArtistRepository.GetAllTrackArtists();

                    // Rebuild caches with complete data
                    _relationshipCache.Clear();
                    _trackToArtistsCache.Clear();
                    _artistToTracksCache.Clear();

                    foreach (var relationship in relationships)
                    {
                        AddToCache(relationship);
                    }

                    return relationships;
                },
                "Fetching all track-artist relationships",
                new List<TrackArtist>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task AddTrackArtist(TrackArtist trackArtist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackArtist == null)
                    {
                        throw new ArgumentNullException(nameof(trackArtist), "Cannot add null track-artist relationship");
                    }

                    // Check if relationship already exists in cache
                    string cacheKey = GetCacheKey(trackArtist.TrackID, trackArtist.ArtistID);
                    if (_relationshipCache.ContainsKey(cacheKey))
                    {
                        // Relationship already exists, no need to add it again
                        return;
                    }

                    // Check if it exists in the database
                    var existingRelationship = await _trackArtistRepository.GetTrackArtist(
                        trackArtist.TrackID, trackArtist.ArtistID);

                    if (existingRelationship != null)
                    {
                        // Already exists in database, just add to cache
                        AddToCache(existingRelationship);
                        return;
                    }

                    // Add to repository
                    await _trackArtistRepository.AddTrackArtist(trackArtist);

                    // Add to cache
                    AddToCache(trackArtist);
                },
                $"Adding track-artist relationship: Track ID {trackArtist?.TrackID}, Artist ID {trackArtist?.ArtistID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteAllTrackArtistsForTrack(int trackID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Remove from caches - get all artists for this track first
                    if (_trackToArtistsCache.TryGetValue(trackID, out var artistIds))
                    {
                        // Remove each relationship from cache
                        foreach (var artistId in artistIds.ToList()) // ToList to avoid modification during iteration
                        {
                            RemoveFromCache(trackID, artistId);
                        }
                    }

                    // Delete from repository
                    await _trackArtistRepository.DeleteAllTrackArtistsForTrack(trackID);
                },
                $"Deleting all track-artist relationships for track ID {trackID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<int>> GetArtistsByTrackId(int trackId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    if (_trackToArtistsCache.TryGetValue(trackId, out var cachedArtists))
                    {
                        return cachedArtists;
                    }

                    // If not in cache, need to fetch all relationships
                    var allRelationships = await GetAllTrackArtists();

                    // Now it should be in cache
                    return _trackToArtistsCache.TryGetValue(trackId, out var artists)
                        ? artists
                        : new List<int>();
                },
                $"Getting artists for track ID {trackId}",
                new List<int>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<int>> GetTracksByArtistId(int artistId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    if (_artistToTracksCache.TryGetValue(artistId, out var cachedTracks))
                    {
                        return cachedTracks;
                    }

                    // If not in cache, need to fetch all relationships
                    var allRelationships = await GetAllTrackArtists();

                    // Now it should be in cache
                    return _artistToTracksCache.TryGetValue(artistId, out var tracks)
                        ? tracks
                        : new List<int>();
                },
                $"Getting tracks for artist ID {artistId}",
                new List<int>(),
                ErrorSeverity.NonCritical,
                false);
        }

        private string GetCacheKey(int trackId, int artistId)
        {
            return $"{trackId}:{artistId}";
        }

        private void AddToCache(TrackArtist relationship)
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
                _trackToArtistsCache.Clear();
                _artistToTracksCache.Clear();
            }

            // Add to relationship cache
            string cacheKey = GetCacheKey(relationship.TrackID, relationship.ArtistID);
            _relationshipCache[cacheKey] = relationship;

            // Add to track->artists cache
            if (!_trackToArtistsCache.TryGetValue(relationship.TrackID, out var artists))
            {
                artists = new List<int>();
                _trackToArtistsCache[relationship.TrackID] = artists;
            }
            if (!artists.Contains(relationship.ArtistID))
            {
                artists.Add(relationship.ArtistID);
            }

            // Add to artist->tracks cache
            if (!_artistToTracksCache.TryGetValue(relationship.ArtistID, out var tracks))
            {
                tracks = new List<int>();
                _artistToTracksCache[relationship.ArtistID] = tracks;
            }
            if (!tracks.Contains(relationship.TrackID))
            {
                tracks.Add(relationship.TrackID);
            }
        }

        private void RemoveFromCache(int trackId, int artistId)
        {
            // Remove from relationship cache
            string cacheKey = GetCacheKey(trackId, artistId);
            _relationshipCache.Remove(cacheKey);

            // Remove from track->artists cache
            if (_trackToArtistsCache.TryGetValue(trackId, out var artists))
            {
                artists.Remove(artistId);
                if (artists.Count == 0)
                {
                    _trackToArtistsCache.Remove(trackId);
                }
            }

            // Remove from artist->tracks cache
            if (_artistToTracksCache.TryGetValue(artistId, out var tracks))
            {
                tracks.Remove(trackId);
                if (tracks.Count == 0)
                {
                    _artistToTracksCache.Remove(artistId);
                }
            }
        }

        public void InvalidateCache()
        {
            _relationshipCache.Clear();
            _trackToArtistsCache.Clear();
            _artistToTracksCache.Clear();
        }
    }
}