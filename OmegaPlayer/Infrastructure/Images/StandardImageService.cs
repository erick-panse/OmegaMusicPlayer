using Avalonia.Media.Imaging;
using System;
using System.Threading.Tasks;
using System.IO;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Navigation.Services;
using OmegaPlayer.Infrastructure.Services.Cache;

namespace OmegaPlayer.Infrastructure.Services.Images
{
    /// <summary>
    /// Provides standardized image loading functionality with predefined quality levels.
    /// </summary>
    public class StandardImageService : IDisposable
    {
        private readonly ImageCacheService _imageCacheService; 
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly INavigationService _navigationService;
        private bool _disposed = false;

        // Standard quality dimensions
        public const int LOW_QUALITY_SIZE = 220;
        public const int MEDIUM_QUALITY_SIZE = 320;
        public const int HIGH_QUALITY_SIZE = 480;
        public const int DETAIL_QUALITY_SIZE = 1080;

        public StandardImageService(
            ImageCacheService imageCacheService,
            IErrorHandlingService errorHandlingService,
            INavigationService navigationService)
        {
            _imageCacheService = imageCacheService;
            _errorHandlingService = errorHandlingService;
            _navigationService = navigationService;

            // Subscribe to navigation events for cache cleanup if needed
            _navigationService.BeforeNavigationChange += OnBeforeNavigationChange;
        }

        private async void OnBeforeNavigationChange(object sender, NavigationEventArgs e)
        {
            // Clear cache on navigation
            _imageCacheService.ClearCache();

            _errorHandlingService.LogError(
                ErrorSeverity.Info,
                "Navigation changed",
                $"Navigated to {e.Type}",
                null,
                false);
        }

        /// <summary>
        /// Loads an image at low quality (220x220).
        /// Suitable for thumbnails in lists and small UI elements.
        /// </summary>
        public async Task<Bitmap> LoadLowQualityAsync(string imagePath, bool isVisible = false)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(imagePath))
                        return null;

                    return await _imageCacheService.LoadThumbnailAsync(
                        imagePath,
                        LOW_QUALITY_SIZE,
                        LOW_QUALITY_SIZE);
                },
                $"Loading low quality image for {Path.GetFileName(imagePath)}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Loads an image at medium quality (320x320).
        /// Suitable for grid views and collection items.
        /// </summary>
        public async Task<Bitmap> LoadMediumQualityAsync(string imagePath, bool isVisible = false)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(imagePath))
                        return null;

                    return await _imageCacheService.LoadHighQualityImageAsync(
                        imagePath,
                        MEDIUM_QUALITY_SIZE,
                        MEDIUM_QUALITY_SIZE);
                },
                $"Loading medium quality image for {Path.GetFileName(imagePath)}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Loads an image at high quality (480x480).
        /// Suitable for larger display areas and detailed views.
        /// </summary>
        public async Task<Bitmap> LoadHighQualityAsync(string imagePath, bool isVisible = false)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(imagePath))
                        return null;

                   return await _imageCacheService.LoadHighQualityImageAsync(
                        imagePath,
                        HIGH_QUALITY_SIZE,
                        HIGH_QUALITY_SIZE);
                },
                $"Loading high quality image for {Path.GetFileName(imagePath)}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Loads an image at custom quality with specified dimensions.
        /// Use for special cases where standard sizes are not sufficient.
        /// </summary>
        public async Task<Bitmap> LoadCustomQualityAsync(string imagePath, int width, int height, bool isVisible = false)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(imagePath))
                        return null;

                    return await _imageCacheService.LoadHighQualityImageAsync(
                        imagePath,
                        width,
                        height);
                },
                $"Loading custom quality image for {Path.GetFileName(imagePath)}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Loads an image optimized for full detail view (1080x1080).
        /// Use for main detail views where high detail is needed.
        /// </summary>
        public async Task<Bitmap> LoadDetailQualityAsync(string imagePath, bool isVisible = false)
        {
            return await LoadCustomQualityAsync(imagePath, DETAIL_QUALITY_SIZE, DETAIL_QUALITY_SIZE, isVisible);
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

        /// <summary>
        /// Notifies that an image is currently visible to prioritize its loading
        /// SIMPLIFIED: No complex prioritization since ImageLoadingService is removed
        /// </summary>
        public async Task NotifyImageVisible(string imagePath, bool isVisible)
        {
            // No-op since we removed complex prioritization
            // Could be used for future optimizations if needed
            await Task.CompletedTask;
        }

        /// <summary>
        /// Cancels all pending image loads
        /// SIMPLIFIED: Just clear cache if needed
        /// </summary>
        public async Task CancelPendingLoads()
        {
            // Optional: Clear cache to "cancel" pending operations
            // _imageCacheService.ClearCache();
            await Task.CompletedTask;
        }

        /// <summary>
        /// Disposes resources used by the service
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            // Unsubscribe from navigation events
            _navigationService.BeforeNavigationChange -= OnBeforeNavigationChange;
        }
    }
}