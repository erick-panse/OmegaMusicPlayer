using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Features.Playlists.Models;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Linq;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playlists
{
    public class PlaylistTracksRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public PlaylistTracksRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<PlaylistTracks>> GetPlaylistTrack(int playlistID)
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

                    using var context = _contextFactory.CreateDbContext();

                    var playlistTracks = await context.PlaylistTracks
                        .AsNoTracking()
                        .Where(pt => pt.PlaylistId == playlistID)
                        .OrderBy(pt => pt.TrackOrder)
                        .Select(pt => new PlaylistTracks
                        {
                            PlaylistID = pt.PlaylistId,
                            TrackID = pt.TrackId,
                            TrackOrder = pt.TrackOrder
                        })
                        .ToListAsync();

                    return playlistTracks;
                },
                $"Getting tracks for playlist {playlistID}",
                new List<PlaylistTracks>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<PlaylistTracks>> GetAllPlaylistTracks()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var playlistTracks = await context.PlaylistTracks
                        .AsNoTracking()
                        .OrderBy(pt => pt.PlaylistId)
                        .ThenBy(pt => pt.TrackOrder)
                        .Select(pt => new PlaylistTracks
                        {
                            PlaylistID = pt.PlaylistId,
                            TrackID = pt.TrackId,
                            TrackOrder = pt.TrackOrder
                        })
                        .ToListAsync();

                    return playlistTracks;
                },
                "Getting all playlist tracks",
                new List<PlaylistTracks>(),
                ErrorSeverity.NonCritical,
                false
            );
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

                    using var context = _contextFactory.CreateDbContext();

                    var newPlaylistTrack = new Infrastructure.Data.Entities.PlaylistTrack
                    {
                        PlaylistId = playlistTrack.PlaylistID,
                        TrackId = playlistTrack.TrackID,
                        TrackOrder = playlistTrack.TrackOrder
                    };

                    context.PlaylistTracks.Add(newPlaylistTrack);
                    await context.SaveChangesAsync();
                },
                $"Adding track {playlistTrack?.TrackID ?? 0} to playlist {playlistTrack?.PlaylistID ?? 0}",
                ErrorSeverity.NonCritical,
                false
            );
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

                    using var context = _contextFactory.CreateDbContext();

                    await context.PlaylistTracks
                        .Where(pt => pt.PlaylistId == playlistID)
                        .ExecuteDeleteAsync();
                },
                $"Deleting all tracks from playlist {playlistID}",
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}