using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Data.Repositories;
using OmegaPlayer.Infrastructure.Services.Cache;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Avalonia.Platform;
using Avalonia.Media.Immutable;

namespace OmegaPlayer.Features.Library.Services
{
    public class ArtistDisplayService
    {
        private readonly AllTracksRepository _allTracksRepository;
        private readonly ImageCacheService _imageCacheService;

        public ArtistDisplayService(
            AllTracksRepository allTracksRepository,
            ImageCacheService imageCacheService)
        {
            _allTracksRepository = allTracksRepository;
            _imageCacheService = imageCacheService;
        }

        public async Task<List<ArtistDisplayModel>> GetArtistsPageAsync(int page, int pageSize)
        {
            var allTracks = _allTracksRepository.AllTracks;

            // Group tracks by artist and create ArtistDisplayModels
            var artistGroups = allTracks
                .SelectMany(t => t.Artists.Select(a => new { Artist = a, Track = t }))
                .GroupBy(x => x.Artist.ArtistID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize);

            var artists = new List<ArtistDisplayModel>();

            foreach (var group in artistGroups)
            {
                var artistTracks = group.Select(x => x.Track).ToList();
                var artist = group.First().Artist;

                var artistModel = new ArtistDisplayModel
                {
                    ArtistID = artist.ArtistID,
                    Name = artist.ArtistName,
                    TrackIDs = artistTracks.Select(t => t.TrackID).ToList(),
                    TotalDuration = TimeSpan.FromTicks(artistTracks.Sum(t => t.Duration.Ticks))
                };

                artists.Add(artistModel);

                // Load low-res photo initially
                await LoadArtistPhotoAsync(artistModel, "low");
            }

            return artists;
        }

        public async Task<List<TrackDisplayModel>> GetArtistTracksAsync(int artistId)
        {
            // Get all tracks for this artist from AllTracksRepository
            return _allTracksRepository.AllTracks
                .Where(t => t.Artists.Any(a => a.ArtistID == artistId))
                .ToList();
        }

        public async Task<List<TrackDisplayModel>> GetTracksForArtistsAsync(List<int> artistIds)
        {
            // Get all tracks for multiple artists
            return _allTracksRepository.AllTracks
                .Where(t => t.Artists.Any(a => artistIds.Contains(a.ArtistID)))
                .ToList();
        }

        public async Task LoadArtistPhotoAsync(ArtistDisplayModel artist, string size = "low")
        {
            try
            {
                int photoSize = size == "high" ? 240 : 120; // Double size for high-res

                if (string.IsNullOrEmpty(artist.PhotoPath) || !File.Exists(artist.PhotoPath))
                {
                    // Create render target bitmap
                    var rtb = new RenderTargetBitmap(new PixelSize(photoSize, photoSize));

                    // Create brushes
                    var grayBrush = new ImmutableSolidColorBrush(Colors.Black);
                    var blueViolet = new ImmutableSolidColorBrush(Colors.BlueViolet);

                    using (var context = rtb.CreateDrawingContext())
                    {
                        // Draw gray circular background
                        var ellipse = new EllipseGeometry(new Rect(0, 0, photoSize, photoSize));
                        context.DrawGeometry(grayBrush, null, ellipse);

                        // Create the artist icon geometry directly
                        var iconGeometry = StreamGeometry.Parse("M24 4A10 10 0 1024 24 10 10 0 1024 4zM36.021 28H11.979C9.785 28 8 29.785 8 31.979V33.5c0 3.312 1.885 6.176 5.307 8.063C16.154 43.135 19.952 44 24 44c7.706 0 16-3.286 16-10.5v-1.521C40 29.785 38.215 28 36.021 28z");

                        // Calculate icon dimensions to fit in the circle with padding
                        double iconSize = photoSize * 0.6; // Icon should take 60% of the photo size
                        double padding = photoSize * 0.2; // 20% padding on each side

                        // Get the original bounds
                        var originalBounds = iconGeometry.Bounds;

                        // Calculate scale to fit desired size
                        double scaleX = iconSize / originalBounds.Width;
                        double scaleY = iconSize / originalBounds.Height;
                        double scale = Math.Min(scaleX, scaleY);

                        // Calculate centered position
                        double translateX = padding + (iconSize - (originalBounds.Width * scale)) / 2;
                        double translateY = padding + (iconSize - (originalBounds.Height * scale)) / 2;

                        // Create transform matrix
                        var transform = Matrix.CreateScale(scale, scale) *
                                      Matrix.CreateTranslation(translateX - (originalBounds.X * scale),
                                                             translateY - (originalBounds.Y * scale));

                        // Draw the icon with transform
                        using (context.PushTransform(transform))
                        {
                            context.DrawGeometry(blueViolet, null, iconGeometry);
                        }
                    }

                    artist.Photo = rtb;
                }
                else
                {
                    artist.Photo = await _imageCacheService.LoadThumbnailAsync(
                        artist.PhotoPath,
                        photoSize,
                        photoSize);
                }

                artist.PhotoSize = size;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading artist photo: {ex.Message}");
            }
        }

        public async Task LoadHighResArtistPhotoAsync(ArtistDisplayModel artist)
        {
            await LoadArtistPhotoAsync(artist, "high");
        }
    }

}
