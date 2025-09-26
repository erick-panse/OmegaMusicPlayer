using System.Collections.Generic;
using System;
using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using OmegaMusicPlayer.Features.Playback.Models;
using OmegaMusicPlayer.Infrastructure.Services.Images;
using OmegaMusicPlayer.Infrastructure.Data.Repositories;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Enums;
using CommunityToolkit.Mvvm.Messaging;

namespace OmegaMusicPlayer.Features.Library.Services
{
    public class TrackLikeUpdateMessage
    {
        public int TrackId { get; }
        public bool IsLiked { get; }

        public TrackLikeUpdateMessage(int trackId, bool isLiked)
        {
            TrackId = trackId;
            IsLiked = isLiked;
        }
    }

    public class TrackDisplayService
    {
        private readonly StandardImageService _standardImageService;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly TrackMetadataService _trackMetadataService;
        private readonly MediaService _mediaService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        public List<TrackDisplayModel> AllTracks { get; set; }

        public TrackDisplayService(
            StandardImageService standardImageService,
            AllTracksRepository allTracksRepository,
            TrackMetadataService trackMetadataService,
            MediaService mediaService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _standardImageService = standardImageService;
            _allTracksRepository = allTracksRepository;
            _trackMetadataService = trackMetadataService;
            _mediaService = mediaService;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;
        }

        private async Task<Bitmap> ExtractAndSaveCover(TrackDisplayModel track, bool isVisible)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Null track provided",
                            "Attempted to extract cover for a null track object",
                            null,
                            false);
                        return null;
                    }

                    if (string.IsNullOrEmpty(track.FilePath) || !File.Exists(track.FilePath))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid track file path",
                            $"The file does not exist at path: {track.FilePath}",
                            null,
                            false);
                        return null;
                    }

                    var file = TagLib.File.Create(track.FilePath);

                    if (file.Tag.Pictures.Length > 0)
                    {
                        // IMPORTANT: Use the existing Media record instead of creating a new one
                        var media = new Media
                        {
                            MediaID = track.CoverID,  // Use the existing CoverID
                            MediaType = "track_cover",
                            CoverPath = null  // Will be updated after saving the image
                        };

                        // Save the image using the existing MediaID
                        using (var ms = new MemoryStream(file.Tag.Pictures.First().Data.Data))
                        {
                            var imageFilePath = await _trackMetadataService.SaveImage(ms, "track_cover", track.CoverID);
                            media.CoverPath = imageFilePath;

                            // Update the existing Media record with the new path
                            await _mediaService.UpdateMediaFilePath(track.CoverID, imageFilePath);

                            // Update the track's cover path
                            track.CoverPath = imageFilePath;

                            try
                            {
                                // Use the isVisible parameter properly for the StandardImageService
                                var thumbnail = await _standardImageService.LoadLowQualityAsync(imageFilePath, isVisible);
                                return thumbnail;
                            }
                            catch (Exception ex)
                            {
                                _errorHandlingService.LogError(
                                    ErrorSeverity.NonCritical,
                                    "Failed to load new thumbnail",
                                    ex.Message,
                                    ex,
                                    false);
                            }
                        }
                    }

                    return null;
                },
                $"Extracting and saving cover for track '{track?.Title ?? "Unknown"}'",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task LoadTrackCoverAsync(TrackDisplayModel track, string size = "low", bool isVisible = false, bool isTopPriority = false)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Null track provided",
                            "Attempted to load cover for a null track object",
                            null,
                            false);
                        return;
                    }

                    // If no cover path exists or file doesn't exist, try to extract from metadata
                    if (string.IsNullOrEmpty(track.CoverPath) || !File.Exists(track.CoverPath))
                    {
                        track.Thumbnail = await ExtractAndSaveCover(track, isVisible);
                        if (track.Thumbnail != null)
                        {
                            track.ThumbnailSize = size;
                        }
                        return;
                    }

                    // Load the appropriate quality based on the size parameter and visibility
                    switch (size.ToLower())
                    {
                        case "low":
                            track.Thumbnail = await _standardImageService.LoadLowQualityAsync(track.CoverPath, isVisible, isTopPriority);
                            break;
                        case "medium":
                            track.Thumbnail = await _standardImageService.LoadMediumQualityAsync(track.CoverPath, isVisible, isTopPriority);
                            break;
                        case "high":
                            track.Thumbnail = await _standardImageService.LoadHighQualityAsync(track.CoverPath, isVisible, isTopPriority);
                            break;
                        case "detail":
                            track.Thumbnail = await _standardImageService.LoadDetailQualityAsync(track.CoverPath, isVisible, isTopPriority);
                            break;
                        default:
                            track.Thumbnail = await _standardImageService.LoadLowQualityAsync(track.CoverPath, isVisible, isTopPriority);
                            break;
                    }

                    // Update the thumbnail size if loading was successful
                    if (track.Thumbnail != null)
                    {
                        track.ThumbnailSize = size;
                    }
                },
                $"Loading thumbnail for track '{track?.Title ?? "Unknown"}' (quality: {size}, visible: {isVisible})",
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Loads track cover asynchronously only if it's visible (optimized version)
        /// </summary>
        public async Task LoadTrackCoverIfVisibleAsync(TrackDisplayModel track, bool isVisible, string size = "low")
        {
            // Only load if the track is actually visible
            if (!isVisible)
            {
                // Still notify the service about the visibility state for cache management
                if (!string.IsNullOrEmpty(track?.CoverPath))
                {
                    await _standardImageService.NotifyImageVisible(track.CoverPath, false);
                }
                return;
            }

            await LoadTrackCoverAsync(track, size, isVisible);
        }

        /// <summary>
        /// Gets a List QueueTracks and returns a List of TrackDisplayModel preserving the TrackOrder in NowPlayingPosition and repeated tracks
        /// </summary>
        public async Task<List<TrackDisplayModel>> GetTrackDisplaysFromQueue(List<QueueTracks> queueTracks)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (queueTracks == null || !queueTracks.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Empty queue tracks list provided",
                            "Attempted to get track displays from an empty or null queue tracks list",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

                    // Retrieve all tracks for the profile from the repository
                    if (_allTracksRepository.AllTracks.Count <= 0)
                    {
                        await _allTracksRepository.LoadTracks();
                        if (_allTracksRepository.AllTracks == null)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Failed to load tracks from repository",
                                "All tracks collection is null after loading attempt",
                                null,
                                false);
                            return new List<TrackDisplayModel>();
                        }
                    }

                    var allTracks = _allTracksRepository.AllTracks;

                    // Create a lookup dictionary for tracks by ID for efficient access
                    // but only for finding the original track data
                    var trackLookup = allTracks
                        .Where(t => t != null)
                        .ToDictionary(t => t.TrackID, t => t);

                    // Create a result list that preserves order and duplicates
                    var resultTracks = new List<TrackDisplayModel>();

                    // Process each queue track in the original order
                    for (int position = 0; position < queueTracks.Count; position++)
                    {
                        var queueTrack = queueTracks[position];
                        if (trackLookup.TryGetValue(queueTrack.TrackID, out var trackModel))
                        {
                            // Create a new instance for each occurrence to prevent shared state issues
                            var trackCopy = new TrackDisplayModel();

                            // Generate a new unique InstanceId for this track instance
                            trackCopy.InstanceId = Guid.NewGuid();

                            // Copy all properties from the original track
                            trackCopy.TrackID = trackModel.TrackID;
                            trackCopy.Title = trackModel.Title;
                            trackCopy.AlbumID = trackModel.AlbumID;
                            trackCopy.AlbumTitle = trackModel.AlbumTitle;
                            trackCopy.Duration = trackModel.Duration;
                            trackCopy.FilePath = trackModel.FilePath;
                            trackCopy.Lyrics = trackModel.Lyrics;
                            trackCopy.CoverPath = trackModel.CoverPath;
                            trackCopy.CoverID = trackModel.CoverID;
                            trackCopy.Genre = trackModel.Genre;
                            trackCopy.ReleaseDate = trackModel.ReleaseDate;
                            trackCopy.PlayCount = trackModel.PlayCount;
                            trackCopy.BitRate = trackModel.BitRate;
                            trackCopy.FileType = trackModel.FileType;
                            trackCopy.Thumbnail = trackModel.Thumbnail;
                            trackCopy.ThumbnailSize = trackModel.ThumbnailSize;
                            trackCopy.IsLiked = trackModel.IsLiked;
                            trackCopy.FileCreatedDate = trackModel.FileCreatedDate;
                            trackCopy.FileModifiedDate = trackModel.FileModifiedDate;

                            // Set the queue position based on the current position since its already ordered by the caller
                            trackCopy.NowPlayingPosition = position;

                            // Copy the Artists list if it exists
                            if (trackModel.Artists != null)
                            {
                                trackCopy.Artists = new List<Artists>(trackModel.Artists);
                            }
                            else
                            {
                                trackCopy.Artists = new List<Artists>();
                            }

                            resultTracks.Add(trackCopy);
                        }
                    }

                    return resultTracks;
                },
                "Getting track displays from queue",
                new List<TrackDisplayModel>(),
                ErrorSeverity.Playback,
                false
            );
        }

        /// <summary>
        /// Preloads covers for a batch of tracks based on their visibility
        /// </summary>
        public async Task PreloadTrackCoversAsync(IEnumerable<TrackDisplayModel> tracks, string quality = "low")
        {
            if (tracks == null) return;

            var tasks = tracks.Select(track => LoadTrackCoverIfVisibleAsync(track, true, quality));

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error during batch track cover preloading",
                    ex.Message,
                    ex,
                    false);
            }
        }
    }
}