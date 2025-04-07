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

namespace OmegaPlayer.Features.Profile.Services
{
    public class ProfileService
    {
        private readonly ProfileRepository _profileRepository;
        private readonly ProfileConfigRepository _profileConfigRepository;
        private readonly StandardImageService _standardImageService;
        private readonly MediaService _mediaService;
        private const string PROFILE_PHOTO_DIR = "profile_photo";

        public ProfileService(
            ProfileRepository profileRepository,
            ProfileConfigRepository profileConfigRepository,
            StandardImageService standardImageService,
            MediaService mediaService)
        {
            _profileRepository = profileRepository;
            _profileConfigRepository = profileConfigRepository;
            _standardImageService = standardImageService;
            _mediaService = mediaService;
        }

        public async Task<Profiles> GetProfileById(int profileID)
        {
            return await _profileRepository.GetProfileById(profileID);
        }

        public async Task<List<Profiles>> GetAllProfiles()
        {
            return await _profileRepository.GetAllProfiles();
        }

        public async Task<int> AddProfile(Profiles profile, Stream photoStream = null)
        {
            if (photoStream != null)
            {
                var media = new Media { MediaType = PROFILE_PHOTO_DIR };
                var mediaId = await _mediaService.AddMedia(media);

                var photoPath = await SaveProfilePhoto(photoStream, mediaId);
                await _mediaService.UpdateMediaFilePath(mediaId, photoPath);
                profile.PhotoID = mediaId;
            }

            var profileId = await _profileRepository.AddProfile(profile);

            // Create associated ProfileConfig
            await _profileConfigRepository.CreateProfileConfig(profileId);

            return profileId;
        }

        public async Task UpdateProfile(Profiles profile, Stream photoStream = null)
        {
            if (photoStream != null)
            {
                if (profile.PhotoID > 0)
                {
                    var oldMedia = await _mediaService.GetMediaById(profile.PhotoID);
                    if (File.Exists(oldMedia?.CoverPath))
                    {
                        File.Delete(oldMedia.CoverPath);
                    }
                }

                var media = new Media { MediaType = PROFILE_PHOTO_DIR };
                var mediaId = await _mediaService.AddMedia(media);

                var photoPath = await SaveProfilePhoto(photoStream, mediaId);
                await _mediaService.UpdateMediaFilePath(mediaId, photoPath);
                profile.PhotoID = mediaId;
            }

            await _profileRepository.UpdateProfile(profile);
        }

        public async Task DeleteProfile(int profileID)
        {
            var profile = await GetProfileById(profileID);
            if (profile.PhotoID > 0)
            {
                var media = await _mediaService.GetMediaById(profile.PhotoID);
                if (File.Exists(media?.CoverPath))
                {
                    File.Delete(media.CoverPath);
                }
                await _mediaService.DeleteMedia(profile.PhotoID);
            }
            await _profileRepository.DeleteProfile(profileID);
        }

        private async Task<string> SaveProfilePhoto(Stream photoStream, int mediaId)
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var photoDir = Path.Combine(baseDir, "media", PROFILE_PHOTO_DIR, mediaId.ToString("D7"));
            Directory.CreateDirectory(photoDir);

            var filePath = Path.Combine(photoDir, $"profile_{mediaId.ToString("D7")}_photo.jpg");

            using (var fileStream = File.Create(filePath))
            {
                await photoStream.CopyToAsync(fileStream);
            }

            return filePath;
        }

        public async Task<Media> GetMediaByProfileId(int photoId)
        {
            return await _mediaService.GetMediaById(photoId);
        }

        public async Task<Bitmap> LoadProfilePhotoAsync(int photoId, string size = "low", bool isVisible = false)
        {
            if (photoId <= 0) return null;

            var media = await _mediaService.GetMediaById(photoId);
            if (media == null || !File.Exists(media.CoverPath)) return null;

            try
            {
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading profile photo: {ex.Message}");
                return null;
            }
        }
    }
}