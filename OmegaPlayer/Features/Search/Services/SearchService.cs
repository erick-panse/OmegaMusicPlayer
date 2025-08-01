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
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly ArtistDisplayService _artistDisplayService;
        private readonly SearchInputCleaner _searchInputCleaner;
        private readonly IErrorHandlingService _errorHandlingService;

        public SearchService(
            AllTracksRepository allTracksRepository,
            AlbumDisplayService albumDisplayService,
            ArtistDisplayService artistDisplayService,
            SearchInputCleaner searchInputCleaner,
            IErrorHandlingService errorHandlingService)
        {
            _allTracksRepository = allTracksRepository;
            _albumDisplayService = albumDisplayService;
            _artistDisplayService = artistDisplayService;
            _searchInputCleaner = searchInputCleaner;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<SearchResults> SearchAsync(string query, int maxPreviewResults = 3)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Clean input first
                    string cleanedQuery = _searchInputCleaner.CleanSearchInput(query);

                    if (string.IsNullOrWhiteSpace(cleanedQuery))
                    {
                        // Log potentially malicious input attempts (without exposing the actual input)
                        if (!string.IsNullOrWhiteSpace(query))
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.Info,
                                "Invalid search query blocked",
                                "A potentially unsafe search query was cleaned and blocked",
                                null,
                                false);
                        }
                        return new SearchResults();
                    }

                    // Convert to lowercase for case-insensitive search
                    cleanedQuery = cleanedQuery.ToLower();

                    // Search all items without loading images initially
                    var tracks = await SearchTracksAsync(cleanedQuery);
                    var albums = await SearchAlbumsAsync(cleanedQuery);
                    var artists = await SearchArtistsAsync(cleanedQuery);

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
                _searchInputCleaner.CreateSearchSummary(query, _searchInputCleaner.CleanSearchInput(query)),
                new SearchResults(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task<List<TrackDisplayModel>> SearchTracksAsync(string cleanedQuery)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(cleanedQuery))
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

                    // Perform safe string matching on in-memory collections
                    var tracks = allTracks
                        .Where(t => IsTrackMatch(t, cleanedQuery))
                        .ToList();

                    return tracks;
                },
                $"Searching tracks",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task<List<AlbumDisplayModel>> SearchAlbumsAsync(string cleanedQuery)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(cleanedQuery))
                    {
                        return new List<AlbumDisplayModel>();
                    }

                    var albums = await _albumDisplayService.GetAllAlbumsAsync();
                    if (albums == null || !albums.Any())
                    {
                        return new List<AlbumDisplayModel>();
                    }

                    var filteredAlbums = albums
                        .Where(a => IsAlbumMatch(a, cleanedQuery))
                        .ToList();

                    return filteredAlbums;
                },
                "Searching albums",
                new List<AlbumDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        private async Task<List<ArtistDisplayModel>> SearchArtistsAsync(string cleanedQuery)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(cleanedQuery))
                    {
                        return new List<ArtistDisplayModel>();
                    }

                    var artists = await _artistDisplayService.GetAllArtistsAsync();
                    if (artists == null || !artists.Any())
                    {
                        return new List<ArtistDisplayModel>();
                    }

                    var filteredArtists = artists
                        .Where(a => IsArtistMatch(a, cleanedQuery))
                        .ToList();

                    return filteredArtists;
                },
                "Searching artists",
                new List<ArtistDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Safe track matching with null checks and length limits
        /// </summary>
        private bool IsTrackMatch(TrackDisplayModel track, string cleanedQuery)
        {
            if (track == null || string.IsNullOrWhiteSpace(cleanedQuery))
                return false;

            try
            {
                // Check title
                if (!string.IsNullOrEmpty(track.Title) &&
                    track.Title.ToLower().Contains(cleanedQuery))
                    return true;

                // Check artists
                if (track.Artists?.Any() == true)
                {
                    if (track.Artists.Any(a => !string.IsNullOrEmpty(a.ArtistName) &&
                                              a.ArtistName.ToLower().Contains(cleanedQuery)))
                        return true;
                }

                // Check album
                if (!string.IsNullOrEmpty(track.AlbumTitle) &&
                    track.AlbumTitle.ToLower().Contains(cleanedQuery))
                    return true;

                return false;
            }
            catch
            {
                // If any exception occurs during matching, skip this track
                return false;
            }
        }

        /// <summary>
        /// Safe album matching with null checks
        /// </summary>
        private bool IsAlbumMatch(AlbumDisplayModel album, string cleanedQuery)
        {
            if (album == null || string.IsNullOrWhiteSpace(cleanedQuery))
                return false;

            try
            {
                // Check title
                if (!string.IsNullOrEmpty(album.Title) &&
                    album.Title.ToLower().Contains(cleanedQuery))
                    return true;

                // Check artist
                if (!string.IsNullOrEmpty(album.ArtistName) &&
                    album.ArtistName.ToLower().Contains(cleanedQuery))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Safe artist matching with null checks
        /// </summary>
        private bool IsArtistMatch(ArtistDisplayModel artist, string cleanedQuery)
        {
            if (artist == null || string.IsNullOrWhiteSpace(cleanedQuery))
                return false;

            try
            {
                return !string.IsNullOrEmpty(artist.Name) &&
                       artist.Name.ToLower().Contains(cleanedQuery);
            }
            catch
            {
                return false;
            }
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