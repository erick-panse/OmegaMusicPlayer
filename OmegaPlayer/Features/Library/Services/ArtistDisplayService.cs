using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Services.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Avalonia.Platform;
using Avalonia.Media.Immutable;

namespace OmegaPlayer.Features.Library.Services
{
    public class ArtistDisplayService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly ImageCacheService _imageCacheService;

        public ArtistDisplayService(
            AllTracksRepository allTracksRepository,
            ImageCacheService imageCacheService)
        {
            _allTracksRepository = allTracksRepository;
            _imageCacheService = imageCacheService;
        }

        public async Task<List<ArtistDisplayModel>> GetArtistsPageAsync(int page, int pageSize)
        {
            var allTracks = _allTracksRepository.AllTracks;

            // Group tracks by artist and create ArtistDisplayModels
            var artistGroups = allTracks
                .SelectMany(t => t.Artists.Select(a => new { Artist = a, Track = t }))
                .GroupBy(x => x.Artist.ArtistID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            var artists = new List<ArtistDisplayModel>();

            foreach (var group in artistGroups)
            {
                var artistTracks = group.Select(x => x.Track).ToList();
                var artist = group.First().Artist;

                var artistModel = new ArtistDisplayModel
                {
                    ArtistID = artist.ArtistID,
                    Name = artist.ArtistName,
                    TrackIDs = artistTracks.Select(t => t.TrackID).ToList(),
                    TotalDuration = TimeSpan.FromTicks(artistTracks.Sum(t => t.Duration.Ticks))
                };

                artists.Add(artistModel);

                // Load low-res photo initially
                await LoadArtistPhotoAsync(artistModel, "low");
            }

            return artists;
        }

        public async Task<List<TrackDisplayModel>> GetArtistTracksAsync(int artistId)
        {
            // Get all tracks for this artist from AllTracksRepository
            return _allTracksRepository.AllTracks
                .Where(t => t.Artists.Any(a => a.ArtistID == artistId))
                .ToList();
        }

        public async Task<List<TrackDisplayModel>> GetTracksForArtistsAsync(List<int> artistIds)
        {
            // Get all tracks for multiple artists
            return _allTracksRepository.AllTracks
                .Where(t => t.Artists.Any(a => artistIds.Contains(a.ArtistID)))
                .ToList();
        }

        public async Task LoadArtistPhotoAsync(ArtistDisplayModel artist)
        {
            try
            {
                if (string.IsNullOrEmpty(artist.PhotoPath)) return;

                artist.Photo = await _imageCacheService.LoadThumbnailAsync(artist.PhotoPath, 110, 110);
                artist.PhotoSize = "low";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading artist photo: {ex.Message}");
            }
        }


        public async Task LoadArtistPhotoAsync(ArtistDisplayModel artist, string size = "low")
        {
            try
            {
                int photoSize = size == "high" ? 240 : 120; // Double size for high-res
                artist.Photo = await _imageCacheService.LoadThumbnailAsync(
                    artist.PhotoPath,
                    photoSize,
                    photoSize);
                artist.PhotoSize = size;
            }
            catch (Exception ex)
            {
                // Log error and possibly load a default image
                Console.WriteLine($"Error loading artist photo: {ex.Message}");
            }
        }

        public async Task LoadHighResArtistPhotoAsync(ArtistDisplayModel artist)
        {
            await LoadArtistPhotoAsync(artist, "high");
        }


    }

}
