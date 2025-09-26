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
    public class TrackGenreRepository
    {
        private readonly IDbContextFactory<OmegaMusicPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackGenreRepository(
            IDbContextFactory<OmegaMusicPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<TrackGenre> GetTrackGenre(int trackID, int genreID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0 || genreID <= 0)
                    {
                        return null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var trackGenre = await context.TrackGenres
                        .AsNoTracking()
                        .Where(tg => tg.TrackId == trackID && tg.GenreId == genreID)
                        .Select(tg => new TrackGenre
                        {
                            TrackID = tg.TrackId,
                            GenreID = tg.GenreId
                        })
                        .FirstOrDefaultAsync();

                    return trackGenre;
                },
                $"Database operation: Get track-genre relationship: Track ID {trackID}, Genre ID {genreID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackGenre>> GetAllTrackGenres()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var trackGenres = await context.TrackGenres
                        .AsNoTracking()
                        .Select(tg => new TrackGenre
                        {
                            TrackID = tg.TrackId,
                            GenreID = tg.GenreId
                        })
                        .ToListAsync();

                    return trackGenres;
                },
                "Database operation: Get all track-genre relationships",
                new List<TrackGenre>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackGenre>> GetTrackGenresByTrackId(int trackID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0)
                    {
                        return new List<TrackGenre>();
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var trackGenres = await context.TrackGenres
                        .AsNoTracking()
                        .Where(tg => tg.TrackId == trackID)
                        .Select(tg => new TrackGenre
                        {
                            TrackID = tg.TrackId,
                            GenreID = tg.GenreId
                        })
                        .ToListAsync();

                    return trackGenres;
                },
                $"Database operation: Get track-genre relationships for track ID {trackID}",
                new List<TrackGenre>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackGenre>> GetTrackGenresByGenreId(int genreID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genreID <= 0)
                    {
                        return new List<TrackGenre>();
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var trackGenres = await context.TrackGenres
                        .AsNoTracking()
                        .Where(tg => tg.GenreId == genreID)
                        .Select(tg => new TrackGenre
                        {
                            TrackID = tg.TrackId,
                            GenreID = tg.GenreId
                        })
                        .ToListAsync();

                    return trackGenres;
                },
                $"Database operation: Get track-genre relationships for genre ID {genreID}",
                new List<TrackGenre>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task AddTrackGenre(TrackGenre trackGenre)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackGenre == null)
                    {
                        throw new ArgumentNullException(nameof(trackGenre), "Cannot add null track-genre relationship");
                    }

                    if (trackGenre.TrackID <= 0 || trackGenre.GenreID <= 0)
                    {
                        throw new ArgumentException("TrackID and GenreID must be valid", nameof(trackGenre));
                    }

                    // Check if relationship already exists to avoid duplicates
                    var existingRelationship = await GetTrackGenre(trackGenre.TrackID, trackGenre.GenreID);
                    if (existingRelationship != null)
                    {
                        return; // Relationship already exists, no need to add
                    }

                    // Verify track and genre exist
                    bool trackAndGenreExist = await VerifyTrackAndGenreExistAsync(
                        trackGenre.TrackID, trackGenre.GenreID);

                    if (!trackAndGenreExist)
                    {
                        throw new InvalidOperationException(
                            $"Cannot create relationship: Track ID {trackGenre.TrackID} " +
                            $"or Genre ID {trackGenre.GenreID} does not exist");
                    }

                    // Remove any existing genre associations for this track (assuming a track can only have one genre)
                    await DeleteExistingTrackGenres(trackGenre.TrackID);

                    using var context = _contextFactory.CreateDbContext();

                    var newTrackGenre = new Infrastructure.Data.Entities.TrackGenre
                    {
                        TrackId = trackGenre.TrackID,
                        GenreId = trackGenre.GenreID
                    };

                    context.TrackGenres.Add(newTrackGenre);
                    await context.SaveChangesAsync();
                },
                $"Database operation: Add track-genre relationship: Track ID {trackGenre?.TrackID}, Genre ID {trackGenre?.GenreID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteTrackGenre(int trackID, int genreID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0 || genreID <= 0)
                    {
                        throw new ArgumentException("TrackID and GenreID must be valid");
                    }

                    using var context = _contextFactory.CreateDbContext();

                    await context.TrackGenres
                        .Where(tg => tg.TrackId == trackID && tg.GenreId == genreID)
                        .ExecuteDeleteAsync();
                },
                $"Database operation: Delete track-genre relationship: Track ID {trackID}, Genre ID {genreID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteAllTrackGenresForTrack(int trackID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0)
                    {
                        throw new ArgumentException("TrackID must be valid");
                    }

                    using var context = _contextFactory.CreateDbContext();

                    await context.TrackGenres
                        .Where(tg => tg.TrackId == trackID)
                        .ExecuteDeleteAsync();
                },
                $"Database operation: Delete all track-genre relationships for track ID {trackID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteAllTrackGenresForGenre(int genreID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (genreID <= 0)
                    {
                        throw new ArgumentException("GenreID must be valid");
                    }

                    // Get "Unknown" genre
                    int unknownGenreId = await GetUnknownGenreId();

                    using var context = _contextFactory.CreateDbContext();

                    // If the provided genreID is the "Unknown" genre, don't delete anything
                    if (genreID == unknownGenreId)
                    {
                        return;
                    }

                    // Update track-genre relationships to point to unknown genre
                    await context.TrackGenres
                        .Where(tg => tg.GenreId == genreID)
                        .ExecuteUpdateAsync(s => s.SetProperty(tg => tg.GenreId, unknownGenreId));
                },
                $"Database operation: Reassign track-genre relationships from genre ID {genreID} to Unknown genre",
                ErrorSeverity.NonCritical,
                false);
        }

        private async Task<bool> VerifyTrackAndGenreExistAsync(int trackID, int genreID)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Check if track exists
                var trackExists = await context.Tracks
                    .AnyAsync(t => t.TrackId == trackID);
                if (!trackExists)
                {
                    return false;
                }

                // Check if genre exists
                var genreExists = await context.Genres
                    .AnyAsync(g => g.GenreId == genreID);
                return genreExists;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error verifying track and genre existence",
                    $"Failed to verify if track ID {trackID} and genre ID {genreID} exist: {ex.Message}",
                    ex,
                    false);
                return false;
            }
        }

        private async Task DeleteExistingTrackGenres(int trackID)
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                await context.TrackGenres
                    .Where(tg => tg.TrackId == trackID)
                    .ExecuteDeleteAsync();
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error deleting existing track-genre relationships",
                    $"Failed to delete existing genre associations for track ID {trackID}: {ex.Message}",
                    ex,
                    false);
            }
        }

        private async Task<int> GetUnknownGenreId()
        {
            try
            {
                using var context = _contextFactory.CreateDbContext();

                // Try to get the "Unknown" genre
                var existingGenre = await context.Genres
                    .Where(g => g.GenreName == "Unknown")
                    .Select(g => g.GenreId)
                    .FirstOrDefaultAsync();

                if (existingGenre != 0)
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

                return newUnknownGenre.GenreId;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error getting Unknown genre",
                    $"Failed to get or create Unknown genre: {ex.Message}",
                    ex,
                    false);
                return 0; // Return 0 as fallback
            }
        }
    }
}