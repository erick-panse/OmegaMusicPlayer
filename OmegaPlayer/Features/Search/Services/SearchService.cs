using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Features.Search.Services
{
    public class SearchService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly TrackDisplayService _trackDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly IErrorHandlingService _errorHandlingService;

        public SearchService(
            AllTracksRepository allTracksRepository,
            TrackDisplayService trackDisplayService,
            AlbumDisplayService albumDisplayService,
            ArtistDisplayService artistDisplayService,
            IErrorHandlingService errorHandlingService)
        {
            _allTracksRepository = allTracksRepository;
            _trackDisplayService = trackDisplayService;
            _albumDisplayService = albumDisplayService;
            _artistDisplayService = artistDisplayService;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<SearchResults> SearchAsync(string query, int maxPreviewResults = 3)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        return new SearchResults();
                    }

                    query = query.ToLower();

                    var tracks = await SearchTracksAsync(query);
                    var albums = await SearchAlbumsAsync(query);
                    var artists = await SearchArtistsAsync(query);

                    return new SearchResults
                    {
                        Tracks = tracks,
                        Albums = albums,
                        Artists = artists,
                        PreviewTracks = tracks.Take(maxPreviewResults).ToList(),
                        PreviewAlbums = albums.Take(maxPreviewResults).ToList(),
                        PreviewArtists = artists.Take(maxPreviewResults).ToList()
                    };
                },
                $"Searching for '{query}'",
                new SearchResults(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task<List<TrackDisplayModel>> SearchTracksAsync(string query)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        return new List<TrackDisplayModel>();
                    }

                    var allTracks = _allTracksRepository.AllTracks;
                    if (allTracks == null || !allTracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Empty tracks collection",
                            "Track collection is empty or not loaded when searching",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

                    var tracks = allTracks
                        .Where(t => (t.Title?.ToLower()?.Contains(query) ?? false) ||
                                   (t.Artists?.Any(a => a.ArtistName?.ToLower()?.Contains(query) ?? false) ?? false) ||
                                   (t.AlbumTitle?.ToLower()?.Contains(query) ?? false))
                        .ToList();

                    // Load thumbnails for tracks - preview items are likely to be visible initially
                    // We set preview items (top results) as visible (true) for immediate loading
                    int count = 0;
                    foreach (var track in tracks)
                    {
                        // First 3 tracks are likely to be visible immediately (for preview)
                        bool isVisible = count < 3;
                        count++;

                        await _trackDisplayService.LoadTrackCoverAsync(track, "low", isVisible);
                    }

                    return tracks;
                },
                $"Searching tracks for '{query}'",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task<List<AlbumDisplayModel>> SearchAlbumsAsync(string query)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        return new List<AlbumDisplayModel>();
                    }

                    var albums = await _albumDisplayService.GetAllAlbumsAsync();
                    if (albums == null || !albums.Any())
                    {
                        return new List<AlbumDisplayModel>();
                    }

                    var filteredAlbums = albums
                        .Where(a => (a.Title?.ToLower()?.Contains(query) ?? false) ||
                                   (a.ArtistName?.ToLower()?.Contains(query) ?? false))
                        .ToList();

                    // Load covers for albums - preview items are likely to be visible initially
                    int count = 0;
                    foreach (var album in filteredAlbums)
                    {
                        // First 3 albums are likely to be visible immediately (for preview)
                        bool isVisible = count < 3;
                        count++;

                        await _albumDisplayService.LoadAlbumCoverAsync(album, "low", isVisible);
                    }

                    return filteredAlbums;
                },
                $"Searching albums for '{query}'",
                new List<AlbumDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task<List<ArtistDisplayModel>> SearchArtistsAsync(string query)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(query))
                    {
                        return new List<ArtistDisplayModel>();
                    }

                    var artists = await _artistDisplayService.GetAllArtistsAsync();
                    if (artists == null || !artists.Any())
                    {
                        return new List<ArtistDisplayModel>();
                    }

                    var filteredArtists = artists
                        .Where(a => a.Name?.ToLower()?.Contains(query) ?? false)
                        .ToList();

                    // Load photos for artists - preview items are likely to be visible initially
                    int count = 0;
                    foreach (var artist in filteredArtists)
                    {
                        // First 3 artists are likely to be visible immediately (for preview)
                        bool isVisible = count < 3;
                        count++;

                        await _artistDisplayService.LoadArtistPhotoAsync(artist, "low", isVisible);
                    }

                    return filteredArtists;
                },
                $"Searching artists for '{query}'",
                new List<ArtistDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }
    }

    public class SearchResults
    {
        public List<TrackDisplayModel> Tracks { get; set; } = new();
        public List<AlbumDisplayModel> Albums { get; set; } = new();
        public List<ArtistDisplayModel> Artists { get; set; } = new();
        public List<TrackDisplayModel> PreviewTracks { get; set; } = new();
        public List<AlbumDisplayModel> PreviewAlbums { get; set; } = new();
        public List<ArtistDisplayModel> PreviewArtists { get; set; } = new();
    }
}