using Microsoft.EntityFrameworkCore;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Infrastructure.Data;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Infrastructure.Services.Database
{
    /// <summary>
    /// Service for performing library maintenance operations like cleaning orphaned records and removing tracks with missing files
    /// </summary>
    public class LibraryMaintenanceService
    {
        private readonly IDbContextFactory<OmegaPlayerDbContext> _contextFactory;
        private readonly TracksRepository _tracksRepository;
        private readonly MediaRepository _mediaRepository;
        private readonly IErrorHandlingService _errorHandlingService;

        // Throttling mechanism to prevent excessive maintenance runs
        private static DateTime _lastMaintenanceRun = DateTime.MinValue;
        private static readonly TimeSpan MAINTENANCE_COOLDOWN = TimeSpan.FromMinutes(5);
        private static bool _hasRunSinceLastUpdate = false;

        public LibraryMaintenanceService(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            TracksRepository tracksRepository,
            MediaRepository mediaRepository,
            IErrorHandlingService errorHandlingService)
        {
            _contextFactory = contextFactory;
            _tracksRepository = tracksRepository;
            _mediaRepository = mediaRepository;
            _errorHandlingService = errorHandlingService;
        }

        /// <summary>
        /// Performs comprehensive library maintenance including orphaned record cleanup and missing file removal
        /// </summary>
        /// <param name="forceRun">If true, bypasses throttling and runs immediately</param>
        public async Task<MaintenanceResult> PerformLibraryMaintenance(bool forceRun = false)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    // Check throttling unless forced
                    if (!forceRun && ShouldSkipMaintenance())
                    {
                        return new MaintenanceResult
                        {
                            WasSkipped = true,
                            SkipReason = "Maintenance run too recently or no changes since last run"
                        };
                    }

                    var result = new MaintenanceResult
                    {
                        StartTime = DateTime.UtcNow
                    };

                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "Starting library maintenance",
                        "Performing cleanup of missing files and orphaned records",
                        null,
                        false);

                    // Step 1: Remove tracks with missing files
                    result.RemovedTracksCount = await CleanupMissingTracks();

                    // Step 2: Clean up orphaned media files
                    result.RemovedMediaCount = await CleanupOrphanedMedia();

                    // Step 3: Clean up orphaned playlist/queue entries
                    result.RemovedOrphanedRecords = await CleanupOrphanedRecords();

                    // Step 4: Update database statistics
                    await UpdateDatabaseStatistics();

                    // Update throttling markers
                    _lastMaintenanceRun = DateTime.UtcNow;
                    _hasRunSinceLastUpdate = true;

                    result.EndTime = DateTime.UtcNow;
                    result.Duration = result.EndTime - result.StartTime;

                    _errorHandlingService.LogInfo(
                        "Library maintenance completed",
                        $"Removed {result.RemovedTracksCount} tracks, {result.RemovedMediaCount} media files, " +
                        $"and {result.RemovedOrphanedRecords} orphaned records in {result.Duration.TotalSeconds:F1} seconds, ",
                        false);

                    return result;
                },
                "Performing library maintenance",
                new MaintenanceResult { HasErrors = true },
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Removes tracks whose files no longer exist on disk
        /// </summary>
        private async Task<int> CleanupMissingTracks()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var allTracks = await _tracksRepository.GetAllTracks();
                    int removedCount = 0;

                    foreach (var track in allTracks)
                    {
                        if (string.IsNullOrEmpty(track.FilePath) || !File.Exists(track.FilePath))
                        {
                            try
                            {
                                await _tracksRepository.DeleteTrack(track.TrackID);
                                removedCount++;

                                _errorHandlingService.LogInfo(
                                    "Removed missing track",
                                    $"Track '{track.Title}' removed because file no longer exists: {track.FilePath}",
                                    false);
                            }
                            catch (Exception ex)
                            {
                                _errorHandlingService.LogError(
                                    ErrorSeverity.NonCritical,
                                    "Failed to remove missing track",
                                    $"Could not remove track '{track.Title}' (ID: {track.TrackID}): {ex.Message}",
                                    ex,
                                    false);
                            }
                        }
                    }

                    return removedCount;
                },
                "Cleaning up tracks with missing files",
                0,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Removes orphaned media files and database records, including unreferenced media entries
        /// </summary>
        private async Task<int> CleanupOrphanedMedia()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    int totalRemoved = 0;

                    // First, run the existing cleanup for orphaned media with missing files
                    totalRemoved += await _mediaRepository.CleanupOrphanedMedia();

                    // Then, check for media records that are not referenced by any table
                    totalRemoved += await CleanupUnreferencedMedia();

                    return totalRemoved;
                },
                "Cleaning up orphaned media files",
                0,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Removes media records that are not referenced by any other table
        /// </summary>
        private async Task<int> CleanupUnreferencedMedia()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();
                    int removedCount = 0;

                    // Get all media records
                    var allMedia = await context.Media.ToListAsync();

                    foreach (var media in allMedia)
                    {
                        bool isReferenced = await IsMediaReferencedInAnyTable(context, media.MediaId);

                        if (!isReferenced)
                        {
                            try
                            {
                                // Delete the physical file if it exists
                                if (!string.IsNullOrEmpty(media.CoverPath) && File.Exists(media.CoverPath))
                                {
                                    File.Delete(media.CoverPath);
                                }

                                // Remove the media record from database
                                context.Media.Remove(media);
                                removedCount++;

                                _errorHandlingService.LogError(
                                    ErrorSeverity.Info,
                                    "Removed unreferenced media",
                                    $"Media ID {media.MediaId} removed as it was not referenced by any table. File: {media.CoverPath ?? "None"}",
                                    null,
                                    false);
                            }
                            catch (Exception ex)
                            {
                                _errorHandlingService.LogError(
                                    ErrorSeverity.NonCritical,
                                    "Failed to remove unreferenced media",
                                    $"Could not remove media ID {media.MediaId}: {ex.Message}",
                                    ex,
                                    false);
                            }
                        }
                    }

                    if (removedCount > 0)
                    {
                        await context.SaveChangesAsync();
                    }

                    return removedCount;
                },
                "Cleaning up unreferenced media records",
                0,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Checks if a media record is referenced by any table (Albums, Tracks, Artists, Profiles)
        /// </summary>
        private async Task<bool> IsMediaReferencedInAnyTable(OmegaPlayerDbContext context, int mediaId)
        {
            try
            {
                // Check Albums table for CoverId references
                var albumCount = await context.Albums
                    .Where(a => a.CoverId == mediaId)
                    .CountAsync();
                if (albumCount > 0)
                {
                    return true;
                }

                // Check Tracks table for CoverId references
                var trackCount = await context.Tracks
                    .Where(t => t.CoverId == mediaId)
                    .CountAsync();
                if (trackCount > 0)
                {
                    return true;
                }

                // Check Artists table for PhotoId references
                var artistCount = await context.Artists
                    .Where(a => a.PhotoId == mediaId)
                    .CountAsync();
                if (artistCount > 0)
                {
                    return true;
                }

                // Check Profiles table for PhotoId references
                var profileCount = await context.Profiles
                    .Where(p => p.PhotoId == mediaId)
                    .CountAsync();
                if (profileCount > 0)
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Error checking media references",
                    $"Failed to check references for media ID {mediaId}: {ex.Message}",
                    ex,
                    false);

                // Return true to be safe - don't delete if we can't verify
                return true;
            }
        }

        /// <summary>
        /// Removes orphaned records from playlist tracks, queue tracks, etc.
        /// </summary>
        private async Task<int> CleanupOrphanedRecords()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();
                    int removedCount = 0;

                    // Remove orphaned playlist tracks
                    var orphanedPlaylistTracks = await context.PlaylistTracks
                        .Where(pt => pt.TrackId != null && !context.Tracks.Any(t => t.TrackId == pt.TrackId))
                        .ToListAsync();

                    if (orphanedPlaylistTracks.Any())
                    {
                        context.PlaylistTracks.RemoveRange(orphanedPlaylistTracks);
                        removedCount += orphanedPlaylistTracks.Count;
                    }

                    // Remove orphaned queue tracks
                    var orphanedQueueTracks = await context.QueueTracks
                        .Where(qt => qt.TrackId != null && !context.Tracks.Any(t => t.TrackId == qt.TrackId))
                        .ToListAsync();

                    if (orphanedQueueTracks.Any())
                    {
                        context.QueueTracks.RemoveRange(orphanedQueueTracks);
                        removedCount += orphanedQueueTracks.Count;
                    }

                    // Remove orphaned play history entries
                    var orphanedPlayHistory = await context.PlayHistories
                        .Where(ph => !context.Tracks.Any(t => t.TrackId == ph.TrackId))
                        .ToListAsync();

                    if (orphanedPlayHistory.Any())
                    {
                        context.PlayHistories.RemoveRange(orphanedPlayHistory);
                        removedCount += orphanedPlayHistory.Count;
                    }

                    // Remove orphaned play counts
                    var orphanedPlayCounts = await context.PlayCounts
                        .Where(pc => !context.Tracks.Any(t => t.TrackId == pc.TrackId))
                        .ToListAsync();

                    if (orphanedPlayCounts.Any())
                    {
                        context.PlayCounts.RemoveRange(orphanedPlayCounts);
                        removedCount += orphanedPlayCounts.Count;
                    }

                    // Remove orphaned likes
                    var orphanedLikes = await context.Likes
                        .Where(l => !context.Tracks.Any(t => t.TrackId == l.TrackId))
                        .ToListAsync();

                    if (orphanedLikes.Any())
                    {
                        context.Likes.RemoveRange(orphanedLikes);
                        removedCount += orphanedLikes.Count;
                    }

                    await context.SaveChangesAsync();
                    return removedCount;
                },
                "Cleaning up orphaned database records",
                0,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Updates database statistics for better query performance
        /// </summary>
        private async Task UpdateDatabaseStatistics()
        {
            await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();
                    await context.Database.ExecuteSqlRawAsync("ANALYZE;");
                },
                "Updating database statistics",
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Removes tracks that exist within a specific directory path
        /// </summary>
        public async Task<int> CleanupTracksInDirectory(string directoryPath)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        return 0;
                    }

                    // Normalize the directory path for comparison
                    var normalizedPath = Path.GetFullPath(directoryPath).TrimEnd(Path.DirectorySeparatorChar);

                    var allTracks = await _tracksRepository.GetAllTracks();
                    int removedCount = 0;

                    foreach (var track in allTracks)
                    {
                        if (!string.IsNullOrEmpty(track.FilePath))
                        {
                            try
                            {
                                var trackDirectory = Path.GetDirectoryName(Path.GetFullPath(track.FilePath));

                                // Check if track is within the removed directory
                                if (!string.IsNullOrEmpty(trackDirectory) &&
                                    trackDirectory.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
                                {
                                    await _tracksRepository.DeleteTrack(track.TrackID);
                                    removedCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                _errorHandlingService.LogError(
                                    ErrorSeverity.NonCritical,
                                    "Error processing track during directory cleanup",
                                    $"Failed to process track '{track.Title}': {ex.Message}",
                                    ex,
                                    false);
                            }
                        }
                    }

                    if (removedCount > 0)
                    {
                        _errorHandlingService.LogInfo(
                            "Directory cleanup completed",
                            $"Removed {removedCount} tracks from directory: {directoryPath}",
                            false);

                        // After removing tracks from a directory, clean up any orphaned media
                        await CleanupOrphanedMedia();
                    }

                    return removedCount;
                },
                $"Cleaning up tracks in directory: {directoryPath}",
                0,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Marks that track metadata has been updated, allowing maintenance to run again
        /// </summary>
        public static void MarkMetadataUpdated()
        {
            _hasRunSinceLastUpdate = false;
        }

        /// <summary>
        /// Determines if maintenance should be skipped based on throttling rules
        /// </summary>
        private bool ShouldSkipMaintenance()
        {
            // Skip if we've run recently and no metadata updates have occurred
            return DateTime.UtcNow - _lastMaintenanceRun < MAINTENANCE_COOLDOWN && _hasRunSinceLastUpdate;
        }
    }

    /// <summary>
    /// Result of a maintenance operation
    /// </summary>
    public class MaintenanceResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int RemovedTracksCount { get; set; }
        public int RemovedMediaCount { get; set; }
        public int RemovedOrphanedRecords { get; set; }
        public bool WasSkipped { get; set; }
        public string SkipReason { get; set; }
        public bool HasErrors { get; set; }

        public int TotalItemsRemoved => RemovedTracksCount + RemovedMediaCount + RemovedOrphanedRecords;
    }
}
