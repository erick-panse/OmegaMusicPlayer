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

        public async Task<List<PlaylistDisplayModel>> GetPlaylistsPageAsync(int pageNumber, int pageSize)
        {
            // Get a page of playlists from the repository
            var playlists = await _playlistService.GetAllPlaylists();
            var displayModels = new List<PlaylistDisplayModel>();

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

                // Get all tracks for this playlist
                var playlistTracks = await _playlistTracksService.GetPlaylistTrack(playlist.PlaylistID);
                if (playlistTracks != null)
                {
                    // Get the corresponding track displays
                    var tracks = _allTracksRepository.AllTracks
                        .Where(t => t.TrackID == playlistTracks.TrackID)
                        .ToList();

                    displayModel.TrackIDs = tracks.Select(t => t.TrackID).ToList();
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

                displayModels.Add(displayModel);
            }

            return displayModels
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public async Task<List<TrackDisplayModel>> GetPlaylistTracksAsync(int playlistId)
        {
            var playlistTrack = await _playlistTracksService.GetPlaylistTrack(playlistId);
            if (playlistTrack == null) return new List<TrackDisplayModel>();

            return _allTracksRepository.AllTracks
                .Where(t => t.TrackID == playlistTrack.TrackID)
                .ToList();
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