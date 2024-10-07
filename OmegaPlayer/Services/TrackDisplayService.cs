using OmegaPlayer.Repositories;
using System.Collections.Generic;
using System;
using Avalonia.Media.Imaging;
using System.Threading.Tasks;
using System.IO;
using System.Collections.ObjectModel;
using OmegaPlayer.Models;
using System.Linq;

namespace OmegaPlayer.Services
{
    public class TrackDisplayService
    {
        private readonly TrackDisplayRepository _repository;
        private readonly ImageCacheService _imageCacheService;

        public TrackDisplayService(TrackDisplayRepository repository, ImageCacheService imageCacheService)
        {
            _repository = repository;
            _imageCacheService = imageCacheService;
        }

        public async Task<List<TrackDisplayModel>> GetAllTracksWithMetadata(int profileID)
        {
            try
            {
                return await _repository.GetAllTracksWithMetadata(profileID);
            }
            catch (Exception ex)
            {
                // Log exception and rethrow or handle it
                Console.WriteLine($"Error fetching all tracks display model: {ex.Message}");
                throw;
            }
        }

        public async Task<List<TrackDisplayModel>> GetTrackDisplaysFromQueue(List<QueueTracks> queueTracks, int profileId)
        {
            List<int> trackIds = queueTracks.Select(q => q.TrackID).ToList();
            return await _repository.GetTracksWithMetadataByIds(trackIds, profileId);
        }


        public async Task<List<TrackDisplayModel>> LoadTracksAsync(int profileID, int pageNumber, int pageSize)
        {
            var tracksDisplay = await _repository.GetTracksWithMetadataAsync(profileID, pageNumber, pageSize);

            foreach (var track in tracksDisplay)
            {
                // Load a lower-resolution thumbnail (e.g., 100x100) first for better performance
                track.Thumbnail = await _imageCacheService.LoadThumbnailAsync(track.CoverPath, 100, 100);
            }

            return tracksDisplay;
        }

        public async Task LoadHighResThumbnailAsync(TrackDisplayModel track)
        {
            // Load high-res image when needed (160x160)
            track.Thumbnail = await _imageCacheService.LoadThumbnailAsync(track.CoverPath, 160, 160);
        }

    }
}
