using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Infrastructure.Data.Repositories;
using System.Linq;
using OmegaPlayer.Infrastructure.Services.Images;

namespace OmegaPlayer.Features.Library.Services
{
    public class AlbumDisplayService
    {
        private readonly AlbumRepository _albumRepository;
        private readonly StandardImageService _standardImageService;
        private readonly MediaService _mediaService;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly ArtistsService _artistService;

        public AlbumDisplayService(
            AlbumRepository albumRepository,
            StandardImageService standardImageService,
            MediaService mediaService,
            AllTracksRepository allTracksRepository,
            ArtistsService artistService)
        {
            _albumRepository = albumRepository;
            _standardImageService = standardImageService;
            _mediaService = mediaService;
            _allTracksRepository = allTracksRepository;
            _artistService = artistService;
        }

        public async Task<List<AlbumDisplayModel>> GetAllAlbumsAsync()
        {
            var albums = _allTracksRepository.AllAlbums;

            // If no albums left, return empty list
            if (!albums.Any())
            {
                return new List<AlbumDisplayModel>();
            }

            var displayModels = new List<AlbumDisplayModel>();

            foreach (var album in albums)
            {
                var displayModel = new AlbumDisplayModel
                {
                    AlbumID = album.AlbumID,
                    Title = album.Title,
                    ArtistID = album.ArtistID,
                    ReleaseDate = album.ReleaseDate
                };

                // Get tracks for this album
                var tracks = await GetAlbumTracksAsync(album.AlbumID);
                displayModel.TrackIDs = tracks.Select(t => t.TrackID).ToList();
                displayModel.TotalDuration = TimeSpan.FromTicks(tracks.Sum(t => t.Duration.Ticks));

                // Get artist name
                var artist = await _artistService.GetArtistById(album.ArtistID);
                if (artist != null)
                {
                    displayModel.ArtistName = artist.ArtistName;
                }

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

        public async Task<AlbumDisplayModel> GetAlbumByIdAsync(int albumId)
        {
            var album = await _albumRepository.GetAlbumById(albumId);
            if (album == null) return null;

            var displayModel = new AlbumDisplayModel
            {
                AlbumID = album.AlbumID,
                Title = album.Title,
                ArtistID = album.ArtistID,
                ReleaseDate = album.ReleaseDate
            };

            // Get tracks for this album
            var tracks = await GetAlbumTracksAsync(album.AlbumID);
            displayModel.TrackIDs = tracks.Select(t => t.TrackID).ToList();
            displayModel.TotalDuration = TimeSpan.FromTicks(tracks.Sum(t => t.Duration.Ticks));

            // Get artist name
            var artist = await _artistService.GetArtistById(album.ArtistID);
            if (artist != null)
            {
                displayModel.ArtistName = artist.ArtistName;
            }

            var media = await _mediaService.GetMediaById(album.CoverID);
            if (media != null)
            {
                displayModel.CoverPath = media.CoverPath;
                await LoadAlbumCoverAsync(displayModel);
            }

            return displayModel;
        }

        public async Task LoadAlbumCoverAsync(AlbumDisplayModel album, string size = "low", bool isVisible = false)
        {
            try
            {
                if (string.IsNullOrEmpty(album.CoverPath)) return;

                switch (size.ToLower())
                {
                    case "low":
                        album.Cover = await _standardImageService.LoadLowQualityAsync(album.CoverPath, isVisible);
                        break;
                    case "medium":
                        album.Cover = await _standardImageService.LoadMediumQualityAsync(album.CoverPath, isVisible);
                        break;
                    case "high":
                        album.Cover = await _standardImageService.LoadHighQualityAsync(album.CoverPath, isVisible);
                        break;
                    case "detail":
                        album.Cover = await _standardImageService.LoadDetailQualityAsync(album.CoverPath, isVisible);
                        break;
                    default:
                        album.Cover = await _standardImageService.LoadLowQualityAsync(album.CoverPath, isVisible);
                        break;
                }

                album.CoverSize = size;
            }
            catch (Exception ex)
            {
                // Log error and possibly load a default image
                Console.WriteLine($"Error loading album cover: {ex.Message}");
            }
        }
    }
}