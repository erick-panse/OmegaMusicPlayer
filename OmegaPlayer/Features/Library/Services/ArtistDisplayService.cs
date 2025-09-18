using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Library.Services
{
    public class ArtistDisplayService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly StandardImageService _standardImageService;
        private readonly ArtistsRepository _artistsRepository;
        private readonly MediaService _mediaService;
        private readonly IErrorHandlingService _errorHandlingService;

        public ArtistDisplayService(
            AllTracksRepository allTracksRepository,
            StandardImageService standardImageService,
            ArtistsRepository artistsRepository,
            MediaService mediaService,
            IErrorHandlingService errorHandlingService)
        {
            _allTracksRepository = allTracksRepository;
            _standardImageService = standardImageService;
            _artistsRepository = artistsRepository;
            _mediaService = mediaService;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<ArtistDisplayModel>> GetAllArtistsAsync()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Use pre-filtered artist cache instead of grouping tracks
                    var artists = _allTracksRepository.AllArtists;
                    var allTracks = _allTracksRepository.AllTracks;

                    if (artists == null || !artists.Any() || allTracks == null || !allTracks.Any())
                    {
                        return new List<ArtistDisplayModel>();
                    }

                    var artistModels = new List<ArtistDisplayModel>();

                    foreach (var artist in artists)
                    {
                        // Get tracks for this artist from the track cache
                        var artistTracks = allTracks
                            .Where(t => t.Artists?.Any(a => a.ArtistID == artist.ArtistID) == true)
                            .ToList();

                        var artistModel = new ArtistDisplayModel
                        {
                            ArtistID = artist.ArtistID,
                            Name = artist.ArtistName,
                            Bio = artist.Bio,
                            TrackIDs = artistTracks.Select(t => t.TrackID).ToList(),
                            TotalDuration = TimeSpan.FromTicks(artistTracks.Sum(t => t.Duration.Ticks))
                        };

                        var media = await _mediaService.GetMediaById(artist.PhotoID);
                        if (media != null)
                        {
                            artistModel.PhotoPath = media.CoverPath;
                        }

                        artistModels.Add(artistModel);
                    }

                    return artistModels;
                },
                "Getting all artists",
                new List<ArtistDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<TrackDisplayModel>> GetArtistTracksAsync(int artistId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artistId <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid artist ID provided",
                            $"Attempted to get tracks for invalid artist ID: {artistId}",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

                    var allTracks = _allTracksRepository.AllTracks;
                    if (allTracks == null || !allTracks.Any())
                    {
                        return new List<TrackDisplayModel>();
                    }

                    // Get all tracks for this artist from AllTracksRepository
                    return allTracks
                        .Where(t => t.Artists.Any(a => a.ArtistID == artistId))
                        .ToList();
                },
                $"Getting tracks for artist {artistId}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<ArtistDisplayModel> GetArtistByIdAsync(int artistId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artistId <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid artist ID provided",
                            $"Attempted to get artist with invalid ID: {artistId}",
                            null,
                            false);
                        return null;
                    }

                    var artist = await _artistsRepository.GetArtistById(artistId);
                    if (artist == null) return null;

                    // Create and populate the display model
                    var artistModel = new ArtistDisplayModel
                    {
                        ArtistID = artist.ArtistID,
                        Name = artist.ArtistName,
                        Bio = artist.Bio
                    };

                    var media = await _mediaService.GetMediaById(artist.PhotoID);
                    if (media != null)
                    {
                        artistModel.PhotoPath = media.CoverPath;
                        await LoadArtistPhotoAsync(artistModel);
                    }

                    // Get all tracks for this artist from AllTracksRepository
                    var tracks = await GetArtistTracksAsync(artist.ArtistID);
                    artistModel.TrackIDs = tracks.Select(t => t.TrackID).ToList();
                    artistModel.TotalDuration = TimeSpan.FromTicks(tracks.Sum(t => t.Duration.Ticks));

                    return artistModel;
                },
                $"Getting artist with ID {artistId}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task LoadArtistPhotoAsync(ArtistDisplayModel artist, string size = "low", bool isVisible = false)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Null artist provided",
                            "Attempted to load photo for a null artist object",
                            null,
                            false);
                        return;
                    }

                    if (string.IsNullOrEmpty(artist.PhotoPath)) return;

                    switch (size.ToLower())
                    {
                        case "low":
                            artist.Photo = await _standardImageService.LoadLowQualityAsync(artist.PhotoPath, isVisible);
                            break;
                        case "medium":
                            artist.Photo = await _standardImageService.LoadMediumQualityAsync(artist.PhotoPath, isVisible);
                            break;
                        case "high":
                            artist.Photo = await _standardImageService.LoadHighQualityAsync(artist.PhotoPath, isVisible);
                            break;
                        case "detail":
                            artist.Photo = await _standardImageService.LoadDetailQualityAsync(artist.PhotoPath, isVisible);
                            break;
                        default:
                            artist.Photo = await _standardImageService.LoadLowQualityAsync(artist.PhotoPath, isVisible);
                            break;
                    }

                    artist.PhotoSize = size;
                },
                $"Loading artist photo for '{artist?.Name ?? "Unknown"}' (quality: {size})",
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}