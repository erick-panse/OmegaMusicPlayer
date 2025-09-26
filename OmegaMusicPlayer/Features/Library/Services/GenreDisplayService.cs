using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Infrastructure.Data.Repositories;
using OmegaMusicPlayer.Infrastructure.Services.Images;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Enums;

namespace OmegaMusicPlayer.Features.Library.Services
{
    public class GenreDisplayService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly StandardImageService _standardImageService;
        private readonly IErrorHandlingService _errorHandlingService;

        public GenreDisplayService(
            AllTracksRepository allTracksRepository,
            StandardImageService standardImageService,
            IErrorHandlingService errorHandlingService)
        {
            _allTracksRepository = allTracksRepository;
            _standardImageService = standardImageService;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<GenreDisplayModel>> GetAllGenresAsync()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Use pre-filtered genre cache instead of grouping tracks
                    var genres = _allTracksRepository.AllGenres;
                    var allTracks = _allTracksRepository.AllTracks;

                    if (genres == null || !genres.Any() || allTracks == null || !allTracks.Any())
                    {
                        return new List<GenreDisplayModel>();
                    }

                    var genreModels = new List<GenreDisplayModel>();

                    foreach (var genre in genres)
                    {
                        // Get tracks for this genre from the track cache
                        var genreTracks = allTracks
                            .Where(t => t.Genre == genre.GenreName)
                            .ToList();

                        var genreModel = new GenreDisplayModel
                        {
                            Name = genre.GenreName,
                            TrackIDs = genreTracks.Select(t => t.TrackID).ToList(),
                            TotalDuration = TimeSpan.FromTicks(genreTracks.Sum(t => t.Duration.Ticks))
                        };

                        // Get first track's photo for the genre display
                        if (genreTracks.Any() && !string.IsNullOrEmpty(genreTracks.First().CoverPath))
                        {
                            genreModel.PhotoPath = genreTracks.First().CoverPath;
                        }

                        genreModels.Add(genreModel);
                    }

                    return genreModels;
                },
                "Getting all genres",
                new List<GenreDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<TrackDisplayModel>> GetGenreTracksAsync(string genreName)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(genreName))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid genre name provided",
                            "Attempted to get tracks for null or empty genre name",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

                    var allTracks = _allTracksRepository.AllTracks;
                    if (allTracks == null || !allTracks.Any())
                    {
                        return new List<TrackDisplayModel>();
                    }

                    return allTracks
                        .Where(t => t.Genre == genreName)
                        .ToList();
                },
                $"Getting tracks for genre '{genreName}'",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<GenreDisplayModel> GetGenreByNameAsync(string genreName)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(genreName))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid genre name provided",
                            "Attempted to get genre with null or empty name",
                            null,
                            false);
                        return null;
                    }

                    var allTracks = _allTracksRepository.AllTracks;
                    if (allTracks == null || !allTracks.Any())
                    {
                        return null;
                    }

                    // Get all tracks for this genre
                    var tracks = allTracks
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
                },
                $"Getting genre with name '{genreName}'",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task LoadGenrePhotoAsync(GenreDisplayModel genre, string size = "low", bool isVisible = false)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genre == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Null genre provided",
                            "Attempted to load photo for a null genre object",
                            null,
                            false);
                        return;
                    }

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
                },
                $"Loading genre photo for '{genre?.Name ?? "Unknown"}' (quality: {size})",
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Loads genre photo asynchronously only if it's visible (optimized version)
        /// </summary>
        public async Task LoadGenrePhotoIfVisibleAsync(GenreDisplayModel genre, bool isVisible, string size = "low")
        {
            // Only load if the genre is actually visible
            if (!isVisible)
            {
                // Still notify the service about the visibility state for cache management
                if (!string.IsNullOrEmpty(genre?.PhotoPath))
                {
                    await _standardImageService.NotifyImageVisible(genre.PhotoPath, false);
                }
                return;
            }

            await LoadGenrePhotoAsync(genre, size, isVisible);
        }
    }
}