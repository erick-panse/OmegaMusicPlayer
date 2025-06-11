using Avalonia;
using Avalonia.Media.Imaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Infrastructure.Services.Images;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services.Cache
{
    public class ImageCacheService : IMemoryPressureResponder
    {
        private readonly ConcurrentDictionary<string, Bitmap> _imageCache = new ConcurrentDictionary<string, Bitmap>();
        private readonly IErrorHandlingService _errorHandlingService;

        // Cache size limit (in MB)
        private const int MaxCacheSizeMB = 100;
        private long _currentCacheSize;

        // Known valid image extensions
        private static readonly HashSet<string> KnownImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp", ".ico"
        };

        // Maximum image dimensions
        private const int MaxValidImageDimension = 8192;

        public ImageCacheService(IErrorHandlingService errorHandlingService, MemoryMonitorService memoryMonitor)
        {
            _errorHandlingService = errorHandlingService;
            memoryMonitor?.RegisterResponder(this);
        }

        public async Task<Bitmap> LoadThumbnailAsync(string imagePath, int targetWidth, int targetHeight, BitmapInterpolationMode quality = BitmapInterpolationMode.MediumQuality)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                    {
                        return null;
                    }

                    // Perform basic validation before attempting to load the image
                    if (!IsValidImageFile(imagePath))
                    {
                        return null;
                    }

                    string cacheKey = $"{imagePath}_{targetWidth}_{targetHeight}";

                    // Try to get from cache first
                    if (_imageCache.TryGetValue(cacheKey, out Bitmap cachedBitmap))
                    {
                        try
                        {
                            // Ensure the bitmap is still valid
                            var _ = cachedBitmap.Size;
                            return cachedBitmap;
                        }
                        catch
                        {
                            // Remove invalid bitmap from cache
                            _imageCache.TryRemove(cacheKey, out _);
                        }
                    }

                    // Standard loading path
                    try
                    {
                        using var fileStream = File.OpenRead(imagePath);
                        // Load the original bitmap
                        using var originalBitmap = new Bitmap(fileStream);

                        // Ensure image isn't corrupted or unreasonably large
                        if (!ValidateImage(originalBitmap))
                        {
                            return null;
                        }

                        // Calculate dimensions
                        double aspectRatio = (double)originalBitmap.PixelSize.Width / originalBitmap.PixelSize.Height;
                        int newWidth, newHeight;

                        if (aspectRatio > 1)
                        {
                            newWidth = targetWidth;
                            newHeight = (int)(targetWidth / aspectRatio);
                        }
                        else
                        {
                            newHeight = targetHeight;
                            newWidth = (int)(targetHeight * aspectRatio);
                        }

                        // Ensure reasonable dimensions
                        newWidth = Math.Max(1, Math.Min(newWidth, MaxValidImageDimension));
                        newHeight = Math.Max(1, Math.Min(newHeight, MaxValidImageDimension));

                        // Create scaled bitmap
                        var resizedBitmap = originalBitmap.CreateScaledBitmap(
                            new PixelSize(newWidth, newHeight),
                            quality); // Adjust quality as needed for stability

                        // Validate the resized bitmap before caching
                        if (ValidateImage(resizedBitmap))
                        {
                            _imageCache.TryAdd(cacheKey, resizedBitmap);
                            _currentCacheSize += GetBitmapSize(resizedBitmap);

                            // Clean cache if needed
                            if (_currentCacheSize > MaxCacheSizeMB * 1024 * 1024)
                            {
                                CleanCache();
                            }
                        }

                        return resizedBitmap;
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to load thumbnail",
                            $"Error loading image from {imagePath}: {ex.Message}",
                            ex,
                            false);
                        return null;
                    }
                },
                $"Loading thumbnail for {Path.GetFileName(imagePath)}",
                null,
                ErrorSeverity.NonCritical,
                false
            ).ConfigureAwait(false); // IMPORTANT: Prevent context switching issues
        }

        /// <summary>
        /// High quality loading without complex operations
        /// </summary>
        public async Task<Bitmap> LoadHighQualityImageAsync(string imagePath, int targetWidth, int targetHeight)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(imagePath))
                    {
                        throw new ArgumentException("Image path cannot be null or empty.", nameof(imagePath));
                    }

                    if (!File.Exists(imagePath))
                    {
                        throw new FileNotFoundException($"Image file not found: {imagePath}");
                    }

                    // Perform basic validation before attempting to load the image
                    if (!IsValidImageFile(imagePath))
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Invalid image file",
                            $"File doesn't appear to be a valid image: {imagePath}",
                            null,
                            false);
                        return null;
                    }

                    string cacheKey = $"HQ_{imagePath}_{targetWidth}_{targetHeight}";

                    // Try to get from cache first
                    if (_imageCache.TryGetValue(cacheKey, out Bitmap cachedBitmap))
                    {
                        try
                        {
                            var _ = cachedBitmap.Size;

                            return cachedBitmap;
                        }
                        catch
                        {
                            _imageCache.TryRemove(cacheKey, out _);
                        }
                    }

                    try
                    {
                        // For increased quality, scale to a higher resolution first, then down to target
                        using (var skiaStream = new SKMemoryStream(File.ReadAllBytes(imagePath)))
                        {
                            using (var skBitmap = SKBitmap.Decode(skiaStream))
                            {
                                if (skBitmap == null)
                                {
                                    throw new InvalidOperationException($"Failed to decode image: {imagePath}");
                                }

                                // Perform validation on the decoded image
                                if (skBitmap.Width <= 0 || skBitmap.Height <= 0 ||
                                    skBitmap.Width > MaxValidImageDimension || skBitmap.Height > MaxValidImageDimension)
                                {
                                    throw new InvalidOperationException($"Image has invalid dimensions: {skBitmap.Width}x{skBitmap.Height}");
                                }

                                // Calculate dimensions while preserving aspect ratio
                                double aspectRatio = (double)skBitmap.Width / skBitmap.Height;
                                int newWidth, newHeight;

                                if (aspectRatio > 1)
                                {
                                    newWidth = targetWidth;
                                    newHeight = (int)(targetWidth / aspectRatio);
                                }
                                else
                                {
                                    newHeight = targetHeight;
                                    newWidth = (int)(targetHeight * aspectRatio);
                                }

                                // Ensure we have reasonable dimensions
                                newWidth = Math.Max(1, Math.Min(newWidth, MaxValidImageDimension));
                                newHeight = Math.Max(1, Math.Min(newHeight, MaxValidImageDimension));

                                // Create a high-quality scaled bitmap
                                using (var scaledBitmap = new SKBitmap(newWidth, newHeight))
                                {
                                    // Use high-quality scaling
                                    skBitmap.ScalePixels(scaledBitmap, SKFilterQuality.High);

                                    // Convert to Avalonia bitmap
                                    var info = new SKImageInfo(scaledBitmap.Width, scaledBitmap.Height);
                                    using (var surface = SKSurface.Create(info))
                                    {
                                        var canvas = surface.Canvas;
                                        canvas.Clear(SKColors.Transparent);
                                        canvas.DrawBitmap(scaledBitmap, 0, 0);

                                        using (var image = surface.Snapshot())
                                        using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                                        {
                                            var memoryStream = new MemoryStream();
                                            data.SaveTo(memoryStream);
                                            memoryStream.Position = 0;

                                            var avaloniaBitmap = new Bitmap(memoryStream);

                                            // Validate the final bitmap before caching
                                            if (ValidateImage(avaloniaBitmap))
                                            {
                                                // Add to cache
                                                _imageCache.TryAdd(cacheKey, avaloniaBitmap);
                                                _currentCacheSize += GetBitmapSize(avaloniaBitmap);

                                                // Clean cache if needed
                                                if (_currentCacheSize > MaxCacheSizeMB * 1024 * 1024)
                                                {
                                                    CleanCache();
                                                }
                                            }
                                            else
                                            {
                                                _errorHandlingService.LogError(
                                                    ErrorSeverity.NonCritical,
                                                    "Invalid high-quality bitmap",
                                                    $"High-quality bitmap validation failed for {imagePath}",
                                                    null,
                                                    false);
                                            }

                                            return avaloniaBitmap;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the specific error before falling back to standard quality
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "High-quality image processing failed",
                            $"Failed to process high-quality image for {Path.GetFileName(imagePath)}. Falling back to standard quality.",
                            ex,
                            false);

                        // Fallback to regular loading method
                        return LoadThumbnailAsync(imagePath, targetWidth, targetHeight).Result;
                    }
                },
                $"Loading high-quality image for {Path.GetFileName(imagePath)}",
                null,
                ErrorSeverity.NonCritical,
                false
            ).ConfigureAwait(false); // IMPORTANT: Prevent context switching issues
        }

        /// <summary>
        /// Validates if an image file appears to have a valid format and size
        /// </summary>
        private bool IsValidImageFile(string imagePath)
        {
            try
            {
                string extension = Path.GetExtension(imagePath);
                if (string.IsNullOrEmpty(extension) || !KnownImageExtensions.Contains(extension))
                {
                    return false;
                }

                var fileInfo = new FileInfo(imagePath);
                return fileInfo.Length > 0 && fileInfo.Length < 20 * 1024 * 1024; // 20MB max
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates if a bitmap is valid
        /// </summary>
        private bool ValidateImage(Bitmap bitmap)
        {
            if (bitmap == null)
                return false;

            try
            {
                return bitmap.PixelSize.Width > 0 &&
                       bitmap.PixelSize.Height > 0 &&
                       bitmap.PixelSize.Width <= MaxValidImageDimension &&
                       bitmap.PixelSize.Height <= MaxValidImageDimension;
            }
            catch
            {
                return false;
            }
        }

        private long GetBitmapSize(Bitmap bitmap)
        {
            if (bitmap == null)
                return 0;

            // More accurate estimation based on bitmap format
            int bytesPerPixel = 4; // Default for RGBA8888

            try
            {
                var format = bitmap.Format;
                if (format.HasValue)
                {
                    // Convert bits to bytes (rounding up to ensure we account for all bits)
                    bytesPerPixel = (format.Value.BitsPerPixel + 7) / 8;
                }
            }
            catch
            {
                // Fallback to default if Format throws an exception
                bytesPerPixel = 4;
            }

            // Calculate size + small overhead for Bitmap object
            return (bitmap.PixelSize.Width * bitmap.PixelSize.Height * bytesPerPixel) + 256;
        }

        /// <summary>
        /// Basic cache cleanup without complex LRU logic
        /// </summary>
        private void CleanCache()
        {
            try
            {
                // Remove 25% of cache entries randomly
                var keysToRemove = _imageCache.Keys.Take(_imageCache.Count / 4).ToList();

                foreach (var key in keysToRemove)
                {
                    if (_imageCache.TryRemove(key, out var bitmap))
                    {
                        try
                        {
                            bitmap?.Dispose();
                        }
                        catch
                        {
                            // Ignore disposal errors
                        }
                    }
                }

                // Recalculate cache size
                _currentCacheSize = _imageCache.Values.Sum(GetBitmapSize);

                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Image cache cleanup",
                    $"Removed {keysToRemove.Count} items from cache",
                    null,
                    false);
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Cache cleanup error",
                    ex.Message,
                    ex,
                    false);
            }
        }

        public void ClearCache()
        {
            try
            {
                foreach (var bitmap in _imageCache.Values)
                {
                    try
                    {
                        bitmap?.Dispose();
                    }
                    catch
                    {
                        // Ignore disposal errors
                    }
                }

                _imageCache.Clear();
                _currentCacheSize = 0;

                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Image cache cleared",
                    "Cleared all cached images",
                    null,
                    false);
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error clearing cache",
                    ex.Message,
                    ex,
                    false);
            }
        }

        #region IMemoryPressureResponder Implementation

        public void OnHighMemoryPressure()
        {
            ClearCache(); // Clear everything on high pressure
        }

        public void OnNormalMemoryPressure()
        {
            // Normal pressure doesn't require any special action
            // Regular cleanup happens during normal cache operations
        }

        #endregion
    }
}