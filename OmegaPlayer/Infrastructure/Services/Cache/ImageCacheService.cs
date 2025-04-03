using Avalonia.Media.Imaging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using SkiaSharp;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Linq;
using OmegaPlayer.Infrastructure.Services.Images;

namespace OmegaPlayer.Infrastructure.Services.Cache
{
    public class ImageCacheService : IMemoryPressureResponder
    {
        private readonly ConcurrentDictionary<string, WeakReference<Bitmap>> _imageCache
            = new ConcurrentDictionary<string, WeakReference<Bitmap>>();

        private readonly IErrorHandlingService _errorHandlingService;

        // Cache size limit (in MB)
        private const int MaxCacheSizeMB = 100;
        private long _currentCacheSize;

        // Track recently accessed keys for LRU algorithm
        private readonly List<string> _recentlyAccessedKeys = new List<string>();
        private readonly object _recentKeysLock = new object();
        private const int MaxRecentKeysTracked = 100;

        // Known valid image extensions
        private static readonly HashSet<string> KnownImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".webp", ".ico"
        };

        // Maximum image dimensions to consider valid
        private const int MaxValidImageDimension = 8192;

        public ImageCacheService(IErrorHandlingService errorHandlingService, MemoryMonitorService memoryMonitor)
        {
            _errorHandlingService = errorHandlingService;

            // Register with memory monitor to receive pressure notifications
            memoryMonitor?.RegisterResponder(this);
        }

        public async Task<Bitmap> LoadThumbnailAsync(string imagePath, int targetWidth, int targetHeight)
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

                    string cacheKey = $"{imagePath}_{targetWidth}_{targetHeight}";

                    // Try to get from cache first
                    if (_imageCache.TryGetValue(cacheKey, out WeakReference<Bitmap> weakRef))
                    {
                        if (weakRef.TryGetTarget(out Bitmap cachedBitmap))
                        {
                            try
                            {
                                // Ensure the bitmap is still valid
                                var _ = cachedBitmap.Size;

                                // Update recently accessed list for LRU algorithm
                                TrackKeyAccess(cacheKey);

                                return cachedBitmap;
                            }
                            catch
                            {
                                // Remove invalid bitmap from cache
                                _imageCache.TryRemove(cacheKey, out _);
                            }
                        }
                    }

                    // Standard loading path
                    return await Task.Run(() =>
                    {
                        try
                        {
                            using (var fileStream = File.OpenRead(imagePath))
                            {
                                // Load the original bitmap
                                using var originalBitmap = new Bitmap(fileStream);

                                // Ensure image isn't corrupted or unreasonably large
                                if (!ValidateImage(originalBitmap))
                                {
                                    _errorHandlingService.LogError(
                                        ErrorSeverity.NonCritical,
                                        "Invalid original bitmap",
                                        $"Original bitmap validation failed for {imagePath}",
                                        null,
                                        false);
                                    return null;
                                }

                                // Get original dimensions
                                double originalWidth = originalBitmap.PixelSize.Width;
                                double originalHeight = originalBitmap.PixelSize.Height;

                                // Calculate new dimensions preserving aspect ratio
                                double aspectRatio = originalWidth / originalHeight;
                                int newWidth, newHeight;

                                if (aspectRatio > 1)
                                {
                                    // Wider than tall
                                    newWidth = targetWidth;
                                    newHeight = (int)(targetWidth / aspectRatio);
                                }
                                else
                                {
                                    // Taller than wide
                                    newHeight = targetHeight;
                                    newWidth = (int)(targetHeight * aspectRatio);
                                }

                                // Ensure dimensions are reasonable
                                newWidth = Math.Max(1, Math.Min(newWidth, MaxValidImageDimension));
                                newHeight = Math.Max(1, Math.Min(newHeight, MaxValidImageDimension));

                                // Improved scaling using HighQuality interpolation
                                var resizedBitmap = originalBitmap.CreateScaledBitmap(
                                    new PixelSize(newWidth, newHeight),
                                    BitmapInterpolationMode.HighQuality);

                                // Validate the resized bitmap before caching
                                if (ValidateImage(resizedBitmap))
                                {
                                    _imageCache.TryAdd(cacheKey, new WeakReference<Bitmap>(resizedBitmap));
                                    _currentCacheSize += GetBitmapSize(resizedBitmap);

                                    // Track this key for LRU algorithm
                                    TrackKeyAccess(cacheKey);

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
                                        "Invalid resized bitmap",
                                        $"Resized bitmap validation failed for {imagePath}",
                                        null,
                                        false);
                                }

                                return resizedBitmap;
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the detailed error but let the SafeExecuteAsync handle the exception
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Failed to load thumbnail",
                                $"Error loading image from {imagePath}: {ex.Message}",
                                ex,
                                false);
                            throw;
                        }
                    });
                },
                $"Loading thumbnail for {Path.GetFileName(imagePath)}",
                null,
                ErrorSeverity.NonCritical,
                false // Don't show notification for image load failures
            );
        }

        /// <summary>
        /// Loads a high-quality version of an image optimized for crisp text rendering
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
                    if (_imageCache.TryGetValue(cacheKey, out WeakReference<Bitmap> weakRef))
                    {
                        if (weakRef.TryGetTarget(out Bitmap cachedBitmap))
                        {
                            try
                            {
                                var _ = cachedBitmap.Size;

                                // Update recently accessed list for LRU algorithm
                                TrackKeyAccess(cacheKey);

                                return cachedBitmap;
                            }
                            catch
                            {
                                _imageCache.TryRemove(cacheKey, out _);
                            }
                        }
                    }

                    return await Task.Run(() =>
                    {
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
                                                    _imageCache.TryAdd(cacheKey, new WeakReference<Bitmap>(avaloniaBitmap));
                                                    _currentCacheSize += GetBitmapSize(avaloniaBitmap);

                                                    // Track this key for LRU algorithm
                                                    TrackKeyAccess(cacheKey);

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
                    });
                },
                $"Loading high-quality image for {Path.GetFileName(imagePath)}",
                null,
                ErrorSeverity.NonCritical,
                false // Don't show notification for image load failures
            );
        }

        /// <summary>
        /// Validates if an image file appears to have a valid format and size
        /// </summary>
        private bool IsValidImageFile(string imagePath)
        {
            try
            {
                // Check file extension
                string extension = Path.GetExtension(imagePath);
                if (!string.IsNullOrEmpty(extension) && KnownImageExtensions.Contains(extension))
                {
                    // Check file size (fast validation)
                    var fileInfo = new FileInfo(imagePath);

                    // Check if file is empty
                    if (fileInfo.Length == 0)
                        return false;

                    // Check if file is too large (> 50MB is suspicious for a cover image)
                    if (fileInfo.Length > 50 * 1024 * 1024)
                        return false;

                    return true;
                }

                // For unknown extensions, check file header
                using (FileStream fs = new FileStream(imagePath, FileMode.Open, FileAccess.Read))
                {
                    if (fs.Length < 8) // Need at least a few bytes to check headers
                        return false;

                    byte[] header = new byte[8];
                    fs.Read(header, 0, header.Length);

                    // Check for common image format headers
                    // JPEG: starts with FF D8
                    if (header[0] == 0xFF && header[1] == 0xD8)
                        return true;

                    // PNG: starts with 89 50 4E 47
                    if (header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47)
                        return true;

                    // GIF: starts with GIF
                    if (header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46)
                        return true;

                    // BMP: starts with BM
                    if (header[0] == 0x42 && header[1] == 0x4D)
                        return true;

                    // WEBP: starts with RIFF and has WEBP at offset 8
                    if (header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46)
                    {
                        byte[] webpHeader = new byte[4];
                        fs.Position = 8;
                        fs.Read(webpHeader, 0, 4);
                        if (webpHeader[0] == 0x57 && webpHeader[1] == 0x45 && webpHeader[2] == 0x42 && webpHeader[3] == 0x50)
                            return true;
                    }

                    // Doesn't match any known image header
                    return false;
                }
            }
            catch
            {
                // If any exception occurs during validation, consider the file invalid
                return false;
            }
        }

        /// <summary>
        /// Validates if a bitmap is valid and usable
        /// </summary>
        private bool ValidateImage(Bitmap bitmap)
        {
            if (bitmap == null)
                return false;

            try
            {
                // Basic validation checks
                if (bitmap.PixelSize.Width <= 0 || bitmap.PixelSize.Height <= 0)
                    return false;

                if (bitmap.PixelSize.Width > MaxValidImageDimension || bitmap.PixelSize.Height > MaxValidImageDimension)
                    return false;

                // Access properties to ensure the bitmap is valid and accessible
                var size = bitmap.Size;
                var format = bitmap.Format;

                return true;
            }
            catch
            {
                // If any exception occurs during validation, consider the bitmap invalid
                return false;
            }
        }

        /// <summary>
        /// Tracks a key being accessed for LRU cache algorithm
        /// </summary>
        private void TrackKeyAccess(string cacheKey)
        {
            lock (_recentKeysLock)
            {
                // Remove if exists and add to front (most recently used)
                _recentlyAccessedKeys.Remove(cacheKey);
                _recentlyAccessedKeys.Insert(0, cacheKey);

                // Trim list if it gets too long
                if (_recentlyAccessedKeys.Count > MaxRecentKeysTracked)
                {
                    _recentlyAccessedKeys.RemoveRange(
                        MaxRecentKeysTracked,
                        _recentlyAccessedKeys.Count - MaxRecentKeysTracked);
                }
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

        private void CleanCache()
        {
            _errorHandlingService.SafeExecute(() =>
            {
                var keysToRemove = new List<string>();
                long freedSpace = 0;

                // First pass: remove weak references that are no longer valid
                foreach (var kvp in _imageCache)
                {
                    if (!kvp.Value.TryGetTarget(out Bitmap bitmap))
                    {
                        keysToRemove.Add(kvp.Key);
                    }
                    else
                    {
                        try
                        {
                            // Try to access the bitmap to see if it's still valid
                            var _ = bitmap.Size;
                        }
                        catch
                        {
                            keysToRemove.Add(kvp.Key);
                            freedSpace += GetBitmapSize(bitmap);
                        }
                    }
                }

                // Second pass: if we still need to free up space, remove least recently used items
                if (_currentCacheSize > MaxCacheSizeMB * 1024 * 1024 && _recentlyAccessedKeys.Count > 0)
                {
                    // Get least recently used keys (from the end of the list)
                    List<string> lruKeys;
                    lock (_recentKeysLock)
                    {
                        // Take 25% of the least recently used items
                        int itemsToRemove = Math.Max(5, _recentlyAccessedKeys.Count / 4);
                        lruKeys = _recentlyAccessedKeys
                            .Skip(Math.Max(0, _recentlyAccessedKeys.Count - itemsToRemove))
                            .ToList();
                    }

                    foreach (var key in lruKeys)
                    {
                        if (_imageCache.TryRemove(key, out var weakRef) &&
                            weakRef.TryGetTarget(out var bitmap))
                        {
                            freedSpace += GetBitmapSize(bitmap);
                            keysToRemove.Add(key);
                        }
                    }

                    // Update recently accessed keys list
                    lock (_recentKeysLock)
                    {
                        foreach (var key in keysToRemove)
                        {
                            _recentlyAccessedKeys.Remove(key);
                        }
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _imageCache.TryRemove(key, out _);
                }

                // Log cache cleanup information
                if (keysToRemove.Count > 0)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "Image cache cleanup",
                        $"Removed {keysToRemove.Count} items from image cache. Freed approximately {freedSpace / (1024 * 1024.0):F2} MB.",
                        null,
                        false);
                }

                // Reset cache size estimate
                _currentCacheSize = EstimateCurrentCacheSize();
            },
            "Cleaning image cache",
            ErrorSeverity.NonCritical,
            false);
        }

        /// <summary>
        /// Estimates the current cache size by iterating through valid items
        /// </summary>
        private long EstimateCurrentCacheSize()
        {
            long size = 0;
            foreach (var kvp in _imageCache)
            {
                if (kvp.Value.TryGetTarget(out var bitmap))
                {
                    try
                    {
                        // Check bitmap validity and add its size
                        var _ = bitmap.Size;
                        size += GetBitmapSize(bitmap);
                    }
                    catch
                    {
                        // Bitmap is invalid but will be cleaned up in next cleanup cycle
                    }
                }
            }
            return size;
        }

        public void ClearCache()
        {
            _errorHandlingService.SafeExecute(() =>
            {
                long previousSize = _currentCacheSize;
                _imageCache.Clear();

                lock (_recentKeysLock)
                {
                    _recentlyAccessedKeys.Clear();
                }

                _currentCacheSize = 0;

                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Image cache cleared",
                    $"Cleared all cached images. Released approximately {previousSize / (1024 * 1024.0):F2} MB.",
                    null,
                    false);

                // Force garbage collection to release bitmap memory
                GC.Collect();
            },
            "Clearing image cache",
            ErrorSeverity.NonCritical,
            false);
        }

        /// <summary>
        /// Cleans the cache more aggressively when the system is under memory pressure
        /// </summary>
        private void CleanCacheAggressively()
        {
            _errorHandlingService.SafeExecute(() =>
            {
                // First: Remove all high-quality images as they consume the most memory
                var highQualityKeys = _imageCache.Keys
                    .Where(k => k.StartsWith("HQ_") || k.Contains("_480_") || k.Contains("_1080_"))
                    .ToList();

                int removedCount = 0;
                long freedSpace = 0;

                foreach (var key in highQualityKeys)
                {
                    if (_imageCache.TryRemove(key, out var weakRef) &&
                        weakRef.TryGetTarget(out var bitmap))
                    {
                        freedSpace += GetBitmapSize(bitmap);
                        removedCount++;

                        lock (_recentKeysLock)
                        {
                            _recentlyAccessedKeys.Remove(key);
                        }
                    }
                }

                // Second: Keep only the 20 most recently used images, remove everything else
                if (_recentlyAccessedKeys.Count > 20)
                {
                    HashSet<string> keysToKeep;
                    lock (_recentKeysLock)
                    {
                        keysToKeep = new HashSet<string>(_recentlyAccessedKeys.Take(20));
                    }

                    var keysToRemove = _imageCache.Keys
                        .Where(k => !keysToKeep.Contains(k))
                        .ToList();

                    foreach (var key in keysToRemove)
                    {
                        if (_imageCache.TryRemove(key, out var weakRef) &&
                            weakRef.TryGetTarget(out var bitmap))
                        {
                            freedSpace += GetBitmapSize(bitmap);
                            removedCount++;
                        }
                    }

                    lock (_recentKeysLock)
                    {
                        _recentlyAccessedKeys.RemoveAll(k => !keysToKeep.Contains(k));
                    }
                }

                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Aggressive image cache cleanup",
                    $"Removed {removedCount} items from image cache due to system memory pressure. Freed approximately {freedSpace / (1024 * 1024.0):F2} MB.",
                    null,
                    false);

                // Reset size counter and force garbage collection
                _currentCacheSize = EstimateCurrentCacheSize();
                GC.Collect();
                GC.WaitForPendingFinalizers();
            },
            "Aggressive cache cleaning",
            ErrorSeverity.NonCritical,
            false);
        }

        #region IMemoryPressureResponder Implementation

        /// <summary>
        /// Handler for high memory pressure notifications
        /// </summary>
        public void OnHighMemoryPressure()
        {
            _errorHandlingService.LogError(
                ErrorSeverity.Info,
                "High memory pressure notification received",
                "Aggressively cleaning image cache to reduce memory usage",
                null,
                false);

            // Perform aggressive cleanup
            CleanCacheAggressively();
        }

        /// <summary>
        /// Handler for normal memory pressure notifications
        /// </summary>
        public void OnNormalMemoryPressure()
        {
            // Normal pressure doesn't require any special action
            // Regular cleanup happens during normal cache operations
        }

        #endregion
    }
}