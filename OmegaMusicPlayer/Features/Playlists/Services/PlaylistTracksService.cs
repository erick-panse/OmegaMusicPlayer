using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaMusicPlayer.Features.Playlists.Models;
using OmegaMusicPlayer.Infrastructure.Data.Repositories.Playlists;
using OmegaMusicPlayer.Features.Library.Models;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Enums;

namespace OmegaMusicPlayer.Features.Playlists.Services
{
    public class PlaylistTracksService
    {
        private readonly PlaylistTracksRepository _playlistTracksRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        public PlaylistTracksService(
            PlaylistTracksRepository playlistTracksRepository,
            IErrorHandlingService errorHandlingService)
        {
            _playlistTracksRepository = playlistTracksRepository;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<PlaylistTracks>> GetAllPlaylistTracksForPlaylist(int playlistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistID <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid playlist ID",
                            $"Attempted to get tracks for invalid playlist ID: {playlistID}",
                            null,
                            false);
                        return new List<PlaylistTracks>();
                    }

                    return await _playlistTracksRepository.GetPlaylistTrack(playlistID);
                },
                $"Getting tracks for playlist {playlistID}",
                new List<PlaylistTracks>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<PlaylistTracks>> GetAllPlaylistTracks()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () => await _playlistTracksRepository.GetAllPlaylistTracks(),
                "Getting all playlist tracks",
                new List<PlaylistTracks>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task AddPlaylistTrack(PlaylistTracks playlistTrack)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistTrack == null)
                    {
                        throw new ArgumentNullException(nameof(playlistTrack), "Cannot add null playlist track");
                    }

                    if (playlistTrack.PlaylistID <= 0)
                    {
                        throw new ArgumentException("Invalid playlist ID", nameof(playlistTrack));
                    }

                    if (playlistTrack.TrackID <= 0)
                    {
                        throw new ArgumentException("Invalid track ID", nameof(playlistTrack));
                    }

                    await _playlistTracksRepository.AddPlaylistTrack(playlistTrack);
                },
                $"Adding track {playlistTrack?.TrackID ?? 0} to playlist {playlistTrack?.PlaylistID ?? 0}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateTrackOrder(int playlistId, List<TrackDisplayModel> tracks)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistId <= 0)
                    {
                        throw new ArgumentException("Invalid playlist ID", nameof(playlistId));
                    }

                    if (tracks == null)
                    {
                        throw new ArgumentNullException(nameof(tracks), "Cannot update track order with null tracks list");
                    }

                    // First remove all existing tracks
                    await DeletePlaylistTrack(playlistId);

                    // Add tracks with new order
                    for (int i = 0; i < tracks.Count; i++)
                    {
                        var track = tracks[i];
                        await AddPlaylistTrack(new PlaylistTracks
                        {
                            PlaylistID = playlistId,
                            TrackID = track.TrackID,
                            TrackOrder = i
                        });
                    }
                },
                $"Updating track order for playlist {playlistId} with {tracks?.Count ?? 0} tracks",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeletePlaylistTrack(int playlistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistID <= 0)
                    {
                        throw new ArgumentException("Invalid playlist ID", nameof(playlistID));
                    }

                    await _playlistTracksRepository.DeletePlaylistTrack(playlistID);
                },
                $"Deleting all tracks from playlist {playlistID}",
                ErrorSeverity.NonCritical,
                false);
        }
    }
}