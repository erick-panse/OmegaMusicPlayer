using Microsoft.EntityFrameworkCore;
using Playlist = OmegaPlayer.Features.Playlists.Models.Playlist;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Linq;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Playlists
{
    public class PlaylistRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public PlaylistRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Playlist> GetPlaylistById(int playlistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistID <= 0)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Invalid playlist ID",
                            $"Attempted to get playlist with invalid ID: {playlistID}",
                            null,
                            false);
                        return null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var playlist = await context.Playlists
                        .AsNoTracking()
                        .Where(p => p.PlaylistId == playlistID)
                        .Select(p => new Playlist
                        {
                            PlaylistID = p.PlaylistId,
                            ProfileID = p.ProfileId,
                            Title = p.Title,
                            CreatedAt = p.CreatedAt,
                            UpdatedAt = p.UpdatedAt
                        })
                        .FirstOrDefaultAsync();

                    return playlist;
                },
                $"Getting playlist with ID {playlistID}",
                null,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<List<Playlist>> GetAllPlaylists()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var playlists = await context.Playlists
                        .AsNoTracking()
                        .Select(p => new Playlist
                        {
                            PlaylistID = p.PlaylistId,
                            ProfileID = p.ProfileId,
                            Title = p.Title,
                            CreatedAt = p.CreatedAt,
                            UpdatedAt = p.UpdatedAt
                        })
                        .ToListAsync();

                    return playlists;
                },
                "Getting all playlists",
                new List<Playlist>(),
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task<int> AddPlaylist(Playlist playlist)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlist == null)
                    {
                        throw new ArgumentNullException(nameof(playlist), "Cannot add null playlist");
                    }

                    if (playlist.ProfileID <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(playlist));
                    }

                    if (string.IsNullOrWhiteSpace(playlist.Title))
                    {
                        playlist.Title = "Untitled Playlist";
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var newPlaylist = new Infrastructure.Data.Entities.Playlist
                    {
                        ProfileId = playlist.ProfileID,
                        Title = playlist.Title,
                        CreatedAt = playlist.CreatedAt,
                        UpdatedAt = playlist.UpdatedAt
                    };

                    context.Playlists.Add(newPlaylist);
                    await context.SaveChangesAsync();

                    return newPlaylist.PlaylistId;
                },
                $"Adding playlist '{playlist?.Title ?? "Unknown"}' for profile {playlist?.ProfileID ?? 0}",
                -1,
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task UpdatePlaylist(Playlist playlist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlist == null)
                    {
                        throw new ArgumentNullException(nameof(playlist), "Cannot update null playlist");
                    }

                    if (playlist.PlaylistID <= 0)
                    {
                        throw new ArgumentException("Invalid playlist ID", nameof(playlist));
                    }

                    if (playlist.ProfileID <= 0)
                    {
                        throw new ArgumentException("Invalid profile ID", nameof(playlist));
                    }

                    if (string.IsNullOrWhiteSpace(playlist.Title))
                    {
                        playlist.Title = "Untitled Playlist";
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var existingPlaylist = await context.Playlists
                        .Where(p => p.PlaylistId == playlist.PlaylistID)
                        .FirstOrDefaultAsync();

                    if (existingPlaylist == null)
                    {
                        throw new InvalidOperationException($"Playlist with ID {playlist.PlaylistID} not found");
                    }

                    existingPlaylist.ProfileId = playlist.ProfileID;
                    existingPlaylist.Title = playlist.Title;
                    existingPlaylist.UpdatedAt = playlist.UpdatedAt;

                    await context.SaveChangesAsync();
                },
                $"Updating playlist '{playlist?.Title ?? "Unknown"}' (ID: {playlist?.PlaylistID ?? 0})",
                ErrorSeverity.NonCritical,
                false
            );
        }

        public async Task DeletePlaylist(int playlistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (playlistID <= 0)
                    {
                        throw new ArgumentException("Invalid playlist ID", nameof(playlistID));
                    }

                    using var context = _contextFactory.CreateDbContext();

                    await context.Playlists
                        .Where(p => p.PlaylistId == playlistID)
                        .ExecuteDeleteAsync();
                },
                $"Deleting playlist with ID {playlistID}",
                ErrorSeverity.NonCritical,
                false
            );
        }
    }
}