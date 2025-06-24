using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Infrastructure.Data;
using OmegaPlayer.Infrastructure.Services.Database;
using System;
using System.Data;
using System.Linq;

namespace OmegaPlayer.Infrastructure.Services.Initialization
{
    /// <summary>
    /// Service to initialize the PostgreSQL database using EF Core and embedded PostgreSQL (Synchronous)
    /// Works with the EmbeddedPostgreSqlService for portable database deployment
    /// </summary>
    public class DatabaseInitializationService
    {
        private readonly EmbeddedPostgreSqlService _embeddedPostgreSqlService;

        public DatabaseInitializationService(EmbeddedPostgreSqlService embeddedPostgreSqlService)
        {
            _embeddedPostgreSqlService = embeddedPostgreSqlService;
        }

        /// <summary>
        /// Ensures the PostgreSQL database exists and is up to date synchronously
        /// </summary>
        public bool InitializeDatabase()
        {
            // Start the embedded PostgreSQL server first
            var serverStarted = _embeddedPostgreSqlService.StartServer();
            if (!serverStarted)
            {
                throw new InvalidOperationException("Failed to start embedded PostgreSQL server");
            }

            // Test the connection
            var connectionWorks = _embeddedPostgreSqlService.TestConnection();
            if (!connectionWorks)
            {
                throw new InvalidOperationException("Cannot connect to embedded PostgreSQL server");
            }

            // Configure DbContext options with the connection string
            var optionsBuilder = new DbContextOptionsBuilder<OmegaPlayerDbContext>();
            optionsBuilder.UseNpgsql(_embeddedPostgreSqlService.ConnectionString, options =>
            {
                // Configure PostgreSQL-specific options
                options.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            });

            using var context = new OmegaPlayerDbContext(optionsBuilder.Options);

            // Check if database needs migration or creation
            var pendingMigrations = context.Database.GetPendingMigrations();
            var appliedMigrations = context.Database.GetAppliedMigrations();

            bool createDb = false;
            try
            {
                createDb = context.Profiles.Any(); // should NOT create DB
            }
            catch { } // don't throw error - should create DB

            if (!appliedMigrations.Any() && !createDb)
            {
                // Database is new - create it
                context.Database.EnsureCreated();

                // Create default data for new database
                CreateDefaultData(context);
            }
            else if (pendingMigrations.Any())
            {
                // Database exists but has pending migrations
                context.Database.Migrate();

                // Ensure default data still exists after migration
                EnsureDefaultDataExists(context);
            }
            else
            {
                // Database is up to date
                // Still check for missing default data
                EnsureDefaultDataExists(context);
            }

            // Final connection test
            var finalTest = context.Database.CanConnect();
            if (!finalTest)
            {
                throw new InvalidOperationException("Final database connection test failed");
            }

            return true;
        }

        /// <summary>
        /// Tests if PostgreSQL database connection works synchronously
        /// </summary>
        public bool TestConnection()
        {
            if (!_embeddedPostgreSqlService.IsServerRunning)
            {
                return false;
            }

            var optionsBuilder = new DbContextOptionsBuilder<OmegaPlayerDbContext>();
            optionsBuilder.UseNpgsql(_embeddedPostgreSqlService.ConnectionString);

            using var context = new OmegaPlayerDbContext(optionsBuilder.Options);
            return context.Database.CanConnect();
        }

        /// <summary>
        /// Creates default data when database is first created
        /// </summary>
        private void CreateDefaultData(OmegaPlayerDbContext context)
        {
            try
            {
                // Create default profile
                CreateDefaultProfile(context);

                // Create default global config
                CreateDefaultGlobalConfig(context);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        /// <summary>
        /// Ensures default data exists (for cases where database exists but data was lost)
        /// </summary>
        private void EnsureDefaultDataExists(OmegaPlayerDbContext context)
        {
            try
            {
                // Check and create default profile if missing
                if (!context.Profiles.Any())
                {
                    CreateDefaultProfile(context);
                }

                // Check and create global config if missing
                if (!context.GlobalConfigs.Any())
                {
                    CreateDefaultGlobalConfig(context);
                }
            }
            catch (Exception ex)
            {
                // Don't throw - this is not critical
            }
        }

        /// <summary>
        /// Creates the default profile
        /// </summary>
        private void CreateDefaultProfile(OmegaPlayerDbContext context)
        {
            var defaultProfile = new OmegaPlayer.Infrastructure.Data.Entities.Profile
            {
                ProfileName = "Default",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Profiles.Add(defaultProfile);
            context.SaveChanges();

            // Create profile config
            var profileConfig = new OmegaPlayer.Infrastructure.Data.Entities.ProfileConfig
            {
                ProfileId = defaultProfile.ProfileId,
                EqualizerPresets = "{}",
                LastVolume = 50,
                Theme = "dark",
                DynamicPause = true,
                ViewState = "{\"albums\": \"grid\", \"artists\": \"list\", \"library\": \"grid\"}",
                SortingState = "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}"
            };

            context.ProfileConfigs.Add(profileConfig);
            context.SaveChanges();
        }

        /// <summary>
        /// Creates the default global configuration
        /// </summary>
        private void CreateDefaultGlobalConfig(OmegaPlayerDbContext context)
        {
            var defaultProfileId = context.Profiles
                .Select(p => p.ProfileId)
                .FirstOrDefault();

            var globalConfig = new OmegaPlayer.Infrastructure.Data.Entities.GlobalConfig
            {
                LanguagePreference = "en",
                LastUsedProfile = defaultProfileId > 0 ? defaultProfileId : null
            };

            context.GlobalConfigs.Add(globalConfig);
            context.SaveChanges();
        }

        /// <summary>
        /// Gets database information for debugging synchronously
        /// </summary>
        public DatabaseInfo GetDatabaseInfo()
        {
            var info = new DatabaseInfo();

            try
            {
                if (!_embeddedPostgreSqlService.IsServerRunning)
                {
                    info.Status = "PostgreSQL server not running";
                    info.IsAccessible = false;
                    return info;
                }

                info.ServerInfo = _embeddedPostgreSqlService.GetServerInfo();
                info.Port = _embeddedPostgreSqlService.Port;
                info.IsAccessible = _embeddedPostgreSqlService.TestConnection();

                if (info.IsAccessible)
                {
                    var optionsBuilder = new DbContextOptionsBuilder<OmegaPlayerDbContext>();
                    optionsBuilder.UseNpgsql(_embeddedPostgreSqlService.ConnectionString);

                    using var context = new OmegaPlayerDbContext(optionsBuilder.Options);

                    // Get record counts
                    info.ProfileCount = context.Profiles.Count();
                    info.TrackCount = context.Tracks.Count();
                    info.PlaylistCount = context.Playlists.Count();
                    info.ArtistCount = context.Artists.Count();
                    info.AlbumCount = context.Albums.Count();

                    // Get database size information
                    GetDatabaseSizeInfo(context, info);

                    info.Status = "Connected and operational";
                }
                else
                {
                    info.Status = "Server running but database not accessible";
                }
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
                info.Status = $"Error: {ex.Message}";
            }

            return info;
        }

        /// <summary>
        /// Gets database size information from PostgreSQL synchronously
        /// </summary>
        private void GetDatabaseSizeInfo(OmegaPlayerDbContext context, DatabaseInfo info)
        {
            try
            {
                // Get database size using PostgreSQL system functions
                var connection = context.Database.GetDbConnection();
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT 
                        pg_database_size(current_database()) as database_size,
                        pg_size_pretty(pg_database_size(current_database())) as database_size_pretty";

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    info.SizeInBytes = reader.GetInt64("database_size");
                    info.SizeFormatted = reader.GetString("database_size_pretty");
                }
            }
            catch (Exception ex)
            {
                info.SizeFormatted = "Unknown";
            }
        }

        /// <summary>
        /// Performs database maintenance tasks synchronously
        /// </summary>
        public bool PerformMaintenance()
        {
            if (!_embeddedPostgreSqlService.IsServerRunning)
            {
                return false;
            }

            var optionsBuilder = new DbContextOptionsBuilder<OmegaPlayerDbContext>();
            optionsBuilder.UseNpgsql(_embeddedPostgreSqlService.ConnectionString);

            using var context = new OmegaPlayerDbContext(optionsBuilder.Options);

            // Update table statistics for better query performance
            context.Database.ExecuteSqlRaw("ANALYZE;");

            // Clean up any orphaned records (if needed)
            CleanupOrphanedRecords(context);

            return true;
        }

        /// <summary>
        /// Cleans up orphaned records in the database synchronously
        /// </summary>
        private void CleanupOrphanedRecords(OmegaPlayerDbContext context)
        {
            try
            {
                // Remove orphaned playlist tracks (tracks that no longer exist)
                var orphanedPlaylistTracks = context.PlaylistTracks
                    .Where(pt => pt.TrackId != null && !context.Tracks.Any(t => t.TrackId == pt.TrackId))
                    .ToList();

                if (orphanedPlaylistTracks.Any())
                {
                    context.PlaylistTracks.RemoveRange(orphanedPlaylistTracks);
                }

                // Remove orphaned queue tracks
                var orphanedQueueTracks = context.QueueTracks
                    .Where(qt => qt.TrackId != null && !context.Tracks.Any(t => t.TrackId == qt.TrackId))
                    .ToList();

                if (orphanedQueueTracks.Any())
                {
                    context.QueueTracks.RemoveRange(orphanedQueueTracks);
                }

                context.SaveChanges();
            }
            catch (Exception ex) { }
        }
    }

    /// <summary>
    /// Enhanced information about the PostgreSQL database
    /// </summary>
    public class DatabaseInfo
    {
        public string ServerInfo { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool IsAccessible { get; set; }
        public long SizeInBytes { get; set; }
        public string SizeFormatted { get; set; } = "0 KB";
        public int ProfileCount { get; set; }
        public int TrackCount { get; set; }
        public int PlaylistCount { get; set; }
        public int ArtistCount { get; set; }
        public int AlbumCount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public string Status { get; set; } = "Unknown";
    }
}