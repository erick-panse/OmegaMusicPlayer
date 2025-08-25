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
    public class GenresRepository
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public GenresRepository(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<Genres> GetGenreByName(string genreName)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(genreName))
                    {
                        return await GetOrCreateUnknownGenre();
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var genre = await context.Genres
                        .AsNoTracking()
                        .Where(g => g.GenreName == genreName)
                        .Select(g => new Genres
                        {
                            GenreID = g.GenreId,
                            GenreName = g.GenreName
                        })
                        .FirstOrDefaultAsync();

                    return genre;
                },
                $"Database operation: Get genre by name '{genreName}'",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<Genres> GetGenreById(int genreID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var genre = await context.Genres
                        .AsNoTracking()
                        .Where(g => g.GenreId == genreID)
                        .Select(g => new Genres
                        {
                            GenreID = g.GenreId,
                            GenreName = g.GenreName
                        })
                        .FirstOrDefaultAsync();

                    return genre;
                },
                $"Database operation: Get genre with ID {genreID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<Genres>> GetAllGenres()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var genres = await context.Genres
                        .AsNoTracking()
                        .OrderBy(g => g.GenreName)
                        .Select(g => new Genres
                        {
                            GenreID = g.GenreId,
                            GenreName = g.GenreName
                        })
                        .ToListAsync();

                    // If no genres exist, create the Unknown genre
                    if (genres.Count == 0)
                    {
                        var unknownGenre = await GetOrCreateUnknownGenre();
                        if (unknownGenre != null)
                        {
                            genres.Add(unknownGenre);
                        }
                    }

                    return genres;
                },
                "Database operation: Get all genres",
                new List<Genres>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<int> AddGenre(Genres genre)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genre == null)
                    {
                        throw new ArgumentNullException(nameof(genre), "Cannot add null genre to database");
                    }

                    // If genre name is empty or null, use "Unknown"
                    string genreName = !string.IsNullOrWhiteSpace(genre.GenreName) ?
                        genre.GenreName : "Unknown";

                    // Check if genre already exists to avoid duplicates
                    var existingGenre = await GetGenreByName(genreName);
                    if (existingGenre != null)
                    {
                        return existingGenre.GenreID;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var newGenre = new Infrastructure.Data.Entities.Genre
                    {
                        GenreName = genreName
                    };

                    context.Genres.Add(newGenre);
                    await context.SaveChangesAsync();

                    return newGenre.GenreId;
                },
                $"Database operation: Add genre '{genre?.GenreName ?? "Unknown"}'",
                -1,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task UpdateGenre(Genres genre)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genre == null || genre.GenreID <= 0)
                    {
                        throw new ArgumentException("Cannot update null genre or genre with invalid ID", nameof(genre));
                    }

                    // If attempting to update to empty name, use "Unknown"
                    string genreName = !string.IsNullOrWhiteSpace(genre.GenreName) ?
                        genre.GenreName : "Unknown";

                    // Don't allow renaming the "Unknown" genre to something else
                    var currentGenre = await GetGenreById(genre.GenreID);
                    if (currentGenre != null && currentGenre.GenreName == "Unknown" &&
                        genreName != "Unknown")
                    {
                        throw new InvalidOperationException("Cannot rename the 'Unknown' genre");
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var existingGenre = await context.Genres
                        .Where(g => g.GenreId == genre.GenreID)
                        .FirstOrDefaultAsync();

                    if (existingGenre == null)
                    {
                        throw new InvalidOperationException($"Genre with ID {genre.GenreID} not found");
                    }

                    existingGenre.GenreName = genreName;
                    await context.SaveChangesAsync();
                },
                $"Database operation: Update genre '{genre?.GenreName ?? "Unknown"}' (ID: {genre?.GenreID})",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteGenre(int genreID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genreID <= 0)
                    {
                        throw new ArgumentException("Cannot delete genre with invalid ID", nameof(genreID));
                    }

                    // Check if this is the "Unknown" genre - don't allow deletion
                    var genre = await GetGenreById(genreID);
                    if (genre != null && genre.GenreName == "Unknown")
                    {
                        throw new InvalidOperationException("Cannot delete the 'Unknown' genre");
                    }

                    using var context = _contextFactory.CreateDbContext();
                    using var transaction = await context.Database.BeginTransactionAsync();

                    try
                    {
                        // First get the unknown genre ID for reassignment
                        var unknownGenre = await GetOrCreateUnknownGenre();
                        int unknownGenreId = unknownGenre?.GenreID ?? 0;

                        // Update track-genre relationships to point to unknown genre
                        await context.TrackGenres
                            .Where(tg => tg.GenreId == genreID)
                            .ExecuteUpdateAsync(s => s.SetProperty(tg => tg.GenreId, unknownGenreId));

                        // Update tracks to set their genreID to unknown genre
                        await context.Tracks
                            .Where(t => t.GenreId == genreID)
                            .ExecuteUpdateAsync(s => s.SetProperty(t => t.GenreId, unknownGenreId));

                        // Then delete the genre
                        await context.Genres
                            .Where(g => g.GenreId == genreID)
                            .ExecuteDeleteAsync();

                        await transaction.CommitAsync();
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                },
                $"Database operation: Delete genre with ID {genreID}",
                ErrorSeverity.NonCritical,
                false);
        }

        private async Task<Genres> GetOrCreateUnknownGenre()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    // Try to get the "Unknown" genre
                    var existingGenre = await context.Genres
                        .Where(g => g.GenreName == "Unknown")
                        .Select(g => new Genres
                        {
                            GenreID = g.GenreId,
                            GenreName = g.GenreName
                        })
                        .FirstOrDefaultAsync();

                    if (existingGenre != null)
                    {
                        return existingGenre;
                    }

                    // If it doesn't exist, create it
                    var newUnknownGenre = new Infrastructure.Data.Entities.Genre
                    {
                        GenreName = "Unknown"
                    };

                    context.Genres.Add(newUnknownGenre);
                    await context.SaveChangesAsync();

                    return new Genres
                    {
                        GenreID = newUnknownGenre.GenreId,
                        GenreName = "Unknown"
                    };
                },
                "Database operation: Get or create Unknown genre",
                null,
                ErrorSeverity.NonCritical,
                false);
        }
    }
}