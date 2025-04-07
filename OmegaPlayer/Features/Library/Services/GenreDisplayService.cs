using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Services.Images;

namespace OmegaPlayer.Features.Library.Services
{
    public class GenreDisplayService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly StandardImageService _standardImageService;

        public GenreDisplayService(
            AllTracksRepository allTracksRepository,
            StandardImageService standardImageService)
        {
            _allTracksRepository = allTracksRepository;
            _standardImageService = standardImageService;
        }

        public async Task<List<GenreDisplayModel>> GetAllGenresAsync()
        {
            var allTracks = _allTracksRepository.AllTracks;

            // Group tracks by genre
            var genreGroups = allTracks
                .Where(t => !string.IsNullOrEmpty(t.Genre))
                .GroupBy(t => t.Genre);

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

        public async Task LoadGenrePhotoAsync(GenreDisplayModel genre, string size = "low", bool isVisible = false)
        {
            try
            {
                if (string.IsNullOrEmpty(genre.PhotoPath)) return;

                switch (size.ToLower())
                {
                    case "low":
                        genre.Photo = await _standardImageService.LoadLowQualityAsync(genre.PhotoPath, isVisible);
                        break;
                    case "medium":
                        genre.Photo = await _standardImageService.LoadMediumQualityAsync(genre.PhotoPath, isVisible);
                        break;
                    case "high":
                        genre.Photo = await _standardImageService.LoadHighQualityAsync(genre.PhotoPath, isVisible);
                        break;
                    case "detail":
                        genre.Photo = await _standardImageService.LoadDetailQualityAsync(genre.PhotoPath, isVisible);
                        break;
                    default:
                        genre.Photo = await _standardImageService.LoadLowQualityAsync(genre.PhotoPath, isVisible);
                        break;
                }

                genre.PhotoSize = size;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading genre photo: {ex.Message}");
            }
        }

    }
}