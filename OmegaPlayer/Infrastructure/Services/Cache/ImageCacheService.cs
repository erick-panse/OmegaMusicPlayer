using Avalonia.Media.Imaging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using SkiaSharp;

namespace OmegaPlayer.Infrastructure.Services.Cache
{
    public class ImageCacheService
    {
        private readonly ConcurrentDictionary<string, WeakReference<Bitmap>> _imageCache
            = new ConcurrentDictionary<string, WeakReference<Bitmap>>();

        // Cache size limit (in MB)
        private const int MaxCacheSizeMB = 100;
        private long _currentCacheSize;

        public async Task<Bitmap> LoadThumbnailAsync(string imagePath, int targetWidth, int targetHeight)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
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
                        return cachedBitmap;
                    }
                    catch
                    {
                        // Remove invalid bitmap from cache
                        _imageCache.TryRemove(cacheKey, out _);
                    }
                }
            }

            return await Task.Run(() =>
            {
                try
                {
                    using (var fileStream = File.OpenRead(imagePath))
                    {
                        // Load the original bitmap
                        using var originalBitmap = new Bitmap(fileStream);

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

                        // Improved scaling using HighQuality interpolation
                        // Use a higher internal resolution for better downscaling
                        var resizedBitmap = originalBitmap.CreateScaledBitmap(
                            new PixelSize(newWidth, newHeight),
                            BitmapInterpolationMode.HighQuality);

                        // Add to cache
                        _imageCache.TryAdd(cacheKey, new WeakReference<Bitmap>(resizedBitmap));
                        _currentCacheSize += GetBitmapSize(resizedBitmap);

                        // Clean cache if needed
                        if (_currentCacheSize > MaxCacheSizeMB * 1024 * 1024)
                        {
                            CleanCache();
                        }

                        return resizedBitmap;
                    }
                }
                catch (Exception ex)
                {
                    ShowMessageBox($"Error loading image {imagePath}: {ex.Message}");
                    throw;
                }
            });
        }

        /// <summary>
        /// Loads a high-quality version of an image optimized for crisp text rendering
        /// </summary>
        public async Task<Bitmap> LoadHighQualityImageAsync(string imagePath, int targetWidth, int targetHeight)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
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
                    using (var stream = File.OpenRead(imagePath))
                    {
                        using (var skiaStream = new SKMemoryStream(File.ReadAllBytes(imagePath)))
                        {
                            using (var skBitmap = SKBitmap.Decode(skiaStream))
                            {
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
                                newWidth = Math.Max(1, newWidth);
                                newHeight = Math.Max(1, newHeight);

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

                                            // Add to cache
                                            _imageCache.TryAdd(cacheKey, new WeakReference<Bitmap>(avaloniaBitmap));
                                            _currentCacheSize += GetBitmapSize(avaloniaBitmap);

                                            // Clean cache if needed
                                            if (_currentCacheSize > MaxCacheSizeMB * 1024 * 1024)
                                            {
                                                CleanCache();
                                            }

                                            return avaloniaBitmap;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ShowMessageBox($"Error loading high-quality image {imagePath}: {ex.Message}");

                    // Fallback to regular loading method
                    return LoadThumbnailAsync(imagePath, targetWidth, targetHeight).Result;
                }
            });
        }

        private long GetBitmapSize(Bitmap bitmap)
        {
            return bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4; // 4 bytes per pixel
        }

        private void CleanCache()
        {
            var keysToRemove = new List<string>();

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
                    }
                }
            }

            foreach (var key in keysToRemove)
            {
                _imageCache.TryRemove(key, out _);
            }

            // Force GC collection if cache is still too large
            if (_currentCacheSize > MaxCacheSizeMB * 1024 * 1024)
            {
                GC.Collect();
                _currentCacheSize = 0; // Reset size counter as we can't accurately track after GC
            }
        }

        public void ClearCache()
        {
            _imageCache.Clear();
            _currentCacheSize = 0;
        }

        private async void ShowMessageBox(string message)
        {
            var messageBox = MessageBoxManager.GetMessageBoxStandard("DI Resolution Result", message, ButtonEnum.Ok, Icon.Info);
            await messageBox.ShowWindowAsync();
        }
    }
}