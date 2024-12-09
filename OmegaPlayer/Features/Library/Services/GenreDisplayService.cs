using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Services.Cache;

namespace OmegaPlayer.Features.Library.Services
{
    public class GenreDisplayService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly ImageCacheService _imageCacheService;

        public GenreDisplayService(
            AllTracksRepository allTracksRepository,
            ImageCacheService imageCacheService)
        {
            _allTracksRepository = allTracksRepository;
            _imageCacheService = imageCacheService;
        }

        public async Task<List<GenreDisplayModel>> GetGenresPageAsync(int page, int pageSize)
        {
            var allTracks = _allTracksRepository.AllTracks;

            // Group tracks by genre
            var genreGroups = allTracks
                .Where(t => !string.IsNullOrEmpty(t.Genre))
                .GroupBy(t => t.Genre)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            var genres = new List<GenreDisplayModel>();

            foreach (var group in genreGroups)
            {
                var genreTracks = group.ToList();

                var genreModel = new GenreDisplayModel
                {
                    Name = group.Key,
                    TrackIDs = genreTracks.Select(t => t.TrackID).ToList(),
                    TotalDuration = TimeSpan.FromTicks(genreTracks.Sum(t => t.Duration.Ticks))
                };

                genres.Add(genreModel);

                // Load low-res photo initially
                await LoadGenrePhotoAsync(genreModel, "low");
            }

            return genres;
        }

        public async Task<List<TrackDisplayModel>> GetGenreTracksAsync(string genreName)
        {
            return _allTracksRepository.AllTracks
                .Where(t => t.Genre == genreName)
                .ToList();
        }

        public async Task LoadGenrePhotoAsync(GenreDisplayModel genre)
        {
            try
            {
                if (string.IsNullOrEmpty(genre.PhotoPath)) return;

                genre.Photo = await _imageCacheService.LoadThumbnailAsync(genre.PhotoPath, 110, 110);
                genre.PhotoSize = "low";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading genre photo: {ex.Message}");
            }
        }
        public async Task<GenreDisplayModel> GetGenreByNameAsync(string genreName)
        {
            // Get all tracks for this genre
            var tracks = _allTracksRepository.AllTracks
                .Where(t => t.Genre == genreName)
                .ToList();

            if (!tracks.Any()) return null;

            var displayModel = new GenreDisplayModel
            {
                Name = genreName,
                TrackIDs = tracks.Select(t => t.TrackID).ToList(),
                TotalDuration = TimeSpan.FromTicks(tracks.Sum(t => t.Duration.Ticks))
            };

            // Get first track's photo for the genre display
            if (tracks.Any() && !string.IsNullOrEmpty(tracks.First().CoverPath))
            {
                displayModel.PhotoPath = tracks.First().CoverPath;
                await LoadGenrePhotoAsync(displayModel);
            }

            return displayModel;
        }

        public async Task LoadGenrePhotoAsync(GenreDisplayModel genre, string size = "low")
        {
            try
            {
                int photoSize = size == "high" ? 240 : 120;
                genre.Photo = await _imageCacheService.LoadThumbnailAsync(
                    genre.PhotoPath,
                    photoSize,
                    photoSize);
                genre.PhotoSize = size;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading genre photo: {ex.Message}");
            }
        }

        public async Task LoadHighResGenrePhotoAsync(GenreDisplayModel genre)
        {
            await LoadGenrePhotoAsync(genre, "high");
        }
    }
}