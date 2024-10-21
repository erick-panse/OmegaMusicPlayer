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
        private readonly AllTracksRepository _allTracksRepository;

        public List<TrackDisplayModel> AllTracks { get; set; }

        public TrackDisplayService(TrackDisplayRepository repository, ImageCacheService imageCacheService, AllTracksRepository allTracksRepository)
        {
            _repository = repository;
            _imageCacheService = imageCacheService;
            _allTracksRepository = allTracksRepository;
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
            // Retrieve all tracks for the profile from the repository

            if (_allTracksRepository.AllTracks == null)
            {
                await _allTracksRepository.LoadTracks();
                if (_allTracksRepository.AllTracks == null) return null;
            }
            var allTracks = _allTracksRepository.AllTracks;
            // Create a dictionary for quick lookup of track IDs from the queueTracks
            var trackIdToOrderMap = queueTracks.ToDictionary(qt => qt.TrackID, qt => qt.TrackOrder);

            // Filter tracks from the repository based on trackIds present in the queueTracks
            var filteredTracks = allTracks
                .Where(track => trackIdToOrderMap.ContainsKey(track.TrackID)) // Only tracks present in queueTracks
                .ToList();

            // Sort the filtered tracks according to their TrackOrder in queueTracks
            var sortedTracks = filteredTracks
                .OrderBy(track => trackIdToOrderMap[track.TrackID]) // Sort by TrackOrder
                .ToList();

            // Return the sorted list of TrackDisplayModel
            return sortedTracks;
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
