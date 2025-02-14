using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;

namespace OmegaPlayer.Features.Search.Services
{
    public class SearchService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly TrackDisplayService _trackDisplayService;
        private readonly AlbumDisplayService _albumDisplayService;
        private readonly ArtistDisplayService _artistDisplayService;

        public SearchService(
            AllTracksRepository allTracksRepository,
            TrackDisplayService trackDisplayService,
            AlbumDisplayService albumDisplayService,
            ArtistDisplayService artistDisplayService)
        {
            _allTracksRepository = allTracksRepository;
            _trackDisplayService = trackDisplayService;
            _albumDisplayService = albumDisplayService;
            _artistDisplayService = artistDisplayService;
        }

        public async Task<SearchResults> SearchAsync(string query, int maxPreviewResults = 3)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new SearchResults();

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
        }

        private async Task<List<TrackDisplayModel>> SearchTracksAsync(string query)
        {
            var tracks = _allTracksRepository.AllTracks
                .Where(t => t.Title.ToLower().Contains(query) ||
                            t.Artists.Any(a => a.ArtistName.ToLower().Contains(query)) ||
                            t.AlbumTitle.ToLower().Contains(query))
                .ToList();

            // Load thumbnails for tracks
            foreach (var track in tracks)
            {
                await _trackDisplayService.LoadHighResThumbnailAsync(track);
            }

            return tracks;
        }

        private async Task<List<AlbumDisplayModel>> SearchAlbumsAsync(string query)
        {
            var albums = await _albumDisplayService.GetAllAlbumsAsync();
            var filteredAlbums = albums
                .Where(a => a.Title.ToLower().Contains(query) ||
                            a.ArtistName.ToLower().Contains(query))
                .ToList();

            // Load covers for albums
            foreach (var album in filteredAlbums)
            {
                await _albumDisplayService.LoadAlbumCoverAsync(album);
            }

            return filteredAlbums;
        }

        private async Task<List<ArtistDisplayModel>> SearchArtistsAsync(string query)
        {
            var artists = await _artistDisplayService.GetAllArtistsAsync();
            var filteredArtists = artists
                .Where(a => a.Name.ToLower().Contains(query))
                .ToList();

            // Load photos for artists
            foreach (var artist in filteredArtists)
            {
                await _artistDisplayService.LoadArtistPhotoAsync(artist);
            }

            return filteredArtists;
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