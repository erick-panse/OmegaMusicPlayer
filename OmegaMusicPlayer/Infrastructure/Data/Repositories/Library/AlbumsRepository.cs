using Microsoft.EntityFrameworkCore;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Infrastructure.Data.Repositories.Library
{
    public class AlbumRepository
    {
        private readonly IDbContextFactory<OmegaMusicPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public AlbumRepository(
            IDbContextFactory<OmegaMusicPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Albums> GetAlbumById(int albumID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var album = await context.Albums
                        .AsNoTracking()
                        .Where(a => a.AlbumId == albumID)
                        .Select(a => new Albums
                        {
                            AlbumID = a.AlbumId,
                            Title = a.Title,
                            ArtistID = a.ArtistId ?? 0,
                            ReleaseDate = a.ReleaseDate ?? DateTime.MinValue,
                            DiscNumber = a.DiscNumber ?? 0,
                            TrackCounter = a.TrackCounter ?? 0,
                            CoverID = a.CoverId ?? 0,
                            CreatedAt = a.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = a.UpdatedAt ?? DateTime.MinValue
                        })
                        .FirstOrDefaultAsync();

                    return album;
                },
                $"Database operation: Get album with ID {albumID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Albums> GetAlbumByTitle(string title, int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var album = await context.Albums
                        .AsNoTracking()
                        .Where(a => a.Title == title && a.ArtistId == artistID)
                        .Select(a => new Albums
                        {
                            AlbumID = a.AlbumId,
                            Title = a.Title,
                            ArtistID = a.ArtistId ?? 0,
                            ReleaseDate = a.ReleaseDate ?? DateTime.MinValue,
                            DiscNumber = a.DiscNumber ?? 0,
                            TrackCounter = a.TrackCounter ?? 0,
                            CoverID = a.CoverId ?? 0,
                            CreatedAt = a.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = a.UpdatedAt ?? DateTime.MinValue
                        })
                        .FirstOrDefaultAsync();

                    return album;
                },
                $"Database operation: Get album by title '{title}' for artist ID {artistID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Albums>> GetAllAlbums()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var albums = await context.Albums
                        .AsNoTracking()
                        .OrderBy(a => a.Title)
                        .Select(a => new Albums
                        {
                            AlbumID = a.AlbumId,
                            Title = a.Title,
                            ArtistID = a.ArtistId ?? 0,
                            ReleaseDate = a.ReleaseDate ?? DateTime.MinValue,
                            DiscNumber = a.DiscNumber ?? 0,
                            TrackCounter = a.TrackCounter ?? 0,
                            CoverID = a.CoverId ?? 0,
                            CreatedAt = a.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = a.UpdatedAt ?? DateTime.MinValue
                        })
                        .ToListAsync();

                    return albums;
                },
                "Database operation: Get all albums",
                new List<Albums>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Albums>> GetAlbumsByArtistId(int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var albums = await context.Albums
                        .AsNoTracking()
                        .Where(a => a.ArtistId == artistID)
                        .OrderBy(a => a.Title)
                        .Select(a => new Albums
                        {
                            AlbumID = a.AlbumId,
                            Title = a.Title,
                            ArtistID = a.ArtistId ?? 0,
                            ReleaseDate = a.ReleaseDate ?? DateTime.MinValue,
                            DiscNumber = a.DiscNumber ?? 0,
                            TrackCounter = a.TrackCounter ?? 0,
                            CoverID = a.CoverId ?? 0,
                            CreatedAt = a.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = a.UpdatedAt ?? DateTime.MinValue
                        })
                        .ToListAsync();

                    return albums;
                },
                $"Database operation: Get albums for artist ID {artistID}",
                new List<Albums>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<int> AddAlbum(Albums album)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (album == null)
                    {
                        throw new ArgumentNullException(nameof(album), "Cannot add null album to database");
                    }

                    if (string.IsNullOrWhiteSpace(album.Title))
                    {
                        throw new ArgumentException("Album must have a title", nameof(album));
                    }

                    // Check if album already exists
                    var existingAlbum = await GetAlbumByTitle(album.Title, album.ArtistID);
                    if (existingAlbum != null)
                    {
                        return existingAlbum.AlbumID;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    // Handle DateTime with default value if not set
                    var releaseDate = album.ReleaseDate;
                    if (releaseDate == default)
                    {
                        releaseDate = DateTime.MinValue; // Default for unknown release date
                    }

                    // Ensure timestamps are set
                    var createdAt = album.CreatedAt;
                    if (createdAt == default)
                    {
                        createdAt = DateTime.UtcNow;
                    }

                    var newAlbum = new Infrastructure.Data.Entities.Album
                    {
                        Title = album.Title,
                        ArtistId = album.ArtistID > 0 ? album.ArtistID : null,
                        ReleaseDate = releaseDate,
                        DiscNumber = album.DiscNumber,
                        TrackCounter = album.TrackCounter,
                        CoverId = album.CoverID > 0 ? album.CoverID : null,
                        CreatedAt = createdAt,
                        UpdatedAt = DateTime.UtcNow
                    };

                    context.Albums.Add(newAlbum);
                    await context.SaveChangesAsync();

                    return newAlbum.AlbumId;
                },
                $"Database operation: Add album '{album?.Title ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateAlbum(Albums album)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (album == null || album.AlbumID <= 0)
                    {
                        throw new ArgumentException("Cannot update null album or album with invalid ID", nameof(album));
                    }

                    if (string.IsNullOrWhiteSpace(album.Title))
                    {
                        throw new ArgumentException("Album must have a title", nameof(album));
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var existingAlbum = await context.Albums
                        .Where(a => a.AlbumId == album.AlbumID)
                        .FirstOrDefaultAsync();

                    if (existingAlbum == null)
                    {
                        throw new InvalidOperationException($"Album with ID {album.AlbumID} not found");
                    }

                    existingAlbum.Title = album.Title;
                    existingAlbum.ArtistId = album.ArtistID > 0 ? album.ArtistID : null;
                    existingAlbum.ReleaseDate = album.ReleaseDate;
                    existingAlbum.DiscNumber = album.DiscNumber;
                    existingAlbum.TrackCounter = album.TrackCounter;
                    existingAlbum.CoverId = album.CoverID > 0 ? album.CoverID : null;
                    existingAlbum.UpdatedAt = DateTime.UtcNow;

                    await context.SaveChangesAsync();
                },
                $"Database operation: Update album '{album?.Title ?? "Unknown"}' (ID: {album?.AlbumID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteAlbum(int albumID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (albumID <= 0)
                    {
                        throw new ArgumentException("Cannot delete album with invalid ID", nameof(albumID));
                    }

                    using var context = _contextFactory.CreateDbContext();
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        // First update tracks to set albumID to null
                        await context.Tracks
                            .Where(t => t.AlbumId == albumID)
                            .ExecuteUpdateAsync(s => s.SetProperty(t => t.AlbumId, (int?)null));

                        // Then delete the album
                        await context.Albums
                            .Where(a => a.AlbumId == albumID)
                            .ExecuteDeleteAsync();

                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                },
                $"Database operation: Delete album with ID {albumID}",
                ErrorSeverity.NonCritical,
                false);
        }
    }
}