using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Services.Cache;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System.Linq;

namespace OmegaPlayer.Features.Library.Services
{
    public class AlbumDisplayService
    {
        private readonly AlbumRepository _albumRepository;
        private readonly ImageCacheService _imageCacheService;
        private readonly MediaService _mediaService;
        private readonly TracksService _tracksService;
        private readonly AllTracksRepository _allTracksRepository;

        public AlbumDisplayService(
            AlbumRepository albumRepository,
            ImageCacheService imageCacheService,
            MediaService mediaService,
            TracksService tracksService,
            AllTracksRepository allTracksRepository)
        {
            _albumRepository = albumRepository;
            _imageCacheService = imageCacheService;
            _mediaService = mediaService;
            _tracksService = tracksService;
            _allTracksRepository = allTracksRepository;
        }

        public async Task<List<AlbumDisplayModel>> GetAlbumsPageAsync(int pageNumber, int pageSize)
        {
            // Get a page of albums from the repository
            var albums = await _albumRepository.GetAllAlbums();
            var displayModels = new List<AlbumDisplayModel>();

            foreach (var album in albums)
            {
                var displayModel = new AlbumDisplayModel
                {
                    AlbumID = album.AlbumID,
                    Title = album.Title,
                    ArtistID = album.ArtistID,
                    ReleaseDate = album.ReleaseDate,
                    // Convert other properties as needed
                };

                // Get the cover path from the media service
                var media = await _mediaService.GetMediaById(album.CoverID);
                if (media != null)
                {
                    displayModel.CoverPath = media.CoverPath;
                }

                displayModels.Add(displayModel);
            }

            return displayModels;
        }

        public async Task<List<TrackDisplayModel>> GetAlbumTracksAsync(int albumId)
        {
            // Get all tracks that belong to this album from AllTracksRepository
            return _allTracksRepository.AllTracks
                .Where(t => t.AlbumID == albumId)
                .ToList();
        }

        public async Task LoadAlbumCoverAsync(AlbumDisplayModel album)
        {
            if (!string.IsNullOrEmpty(album.CoverPath))
            {
                try
                {
                    album.Cover = await _imageCacheService.LoadThumbnailAsync(album.CoverPath, 110, 110);
                    album.CoverSize = "low";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading album cover: {ex.Message}");
                    // Handle error - maybe set a default cover
                }
            }
        }

        public async Task LoadHighResAlbumCoverAsync(AlbumDisplayModel album)
        {
            if (!string.IsNullOrEmpty(album.CoverPath))
            {
                try
                {
                    album.Cover = await _imageCacheService.LoadThumbnailAsync(album.CoverPath, 160, 160);
                    album.CoverSize = "high";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading high-res album cover: {ex.Message}");
                }
            }
        }
    }
}
