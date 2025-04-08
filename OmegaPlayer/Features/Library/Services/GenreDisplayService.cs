using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Features.Library.Services
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
                    var allTracks = _allTracksRepository.AllTracks;
                    if (allTracks == null || !allTracks.Any())
                    {
                        return new List<GenreDisplayModel>();
                    }

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
    }
}