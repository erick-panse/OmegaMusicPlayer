using System.Collections.Generic;
using Avalonia.Media.Imaging;
using System.IO;
using System.Threading.Tasks;
using Avalonia;

namespace OmegaPlayer.Services
{
    public class ImageCacheService
    {
        private Dictionary<string, Bitmap> _imageCache = new Dictionary<string, Bitmap>();

        public async Task<Bitmap> LoadImage(string filePath)
        {
            if (_imageCache.ContainsKey(filePath))
            {
                return _imageCache[filePath];
            }

            if (File.Exists(filePath))
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var bitmap = new Bitmap(stream);
                    _imageCache[filePath] = bitmap;
                    return bitmap;
                }
            }

            return null;
        }

        public async Task<Bitmap> LoadThumbnailAsync(string CoverPath, int width, int height)
        {
            // Ensure the file exists
            if (!File.Exists(CoverPath))
                return null;

            return await Task.Run(() =>
            {
                using (var stream = File.OpenRead(CoverPath))
                {
                    var bitmap = new Bitmap(stream);

                    // Resize the bitmap to the desired thumbnail size
                    var resizedBitmap = bitmap.CreateScaledBitmap(new Avalonia.PixelSize(width, height));

                    return resizedBitmap;
                }
            });
         }
    }
}
