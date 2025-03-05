using Avalonia.Media.Imaging;
using OmegaPlayer.Infrastructure.Services.Cache;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services.Images
{
    /// <summary>
    /// Provides standardized image loading functionality with predefined quality levels
    /// to ensure consistency throughout the application.
    /// </summary>
    public class StandardImageService
    {
        private readonly ImageCacheService _imageCacheService;

        // Standard quality dimensions
        public const int LOW_QUALITY_SIZE = 220;
        public const int MEDIUM_QUALITY_SIZE = 320;
        public const int HIGH_QUALITY_SIZE = 480;
        public const int DETAIL_QUALITY_SIZE = 1080;

        public StandardImageService(ImageCacheService imageCacheService)
        {
            _imageCacheService = imageCacheService;
        }

        /// <summary>
        /// Loads an image at low quality (220x220).
        /// Suitable for thumbnails in lists and small UI elements.
        /// </summary>
        public async Task<Bitmap> LoadLowQualityAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            try
            {
                // Use the high-quality loading for all image sizes
                return await _imageCacheService.LoadHighQualityImageAsync(imagePath, LOW_QUALITY_SIZE, LOW_QUALITY_SIZE);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading low quality image: {ex.Message}");
                try
                {
                    // Fallback to regular loading if the high-quality method fails
                    return await _imageCacheService.LoadThumbnailAsync(imagePath, LOW_QUALITY_SIZE, LOW_QUALITY_SIZE);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Loads an image at medium quality (320x320).
        /// Suitable for grid views and collection items.
        /// </summary>
        public async Task<Bitmap> LoadMediumQualityAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            try
            {
                return await _imageCacheService.LoadHighQualityImageAsync(imagePath, MEDIUM_QUALITY_SIZE, MEDIUM_QUALITY_SIZE);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading medium quality image: {ex.Message}");
                try
                {
                    return await _imageCacheService.LoadThumbnailAsync(imagePath, MEDIUM_QUALITY_SIZE, MEDIUM_QUALITY_SIZE);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Loads an image at high quality (480x480).
        /// Suitable for larger display areas and detailed views.
        /// </summary>
        public async Task<Bitmap> LoadHighQualityAsync(string imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            try
            {
                return await _imageCacheService.LoadHighQualityImageAsync(imagePath, HIGH_QUALITY_SIZE, HIGH_QUALITY_SIZE);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading high quality image: {ex.Message}");
                try
                {
                    return await _imageCacheService.LoadThumbnailAsync(imagePath, HIGH_QUALITY_SIZE, HIGH_QUALITY_SIZE);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Loads an image at custom quality with specified dimensions.
        /// Use for special cases where standard sizes are not sufficient.
        /// </summary>
        public async Task<Bitmap> LoadCustomQualityAsync(string imagePath, int width, int height)
        {
            if (string.IsNullOrEmpty(imagePath))
                return null;

            try
            {
                return await _imageCacheService.LoadHighQualityImageAsync(imagePath, width, height);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading custom quality image: {ex.Message}");
                try
                {
                    return await _imageCacheService.LoadThumbnailAsync(imagePath, width, height);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Loads an image optimized for full detail view (1080x1080).
        /// Use for main detail views where high detail is needed.
        /// </summary>
        public async Task<Bitmap> LoadDetailQualityAsync(string imagePath)
        {
            return await LoadCustomQualityAsync(imagePath, DETAIL_QUALITY_SIZE, DETAIL_QUALITY_SIZE);
        }

        /// <summary>
        /// Gets the standard size string identifier based on pixel dimensions
        /// </summary>
        public static string GetSizeIdentifier(int size)
        {
            if (size <= LOW_QUALITY_SIZE) return "low";
            if (size <= MEDIUM_QUALITY_SIZE) return "medium";
            if (size <= HIGH_QUALITY_SIZE) return "high";
            return "detail";
        }
    }
}