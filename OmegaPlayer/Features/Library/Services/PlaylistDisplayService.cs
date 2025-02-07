using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using OmegaPlayer.Features.Library.Models;
using OmegaPlayer.Infrastructure.Services.Cache;
using OmegaPlayer.Features.Playlists.Services;
using OmegaPlayer.Features.Library.Services;
using OmegaPlayer.Infrastructure.Data.Repositories;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Services;

namespace OmegaPlayer.Features.Library.Services
{
    public class PlaylistDisplayService
    {
        private readonly PlaylistService _playlistService;
        private readonly ImageCacheService _imageCacheService;
        private readonly TracksService _tracksService;
        private readonly MediaService _mediaService;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly PlaylistTracksService _playlistTracksService;
        private readonly ProfileManager _profileManager;
        private readonly IMessenger _messenger;

        public PlaylistDisplayService(
            PlaylistService playlistService,
            ImageCacheService imageCacheService,
            MediaService mediaService,
            AllTracksRepository allTracksRepository,
            TracksService tracksService,
            PlaylistTracksService playlistTracksService,
            ProfileManager profileManager,
            IMessenger messenger)
        {
            _playlistService = playlistService;
            _imageCacheService = imageCacheService;
            _mediaService = mediaService;
            _allTracksRepository = allTracksRepository;
            _tracksService = tracksService;
            _playlistTracksService = playlistTracksService;
            _profileManager = profileManager;
            _messenger = messenger;
        }

        public async Task<List<PlaylistDisplayModel>> GetAllPlaylistDisplaysAsync()
        {
            try
            {
                // Get all playlists
                var playlists = await _playlistService.GetAllPlaylists();
                // keep only playlists available for the current profile
                playlists = playlists.Where(p => p.ProfileID == _profileManager.CurrentProfile.ProfileID).ToList();
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
                        // Get all unique track IDs to fetch track data
                        var uniqueTrackIds = playlistTracks.Select(pt => pt.TrackID).Distinct().ToList();

                        // Get track data
                        var tracks = _allTracksRepository.AllTracks
                            .Where(t => uniqueTrackIds.Contains(t.TrackID))
                            .ToDictionary(t => t.TrackID);

                        // Create list of track IDs preserving duplicates
                        displayModel.TrackIDs = playlistTracks
                            .OrderBy(pt => pt.TrackOrder)
                            .Select(pt => pt.TrackID)
                            .ToList();

                        // Calculate total duration by summing up each track instance
                        var totalTicks = playlistTracks.Sum(pt =>
                            tracks.TryGetValue(pt.TrackID, out var track) ? track.Duration.Ticks : 0);
                        displayModel.TotalDuration = TimeSpan.FromTicks(totalTicks);

                        // Get cover from the first track
                        var firstTrackId = playlistTracks.FirstOrDefault()?.TrackID;
                        if (firstTrackId.HasValue && tracks.TryGetValue(firstTrackId.Value, out var firstTrack))
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

                // Get all unique track IDs to fetch track data
                var uniqueTrackIds = playlistTracks.Select(pt => pt.TrackID).Distinct().ToList();

                // Get the track data from repository
                var tracksData = _allTracksRepository.AllTracks
                    .Where(t => uniqueTrackIds.Contains(t.TrackID))
                    .ToDictionary(t => t.TrackID);

                // Create the final list preserving duplicates and order
                var orderedTracks = new List<TrackDisplayModel>();
                var orderedPlaylistTracks = playlistTracks.OrderBy(pt => pt.TrackOrder).ToList();

                for (int position = 0; position < orderedPlaylistTracks.Count; position++)
                {
                    var playlistTrack = orderedPlaylistTracks[position];
                    if (tracksData.TryGetValue(playlistTrack.TrackID, out var track))
                    {
                        var trackCopy = new TrackDisplayModel(_messenger, _tracksService)
                        {
                            TrackID = track.TrackID,
                            Title = track.Title,
                            AlbumID = track.AlbumID,
                            AlbumTitle = track.AlbumTitle,
                            Duration = track.Duration,
                            FilePath = track.FilePath,
                            Genre = track.Genre,
                            CoverPath = track.CoverPath,
                            ReleaseDate = track.ReleaseDate,
                            PlayCount = track.PlayCount,
                            CoverID = track.CoverID,
                            IsLiked = track.IsLiked,
                            Artists = track.Artists ?? new List<Artists>(),
                            Thumbnail = track.Thumbnail,
                            PlaylistPosition = position
                        };
                        orderedTracks.Add(trackCopy);
                    }
                }

                return orderedTracks;
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