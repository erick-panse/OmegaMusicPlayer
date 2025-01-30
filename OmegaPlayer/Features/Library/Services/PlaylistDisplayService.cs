using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Services.Cache;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;

namespace OmegaPlayer.Features.Library.Services
{
    public class PlaylistDisplayService
    {
        private readonly PlaylistService _playlistService;
        private readonly ImageCacheService _imageCacheService;
        private readonly MediaService _mediaService;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly PlaylistTracksService _playlistTracksService;

        public PlaylistDisplayService(
            PlaylistService playlistService,
            ImageCacheService imageCacheService,
            MediaService mediaService,
            AllTracksRepository allTracksRepository,
            PlaylistTracksService playlistTracksService)
        {
            _playlistService = playlistService;
            _imageCacheService = imageCacheService;
            _mediaService = mediaService;
            _allTracksRepository = allTracksRepository;
            _playlistTracksService = playlistTracksService;
        }

        public async Task<List<PlaylistDisplayModel>> GetAllPlaylistDisplaysAsync()
        {
            try
            {
                // Get all playlists
                var playlists = await _playlistService.GetAllPlaylists();
                var displayModels = new List<PlaylistDisplayModel>();

                // Get all playlist tracks for all playlists
                var allPlaylistTracks = await _playlistTracksService.GetAllPlaylistTracks();
                var tracksByPlaylist = allPlaylistTracks.GroupBy(pt => pt.PlaylistID)
                    .ToDictionary(g => g.Key, g => g.ToList());

                foreach (var playlist in playlists)
                {
                    var displayModel = new PlaylistDisplayModel
                    {
                        PlaylistID = playlist.PlaylistID,
                        Title = playlist.Title,
                        ProfileID = playlist.ProfileID,
                        CreatedAt = playlist.CreatedAt,
                        UpdatedAt = playlist.UpdatedAt,
                    };

                    // Get tracks for this playlist
                    if (tracksByPlaylist.TryGetValue(playlist.PlaylistID, out var playlistTracks))
                    {
                        var trackIds = playlistTracks.Select(pt => pt.TrackID).ToList();
                        var tracks = _allTracksRepository.AllTracks
                            .Where(t => trackIds.Contains(t.TrackID))
                            .OrderBy(t => playlistTracks.First(pt => pt.TrackID == t.TrackID).TrackOrder)
                            .ToList();

                        displayModel.TrackIDs = trackIds;
                        displayModel.TotalDuration = TimeSpan.FromTicks(tracks.Sum(t => t.Duration.Ticks));

                        // Use the first track's cover for the playlist if available
                        var firstTrack = tracks.FirstOrDefault();
                        if (firstTrack != null)
                        {
                            var media = await _mediaService.GetMediaById(firstTrack.CoverID);
                            if (media != null)
                            {
                                displayModel.CoverPath = media.CoverPath;
                            }
                        }
                    }
                    else
                    {
                        // Initialize empty playlist
                        displayModel.TrackIDs = new List<int>();
                        displayModel.TotalDuration = TimeSpan.Zero;
                    }

                    displayModels.Add(displayModel);
                }

                return displayModels;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting playlist displays: {ex.Message}");
                throw;
            }
        }


        public async Task<List<TrackDisplayModel>> GetPlaylistTracksAsync(int playlistId)
        {
            try
            {
                var playlistTracks = await _playlistTracksService.GetAllPlaylistTracksForPlaylist(playlistId);
                if (!playlistTracks.Any()) return new List<TrackDisplayModel>();

                var trackIds = playlistTracks.Select(pt => pt.TrackID).ToList();

                return _allTracksRepository.AllTracks
                    .Where(t => trackIds.Contains(t.TrackID))
                    .OrderBy(t => playlistTracks.First(pt => pt.TrackID == t.TrackID).TrackOrder)
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting playlist tracks: {ex.Message}");
                throw;
            }
        }


        public async Task LoadPlaylistCoverAsync(PlaylistDisplayModel playlist)
        {
            if (!string.IsNullOrEmpty(playlist.CoverPath))
            {
                try
                {
                    playlist.Cover = await _imageCacheService.LoadThumbnailAsync(playlist.CoverPath, 110, 110);
                    playlist.CoverSize = "low";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading playlist cover: {ex.Message}");
                }
            }
        }

        public async Task LoadHighResPlaylistCoverAsync(PlaylistDisplayModel playlist)
        {
            if (!string.IsNullOrEmpty(playlist.CoverPath))
            {
                try
                {
                    playlist.Cover = await _imageCacheService.LoadThumbnailAsync(playlist.CoverPath, 160, 160);
                    playlist.CoverSize = "high";
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading high-res playlist cover: {ex.Message}");
                }
            }
        }
    }
}