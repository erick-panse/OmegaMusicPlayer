using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Services;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Features.Playlists.Services;
using OmegaMusicPlayer.Infrastructure.Data.Repositories;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.Infrastructure.Services.Images;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Features.Library.Services
{
    public class PlaylistDisplayService
    {
        private readonly PlaylistService _playlistService;
        private readonly MediaService _mediaService;
        private readonly AllTracksRepository _allTracksRepository;
        private readonly PlaylistTracksService _playlistTracksService;
        private readonly ProfileManager _profileManager;
        private readonly StandardImageService _standardImageService;
        private readonly LocalizationService _localizationService;
        private readonly IErrorHandlingService _errorHandlingService;

        // Constants for Favorites playlist
        public string FavoritesPlaylistTitle;
        public const int FAVORITES_PLAYLIST_ID = -1; // Use a special ID for internal handling

        public PlaylistDisplayService(
            PlaylistService playlistService,
            MediaService mediaService,
            AllTracksRepository allTracksRepository,
            PlaylistTracksService playlistTracksService,
            ProfileManager profileManager,
            StandardImageService standardImageService,
            LocalizationService localizationService,
            IErrorHandlingService errorHandlingService)
        {
            _playlistService = playlistService;
            _mediaService = mediaService;
            _allTracksRepository = allTracksRepository;
            _playlistTracksService = playlistTracksService;
            _profileManager = profileManager;
            _standardImageService = standardImageService;
            _localizationService = localizationService;
            _errorHandlingService = errorHandlingService;

            FavoritesPlaylistTitle = _playlistService.GetFavoriteTitle();
        }

        private string GetLocalizedFavoritesName()
        {
            return _localizationService["Favorites"];
        }

        public async Task<List<PlaylistDisplayModel>> GetAllPlaylistDisplaysAsync()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Get all playlists
                    var playlists = await _playlistService.GetAllPlaylists();

                    // Ensure we have a valid profile
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    if (profile == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No active profile when loading playlists",
                            "Attempted to load playlists but no profile is currently active",
                            null,
                            false);
                        return new List<PlaylistDisplayModel>();
                    }

                    // keep only playlists available for the current profile
                    playlists = playlists.Where(p => p.ProfileID == profile.ProfileID).ToList();
                    var displayModels = new List<PlaylistDisplayModel>();

                    // Get all playlist tracks for all playlists
                    var allPlaylistTracks = await _playlistTracksService.GetAllPlaylistTracks();
                    var tracksByPlaylist = allPlaylistTracks.GroupBy(pt => pt.PlaylistID)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    // Ensure the Favorites playlist exists and sync them
                    await EnsureFavoritesPlaylist();

                    // Add Favorites playlist first
                    var favoritesModel = await CreateFavoritesPlaylistModel();
                    if (favoritesModel != null)
                    {
                        displayModels.Add(favoritesModel);
                    }

                    foreach (var playlist in playlists)
                    {
                        // Skip if this is the Favorites playlist
                        if (playlist.Title == FavoritesPlaylistTitle)
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
                },
                "Getting all playlist displays",
                new List<PlaylistDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<PlaylistDisplayModel> CreateFavoritesPlaylistModel()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    if (profile == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No active profile when creating favorites playlist",
                            "Attempted to create favorites playlist but no profile is currently active",
                            null,
                            false);
                        return null;
                    }

                    // Find the actual Favorites playlist using system identifier
                    var playlists = await _playlistService.GetAllPlaylists();
                    var favoritesPlaylist = playlists.FirstOrDefault(p =>
                        p.ProfileID == profile.ProfileID &&
                        p.Title == FavoritesPlaylistTitle); // Use system identifier

                    if (favoritesPlaylist == null)
                        return null;

                    // Create the display model with localized title
                    var displayModel = new PlaylistDisplayModel
                    {
                        PlaylistID = favoritesPlaylist.PlaylistID,
                        Title = GetLocalizedFavoritesName(), // Display localized name
                        ProfileID = favoritesPlaylist.ProfileID,
                        CreatedAt = favoritesPlaylist.CreatedAt,
                        UpdatedAt = favoritesPlaylist.UpdatedAt,
                        IsFavoritePlaylist = true // Flag this as the special Favorites playlist
                    };

                    // Get tracks and set other properties
                    var tracks = await GetPlaylistTracksAsync(favoritesPlaylist.PlaylistID);
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
                        }
                    }

                    return displayModel;
                },
                "Creating favorites playlist model",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<int> EnsureFavoritesPlaylist()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    if (profile == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No active profile when ensuring favorites playlist",
                            "Attempted to ensure favorites playlist but no profile is currently active",
                            null,
                            false);
                        return -1;
                    }

                    // Check if Favorites playlist already exists for this profile
                    var playlists = await _playlistService.GetAllPlaylists();
                    var favoritesPlaylist = playlists.FirstOrDefault(p =>
                        p.ProfileID == profile.ProfileID &&
                        p.Title == FavoritesPlaylistTitle);

                    // If it exists, return its ID
                    if (favoritesPlaylist != null)
                        return favoritesPlaylist.PlaylistID;

                    // Otherwise, create it
                    var newPlaylist = new OmegaMusicPlayer.Features.Playlists.Models.Playlist
                    {
                        ProfileID = profile.ProfileID,
                        Title = FavoritesPlaylistTitle, // Store system identifier
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    int playlistId = await _playlistService.AddPlaylist(newPlaylist, profile.ProfileID, isSystemCreated: true);
                    await SyncFavoritesPlaylist(playlistId);

                    return playlistId;
                },
                "Ensuring favorites playlist exists",
                -1,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task SyncFavoritesPlaylist(int? playlistId = null)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Ensure we have a valid profile
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    if (profile == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No active profile when syncing favorites playlist",
                            "Attempted to sync favorites playlist but no profile is currently active",
                            null,
                            false);
                        return;
                    }

                    // Get or find the Favorites playlist ID
                    int favoritesId = playlistId ?? await EnsureFavoritesPlaylist();
                    if (favoritesId < 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to get favorites playlist ID",
                            "Could not obtain valid favorites playlist ID for syncing",
                            null,
                            false);
                        return;
                    }

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

                    // Only update if there are changes
                    var updatedTracksIds = updatedTracks.Select(ut => ut.TrackID).ToHashSet();
                    if (!existingTrackIds.SequenceEqual(updatedTracksIds))
                    {
                        // Update the playlist with the new track order
                        await _playlistTracksService.UpdateTrackOrder(favoritesId, updatedTracks);
                    }
                },
                "Syncing favorites playlist",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<TrackDisplayModel>> GetPlaylistTracksAsync(int playlistId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistId <= 0 && playlistId != FAVORITES_PLAYLIST_ID)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid playlist ID provided",
                            $"Attempted to get tracks for invalid playlist ID: {playlistId}",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

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
                            var trackCopy = new TrackDisplayModel()
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
                },
                $"Getting tracks for playlist {playlistId}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        // Check if a playlist is the Favorites playlist
        public async Task<bool> IsFavoritesPlaylistAsync(int playlistId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var favoritesId = await GetFavoritesPlaylistIdAsync();
                    return favoritesId == playlistId;
                },
                $"Checking if playlist {playlistId} is the favorites playlist",
                false,
                ErrorSeverity.NonCritical,
                false
            );
        }

        // Get the ID of the Favorites playlist
        public async Task<int> GetFavoritesPlaylistIdAsync()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var profile = await _profileManager.GetCurrentProfileAsync();
                    if (profile == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "No active profile when getting favorites playlist ID",
                            "Attempted to get favorites playlist ID but no profile is currently active",
                            null,
                            false);
                        return -1;
                    }

                    var playlists = await _playlistService.GetAllPlaylists();
                    var favoritesPlaylist = playlists.FirstOrDefault(p =>
                        p.ProfileID == profile.ProfileID &&
                        p.Title == FavoritesPlaylistTitle);

                    return favoritesPlaylist?.PlaylistID ?? -1;
                },
                "Getting favorites playlist ID",
                -1,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task LoadPlaylistCoverAsync(PlaylistDisplayModel playlist, string size = "low", bool isVisible = false)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlist == null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Null playlist provided",
                            "Attempted to load cover for a null playlist object",
                            null,
                            false);
                        return;
                    }

                    if (string.IsNullOrEmpty(playlist.CoverPath)) return;

                    switch (size.ToLower())
                    {
                        case "low":
                            playlist.Cover = await _standardImageService.LoadLowQualityAsync(playlist.CoverPath, isVisible);
                            break;
                        case "medium":
                            playlist.Cover = await _standardImageService.LoadMediumQualityAsync(playlist.CoverPath, isVisible);
                            break;
                        case "high":
                            playlist.Cover = await _standardImageService.LoadHighQualityAsync(playlist.CoverPath, isVisible);
                            break;
                        default:
                            playlist.Cover = await _standardImageService.LoadLowQualityAsync(playlist.CoverPath, isVisible);
                            break;
                    }
                    playlist.CoverSize = size;

                },
                $"Loading playlist cover for '{playlist?.Title ?? "Unknown"}' (quality: {size})",
                ErrorSeverity.NonCritical,
                false
            );
        }

        /// <summary>
        /// Loads playlist cover asynchronously only if it's visible (optimized version)
        /// </summary>
        public async Task LoadPlaylistCoverIfVisibleAsync(PlaylistDisplayModel playlist, bool isVisible, string size = "low")
        {
            // Only load if the playlist is actually visible
            if (!isVisible)
            {
                // Still notify the service about the visibility state for cache management
                if (!string.IsNullOrEmpty(playlist?.CoverPath))
                {
                    await _standardImageService.NotifyImageVisible(playlist.CoverPath, false);
                }
                return;
            }

            await LoadPlaylistCoverAsync(playlist, size, isVisible);
        }

    }
}