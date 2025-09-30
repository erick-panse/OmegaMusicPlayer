using Microsoft.EntityFrameworkCore;
using OmegaMusicPlayer.Core;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using File = System.IO.File;
using Genres = OmegaMusicPlayer.Features.Library.Models.Genres;

namespace OmegaMusicPlayer.Features.Library.Services
{
    public class TrackMetadataService
    {
        private readonly IDbContextFactory<OmegaMusicPlayerDbContext> _contextFactory;
        private readonly TracksService _trackService;
        private readonly ArtistsService _artistService;
        private readonly AlbumService _albumService;
        private readonly GenresService _genreService;
        private readonly MediaService _mediaService;
        private readonly TrackArtistService _trackArtistService;
        private readonly TrackGenreService _trackGenreService;
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackMetadataService(
            IDbContextFactory<OmegaMusicPlayerDbContext> contextFactory,
            TracksService trackService,
            ArtistsService artistService,
            AlbumService albumService,
            GenresService genreService,
            MediaService mediaService,
            TrackArtistService trackArtistService,
            TrackGenreService trackGenreService,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _trackService = trackService;
            _artistService = artistService;
            _albumService = albumService;
            _genreService = genreService;
            _mediaService = mediaService;
            _trackArtistService = trackArtistService;
            _trackGenreService = trackGenreService;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<bool> PopulateTrackMetadata(string filePath)
        {
            var result = await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException("The specified track file does not exist.", filePath);
                    }

                    var file = TagLib.File.Create(filePath);
                    if (file == null)
                    {
                        throw new InvalidOperationException($"Failed to extract metadata from file: {filePath}");
                    }

                    // Get file system dates
                    var fileInfo = new FileInfo(filePath);

                    // Execute all database operations in a transaction
                    var success = await ExecuteInTransactionAsync(async () =>
                    {
                        // Check if track already exists
                        Tracks track = await _trackService.GetTrackByPath(filePath) ?? new Tracks();

                        var artistsIds = await ProcessArtistsAsync(file.Tag.Performers);

                        var firstArtistId = artistsIds.Count > 0 ? artistsIds[0] : 0;
                        var albumId = await ProcessAlbumAsync(file.Tag.Album, firstArtistId);

                        var genreId = await ProcessGenreAsync(file.Tag.Genres);

                        // Handle Cover for both track and album
                        if (track.CoverID == 0)
                        {
                            var media = await SaveMedia(file, filePath, "track_cover");
                            if (media != null)
                            {
                                track.CoverID = media.MediaID;

                                // Update album cover if album exists and doesn't have cover
                                if (albumId > 0)
                                {
                                    var album = await _albumService.GetAlbumById(albumId);
                                    if (album != null && album.CoverID == 0)
                                    {
                                        album.CoverID = media.MediaID;
                                        await _albumService.UpdateAlbum(album);
                                    }
                                }
                            }
                        }

                        // Handle Track
                        if (track.TrackID == 0)
                        {
                            track.FilePath = filePath;
                            track.Title = string.IsNullOrEmpty(file.Tag.Title) ?
                                Path.GetFileNameWithoutExtension(filePath) : file.Tag.Title;
                            track.Duration = file.Properties.Duration;
                            track.BitRate = file.Properties.AudioBitrate;
                            track.FileSize = (int)new FileInfo(filePath).Length;
                            track.Lyrics = file.Tag.Lyrics;
                            track.FileType = Path.GetExtension(filePath)?.TrimStart('.');
                            track.CreatedAt = fileInfo.CreationTimeUtc;
                            track.UpdatedAt = fileInfo.LastWriteTimeUtc;
                            track.PlayCount = 0;
                            track.AlbumID = albumId;
                            track.GenreID = genreId;
                            track.TrackID = await _trackService.AddTrack(track);
                        }

                        // Link Track to Artists
                        await LinkTrackToArtistsAsync(track.TrackID, artistsIds);

                        // Link Track to Genre
                        await LinkTrackToGenreAsync(track.TrackID, genreId);

                        // Try to extract and save artist images from metadata
                        try
                        {
                            foreach (var artistId in artistsIds.Where(id => id > 0))
                            {
                                // Check if artist already has a photo
                                var artist = await _artistService.GetArtistById(artistId);
                                if (artist != null && artist.PhotoID == 0)
                                {
                                    // Try to extract artist image from file metadata
                                    var artistMedia = await SaveArtistMedia(file, filePath, artistId);
                                    if (artistMedia != null)
                                    {
                                        // Update artist with new photo
                                        artist.PhotoID = artistMedia.MediaID;
                                        artist.UpdatedAt = DateTime.UtcNow;
                                        await _artistService.UpdateArtist(artist);

                                        _errorHandlingService.LogInfo(
                                            "Artist photo extracted",
                                            $"Found and saved artist photo for {artist.ArtistName} from {Path.GetFileName(filePath)}",
                                            false);
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error extracting artist images",
                                $"Failed to extract artist images from {Path.GetFileName(filePath)}",
                                ex,
                                false);
                        }
                    });

                    return success;
                },
                $"Populating metadata for {Path.GetFileName(filePath)}",
                false,
                ErrorSeverity.NonCritical,
                false);

            return result;
        }

        public async Task<bool> UpdateTrackMetadata(string filePath)
        {
            var result = await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (!File.Exists(filePath))
                    {
                        throw new FileNotFoundException("The specified track file does not exist.", filePath);
                    }

                    // Get existing track information
                    var existingTrack = await _trackService.GetTrackByPath(filePath);
                    if (existingTrack == null)
                    {
                        // If track doesn't exist, call PopulateTrackMetadata instead
                        return await PopulateTrackMetadata(filePath);
                    }

                    // Get current file system dates
                    var fileInfo = new FileInfo(filePath);

                    // Read the file's metadata
                    var file = await _errorHandlingService.SafeExecuteAsync(
                        async () => TagLib.File.Create(filePath),
                        $"Reading updated metadata for file {Path.GetFileName(filePath)}",
                        null,
                        ErrorSeverity.NonCritical,
                        false);

                    if (file == null)
                    {
                        throw new InvalidOperationException($"Failed to extract metadata from file: {filePath}");
                    }

                    // Update track properties
                    existingTrack.Title = string.IsNullOrEmpty(file.Tag.Title) ?
                        Path.GetFileNameWithoutExtension(filePath) : file.Tag.Title;
                    existingTrack.Duration = file.Properties.Duration;
                    existingTrack.BitRate = file.Properties.AudioBitrate;
                    existingTrack.FileSize = (int)new FileInfo(filePath).Length;
                    existingTrack.Lyrics = file.Tag.Lyrics;
                    existingTrack.UpdatedAt = fileInfo.LastWriteTimeUtc;

                    // Process artists and update associations
                    var artistIds = await ProcessArtistsAsync(file.Tag.Performers);
                    await LinkTrackToArtistsAsync(existingTrack.TrackID, artistIds, true);

                    // Process genre and update association
                    var genreId = await ProcessGenreAsync(file.Tag.Genres);
                    existingTrack.GenreID = genreId;
                    await LinkTrackToGenreAsync(existingTrack.TrackID, genreId, true);

                    // Update album information if needed
                    var firstArtistId = artistIds.Count > 0 ? artistIds.First() : 0;
                    var albumId = await ProcessAlbumAsync(file.Tag.Album, firstArtistId);
                    existingTrack.AlbumID = albumId;

                    // Handle Cover for both track and album
                    if (file.Tag.Pictures != null && file.Tag.Pictures.Length > 0)
                    {
                        var media = await SaveMedia(file, filePath, "track_cover");
                        if (media != null)
                        {
                            existingTrack.CoverID = media.MediaID;

                            // Update album cover if album exists
                            if (albumId > 0)
                            {
                                var album = await _albumService.GetAlbumById(albumId);
                                if (album != null)
                                {
                                    album.CoverID = media.MediaID;
                                    await _albumService.UpdateAlbum(album);
                                }
                            }
                        }
                    }
                    
                    // Try to extract and save artist images from metadata
                    try
                    {
                        foreach (var artistId in artistIds.Where(id => id > 0))
                        {
                            // Check if artist already has a photo
                            var artist = await _artistService.GetArtistById(artistId);
                            if (artist != null && artist.PhotoID == 0)
                            {
                                // Try to extract artist image from file metadata
                                var artistMedia = await SaveArtistMedia(file, filePath, artistId);
                                if (artistMedia != null)
                                {
                                    // Update artist with new photo
                                    artist.PhotoID = artistMedia.MediaID;
                                    artist.UpdatedAt = DateTime.UtcNow;
                                    await _artistService.UpdateArtist(artist);

                                    _errorHandlingService.LogInfo(
                                        "Artist photo extracted",
                                        $"Found and saved artist photo for {artist.ArtistName} from {Path.GetFileName(filePath)}",
                                        false);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Error extracting artist images",
                            $"Failed to extract artist images from {Path.GetFileName(filePath)}",
                            ex,
                            false);
                    }

                    // Save updated track information
                    await _trackService.UpdateTrack(existingTrack);

                    return true;
                },
                $"Updating metadata for {Path.GetFileName(filePath)}",
                false,
                ErrorSeverity.NonCritical,
                false);

            return result;
        }

        private async Task<List<int>> ProcessArtistsAsync(string[] artistNames)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var artistIds = new List<int>();

                    // If there are no artists in the tag, add an unknown artist
                    if (artistNames == null || artistNames.Length == 0 || artistNames.All(a => string.IsNullOrWhiteSpace(a)))
                    {
                        // Try to find existing "Unknown Artist" entry
                        var unknownArtist = await _artistService.GetArtistByName("Unknown Artist");
                        if (unknownArtist == null)
                        {
                            // Create Unknown Artist
                            var newUnknownArtist = new Artists
                            {
                                ArtistName = "Unknown Artist",
                                Bio = "Tracks with missing artist information",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            var unknownId = await _artistService.AddArtist(newUnknownArtist);
                            artistIds.Add(unknownId);
                        }
                        else
                        {
                            artistIds.Add(unknownArtist.ArtistID);
                        }
                        return artistIds;
                    }

                    // Process all artists that have non-empty names
                    foreach (var artistName in artistNames.Where(a => !string.IsNullOrWhiteSpace(a)))
                    {
                        var artist = await _artistService.GetArtistByName(artistName);

                        if (artist == null)
                        {
                            // Create new artist
                            var newArtist = new Artists
                            {
                                ArtistName = artistName,
                                Bio = "",
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };
                            var artistId = await _artistService.AddArtist(newArtist);
                            artistIds.Add(artistId);
                        }
                        else
                        {
                            artistIds.Add(artist.ArtistID);
                        }
                    }

                    return artistIds;
                },
                "Processing artist information",
                new List<int> { 0 }, // Default to unknown artist (ID 0) on failure
                ErrorSeverity.NonCritical,
                false);
        }

        private async Task<int> ProcessAlbumAsync(string albumTitle, int firstArtistId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // If no album title, return 0 (no album)
                    if (string.IsNullOrWhiteSpace(albumTitle))
                    {
                        return 0;
                    }

                    // Try to find existing album
                    var album = await _albumService.GetAlbumByTitle(albumTitle, firstArtistId);

                    if (album == null)
                    {
                        // Create new album
                        var newAlbum = new Albums
                        {
                            Title = albumTitle,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow,
                            ArtistID = firstArtistId
                        };
                        return await _albumService.AddAlbum(newAlbum);
                    }

                    return album.AlbumID;
                },
                "Processing album information",
                0, // Default to no album (ID 0) on failure
                ErrorSeverity.NonCritical,
                false);
        }

        private async Task<int> ProcessGenreAsync(string[] genreNames)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Default to "Unknown Genre" if no genres in tag
                    var genreName = "Unknown Genre";
                    if (genreNames != null && genreNames.Length > 0 && !string.IsNullOrWhiteSpace(genreNames[0]))
                    {
                        genreName = genreNames[0];
                    }

                    var genre = await _genreService.GetGenreByName(genreName);

                    if (genre == null)
                    {
                        // Create new genre
                        var newGenre = new Genres { GenreName = genreName };
                        return await _genreService.AddGenre(newGenre);
                    }

                    return genre.GenreID;
                },
                "Processing genre information",
                0, // Default to unknown genre (ID 0) on failure
                ErrorSeverity.NonCritical,
                false);
        }

        private async Task LinkTrackToArtistsAsync(int trackId, List<int> artistIds, bool isEditingTrack = false)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (isEditingTrack)
                    {
                        // First, delete all existing artist relationships for this track
                        await _trackArtistService.DeleteAllTrackArtistsForTrack(trackId);
                    }

                    // Then create new relationships
                    foreach (var artistId in artistIds.Where(id => id > 0))
                    {
                        var newTrackArtist = new TrackArtist
                        {
                            TrackID = trackId,
                            ArtistID = artistId
                        };
                        await _trackArtistService.AddTrackArtist(newTrackArtist);
                    }
                },
                $"Linking track {trackId} to artists",
                ErrorSeverity.NonCritical,
                false);
        }

        private async Task LinkTrackToGenreAsync(int trackId, int genreId, bool isEditingTrack = false)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genreId <= 0) return;

                    if (isEditingTrack)
                    {
                        // First, delete all existing genre relationships for this track
                        await _trackGenreService.DeleteAllTrackGenresForTrack(trackId);
                    }

                    // Then create new relationship
                    var newTrackGenre = new TrackGenre
                    {
                        TrackID = trackId,
                        GenreID = genreId
                    };
                    await _trackGenreService.AddTrackGenre(newTrackGenre);
                },
                $"Linking track {trackId} to genre {genreId}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Media> SaveMedia(TagLib.File file, string filePath, string mediaType)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check if the file has any pictures/artwork
                    if (file.Tag.Pictures == null || file.Tag.Pictures.Length == 0)
                    {
                        return null;
                    }

                    var picture = file.Tag.Pictures.First();
                    if (picture.Data == null || picture.Data.Data == null || picture.Data.Data.Length == 0)
                    {
                        return null;
                    }

                    // Step 1: Insert media without the file path and retrieve the MediaID
                    var media = new Media
                    {
                        CoverPath = null, // Will be set later after saving the image
                        MediaType = mediaType
                    };

                    // Insert into the database and get MediaID
                    int mediaId = await _mediaService.AddMedia(media);
                    media.MediaID = mediaId;

                    try
                    {
                        // Step 2: Save the image with MediaID
                        using (var ms = new MemoryStream(picture.Data.Data))
                        {
                            var imageFilePath = await SaveImage(ms, mediaType, mediaId);
                            media.CoverPath = imageFilePath;

                            // Step 3: Update the Media record with the correct file path
                            await _mediaService.UpdateMediaFilePath(mediaId, media.CoverPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to save image from track metadata",
                            $"Could not save artwork for {Path.GetFileName(filePath)}.",
                            ex,
                            false);
                    }

                    return media;
                },
                $"Saving media from {Path.GetFileName(filePath)}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<string> SaveImage(Stream imageStream, string mediaType, int mediaID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var projectBaseDirectory = AppConfiguration.MediaPath;
                    var baseDirectory = Path.Combine(projectBaseDirectory, mediaType);
                    var subDirectory = Path.Combine(baseDirectory, mediaID.ToString("D7"));

                    // Create directory structure if it doesn't exist
                    Directory.CreateDirectory(subDirectory);

                    string fileName = mediaType switch
                    {
                        "track_cover" => $"track_{mediaID.ToString("D7")}_cover.jpg",
                        "album_cover" => $"album_{mediaID.ToString("D7")}_cover.jpg",
                        "artist_photo" => $"artist_{mediaID.ToString("D7")}_photo.jpg",
                        _ => throw new ArgumentException($"Invalid media type: {mediaType}")
                    };

                    var imagePath = Path.Combine(subDirectory, fileName);

                    // Make sure the directory exists before writing
                    if (!Directory.Exists(Path.GetDirectoryName(imagePath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(imagePath));
                    }

                    // Save the image file
                    using (var fileStream = new FileStream(imagePath, FileMode.Create, FileAccess.Write))
                    {
                        await imageStream.CopyToAsync(fileStream);
                    }

                    return imagePath;
                },
                $"Saving image for media ID {mediaID}",
                null, // No fallback path
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Extracts and saves artist image from music file metadata
        /// </summary>
        public async Task<Media> SaveArtistMedia(TagLib.File file, string filePath, int artistId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check if the file has any pictures/artwork
                    if (file.Tag.Pictures == null || file.Tag.Pictures.Length == 0)
                    {
                        return null;
                    }

                    // Look for artist image (usually secondary images after album cover)
                    var artistPicture = file.Tag.Pictures.FirstOrDefault(p => p.Type == TagLib.PictureType.Artist);

                    // If no specific artist image, try other types that might be artist photos
                    if (artistPicture == null)
                    {
                        artistPicture = file.Tag.Pictures.FirstOrDefault(p =>
                            p.Type == TagLib.PictureType.Band ||
                            p.Type == TagLib.PictureType.LeadArtist ||
                            p.Type == TagLib.PictureType.Other);
                    }

                    // Skip if no suitable image found or if it's the same as album cover
                    if (artistPicture == null || artistPicture.Data == null || artistPicture.Data.Data == null || artistPicture.Data.Data.Length == 0)
                    {
                        return null;
                    }

                    // Step 1: Insert media without the file path and retrieve the MediaID
                    var media = new Media
                    {
                        CoverPath = null, // Will be set later after saving the image
                        MediaType = "artist_photo"
                    };

                    // Insert into the database and get MediaID
                    int mediaId = await _mediaService.AddMedia(media);
                    media.MediaID = mediaId;

                    try
                    {
                        // Step 2: Save the image with MediaID
                        using (var ms = new MemoryStream(artistPicture.Data.Data))
                        {
                            var imageFilePath = await SaveImage(ms, "artist_photo", mediaId);
                            media.CoverPath = imageFilePath;

                            // Step 3: Update the Media record with the correct file path
                            await _mediaService.UpdateMediaFilePath(mediaId, media.CoverPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to save artist image from track metadata",
                            $"Could not save artist artwork for {Path.GetFileName(filePath)}.",
                            ex,
                            false);
                    }

                    return media;
                },
                $"Saving artist media from {Path.GetFileName(filePath)}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Saves an image to artist metadata if the file supports it and doesn't already have an artist image
        /// </summary>
        public async Task<bool> SaveImageToArtistMetadata(string trackFilePath, string imageFilePath, string artistName)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (!File.Exists(trackFilePath) || !File.Exists(imageFilePath))
                    {
                        return false;
                    }

                    var file = TagLib.File.Create(trackFilePath);
                    if (file == null) return false;

                    // Check if there's already an artist image
                    var existingArtistPicture = file.Tag.Pictures?.FirstOrDefault(p =>
                        p.Type == TagLib.PictureType.Artist ||
                        p.Type == TagLib.PictureType.Band ||
                        p.Type == TagLib.PictureType.LeadArtist);

                    if (existingArtistPicture != null)
                    {
                        return false; // Already has artist image
                    }

                    // Read the image file
                    var imageData = await File.ReadAllBytesAsync(imageFilePath);
                    var mimeType = GetMimeTypeFromExtension(Path.GetExtension(imageFilePath));

                    // Create new picture
                    var picture = new TagLib.Picture
                    {
                        Type = TagLib.PictureType.Artist,
                        MimeType = mimeType,
                        Description = $"Artist photo: {artistName}",
                        Data = imageData
                    };

                    // Add to existing pictures (don't replace, just add)
                    List<TagLib.IPicture> pictures = file.Tag.Pictures?.ToList();
                    pictures.Add(picture);
                    file.Tag.Pictures = pictures.ToArray();

                    // Save the file
                    file.Save();
                    file.Dispose();

                    return true;
                },
                $"Saving image to artist metadata for {Path.GetFileName(trackFilePath)}",
                false,
                ErrorSeverity.NonCritical,
                false);
        }

        private string GetMimeTypeFromExtension(string extension)
        {
            return extension.ToLower() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };
        }

        /// <summary>
        /// Executes a database operation within a transaction using EF Core.
        /// </summary>
        private async Task<bool> ExecuteInTransactionAsync(Func<Task> operation)
        {
            using var context = _contextFactory.CreateDbContext();
            using var transaction = await context.Database.BeginTransactionAsync();

            try
            {
                // Execute the operation
                await operation();

                // Commit the transaction
                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Log the error
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Transaction failed",
                    $"Database operation failed: {ex.Message}",
                    ex,
                    true);

                // Rollback the transaction
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Rollback failed",
                        rollbackEx.Message,
                        rollbackEx,
                        false);
                }

                return false;
            }
        }
    }
}