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
    public class ArtistsRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public ArtistsRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Artists> GetArtistById(int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var artist = await context.Artists
                        .AsNoTracking()
                        .Where(a => a.ArtistId == artistID)
                        .Select(a => new Artists
                        {
                            ArtistID = a.ArtistId,
                            ArtistName = a.ArtistName,
                            PhotoID = a.PhotoId ?? 0,
                            Bio = a.Bio,
                            CreatedAt = a.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = a.UpdatedAt ?? DateTime.MinValue
                        })
                        .FirstOrDefaultAsync();

                    return artist;
                },
                $"Database operation: Get artist with ID {artistID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Artists> GetArtistByName(string artistName)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(artistName))
                    {
                        return null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var artist = await context.Artists
                        .AsNoTracking()
                        .Where(a => a.ArtistName == artistName)
                        .Select(a => new Artists
                        {
                            ArtistID = a.ArtistId,
                            ArtistName = a.ArtistName,
                            PhotoID = a.PhotoId ?? 0,
                            Bio = a.Bio,
                            CreatedAt = a.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = a.UpdatedAt ?? DateTime.MinValue
                        })
                        .FirstOrDefaultAsync();

                    return artist;
                },
                $"Database operation: Get artist by name '{artistName}'",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Artists>> GetAllArtists()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var artists = await context.Artists
                        .AsNoTracking()
                        .OrderBy(a => a.ArtistName)
                        .Select(a => new Artists
                        {
                            ArtistID = a.ArtistId,
                            ArtistName = a.ArtistName,
                            PhotoID = a.PhotoId ?? 0,
                            Bio = a.Bio,
                            CreatedAt = a.CreatedAt ?? DateTime.MinValue,
                            UpdatedAt = a.UpdatedAt ?? DateTime.MinValue
                        })
                        .ToListAsync();

                    return artists;
                },
                "Database operation: Get all artists",
                new List<Artists>(),
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task<int> AddArtist(Artists artist)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null)
                    {
                        throw new ArgumentNullException(nameof(artist), "Cannot add null artist to database");
                    }

                    if (string.IsNullOrWhiteSpace(artist.ArtistName))
                    {
                        throw new ArgumentException("Artist must have a name", nameof(artist));
                    }

                    // Check if artist already exists
                    var existingArtist = await GetArtistByName(artist.ArtistName);
                    if (existingArtist != null)
                    {
                        return existingArtist.ArtistID;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var newArtist = new Infrastructure.Data.Entities.Artist
                    {
                        ArtistName = artist.ArtistName,
                        Bio = artist.Bio,
                        PhotoId = artist.PhotoID > 0 ? artist.PhotoID : null,
                        CreatedAt = artist.CreatedAt != default ? artist.CreatedAt : DateTime.UtcNow,
                        UpdatedAt = artist.UpdatedAt != default ? artist.UpdatedAt : DateTime.UtcNow
                    };

                    context.Artists.Add(newArtist);
                    await context.SaveChangesAsync();

                    return newArtist.ArtistId;
                },
                $"Database operation: Add artist '{artist?.ArtistName ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateArtist(Artists artist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artist == null || artist.ArtistID <= 0)
                    {
                        throw new ArgumentException("Cannot update null artist or artist with invalid ID", nameof(artist));
                    }

                    if (string.IsNullOrWhiteSpace(artist.ArtistName))
                    {
                        throw new ArgumentException("Artist must have a name", nameof(artist));
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var existingArtist = await context.Artists
                        .Where(a => a.ArtistId == artist.ArtistID)
                        .FirstOrDefaultAsync();

                    if (existingArtist == null)
                    {
                        throw new InvalidOperationException($"Artist with ID {artist.ArtistID} not found");
                    }

                    existingArtist.ArtistName = artist.ArtistName;
                    existingArtist.Bio = artist.Bio;
                    existingArtist.PhotoId = artist.PhotoID > 0 ? artist.PhotoID : null;
                    existingArtist.UpdatedAt = DateTime.UtcNow;

                    await context.SaveChangesAsync();
                },
                $"Database operation: Update artist '{artist?.ArtistName ?? "Unknown"}' (ID: {artist?.ArtistID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteArtist(int artistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artistID <= 0)
                    {
                        throw new ArgumentException("Cannot delete artist with invalid ID", nameof(artistID));
                    }

                    using var context = _contextFactory.CreateDbContext();
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        // Delete artist relationships first
                        await DeleteArtistRelationships(context, artistID);

                        // Then delete the artist
                        await context.Artists
                            .Where(a => a.ArtistId == artistID)
                            .ExecuteDeleteAsync();

                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                },
                $"Database operation: Delete artist with ID {artistID}",
                ErrorSeverity.NonCritical,
                true);
        }

        private async Task DeleteArtistRelationships(OmegaPlayerDbContext context, int artistID)
        {
            // Delete track-artist relationships
            await context.TrackArtists
                .Where(ta => ta.ArtistId == artistID)
                .ExecuteDeleteAsync();

            // Update albums to set artistID to null
            await context.Albums
                .Where(a => a.ArtistId == artistID)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.ArtistId, (int?)null));
        }
    }
}