using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Data.Repositories.Library
{
    public class TrackDisplayRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IMessenger _messenger;
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackDisplayRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IMessenger messenger,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _messenger = messenger;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<List<TrackDisplayModel>> GetAllTracksWithMetadata(int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var tracks = await context.Tracks
                        .AsNoTracking()
                        .Include(t => t.Album)
                        .Include(t => t.Cover)
                        .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
                        .GroupJoin(context.Genres, t => t.GenreId, g => g.GenreId, (t, g) => new { Track = t, Genres = g })
                        .SelectMany(x => x.Genres.DefaultIfEmpty(), (x, genre) => new { x.Track, Genre = genre })
                        .GroupJoin(context.PlayCounts.Where(pc => pc.ProfileId == profileId),
                            x => x.Track.TrackId, pc => pc.TrackId, (x, pc) => new { x.Track, x.Genre, PlayCounts = pc })
                        .SelectMany(x => x.PlayCounts.DefaultIfEmpty(), (x, pc) => new { x.Track, x.Genre, PlayCount = pc })
                        .GroupJoin(context.Likes.Where(l => l.ProfileId == profileId),
                            x => x.Track.TrackId, l => l.TrackId, (x, likes) => new { x.Track, x.Genre, x.PlayCount, Likes = likes })
                        .SelectMany(x => x.Likes.DefaultIfEmpty(), (x, like) => new TrackDisplayModel()
                        {
                            TrackID = x.Track.TrackId,
                            Title = x.Track.Title,
                            CoverID = x.Track.CoverId ?? 0,
                            AlbumTitle = x.Track.Album != null ? x.Track.Album.Title : null,
                            AlbumID = x.Track.AlbumId ?? 0,
                            Duration = x.Track.Duration ?? TimeSpan.Zero,
                            FilePath = x.Track.FilePath,
                            Lyrics = x.Track.Lyrics ?? null,
                            Genre = x.Genre != null ? x.Genre.GenreName : null,
                            CoverPath = x.Track.Cover != null ? x.Track.Cover.CoverPath : null,
                            ReleaseDate = x.Track.ReleaseDate ?? DateTime.MinValue,
                            BitRate = x.Track.Bitrate ?? 0,
                            FileType = x.Track.FileType,
                            FileCreatedDate = x.Track.CreatedAt ?? DateTime.MinValue,
                            FileModifiedDate = x.Track.UpdatedAt ?? DateTime.MinValue,
                            PlayCount = x.PlayCount != null ? x.PlayCount.Count : 0,
                            IsLiked = like != null,
                            Artists = x.Track.TrackArtists.Select(ta => new Artists
                            {
                                ArtistID = ta.Artist.ArtistId,
                                ArtistName = ta.Artist.ArtistName
                            }).ToList()
                        })
                        .ToListAsync();

                    return tracks;
                },
                $"Retrieving all tracks with metadata for profile {profileId}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.Playback,
                false
            );
        }

        public async Task<List<TrackDisplayModel>> GetTracksWithMetadataByIds(List<int> trackIds, int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Validate input
                    if (trackIds == null || !trackIds.Any())
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Empty track IDs list provided",
                            "Attempted to get tracks with an empty or null track ID list.",
                            null,
                            false);
                        return new List<TrackDisplayModel>();
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var tracks = await context.Tracks
                        .AsNoTracking()
                        .Include(t => t.Album)
                        .Include(t => t.Cover)
                        .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
                        .Where(t => trackIds.Contains(t.TrackId))
                        .GroupJoin(context.Genres, t => t.GenreId, g => g.GenreId, (t, g) => new { Track = t, Genres = g })
                        .SelectMany(x => x.Genres.DefaultIfEmpty(), (x, genre) => new { x.Track, Genre = genre })
                        .GroupJoin(context.PlayCounts.Where(pc => pc.ProfileId == profileId),
                            x => x.Track.TrackId, pc => pc.TrackId, (x, pc) => new { x.Track, x.Genre, PlayCounts = pc })
                        .SelectMany(x => x.PlayCounts.DefaultIfEmpty(), (x, pc) => new { x.Track, x.Genre, PlayCount = pc })
                        .GroupJoin(context.Likes.Where(l => l.ProfileId == profileId),
                            x => x.Track.TrackId, l => l.TrackId, (x, likes) => new { x.Track, x.Genre, x.PlayCount, Likes = likes })
                        .SelectMany(x => x.Likes.DefaultIfEmpty(), (x, like) => new TrackDisplayModel()
                        {
                            TrackID = x.Track.TrackId,
                            Title = x.Track.Title,
                            CoverID = x.Track.CoverId ?? 0,
                            AlbumTitle = x.Track.Album != null ? x.Track.Album.Title : null,
                            AlbumID = x.Track.AlbumId ?? 0,
                            Duration = x.Track.Duration ?? TimeSpan.Zero,
                            FilePath = x.Track.FilePath,
                            Lyrics = x.Track.Lyrics ?? null,
                            Genre = x.Genre != null ? x.Genre.GenreName : null,
                            CoverPath = x.Track.Cover != null ? x.Track.Cover.CoverPath : null,
                            ReleaseDate = x.Track.ReleaseDate ?? DateTime.MinValue,
                            BitRate = x.Track.Bitrate ?? 0,
                            FileType = x.Track.FileType,
                            FileCreatedDate = x.Track.CreatedAt ?? DateTime.MinValue,
                            FileModifiedDate = x.Track.UpdatedAt ?? DateTime.MinValue,
                            PlayCount = x.PlayCount != null ? x.PlayCount.Count : 0,
                            IsLiked = like != null,
                            Artists = x.Track.TrackArtists.Select(ta => new Artists
                            {
                                ArtistID = ta.Artist.ArtistId,
                                ArtistName = ta.Artist.ArtistName
                            }).ToList()
                        })
                        .ToListAsync();

                    return tracks;
                },
                $"Getting tracks by IDs ({trackIds?.Count ?? 0} tracks) for profile {profileId}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.Playback,
                false
            );
        }

        /// <summary>
        /// Gets tracks with metadata for a specific album
        /// </summary>
        public async Task<List<TrackDisplayModel>> GetTracksByAlbumId(int albumId, int profileId)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var tracks = await context.Tracks
                        .AsNoTracking()
                        .Include(t => t.Album)
                        .Include(t => t.Cover)
                        .Include(t => t.TrackArtists).ThenInclude(ta => ta.Artist)
                        .Where(t => t.AlbumId == albumId)
                        .OrderBy(t => t.TrackNumber)
                        .ThenBy(t => t.Title)
                        .GroupJoin(context.Genres, t => t.GenreId, g => g.GenreId, (t, g) => new { Track = t, Genres = g })
                        .SelectMany(x => x.Genres.DefaultIfEmpty(), (x, genre) => new { x.Track, Genre = genre })
                        .GroupJoin(context.PlayCounts.Where(pc => pc.ProfileId == profileId),
                            x => x.Track.TrackId, pc => pc.TrackId, (x, pc) => new { x.Track, x.Genre, PlayCounts = pc })
                        .SelectMany(x => x.PlayCounts.DefaultIfEmpty(), (x, pc) => new { x.Track, x.Genre, PlayCount = pc })
                        .GroupJoin(context.Likes.Where(l => l.ProfileId == profileId),
                            x => x.Track.TrackId, l => l.TrackId, (x, likes) => new { x.Track, x.Genre, x.PlayCount, Likes = likes })
                        .SelectMany(x => x.Likes.DefaultIfEmpty(), (x, like) => new TrackDisplayModel()
                        {
                            TrackID = x.Track.TrackId,
                            Title = x.Track.Title,
                            CoverID = x.Track.CoverId ?? 0,
                            AlbumTitle = x.Track.Album != null ? x.Track.Album.Title : null,
                            AlbumID = x.Track.AlbumId ?? 0,
                            Duration = x.Track.Duration ?? TimeSpan.Zero,
                            FilePath = x.Track.FilePath,
                            Lyrics = x.Track.Lyrics ?? null,
                            Genre = x.Genre != null ? x.Genre.GenreName : null,
                            CoverPath = x.Track.Cover != null ? x.Track.Cover.CoverPath : null,
                            ReleaseDate = x.Track.ReleaseDate ?? DateTime.MinValue,
                            BitRate = x.Track.Bitrate ?? 0,
                            FileType = x.Track.FileType,
                            FileCreatedDate = x.Track.CreatedAt ?? DateTime.MinValue,
                            FileModifiedDate = x.Track.UpdatedAt ?? DateTime.MinValue,
                            PlayCount = x.PlayCount != null ? x.PlayCount.Count : 0,
                            IsLiked = like != null,
                            Artists = x.Track.TrackArtists.Select(ta => new Artists
                            {
                                ArtistID = ta.Artist.ArtistId,
                                ArtistName = ta.Artist.ArtistName
                            }).ToList()
                        })
                        .ToListAsync();

                    return tracks;
                },
                $"Getting tracks for album {albumId}, profile {profileId}",
                new List<TrackDisplayModel>(),
                ErrorSeverity.Playback,
                false
            );
        }
    }
}