using Avalonia.Media.Imaging;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services.Cache
{
    public class ImageCacheService
    {
        private readonly ConcurrentDictionary<string, WeakReference<Bitmap>> _imageCache
            = new ConcurrentDictionary<string, WeakReference<Bitmap>>();

        // Cache size limit (in MB)
        private const int MaxCacheSizeMB = 100;
        private long _currentCacheSize;

        public async Task<Bitmap> LoadThumbnailAsync(string imagePath, int width, int height)
        {
            if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Image file not found: {imagePath}");
            }

            string cacheKey = $"{imagePath}_{width}_{height}";

            // Try to get from cache first
            if (_imageCache.TryGetValue(cacheKey, out WeakReference<Bitmap> weakRef))
            {
                if (weakRef.TryGetTarget(out Bitmap cachedBitmap))
                {
                    try
                    {
                        // Try to access the bitmap to see if it's still valid
                        var _ = cachedBitmap.Size;
                        return cachedBitmap;
                    }
                    catch
                    {
                        // If accessing the bitmap throws an exception, remove it from cache
                        _imageCache.TryRemove(cacheKey, out _);
                    }
                }
            }

            return await Task.Run(() =>
            {
                try
                {
                    using (var stream = File.OpenRead(imagePath))
                    {
                        var bitmap = new Bitmap(stream);
                        var resizedBitmap = bitmap.CreateScaledBitmap(
                            new Avalonia.PixelSize(width, height));

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