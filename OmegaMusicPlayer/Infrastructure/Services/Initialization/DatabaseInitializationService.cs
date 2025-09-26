using Microsoft.EntityFrameworkCore;
using OmegaMusicPlayer.Infrastructure.Data;
using OmegaMusicPlayer.Infrastructure.Services.Database;
using System;
using System.Data;
using System.Linq;

namespace OmegaMusicPlayer.Infrastructure.Services.Initialization
{
    /// <summary>
    /// Service to initialize the PostgreSQL database using EF Core and embedded PostgreSQL (Synchronous)
    /// Works with the EmbeddedPostgreSqlService for portable database deployment
    /// </summary>
    public class DatabaseInitializationService
    {
        private readonly EmbeddedPostgreSqlService _embeddedPostgreSqlService;
        private readonly DatabaseErrorHandlingService _errorHandler;

        private readonly string _defaultTheme =
            "{\"ThemeType\":2," +
            "\"MainStartColor\":null,\"MainEndColor\":null," +
            "\"SecondaryStartColor\":null,\"SecondaryEndColor\":null," +
            "\"AccentStartColor\":null,\"AccentEndColor\":null," +
            "\"TextStartColor\":null,\"TextEndColor\":null}";

        private readonly string _defaultSortingState =
            "{\"home\": {\"SortType\": 0, \"SortDirection\": 0}, \"album\": {\"SortType\": 0, \"SortDirection\": 0}, \"genre\": {\"SortType\": 0, \"SortDirection\": 0}," +
            " \"artist\": {\"SortType\": 0, \"SortDirection\": 0}, \"config\": {\"SortType\": 0, \"SortDirection\": 0}, \"folder\": {\"SortType\": 0, \"SortDirection\": 0}," +
            " \"details\": {\"SortType\": 0, \"SortDirection\": 0}, \"library\": {\"SortType\": 0, \"SortDirection\": 0}, \"playlist\": {\"SortType\": 0, \"SortDirection\": 0}}";

        private readonly string _defaultViewState = "{\"LibraryViewType\":\"Card\",\"DetailsViewType\":\"Image\",\"ContentType\":\"Library\"}";

        public DatabaseInitializationService(EmbeddedPostgreSqlService embeddedPostgreSqlService)
        {
            _embeddedPostgreSqlService = embeddedPostgreSqlService;
            _errorHandler = embeddedPostgreSqlService.ErrorHandler;
        }

        /// <summary>
        /// Initializes the database
        /// </summary>
        public DatabaseInitializationResult InitializeDatabase()
        {
            var result = new DatabaseInitializationResult();

            try
            {
                // Phase 1: Start PostgreSQL server
                var serverResult = _embeddedPostgreSqlService.StartServer();
                if (!serverResult.Success)
                {
                    result.Success = false;
                    result.Error = serverResult.Error;
                    result.Phase = $"Server Startup - {serverResult.Phase}";
                    return result;
                }

                // Phase 2: Test connection
                try
                {
                    var connectionWorks = _embeddedPostgreSqlService.TestConnection();
                    if (!connectionWorks)
                    {
                        var error = _errorHandler.AnalyzeException(
                            new InvalidOperationException("Cannot connect to PostgreSQL server after successful startup"),
                            "Connection Test");
                        result.Success = false;
                        result.Error = error;
                        result.Phase = "Connection Test";
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    var error = _errorHandler.AnalyzeException(ex, "Connection Test");
                    result.Success = false;
                    result.Error = error;
                    result.Phase = "Connection Test";
                    return result;
                }

                // Phase 3: Configure DbContext and handle database operations
                try
                {
                    var optionsBuilder = new DbContextOptionsBuilder<OmegaMusicPlayerDbContext>();
                    optionsBuilder.UseNpgsql(_embeddedPostgreSqlService.ConnectionString, options =>
                    {
                        // Configure PostgreSQL-specific options
                        options.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorCodesToAdd: null);
                        options.CommandTimeout(30);
                    });

                    using var context = new OmegaMusicPlayerDbContext(optionsBuilder.Options);

                    // Phase 4: Database schema operations
                    try
                    {
                        // Check if database needs migration or creation
                        var pendingMigrations = context.Database.GetPendingMigrations();
                        var appliedMigrations = context.Database.GetAppliedMigrations();

                        bool createDb = false;
                        try
                        {
                            createDb = context.Profiles.Any(); // should NOT create DB if profiles exist
                        }
                        catch
                        {
                            // Exception means DB needs to be created
                            createDb = false;
                        }

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
                    }
                    catch (Exception ex)
                    {
                        var error = _errorHandler.AnalyzeException(ex, "Database Schema Operations");
                        result.Success = false;
                        result.Error = error;
                        result.Phase = "Database Schema Operations";
                        return result;
                    }

                    // Phase 5: Final connection test
                    try
                    {
                        var finalTest = context.Database.CanConnect();
                        if (!finalTest)
                        {
                            var error = _errorHandler.AnalyzeException(
                                new InvalidOperationException("Final database connection test failed"),
                                "Final Connection Test");
                            result.Success = false;
                            result.Error = error;
                            result.Phase = "Final Connection Test";
                            return result;
                        }
                    }
                    catch (Exception ex)
                    {
                        var error = _errorHandler.AnalyzeException(ex, "Final Connection Test");
                        result.Success = false;
                        result.Error = error;
                        result.Phase = "Final Connection Test";
                        return result;
                    }
                }
                catch (Exception ex)
                {
                    var error = _errorHandler.AnalyzeException(ex, "DbContext Configuration");
                    result.Success = false;
                    result.Error = error;
                    result.Phase = "DbContext Configuration";
                    return result;
                }

                result.Success = true;
                result.ConnectionString = _embeddedPostgreSqlService.ConnectionString;
                return result;

            }
            catch (Exception ex)
            {
                // Catch-all for any unexpected exceptions
                var error = _errorHandler.AnalyzeException(ex, "Database Initialization");
                result.Success = false;
                result.Error = error;
                result.Phase = "Unexpected Error";
                return result;
            }
        }

        /// <summary>
        /// Creates default data when database is first created
        /// </summary>
        private void CreateDefaultData(OmegaMusicPlayerDbContext context)
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
                throw new InvalidOperationException("Failed to create default database data", ex);
            }
        }

        /// <summary>
        /// Ensures default data exists (for cases where database exists but data was lost)
        /// </summary>
        private void EnsureDefaultDataExists(OmegaMusicPlayerDbContext context)
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
                // Don't throw
            }
        }

        /// <summary>
        /// Creates the default profile
        /// </summary>
        private void CreateDefaultProfile(OmegaMusicPlayerDbContext context)
        {
            var defaultProfile = new Data.Entities.Profile
            {
                ProfileName = "Default",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Profiles.Add(defaultProfile);
            context.SaveChanges();

            // Create profile config
            var profileConfig = new Data.Entities.ProfileConfig
            {
                ProfileId = defaultProfile.ProfileId,
                EqualizerPresets = "{}",
                LastVolume = 50,
                Theme = _defaultTheme,
                DynamicPause = false,
                ViewState = _defaultViewState,
                SortingState = _defaultSortingState
            };

            context.ProfileConfigs.Add(profileConfig);
            context.SaveChanges();
        }

        /// <summary>
        /// Creates the default global configuration
        /// </summary>
        private void CreateDefaultGlobalConfig(OmegaMusicPlayerDbContext context)
        {
            var defaultProfileId = context.Profiles
                .Select(p => p.ProfileId)
                .FirstOrDefault();

            var globalConfig = new Data.Entities.GlobalConfig
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
                    var optionsBuilder = new DbContextOptionsBuilder<OmegaMusicPlayerDbContext>();
                    optionsBuilder.UseNpgsql(_embeddedPostgreSqlService.ConnectionString);

                    using var context = new OmegaMusicPlayerDbContext(optionsBuilder.Options);

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
        private void GetDatabaseSizeInfo(OmegaMusicPlayerDbContext context, DatabaseInfo info)
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
                info.SizeFormatted = $"Unknown ({ex.Message})";
            }
        }
    }

    /// <summary>
    /// Result object for database initialization operations
    /// </summary>
    public class DatabaseInitializationResult
    {
        public bool Success { get; set; }
        public DatabaseErrorHandlingService.DatabaseError? Error { get; set; }
        public string? Phase { get; set; }
        public string? ConnectionString { get; set; }

        public static DatabaseInitializationResult IsSuccess(string? connectionString = null)
        {
            return new DatabaseInitializationResult
            {
                Success = true,
                ConnectionString = connectionString
            };
        }

        public static DatabaseInitializationResult Failure(DatabaseErrorHandlingService.DatabaseError error, string phase)
        {
            return new DatabaseInitializationResult
            {
                Success = false,
                Error = error,
                Phase = phase
            };
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