using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.Services
{
    public class TracksService
    {
        private readonly TracksRepository _tracksRepository;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        // Cache for frequently accessed tracks to reduce DB load during scanning
        private readonly Dictionary<string, Tracks> _pathCache = new Dictionary<string, Tracks>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, Tracks> _idCache = new Dictionary<int, Tracks>();
        private const int MAX_CACHE_SIZE = 1000;

        public TracksService(
            TracksRepository tracksRepository,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _tracksRepository = tracksRepository;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            _messenger.Register<AllTracksInvalidatedMessage>(this, (r, m) => InvalidateCache());
        }

        public async Task<Tracks> GetTrackById(int trackID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    if (_idCache.TryGetValue(trackID, out var cachedTrack))
                    {
                        return cachedTrack;
                    }

                    var track = await _tracksRepository.GetTrackById(trackID);

                    // Cache the result if not null
                    if (track != null)
                    {
                        AddToCache(track);
                    }

                    return track;
                },
                $"Fetching track with ID {trackID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Tracks> GetTrackByPath(string filePath)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Normalize path for cache lookup
                    string normalizedPath = filePath?.Replace('\\', '/');

                    // Check cache first
                    if (!string.IsNullOrEmpty(normalizedPath) && _pathCache.TryGetValue(normalizedPath, out var cachedTrack))
                    {
                        return cachedTrack;
                    }

                    var track = await _tracksRepository.GetTrackByPath(filePath);

                    // Cache the result if not null
                    if (track != null)
                    {
                        AddToCache(track);
                    }

                    return track;
                },
                $"Fetching track by path: {Path.GetFileName(filePath)}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Tracks>> GetAllTracks()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var tracks = await _tracksRepository.GetAllTracks();

                    // Clear and rebuild cache with results
                    _idCache.Clear();
                    _pathCache.Clear();

                    // Cache up to MAX_CACHE_SIZE tracks
                    foreach (var track in tracks.Take(MAX_CACHE_SIZE))
                    {
                        AddToCache(track);
                    }

                    return tracks;
                },
                "Fetching all tracks",
                new List<Tracks>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> AddTrack(Tracks track)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null)
                    {
                        throw new ArgumentNullException(nameof(track), "Cannot add null track");
                    }

                    int trackId = await _tracksRepository.AddTrack(track);
                    track.TrackID = trackId;

                    // Update cache
                    AddToCache(track);

                    return trackId;
                },
                $"Adding track: {track?.Title ?? "Unknown"}",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateTrack(Tracks track)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null)
                    {
                        throw new ArgumentNullException(nameof(track), "Cannot update null track");
                    }

                    await _tracksRepository.UpdateTrack(track);

                    // Update cache
                    AddToCache(track);
                },
                $"Updating track: {track?.Title ?? "Unknown"} (ID: {track?.TrackID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteTrack(int trackID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Remove from cache first
                    if (_idCache.TryGetValue(trackID, out var trackToRemove))
                    {
                        _idCache.Remove(trackID);

                        if (!string.IsNullOrEmpty(trackToRemove.FilePath))
                        {
                            string normalizedPath = trackToRemove.FilePath.Replace('\\', '/');
                            _pathCache.Remove(normalizedPath);
                        }
                    }

                    await _tracksRepository.DeleteTrack(trackID);
                },
                $"Deleting track with ID {trackID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private void AddToCache(Tracks track)
        {
            // Ensure the cache doesn't grow too large
            if (_idCache.Count >= MAX_CACHE_SIZE)
            {
                // Simple eviction strategy: clear half the cache
                var keysToRemove = _idCache.Keys.Take(_idCache.Count / 2).ToList();
                foreach (var key in keysToRemove)
                {
                    _idCache.Remove(key);
                }

                // Clear path cache too
                _pathCache.Clear();
            }

            // Add/update the track in the ID cache
            _idCache[track.TrackID] = track;

            // Add to path cache if it has a file path
            if (!string.IsNullOrEmpty(track.FilePath))
            {
                string normalizedPath = track.FilePath.Replace('\\', '/');
                _pathCache[normalizedPath] = track;
            }
        }

        public void InvalidateCache()
        {
            _idCache.Clear();
            _pathCache.Clear();
        }
    }
}