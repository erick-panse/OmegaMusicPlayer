using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Infrastructure.Data.Repositories.Profile;
using OmegaPlayer.Features.Library.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Services.Images;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;

namespace OmegaPlayer.Features.Profile.Services
{
    public class ProfileService
    {
        private readonly ProfileRepository _profileRepository;
        private readonly ProfileConfigRepository _profileConfigRepository;
        private readonly StandardImageService _standardImageService;
        private readonly MediaService _mediaService;
        private readonly IErrorHandlingService _errorHandlingService;
        private const string PROFILE_PHOTO_DIR = "profile_photo";

        public ProfileService(
            ProfileRepository profileRepository,
            ProfileConfigRepository profileConfigRepository,
            StandardImageService standardImageService,
            MediaService mediaService,
            IErrorHandlingService errorHandlingService)
        {
            _profileRepository = profileRepository;
            _profileConfigRepository = profileConfigRepository;
            _standardImageService = standardImageService;
            _mediaService = mediaService;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Profiles> GetProfileById(int profileID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (profileID <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid profile ID",
                            $"Attempted to get profile with invalid ID: {profileID}",
                            null,
                            false);
                        return null;
                    }

                    return await _profileRepository.GetProfileById(profileID);
                },
                $"Getting profile with ID {profileID}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<Profiles>> GetAllProfiles()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () => await _profileRepository.GetAllProfiles(),
                "Getting all profiles",
                new List<Profiles>(), // Return empty list if operation fails
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<int> AddProfile(Profiles profile, Stream photoStream = null)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (profile == null)
                    {
                        throw new ArgumentNullException(nameof(profile), "Cannot add a null profile");
                    }

                    if (string.IsNullOrWhiteSpace(profile.ProfileName))
                    {
                        profile.ProfileName = "Unnamed Profile";
                    }

                    // Process photo if provided
                    if (photoStream != null)
                    {
                        var media = new Media { MediaType = PROFILE_PHOTO_DIR };
                        var mediaId = await _mediaService.AddMedia(media);

                        var photoPath = await SaveProfilePhoto(photoStream, mediaId);
                        await _mediaService.UpdateMediaFilePath(mediaId, photoPath);
                        profile.PhotoID = mediaId;
                    }

                    // Ensure creation dates are set
                    if (profile.CreatedAt == default)
                    {
                        profile.CreatedAt = DateTime.Now;
                    }
                    if (profile.UpdatedAt == default)
                    {
                        profile.UpdatedAt = DateTime.Now;
                    }

                    var profileId = await _profileRepository.AddProfile(profile);

                    // Create associated ProfileConfig
                    await _profileConfigRepository.CreateProfileConfig(profileId);

                    return profileId;
                },
                $"Adding profile '{profile?.ProfileName ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                true // Show notification for user-initiated action
            );
        }

        public async Task UpdateProfile(Profiles profile, Stream photoStream = null)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (profile == null)
                    {
                        throw new ArgumentNullException(nameof(profile), "Cannot update a null profile");
                    }

                    if (profile.ProfileID <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(profile));
                    }

                    if (string.IsNullOrWhiteSpace(profile.ProfileName))
                    {
                        profile.ProfileName = "Unnamed Profile";
                    }

                    // Update photo if a new one is provided
                    if (photoStream != null)
                    {
                        // Clean up old photo if it exists
                        if (profile.PhotoID > 0)
                        {
                            var oldMedia = await _mediaService.GetMediaById(profile.PhotoID);
                            if (oldMedia != null && !string.IsNullOrEmpty(oldMedia.CoverPath) && File.Exists(oldMedia.CoverPath))
                            {
                                File.Delete(oldMedia.CoverPath);
                            }
                        }

                        // Create and save new photo
                        var media = new Media { MediaType = PROFILE_PHOTO_DIR };
                        var mediaId = await _mediaService.AddMedia(media);

                        var photoPath = await SaveProfilePhoto(photoStream, mediaId);
                        await _mediaService.UpdateMediaFilePath(mediaId, photoPath);
                        profile.PhotoID = mediaId;
                    }

                    // Update timestamp
                    profile.UpdatedAt = DateTime.Now;

                    await _profileRepository.UpdateProfile(profile);
                },
                $"Updating profile '{profile?.ProfileName ?? "Unknown"}' (ID: {profile?.ProfileID ?? 0})",
                ErrorSeverity.NonCritical,
                true // Show notification for user-initiated action
            );
        }

        public async Task DeleteProfile(int profileID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (profileID <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(profileID));
                    }

                    // Get profile to access photo ID
                    var profile = await GetProfileById(profileID);
                    if (profile == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Profile not found",
                            $"Attempted to delete non-existent profile with ID: {profileID}",
                            null,
                            false);
                        return;
                    }

                    // Clean up photo if it exists
                    if (profile.PhotoID > 0)
                    {
                        var media = await _mediaService.GetMediaById(profile.PhotoID);
                        if (media != null && !string.IsNullOrEmpty(media.CoverPath) && File.Exists(media.CoverPath))
                        {
                            File.Delete(media.CoverPath);
                        }
                        await _mediaService.DeleteMedia(profile.PhotoID);
                    }

                    await _profileRepository.DeleteProfile(profileID);
                },
                $"Deleting profile with ID {profileID}",
                ErrorSeverity.NonCritical,
                true // Show notification for user-initiated action
            );
        }

        private async Task<string> SaveProfilePhoto(Stream photoStream, int mediaId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (photoStream == null)
                    {
                        throw new ArgumentNullException(nameof(photoStream), "Cannot save a null photo stream");
                    }

                    if (mediaId <= 0)
                    {
                        throw new ArgumentException("Invalid media ID", nameof(mediaId));
                    }

                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var photoDir = Path.Combine(baseDir, "media", PROFILE_PHOTO_DIR, mediaId.ToString("D7"));
                    Directory.CreateDirectory(photoDir);

                    var filePath = Path.Combine(photoDir, $"profile_{mediaId.ToString("D7")}_photo.jpg");

                    using (var fileStream = File.Create(filePath))
                    {
                        await photoStream.CopyToAsync(fileStream);
                    }

                    return filePath;
                },
                $"Saving profile photo for media ID {mediaId}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<Media> GetMediaByProfileId(int photoId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (photoId <= 0)
                    {
                        return null;
                    }

                    return await _mediaService.GetMediaById(photoId);
                },
                $"Getting media for profile photo ID {photoId}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<Bitmap> LoadProfilePhotoAsync(int photoId, string size = "low", bool isVisible = false)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (photoId <= 0)
                    {
                        return null;
                    }

                    var media = await _mediaService.GetMediaById(photoId);
                    if (media == null || string.IsNullOrEmpty(media.CoverPath) || !File.Exists(media.CoverPath))
                    {
                        return null;
                    }

                    switch (size.ToLower())
                    {
                        case "low":
                            return await _standardImageService.LoadLowQualityAsync(media.CoverPath, isVisible);
                        case "medium":
                            return await _standardImageService.LoadMediumQualityAsync(media.CoverPath, isVisible);
                        case "high":
                            return await _standardImageService.LoadHighQualityAsync(media.CoverPath, isVisible);
                        default:
                            return await _standardImageService.LoadLowQualityAsync(media.CoverPath, isVisible);
                    }
                },
                $"Loading profile photo with ID {photoId} (quality: {size})",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}