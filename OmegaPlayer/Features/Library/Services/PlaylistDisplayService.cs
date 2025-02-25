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

        // Constants for Favorites playlist
        public const string FAVORITES_PLAYLIST_TITLE = "Favorites";
        public const int FAVORITES_PLAYLIST_ID = -1; // Use a special ID for internal handling

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

                // Ensure the Favorites playlist exists and sync them
                await SyncFavoritesPlaylist();

                // Add Favorites playlist first
                var favoritesModel = await CreateFavoritesPlaylistModel();
                if (favoritesModel != null)
                {
                    displayModels.Add(favoritesModel);
                }

                foreach (var playlist in playlists)
                {
                    // Skip if this is the Favorites playlist (we already added it)
                    if (playlist.Title == FAVORITES_PLAYLIST_TITLE)
                        continue;

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

        public async Task<PlaylistDisplayModel> CreateFavoritesPlaylistModel()
        {
            try
            {
                // Find the actual Favorites playlist
                var playlists = await _playlistService.GetAllPlaylists();
                var favoritesPlaylist = playlists.FirstOrDefault(p =>
                    p.ProfileID == _profileManager.CurrentProfile.ProfileID &&
                    p.Title == FAVORITES_PLAYLIST_TITLE);

                if (favoritesPlaylist == null)
                    return null;

                // Create the display model
                var displayModel = new PlaylistDisplayModel
                {
                    PlaylistID = favoritesPlaylist.PlaylistID,
                    Title = FAVORITES_PLAYLIST_TITLE,
                    ProfileID = favoritesPlaylist.ProfileID,
                    CreatedAt = favoritesPlaylist.CreatedAt,
                    UpdatedAt = favoritesPlaylist.UpdatedAt,
                    IsFavoritePlaylist = true // Flag this as the special Favorites playlist
                };

                // Get the tracks for this playlist
                var tracks = await GetPlaylistTracksAsync(favoritesPlaylist.PlaylistID);

                // Set additional properties
                displayModel.TrackIDs = tracks.Select(t => t.TrackID).ToList();
                displayModel.TotalDuration = TimeSpan.FromTicks(tracks.Sum(t => t.Duration.Ticks));

                // Get cover from the first track if available
                if (tracks.Any())
                {
                    var firstTrack = tracks.First();
                    var media = await _mediaService.GetMediaById(firstTrack.CoverID);
                    if (media != null)
                    {
                        displayModel.CoverPath = media.CoverPath;
                        await LoadPlaylistCoverAsync(displayModel);
                    }
                }

                return displayModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating favorites playlist model: {ex.Message}");
                return null;
            }
        }

        public async Task<int> EnsureFavoritesPlaylist()
        {
            try
            {
                // Check if Favorites playlist already exists for this profile
                var playlists = await _playlistService.GetAllPlaylists();
                var favoritesPlaylist = playlists.FirstOrDefault(p =>
                    p.ProfileID == _profileManager.CurrentProfile.ProfileID &&
                    p.Title == FAVORITES_PLAYLIST_TITLE);

                // If it exists, return its ID
                if (favoritesPlaylist != null)
                    return favoritesPlaylist.PlaylistID;

                // Otherwise, create it
                var newPlaylist = new OmegaPlayer.Features.Playlists.Models.Playlist
                {
                    ProfileID = _profileManager.CurrentProfile.ProfileID,
                    Title = FAVORITES_PLAYLIST_TITLE,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                int playlistId = await _playlistService.AddPlaylist(newPlaylist);

                // Sync with all currently liked tracks
                await SyncFavoritesPlaylist(playlistId);

                return playlistId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error ensuring favorites playlist: {ex.Message}");
                throw;
            }
        }

        public async Task SyncFavoritesPlaylist(int? playlistId = null)
        {
            try
            {
                // Get or find the Favorites playlist ID
                int favoritesId = playlistId ?? await EnsureFavoritesPlaylist();

                // Get all liked tracks with their IDs for quick lookup
                await _allTracksRepository.LoadTracks();
                var likedTracks = _allTracksRepository.AllTracks
                    .Where(t => t.IsLiked)
                    .ToList();
                var likedTrackIds = likedTracks.Select(t => t.TrackID).ToHashSet();

                // Get current tracks in the playlist
                var existingPlaylistTracks = await _playlistTracksService.GetAllPlaylistTracksForPlaylist(favoritesId);
                var existingTrackIds = existingPlaylistTracks.Select(pt => pt.TrackID).ToHashSet();

                // Add liked tracks that aren't in the playlist yet while preserving existing order
                var updatedTracks = new List<TrackDisplayModel>();

                // First, add existing tracks in their current order (but only if they're still liked)
                foreach (var playlistTrack in existingPlaylistTracks.OrderBy(pt => pt.TrackOrder))
                {
                    // Only keep the track if it's still in the liked tracks
                    if (likedTrackIds.Contains(playlistTrack.TrackID))
                    {
                        // Find the track in the liked tracks list
                        var track = likedTracks.First(t => t.TrackID == playlistTrack.TrackID);
                        updatedTracks.Add(track);
                    }
                }

                // Then, add any new liked tracks that weren't in the playlist
                foreach (var track in likedTracks)
                {
                    if (!existingTrackIds.Contains(track.TrackID))
                    {
                        updatedTracks.Add(track);
                    }
                }

                // Update the playlist with the new track order
                await _playlistTracksService.UpdateTrackOrder(favoritesId, updatedTracks);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing favorites playlist: {ex.Message}");
            }
        }

        public async Task<List<TrackDisplayModel>> GetPlaylistTracksAsync(int playlistId)
        {
            try
            {
                // Get or find the Favorites playlist ID
                int favoritesId = await EnsureFavoritesPlaylist();
                if (favoritesId == playlistId)
                {
                    await SyncFavoritesPlaylist(playlistId);
                }

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

        // Check if a playlist is the Favorites playlist
        public bool IsFavoritesPlaylist(int playlistId)
        {
            return GetFavoritesPlaylistIdAsync().Result == playlistId;
        }

        // Get the ID of the Favorites playlist
        public async Task<int> GetFavoritesPlaylistIdAsync()
        {
            var playlists = await _playlistService.GetAllPlaylists();
            var favoritesPlaylist = playlists.FirstOrDefault(p =>
                p.ProfileID == _profileManager.CurrentProfile.ProfileID &&
                p.Title == FAVORITES_PLAYLIST_TITLE);

            return favoritesPlaylist?.PlaylistID ?? -1;
        }
    }
}