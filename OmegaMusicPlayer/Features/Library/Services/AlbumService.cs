using CommunityToolkit.Mvvm.Messaging;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Messages;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Infrastructure.Data.Repositories.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Library.Services
{
    public class AlbumService
    {
        private readonly AlbumRepository _albumRepository;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        // Cache for performance optimization
        private readonly Dictionary<int, Albums> _idCache = new Dictionary<int, Albums>();
        private readonly Dictionary<string, Dictionary<int, Albums>> _titleArtistCache = new Dictionary<string, Dictionary<int, Albums>>(StringComparer.OrdinalIgnoreCase);
        private const int MAX_CACHE_SIZE = 500;

        public AlbumService(
            AlbumRepository albumRepository,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _albumRepository = albumRepository;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            _messenger.Register<AllTracksInvalidatedMessage>(this, (r, m) => InvalidateCache());
        }

        public async Task<Albums> GetAlbumById(int albumID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check cache first
                    if (_idCache.TryGetValue(albumID, out var cachedAlbum))
                    {
                        return cachedAlbum;
                    }

                    var album = await _albumRepository.GetAlbumById(albumID);

                    // Add to cache if not null
                    if (album != null)
                    {
                        AddToCache(album);
                    }

                    return album;
                },
                $"Fetching album with ID {albumID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Albums> GetAlbumByTitle(string title, int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Return null for null/empty title
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return null;
                    }

                    // Check cache first
                    if (_titleArtistCache.TryGetValue(title, out var artistAlbums) &&
                        artistAlbums.TryGetValue(artistID, out var cachedAlbum))
                    {
                        return cachedAlbum;
                    }

                    var album = await _albumRepository.GetAlbumByTitle(title, artistID);

                    // Add to cache if not null
                    if (album != null)
                    {
                        AddToCache(album);
                    }

                    return album;
                },
                $"Fetching album by title: {title} (Artist ID: {artistID})",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Albums>> GetAllAlbums()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var albums = await _albumRepository.GetAllAlbums();

                    // Rebuild cache with results
                    InvalidateCache();

                    // Add to cache up to max size
                    foreach (var album in albums.Take(MAX_CACHE_SIZE))
                    {
                        AddToCache(album);
                    }

                    return albums;
                },
                "Fetching all albums",
                new List<Albums>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<int> AddAlbum(Albums album)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (album == null)
                    {
                        throw new ArgumentNullException(nameof(album), "Cannot add null album");
                    }

                    int albumId = await _albumRepository.AddAlbum(album);
                    album.AlbumID = albumId;

                    // Add to cache
                    AddToCache(album);

                    return albumId;
                },
                $"Adding album: {album?.Title ?? "Unknown"}",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateAlbum(Albums album)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (album == null)
                    {
                        throw new ArgumentNullException(nameof(album), "Cannot update null album");
                    }

                    await _albumRepository.UpdateAlbum(album);

                    // Update cache
                    if (album.AlbumID > 0)
                    {
                        // If the title or artist ID changed, we need to update title-artist cache
                        if (_idCache.TryGetValue(album.AlbumID, out var oldAlbum) &&
                            (oldAlbum.Title != album.Title || oldAlbum.ArtistID != album.ArtistID))
                        {
                            // Remove old title-artist mapping
                            if (_titleArtistCache.TryGetValue(oldAlbum.Title, out var artistMap))
                            {
                                artistMap.Remove(oldAlbum.ArtistID);

                                // Remove the title entry if it's empty
                                if (artistMap.Count == 0)
                                {
                                    _titleArtistCache.Remove(oldAlbum.Title);
                                }
                            }
                        }

                        AddToCache(album);
                    }
                },
                $"Updating album: {album?.Title ?? "Unknown"} (ID: {album?.AlbumID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteAlbum(int albumID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Remove from caches
                    if (_idCache.TryGetValue(albumID, out var album))
                    {
                        _idCache.Remove(albumID);

                        if (!string.IsNullOrEmpty(album.Title) &&
                            _titleArtistCache.TryGetValue(album.Title, out var artistMap))
                        {
                            artistMap.Remove(album.ArtistID);

                            // Remove the title entry if it's empty
                            if (artistMap.Count == 0)
                            {
                                _titleArtistCache.Remove(album.Title);
                            }
                        }
                    }

                    await _albumRepository.DeleteAlbum(albumID);
                },
                $"Deleting album with ID {albumID}",
                ErrorSeverity.NonCritical,
                false);
        }

        private void AddToCache(Albums album)
        {
            // Manage cache size
            if (_idCache.Count >= MAX_CACHE_SIZE)
            {
                // Simple cleanup: remove oldest third of entries
                var keysToRemove = _idCache.Keys.Take(_idCache.Count / 3).ToList();
                foreach (var key in keysToRemove)
                {
                    if (_idCache.TryGetValue(key, out var oldAlbum))
                    {
                        _idCache.Remove(key);

                        // Also remove from title-artist cache
                        if (!string.IsNullOrEmpty(oldAlbum.Title) &&
                            _titleArtistCache.TryGetValue(oldAlbum.Title, out var artistMap))
                        {
                            artistMap.Remove(oldAlbum.ArtistID);

                            // Remove title entry if empty
                            if (artistMap.Count == 0)
                            {
                                _titleArtistCache.Remove(oldAlbum.Title);
                            }
                        }
                    }
                }
            }

            // Add/update ID cache
            _idCache[album.AlbumID] = album;

            // Add/update title-artist cache if title is not empty
            if (!string.IsNullOrEmpty(album.Title))
            {
                if (!_titleArtistCache.TryGetValue(album.Title, out var artistMap))
                {
                    artistMap = new Dictionary<int, Albums>();
                    _titleArtistCache[album.Title] = artistMap;
                }

                artistMap[album.ArtistID] = album;
            }
        }

        public void InvalidateCache()
        {
            _idCache.Clear();
            _titleArtistCache.Clear();
        }
    }
}