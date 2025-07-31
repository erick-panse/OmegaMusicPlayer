using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Messages;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Features.Profile.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Data.Repositories.Profile;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Features.Profile.Services
{
    public class ProfileService
    {
        private readonly ProfileRepository _profileRepository;
        private readonly ProfileConfigRepository _profileConfigRepository;
        private readonly StandardImageService _standardImageService;
        private readonly MediaService _mediaService;
        private readonly LocalizationService _localizationService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;
        private const string PROFILE_PHOTO_DIR = "profile_photo";
        private const int NAME_CHAR_LIMIT = 30;

        public ProfileService(
            ProfileRepository profileRepository,
            ProfileConfigRepository profileConfigRepository,
            StandardImageService standardImageService,
            MediaService mediaService,
            LocalizationService localizationService,
            IErrorHandlingService errorHandlingService,
            IMessenger messenger)
        {
            _profileRepository = profileRepository;
            _profileConfigRepository = profileConfigRepository;
            _standardImageService = standardImageService;
            _mediaService = mediaService;
            _localizationService = localizationService;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;
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
                new List<Profiles>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<int> GetProfileCount()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profiles = await _profileRepository.GetAllProfiles();
                    return profiles?.Count ?? 0;
                },
                "Getting profile count",
                0,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<bool> IsProfileNameExists(string profileName, int? excludeProfileId = null)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(profileName))
                        return false;

                    var profiles = await _profileRepository.GetAllProfiles();
                    return profiles.Any(p =>
                        string.Equals(p.ProfileName, profileName.Trim(), StringComparison.OrdinalIgnoreCase)
                        && p.ProfileID != excludeProfileId);
                },
                "Checking if profile name exists",
                false,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public string ValidateProfileName(string profileName, int? excludeProfileId = null)
        {
            // Check for null/empty/whitespace
            if (string.IsNullOrWhiteSpace(profileName))
                return _localizationService["ProfileNameEmpty"];

            // Trim and check again
            profileName = profileName.Trim();
            if (string.IsNullOrEmpty(profileName))
                return _localizationService["ProfileNameEmpty"];

            // Check length
            if (profileName.Length > NAME_CHAR_LIMIT)
                return _localizationService["ProfileNameTooLongFirstHalf"] + NAME_CHAR_LIMIT + _localizationService["ProfileNameTooLongSecondHalf"];

            if (profileName.Length < 2)
                return _localizationService["ProfileNameTooShort"];

            // Check for invalid characters
            var invalidChars = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|', ';' });
            if (profileName.Any(c => invalidChars.Contains(c)))
                return _localizationService["ProfileNameInvalidChars"];

            // Check for reserved names
            var reservedNames = new[] { "default" };
            if (reservedNames.Contains(profileName.ToLower()))
                return _localizationService["ProfileNameSystemReserved"];

            return null; // Valid
        }

        public async Task<string> ValidateProfileNameAsync(string profileName, int? excludeProfileId = null)
        {
            // First check basic validation
            var basicValidation = ValidateProfileName(profileName, excludeProfileId);
            if (basicValidation != null)
                return basicValidation;

            // Then check for duplicates
            var isDuplicate = await IsProfileNameExists(profileName, excludeProfileId);
            if (isDuplicate)
                return _localizationService["ProfileNameAlreadyExists"];

            return null; // Valid
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

                    // Check profile limit before creating
                    var currentCount = await GetProfileCount();
                    if (currentCount >= 20)
                    {
                        throw new InvalidOperationException("Cannot create more than 20 profiles. Please delete an existing profile first.");
                    }

                    // Validate profile name
                    var validationMessage = await ValidateProfileNameAsync(profile.ProfileName);
                    if (!string.IsNullOrEmpty(validationMessage))
                    {
                        throw new ArgumentException(validationMessage, nameof(profile.ProfileName));
                    }

                    // Trim the profile name
                    profile.ProfileName = profile.ProfileName.Trim();

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
                        profile.CreatedAt = DateTime.UtcNow;
                    }
                    if (profile.UpdatedAt == default)
                    {
                        profile.UpdatedAt = DateTime.UtcNow;
                    }

                    var profileId = await _profileRepository.AddProfile(profile);

                    // Create associated ProfileConfig
                    await _profileConfigRepository.CreateProfileConfig(profileId);

                    return profileId;
                },
                $"Adding profile '{profile?.ProfileName ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                true
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

                    // Validate profile name (excluding current profile from duplicate check)
                    var validationMessage = await ValidateProfileNameAsync(profile.ProfileName, profile.ProfileID);
                    if (!string.IsNullOrEmpty(validationMessage))
                    {
                        throw new ArgumentException(validationMessage, nameof(profile.ProfileName));
                    }

                    // Trim the profile name
                    profile.ProfileName = profile.ProfileName.Trim();

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
                    profile.UpdatedAt = DateTime.UtcNow;

                    await _profileRepository.UpdateProfile(profile);

                    // Notify subscribers about profile change
                    _messenger.Send(new ProfileChangedMessage());
                },
                $"Updating profile '{profile?.ProfileName ?? "Unknown"}' (ID: {profile?.ProfileID ?? 0})",
                ErrorSeverity.NonCritical,
                true
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