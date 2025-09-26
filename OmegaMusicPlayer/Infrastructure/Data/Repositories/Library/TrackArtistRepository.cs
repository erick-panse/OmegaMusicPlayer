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
    public class TrackArtistRepository
    {
        private readonly IDbContextFactory<OmegaMusicPlayerDbContext> _contextFactory;
        private readonly IErrorHandlingService _errorHandlingService;

        public TrackArtistRepository(
            IDbContextFactory<OmegaMusicPlayerDbContext> contextFactory,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _errorHandlingService = errorHandlingService;
        }

        public async Task<TrackArtist> GetTrackArtist(int trackID, int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0 || artistID <= 0)
                    {
                        return null;
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var trackArtist = await context.TrackArtists
                        .AsNoTracking()
                        .Where(ta => ta.TrackId == trackID && ta.ArtistId == artistID)
                        .Select(ta => new TrackArtist
                        {
                            TrackID = ta.TrackId,
                            ArtistID = ta.ArtistId
                        })
                        .FirstOrDefaultAsync();

                    return trackArtist;
                },
                $"Database operation: Get track-artist relationship: Track ID {trackID}, Artist ID {artistID}",
                null,
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackArtist>> GetAllTrackArtists()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();

                    var trackArtists = await context.TrackArtists
                        .AsNoTracking()
                        .Select(ta => new TrackArtist
                        {
                            TrackID = ta.TrackId,
                            ArtistID = ta.ArtistId
                        })
                        .ToListAsync();

                    return trackArtists;
                },
                "Database operation: Get all track-artist relationships",
                new List<TrackArtist>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackArtist>> GetTrackArtistsByTrackId(int trackID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0)
                    {
                        return new List<TrackArtist>();
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var trackArtists = await context.TrackArtists
                        .AsNoTracking()
                        .Where(ta => ta.TrackId == trackID)
                        .Select(ta => new TrackArtist
                        {
                            TrackID = ta.TrackId,
                            ArtistID = ta.ArtistId
                        })
                        .ToListAsync();

                    return trackArtists;
                },
                $"Database operation: Get track-artist relationships for track ID {trackID}",
                new List<TrackArtist>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task<List<TrackArtist>> GetTrackArtistsByArtistId(int artistID)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artistID <= 0)
                    {
                        return new List<TrackArtist>();
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var trackArtists = await context.TrackArtists
                        .AsNoTracking()
                        .Where(ta => ta.ArtistId == artistID)
                        .Select(ta => new TrackArtist
                        {
                            TrackID = ta.TrackId,
                            ArtistID = ta.ArtistId
                        })
                        .ToListAsync();

                    return trackArtists;
                },
                $"Database operation: Get track-artist relationships for artist ID {artistID}",
                new List<TrackArtist>(),
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task AddTrackArtist(TrackArtist trackArtist)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackArtist == null)
                    {
                        throw new ArgumentNullException(nameof(trackArtist), "Cannot add null track-artist relationship");
                    }

                    if (trackArtist.TrackID <= 0 || trackArtist.ArtistID <= 0)
                    {
                        throw new ArgumentException("TrackID and ArtistID must be valid", nameof(trackArtist));
                    }

                    // Check if relationship already exists to avoid duplicates
                    var existingRelationship = await GetTrackArtist(trackArtist.TrackID, trackArtist.ArtistID);
                    if (existingRelationship != null)
                    {
                        return; // Relationship already exists, no need to add
                    }

                    // Verify track and artist exist
                    bool trackAndArtistExist = await VerifyTrackAndArtistExistAsync(
                        trackArtist.TrackID, trackArtist.ArtistID);

                    if (!trackAndArtistExist)
                    {
                        throw new InvalidOperationException(
                            $"Cannot create relationship: Track ID {trackArtist.TrackID} " +
                            $"or Artist ID {trackArtist.ArtistID} does not exist");
                    }

                    using var context = _contextFactory.CreateDbContext();

                    var newTrackArtist = new Infrastructure.Data.Entities.TrackArtist
                    {
                        TrackId = trackArtist.TrackID,
                        ArtistId = trackArtist.ArtistID
                    };

                    context.TrackArtists.Add(newTrackArtist);
                    await context.SaveChangesAsync();
                },
                $"Database operation: Add track-artist relationship: Track ID {trackArtist?.TrackID}, Artist ID {trackArtist?.ArtistID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteTrackArtist(int trackID, int artistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0 || artistID <= 0)
                    {
                        throw new ArgumentException("TrackID and ArtistID must be valid");
                    }

                    using var context = _contextFactory.CreateDbContext();

                    await context.TrackArtists
                        .Where(ta => ta.TrackId == trackID && ta.ArtistId == artistID)
                        .ExecuteDeleteAsync();
                },
                $"Database operation: Delete track-artist relationship: Track ID {trackID}, Artist ID {artistID}",
                ErrorSeverity.NonCritical,
                false);
        }

        public async Task DeleteAllTrackArtistsForTrack(int trackID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (trackID <= 0)
                    {
                        throw new ArgumentException("TrackID must be valid");
                    }

                    using var context = _contextFactory.CreateDbContext();

                    await context.TrackArtists
                        .Where(ta => ta.TrackId == trackID)
                        .ExecuteDeleteAsync();
                },
                $"Database operation: Delete all track-artist relationships for track ID {trackID}",
                ErrorSeverity.NonCritical,
                true);
        }

        public async Task DeleteAllTrackArtistsForArtist(int artistID)
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (artistID <= 0)
                    {
                        throw new ArgumentException("ArtistID must be valid");
                    }

                    using var context = _contextFactory.CreateDbContext();

                    await context.TrackArtists
                        .Where(ta => ta.ArtistId == artistID)
                        .ExecuteDeleteAsync();
                },
                $"Database operation: Delete all track-artist relationships for artist ID {artistID}",
                ErrorSeverity.NonCritical,
                false);
        }

        private async Task<bool> VerifyTrackAndArtistExistAsync(int trackID, int artistID)
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

                // Check if artist exists
                var artistExists = await context.Artists
                    .AnyAsync(a => a.ArtistId == artistID);
                return artistExists;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error verifying track and artist existence",
                    $"Failed to verify if track ID {trackID} and artist ID {artistID} exist: {ex.Message}",
                    ex,
                    false);
                return false;
            }
        }
    }
}