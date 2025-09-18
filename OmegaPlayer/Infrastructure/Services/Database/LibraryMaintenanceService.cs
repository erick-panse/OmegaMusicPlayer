using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Features.Playback.ViewModels;
using OmegaPlayer.Infrastructure.Data;
using OmegaPlayer.Infrastructure.Data.Repositories.Library;
using OmegaPlayer.UI;
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
        private readonly TrackQueueViewModel _trackQueueViewModel;
        private readonly LocalizationService _localizationService;

        // Throttling mechanism to prevent excessive maintenance runs
        private static DateTime _lastMaintenanceRun = DateTime.MinValue;
        private static readonly TimeSpan MAINTENANCE_COOLDOWN = TimeSpan.FromMinutes(5);
        private static bool _hasRunSinceLastUpdate = false;

        public LibraryMaintenanceService(
            IDbContextFactory<OmegaPlayerDbContext> contextFactory,
            TracksRepository tracksRepository,
            MediaRepository mediaRepository,
            IErrorHandlingService errorHandlingService,
            TrackQueueViewModel trackQueueViewModel,
            LocalizationService localizationService)
        {
            _contextFactory = contextFactory;
            _tracksRepository = tracksRepository;
            _mediaRepository = mediaRepository;
            _errorHandlingService = errorHandlingService;
            _trackQueueViewModel = trackQueueViewModel;
            _localizationService = localizationService;
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

                    // Step 4: Clean up orphaned artists, albums, and genres
                    var orphanedEntitiesResult = await CleanupOrphanedEntities();
                    result.RemovedOrphanedRecords += orphanedEntitiesResult.TotalRemoved;

                    // Step 5: Update database statistics
                    await UpdateDatabaseStatistics();

                    // Update throttling markers
                    _lastMaintenanceRun = DateTime.UtcNow;
                    _hasRunSinceLastUpdate = true;

                    result.EndTime = DateTime.UtcNow;
                    result.Duration = result.EndTime - result.StartTime;

                    _errorHandlingService.LogInfo(
                        "Library maintenance completed",
                        $"Removed {result.RemovedTracksCount} tracks, {result.RemovedMediaCount} media files, " +
                        $"and {result.RemovedOrphanedRecords} orphaned records in {result.Duration.TotalSeconds:F1} seconds",
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
        /// Cleans up orphaned artists, albums, and genres that no longer have associated tracks
        /// </summary>
        private async Task<OrphanedEntitiesResult> CleanupOrphanedEntities()
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    using var context = _contextFactory.CreateDbContext();
                    var result = new OrphanedEntitiesResult();

                    // Clean up orphaned artists (artists with no tracks)
                    var orphanedArtists = await context.Artists
                        .Where(a => !context.TrackArtists.Any(ta => ta.ArtistId == a.ArtistId))
                        .ToListAsync();

                    if (orphanedArtists.Any())
                    {
                        context.Artists.RemoveRange(orphanedArtists);
                        result.RemovedArtists = orphanedArtists.Count;

                        _errorHandlingService.LogInfo(
                            "Cleaned up orphaned artists",
                            $"Removed {orphanedArtists.Count} artists with no associated tracks",
                            false);
                    }

                    // Clean up orphaned albums (albums with no tracks)
                    var orphanedAlbums = await context.Albums
                        .Where(a => !context.Tracks.Any(t => t.AlbumId == a.AlbumId))
                        .ToListAsync();

                    if (orphanedAlbums.Any())
                    {
                        context.Albums.RemoveRange(orphanedAlbums);
                        result.RemovedAlbums = orphanedAlbums.Count;

                        _errorHandlingService.LogInfo(
                            "Cleaned up orphaned albums",
                            $"Removed {orphanedAlbums.Count} albums with no associated tracks",
                            false);
                    }

                    // Clean up orphaned genres (genres with no tracks)
                    var orphanedGenres = await context.Genres
                        .Where(g => !context.TrackGenres.Any(tg => tg.GenreId == g.GenreId))
                        .ToListAsync();

                    if (orphanedGenres.Any())
                    {
                        context.Genres.RemoveRange(orphanedGenres);
                        result.RemovedGenres = orphanedGenres.Count;

                        _errorHandlingService.LogInfo(
                            "Cleaned up orphaned genres",
                            $"Removed {orphanedGenres.Count} genres with no associated tracks",
                            false);
                    }

                    // Save all changes
                    if (result.TotalRemoved > 0)
                    {
                        await context.SaveChangesAsync();
                    }

                    return result;
                },
                "Cleaning up orphaned entities (artists, albums, genres)",
                new OrphanedEntitiesResult(),
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
        /// Removes tracks that exist within a specific directory path and cleans up orphaned entities
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

                        var queueCleared = await CheckAndClearQueueForDeletedDirectory(directoryPath);

                        // After removing tracks from a directory, perform cleanup
                        await CleanupOrphanedMedia();
                        await CleanupOrphanedRecords();
                        var orphanedEntitiesResult = await CleanupOrphanedEntities();

                        _errorHandlingService.LogInfo(
                            "Orphaned entities cleanup completed",
                            $"Removed {orphanedEntitiesResult.RemovedArtists} artists, " +
                            $"{orphanedEntitiesResult.RemovedAlbums} albums, " +
                            $"{orphanedEntitiesResult.RemovedGenres} genres",
                            false);
                    }

                    return removedCount;
                },
                $"Cleaning up tracks in directory: {directoryPath}",
                0,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Checks if any tracks from a specific directory path are in the current queue and clears it
        /// </summary>
        public async Task<bool> CheckAndClearQueueForDeletedDirectory(string directoryPath)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    if (string.IsNullOrEmpty(directoryPath))
                    {
                        return false;
                    }

                    // Normalize the directory path for comparison
                    var normalizedPath = System.IO.Path.GetFullPath(directoryPath)
                        .TrimEnd(System.IO.Path.DirectorySeparatorChar);

                    // Check if any tracks in the current queue are from the deleted directory
                    var currentQueue = _trackQueueViewModel.NowPlayingQueue;
                    if (currentQueue == null || !currentQueue.Any())
                    {
                        return false;
                    }

                    var tracksInDeletedDirectory = currentQueue
                        .Where(track => !string.IsNullOrEmpty(track.FilePath))
                        .Where(track =>
                        {
                            try
                            {
                                var trackDirectory = System.IO.Path.GetDirectoryName(
                                    System.IO.Path.GetFullPath(track.FilePath));

                                return !string.IsNullOrEmpty(trackDirectory) &&
                                       trackDirectory.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase);
                            }
                            catch
                            {
                                return false; // Skip tracks with invalid paths
                            }
                        })
                        .ToList();

                    if (!tracksInDeletedDirectory.Any())
                    {
                        return false; // No tracks from deleted directory in queue
                    }

                    _errorHandlingService.LogInfo(
                        "Tracks from deleted directory found in queue",
                        $"Found {tracksInDeletedDirectory.Count} tracks from deleted directory '{directoryPath}' in current queue. Clearing queue.",
                        false);

                    var trackControlViewModel = App.ServiceProvider.GetService<TrackControlViewModel>();
                    if (trackControlViewModel == null) return false;

                    // Clear current playback and queue from memory
                    trackControlViewModel.ClearPlayback();
                    await _trackQueueViewModel.ClearQueue();

                    _errorHandlingService.LogInfo(
                        _localizationService["QueueClearedDeletedDirectory_Title"],
                        _localizationService["QueueClearedDeletedDirectory_Message"],
                        true); // Show notification to user

                    return true; // Queue was cleared
                },
                $"Checking and clearing queue for deleted directory: {directoryPath}",
                false,
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

    /// <summary>
    /// Result of orphaned entities cleanup
    /// </summary>
    public class OrphanedEntitiesResult
    {
        public int RemovedArtists { get; set; }
        public int RemovedAlbums { get; set; }
        public int RemovedGenres { get; set; }

        public int TotalRemoved => RemovedArtists + RemovedAlbums + RemovedGenres;
    }
}