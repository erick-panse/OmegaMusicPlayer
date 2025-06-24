using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TracksRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public TracksRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Tracks> GetTrackById(int trackID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var track = await context.Tracks
                        .AsNoTracking()
                        .Where(t => t.TrackId == trackID)
                        .Select(t => new Tracks
                        {
                            TrackID = t.TrackId,
                            Title = t.Title,
                            AlbumID = t.AlbumId ?? 0,
                            Duration = t.Duration ?? TimeSpan.Zero,
                            ReleaseDate = t.ReleaseDate ?? DateTime.MinValue,
                            TrackNumber = t.TrackNumber ?? 0,
                            FilePath = t.FilePath,
                            Lyrics = t.Lyrics,
                            BitRate = t.Bitrate ?? 0,
                            FileSize = t.FileSize ?? 0,
                            FileType = t.FileType,
                            CreatedAt = t.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = t.UpdatedAt ?? DateTime.MinValue,
                            CoverID = t.CoverId ?? 0,
                            GenreID = t.GenreId ?? 0
                        })
                        .FirstOrDefaultAsync();

                    return track;
                },
                $"Database operation: Get track with ID {trackID}",
                null,
                ErrorSeverity.Playback,
                false);
        }

        public async Task<Tracks> GetTrackByPath(string filePath)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(filePath))
                    {
                        return null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var track = await context.Tracks
                        .AsNoTracking()
                        .Where(t => t.FilePath == filePath)
                        .Select(t => new Tracks
                        {
                            TrackID = t.TrackId,
                            Title = t.Title,
                            AlbumID = t.AlbumId ?? 0,
                            Duration = t.Duration ?? TimeSpan.Zero,
                            ReleaseDate = t.ReleaseDate ?? DateTime.MinValue,
                            TrackNumber = t.TrackNumber ?? 0,
                            FilePath = t.FilePath,
                            Lyrics = t.Lyrics,
                            BitRate = t.Bitrate ?? 0,
                            FileSize = t.FileSize ?? 0,
                            FileType = t.FileType,
                            CreatedAt = t.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = t.UpdatedAt ?? DateTime.MinValue,
                            CoverID = t.CoverId ?? 0,
                            GenreID = t.GenreId ?? 0
                        })
                        .FirstOrDefaultAsync();

                    return track;
                },
                $"Database operation: Get track by path {Path.GetFileName(filePath)}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Tracks>> GetAllTracks()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var tracks = await context.Tracks
                        .AsNoTracking()
                        .OrderBy(t => t.Title)
                        .Select(t => new Tracks
                        {
                            TrackID = t.TrackId,
                            Title = t.Title,
                            AlbumID = t.AlbumId ?? 0,
                            Duration = t.Duration ?? TimeSpan.Zero,
                            ReleaseDate = t.ReleaseDate ?? DateTime.MinValue,
                            TrackNumber = t.TrackNumber ?? 0,
                            FilePath = t.FilePath,
                            Lyrics = t.Lyrics,
                            BitRate = t.Bitrate ?? 0,
                            FileSize = t.FileSize ?? 0,
                            FileType = t.FileType,
                            CreatedAt = t.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = t.UpdatedAt ?? DateTime.MinValue,
                            CoverID = t.CoverId ?? 0,
                            GenreID = t.GenreId ?? 0
                        })
                        .ToListAsync();

                    return tracks;
                },
                "Database operation: Get all tracks",
                new List<Tracks>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> AddTrack(Tracks track)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null)
                    {
                        throw new ArgumentNullException(nameof(track), "Cannot add null track to database");
                    }

                    // Validate essential track properties
                    if (string.IsNullOrEmpty(track.FilePath))
                    {
                        throw new ArgumentException("Track must have a file path", nameof(track));
                    }

                    using var context = _contextFactory.CreateDbContext();

                    // Ensure required fields have default values
                    var title = track.Title ?? Path.GetFileNameWithoutExtension(track.FilePath);
                    var fileType = track.FileType ?? Path.GetExtension(track.FilePath)?.TrimStart('.');
                    var createdAt = track.CreatedAt != default ? track.CreatedAt : DateTime.UtcNow;

                    var newTrack = new Infrastructure.Data.Entities.Track
                    {
                        Title = title,
                        AlbumId = track.AlbumID > 0 ? track.AlbumID : null,
                        Duration = track.Duration.Ticks > 0 ? track.Duration : null,
                        ReleaseDate = track.ReleaseDate != default ? track.ReleaseDate : DateTime.MinValue,
                        TrackNumber = track.TrackNumber,
                        FilePath = track.FilePath,
                        Lyrics = track.Lyrics,
                        Bitrate = track.BitRate,
                        FileSize = track.FileSize,
                        FileType = fileType,
                        CreatedAt = createdAt,
                        UpdatedAt = DateTime.UtcNow,
                        CoverId = track.CoverID > 0 ? track.CoverID : null,
                        GenreId = track.GenreID > 0 ? track.GenreID : null
                    };

                    context.Tracks.Add(newTrack);
                    await context.SaveChangesAsync();

                    return newTrack.TrackId;
                },
                $"Database operation: Add track {track?.Title ?? "Unknown"}",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateTrack(Tracks track)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (track == null || track.TrackID <= 0)
                    {
                        throw new ArgumentException("Cannot update null track or track with invalid ID", nameof(track));
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var existingTrack = await context.Tracks
                        .Where(t => t.TrackId == track.TrackID)
                        .FirstOrDefaultAsync();

                    if (existingTrack == null)
                    {
                        throw new InvalidOperationException($"Track with ID {track.TrackID} not found");
                    }

                    // Ensure required fields have default values
                    var title = track.Title ?? Path.GetFileNameWithoutExtension(track.FilePath);
                    var fileType = track.FileType ?? Path.GetExtension(track.FilePath)?.TrimStart('.');

                    existingTrack.Title = title;
                    existingTrack.AlbumId = track.AlbumID > 0 ? track.AlbumID : null;
                    existingTrack.Duration = track.Duration.Ticks > 0 ? track.Duration : null;
                    existingTrack.ReleaseDate = track.ReleaseDate != default ? track.ReleaseDate : DateTime.MinValue;
                    existingTrack.TrackNumber = track.TrackNumber;
                    existingTrack.FilePath = track.FilePath;
                    existingTrack.Lyrics = track.Lyrics;
                    existingTrack.Bitrate = track.BitRate;
                    existingTrack.FileSize = track.FileSize;
                    existingTrack.FileType = fileType;
                    existingTrack.UpdatedAt = DateTime.UtcNow;
                    existingTrack.CoverId = track.CoverID > 0 ? track.CoverID : null;
                    existingTrack.GenreId = track.GenreID > 0 ? track.GenreID : null;

                    await context.SaveChangesAsync();
                },
                $"Database operation: Update track {track?.Title ?? "Unknown"} (ID: {track?.TrackID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteTrack(int trackID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0)
                    {
                        throw new ArgumentException("Cannot delete track with invalid ID", nameof(trackID));
                    }

                    using var context = _contextFactory.CreateDbContext();
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        // First delete any associated data that might cause foreign key constraints
                        await DeleteTrackRelationships(context, trackID);

                        // Then delete the track
                        await context.Tracks
                            .Where(t => t.TrackId == trackID)
                            .ExecuteDeleteAsync();

                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                },
                $"Database operation: Delete track with ID {trackID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task DeleteTrackRelationships(OmegaPlayerDbContext context, int trackID)
        {
            // Delete track-artist relationships
            await context.TrackArtists
                .Where(ta => ta.TrackId == trackID)
                .ExecuteDeleteAsync();

            // Delete track-genre relationships
            await context.TrackGenres
                .Where(tg => tg.TrackId == trackID)
                .ExecuteDeleteAsync();

            // Delete playlist-track relationships if they exist
            await context.PlaylistTracks
                .Where(pt => pt.TrackId == trackID)
                .ExecuteDeleteAsync();

            // Delete from play history if exists
            await context.PlayHistories
                .Where(ph => ph.TrackId == trackID)
                .ExecuteDeleteAsync();

            // Delete from queue if exists
            await context.QueueTracks
                .Where(qt => qt.TrackId == trackID)
                .ExecuteDeleteAsync();

            // Delete from play counts if exists
            await context.PlayCounts
                .Where(pc => pc.TrackId == trackID)
                .ExecuteDeleteAsync();

            // Delete from likes if exists
            await context.Likes
                .Where(l => l.TrackId == trackID)
                .ExecuteDeleteAsync();
        }
    }
}