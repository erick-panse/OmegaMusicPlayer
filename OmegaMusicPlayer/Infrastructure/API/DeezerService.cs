using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Library.Services;

namespace OmegaMusicPlayer.Infrastructure.API
{
    public class DeezerService
    {
        private readonly HttpClient _httpClient;
        private readonly MediaService _mediaService;
        private readonly ArtistsService _artistsService;
        private readonly TrackMetadataService _trackMetadataService;
        private readonly IErrorHandlingService _errorHandlingService;

        // Deezer API configuration
        private const string BASE_URL = "https://api.deezer.com";

        // Rate limiting - Deezer is generous but we'll be respectful
        private static readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
        private static DateTime _lastRequestTime = DateTime.MinValue;
        private const int MIN_REQUEST_INTERVAL_MS = 200; // 10 requests per second max

        // Artist name cleaning patterns
        private static readonly string[] FEATURING_PATTERNS = {
            " & ", ",", "(", "[", "{", " - ", " and ", " x ", " vs ", " with ", " feat.", " feat ", " featuring ", " ft.", " ft "
        };

        public DeezerService(
            MediaService mediaService,
            ArtistsService artistsService,
            TrackMetadataService trackMetadataService,
            IErrorHandlingService errorHandlingService)
        {
            _mediaService = mediaService;
            _artistsService = artistsService;
            _trackMetadataService = trackMetadataService;
            _errorHandlingService = errorHandlingService;

            _httpClient = new HttpClient();

            // Get version from assembly
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"OmegaMusicPlayer/{version} (https://github.com/erick-panse/OmegaMusicPlayer)");
        }

        /// <summary>
        /// Fetches and saves artist photo from Deezer API
        /// </summary>
        public async Task<bool> FetchAndSaveArtistData(int artistId, string artistName, CancellationToken cancellationToken = default)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(artistName))
                        return false;

                    // Clean artist name (extract primary artist from featuring/collaboration)
                    var cleanArtistName = CleanArtistName(artistName);

                    // Apply rate limiting
                    await ApplyRateLimit(cancellationToken);

                    // Search for artist
                    var artistData = await SearchAndGetArtistData(cleanArtistName, cancellationToken);
                    if (artistData == null)
                        return false;

                    var updated = false;

                    // Get current artist from database
                    var artist = await _artistsService.GetArtistById(artistId);
                    if (artist == null)
                        return false;

                    // Download and save image if available and artist doesn't have one
                    if (!string.IsNullOrWhiteSpace(artistData.ImageUrl) && artist.PhotoID == 0)
                    {
                        var imageSuccess = await DownloadAndSaveArtistImage(artistId, artistName, artistData.ImageUrl, cancellationToken);
                        if (imageSuccess)
                            updated = true;
                    }

                    // Always update LastApiDataSearch timestamp to prevent re-searching
                    artist.LastApiDataSearch = DateTime.UtcNow;
                    artist.UpdatedAt = DateTime.UtcNow;
                    await _artistsService.UpdateArtist(artist);

                    if (updated)
                    {
                        _errorHandlingService.LogInfo(
                            "Data updated with Deezer",
                            $"Updated artist data for {artistName}",
                            false);
                    }

                    return updated;
                },
                $"Fetching Deezer data for artist: {artistName}",
                false,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Searches for artist and returns combined data
        /// </summary>
        private async Task<DeezerArtistData> SearchAndGetArtistData(string artistName, CancellationToken cancellationToken)
        {
            try
            {
                // Search for artist
                var searchUrl = $"{BASE_URL}/search/artist?q={Uri.EscapeDataString(artistName)}&limit=5";
                var response = await _httpClient.GetAsync(searchUrl, cancellationToken);

                if (!response.IsSuccessStatusCode)
                    return null;

                var searchJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var searchResult = JsonSerializer.Deserialize<DeezerSearchResponse>(searchJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                // Find best match
                var bestMatch = FindBestArtistMatch(searchResult?.Data, artistName);
                if (bestMatch == null)
                    return null;

                // Get detailed artist info
                await ApplyRateLimit(cancellationToken);
                var detailUrl = $"{BASE_URL}/artist/{bestMatch.Id}";
                var detailResponse = await _httpClient.GetAsync(detailUrl, cancellationToken);

                if (!detailResponse.IsSuccessStatusCode)
                    return null;

                var detailJson = await detailResponse.Content.ReadAsStringAsync(cancellationToken);
                var artistDetail = JsonSerializer.Deserialize<DeezerArtistDetail>(detailJson, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                });

                return new DeezerArtistData
                {
                    Name = artistDetail?.Name ?? bestMatch.Name,
                    ImageUrl = GetBestImageUrl(artistDetail?.PictureXl, artistDetail?.PictureBig, artistDetail?.Picture)
                };
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error searching Deezer artist",
                    $"Failed to search for artist: {artistName}",
                    ex,
                    false);
                return null;
            }
        }

        /// <summary>
        /// Finds the best artist match from search results
        /// </summary>
        private DeezerArtistSearchResult FindBestArtistMatch(List<DeezerArtistSearchResult> results, string searchName)
        {
            if (results == null || !results.Any())
                return null;

            // Exact match first
            var exactMatch = results.FirstOrDefault(r =>
                string.Equals(r.Name, searchName, StringComparison.OrdinalIgnoreCase));

            if (exactMatch != null)
                return exactMatch;

            // Partial match
            var partialMatch = results.FirstOrDefault(r =>
                r.Name?.Contains(searchName, StringComparison.OrdinalIgnoreCase) == true ||
                searchName.Contains(r.Name ?? "", StringComparison.OrdinalIgnoreCase));

            return partialMatch ?? results.First();
        }

        /// <summary>
        /// Selects the best quality image URL
        /// </summary>
        private string GetBestImageUrl(params string[] imageUrls)
        {
            return imageUrls.FirstOrDefault(url => !string.IsNullOrEmpty(url) && IsValidImageUrl(url));
        }

        /// <summary>
        /// Validates image URL
        /// </summary>
        private bool IsValidImageUrl(string url)
        {
            return !string.IsNullOrEmpty(url) &&
                   Uri.IsWellFormedUriString(url, UriKind.Absolute) &&
                   !url.Contains("2a96cbd8b46e442fc41c2b86b821562f"); // Avoid placeholder images
        }

        /// <summary>
        /// Downloads and saves artist image
        /// </summary>
        private async Task<bool> DownloadAndSaveArtistImage(int artistId, string artistName, string imageUrl, CancellationToken cancellationToken)
        {
            try
            {
                // Download the image
                var response = await _httpClient.GetAsync(imageUrl, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return false;

                var imageData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                if (imageData.Length == 0 || !IsValidImageData(imageData))
                    return false;

                // Create media entry
                var media = new Media
                {
                    CoverPath = null,
                    MediaType = "artist_photo"
                };

                int mediaId = await _mediaService.AddMedia(media);
                media.MediaID = mediaId;

                // Save image file
                using (var imageStream = new MemoryStream(imageData))
                {
                    var imageFilePath = await _trackMetadataService.SaveImage(imageStream, "artist_photo", mediaId);
                    media.CoverPath = imageFilePath;
                    await _mediaService.UpdateMediaFilePath(mediaId, imageFilePath);
                }

                // Update artist with photo
                var artist = await _artistsService.GetArtistById(artistId);
                if (artist != null)
                {
                    artist.PhotoID = mediaId;
                    artist.UpdatedAt = DateTime.UtcNow;
                    await _artistsService.UpdateArtist(artist);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to download Deezer image",
                    $"Failed to download image for artist: {artistName}",
                    ex,
                    false);
                return false;
            }
        }

        /// <summary>
        /// Cleans artist name by extracting primary artist
        /// </summary>
        private string CleanArtistName(string artistName)
        {
            if (string.IsNullOrWhiteSpace(artistName))
                return artistName;

            var cleaned = artistName.Trim();

            // Extract primary artist before featuring patterns
            foreach (var pattern in FEATURING_PATTERNS)
            {
                var index = cleaned.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (index > 0)
                {
                    cleaned = cleaned.Substring(0, index).Trim();
                    break;
                }
            }

            return cleaned;
        }

        /// <summary>
        /// Validates image data by checking file headers
        /// </summary>
        private bool IsValidImageData(byte[] data)
        {
            if (data == null || data.Length < 8)
                return false;

            // Check for common image file signatures
            // JPEG: FF D8 FF
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return true;

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return true;

            // WebP: 52 49 46 46 (RIFF) ... 57 45 42 50 (WEBP)
            if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
                return true;

            return false;
        }

        /// <summary>
        /// Applies rate limiting
        /// </summary>
        private async Task ApplyRateLimit(CancellationToken cancellationToken)
        {
            await _rateLimitSemaphore.WaitAsync(cancellationToken);
            try
            {
                var elapsed = DateTime.Now - _lastRequestTime;
                if (elapsed.TotalMilliseconds < MIN_REQUEST_INTERVAL_MS)
                {
                    var delay = MIN_REQUEST_INTERVAL_MS - (int)elapsed.TotalMilliseconds;
                    await Task.Delay(delay, cancellationToken);
                }
                _lastRequestTime = DateTime.Now;
            }
            finally
            {
                _rateLimitSemaphore.Release();
            }
        }

        /// <summary>
        /// Batch fetch artist data for multiple artists without photos
        /// </summary>
        public async Task<int> FetchMissingArtistData(CancellationToken cancellationToken = default, int maxArtists = 50)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    _errorHandlingService.LogInfo(
                        "Starting Deezer artist data batch fetch",
                        "Fetching missing artist data from Deezer...",
                        false);

                    var allArtists = await _artistsService.GetAllArtists();
                    var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                    var artistsNeedingData = allArtists
                        .Where(a => a.PhotoID == 0 &&
                                    !string.IsNullOrWhiteSpace(a.ArtistName) &&
                                    (a.LastApiDataSearch == null || a.LastApiDataSearch < thirtyDaysAgo))
                        .Take(maxArtists)
                        .ToList();

                    if (!artistsNeedingData.Any())
                    {
                        // All artists already have photos or no artists found
                        return 0;
                    }

                    int dataFound = 0;
                    int processed = 0;

                    foreach (var artist in artistsNeedingData)
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var success = await FetchAndSaveArtistData(
                                artist.ArtistID,
                                artist.ArtistName,
                                cancellationToken);

                            if (success)
                                dataFound++;

                            processed++;

                            // Log progress every 10 artists
                            if (processed % 10 == 0)
                            {
                                _errorHandlingService.LogInfo(
                                    "Deezer batch fetch progress",
                                    $"Processed {processed}/{artistsNeedingData.Count} artists, updated {dataFound} artists",
                                    false);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            _errorHandlingService.LogError(
                                ErrorSeverity.NonCritical,
                                "Error in batch data fetch",
                                $"Failed to fetch data for {artist.ArtistName}",
                                ex,
                                false);
                        }
                    }

                    _errorHandlingService.LogInfo(
                        "Deezer batch fetch completed",
                        $"Successfully updated {dataFound} artists from {processed} processed artists.",
                        false);

                    return dataFound;
                },
                "Batch fetching artist data from Deezer",
                0,
                ErrorSeverity.NonCritical,
                false);
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    #region Data Models

    public class DeezerArtistData
    {
        public string Name { get; set; }
        public string ImageUrl { get; set; }
    }

    public class DeezerSearchResponse
    {
        public List<DeezerArtistSearchResult> Data { get; set; }
    }

    public class DeezerArtistSearchResult
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Picture { get; set; }
        public string PictureBig { get; set; }
        public string PictureXl { get; set; }
    }

    public class DeezerArtistDetail
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Picture { get; set; }
        public string PictureBig { get; set; }
        public string PictureXl { get; set; }
        public int NbFan { get; set; }
    }

    #endregion
}