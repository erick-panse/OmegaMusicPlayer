using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Infrastructure.Data;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

namespace OmegaPlayer.Infrastructure.Services.Initialization
{
    /// <summary>
    /// Simple service to initialize the SQLite database using EF Core
    /// Works alongside your existing repository system
    /// </summary>
    public class DatabaseInitializationService
    {
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly ILogger<DatabaseInitializationService>? _logger;

        public DatabaseInitializationService(
            IErrorHandlingService errorHandlingService,
            ILogger<DatabaseInitializationService>? logger = null)
        {
            _errorHandlingService = errorHandlingService;
            _logger = logger;
        }

        /// <summary>
        /// Ensures the SQLite database exists and is up to date
        /// </summary>
        public async Task<bool> InitializeDatabaseAsync(string connectionString)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    _logger?.LogInformation("Initializing OmegaPlayer SQLite database...");

                    // Ensure the directory exists
                    EnsureDatabaseDirectoryExists(connectionString);

                    var optionsBuilder = new DbContextOptionsBuilder<OmegaPlayerDbContext>();
                    optionsBuilder.UseSqlite(connectionString);

                    using var context = new OmegaPlayerDbContext(optionsBuilder.Options);

                    // Ensure database exists (creates if it doesn't)
                    var created = await context.Database.EnsureCreatedAsync();

                    if (created)
                    {
                        _logger?.LogInformation("SQLite database created successfully");

                        // Create default data if database was just created
                        await CreateDefaultDataAsync(context);
                    }
                    else
                    {
                        _logger?.LogInformation("SQLite database already exists");

                        // Check if we need to create default data (in case it was deleted)
                        await EnsureDefaultDataExistsAsync(context);
                    }

                    // Test connection
                    var canConnect = await context.Database.CanConnectAsync();
                    if (!canConnect)
                    {
                        throw new InvalidOperationException("Cannot connect to SQLite database with provided connection string");
                    }

                    _logger?.LogInformation("Database initialization completed");
                    return true;
                },
                "SQLite database initialization",
                false,
                ErrorSeverity.Critical,
                true);
        }

        /// <summary>
        /// Tests if SQLite database connection works
        /// </summary>
        public async Task<bool> TestConnectionAsync(string connectionString)
        {
            return await _errorHandlingService.SafeExecuteAsync(
                async () =>
                {
                    var optionsBuilder = new DbContextOptionsBuilder<OmegaPlayerDbContext>();
                    optionsBuilder.UseSqlite(connectionString);

                    using var context = new OmegaPlayerDbContext(optionsBuilder.Options);
                    return await context.Database.CanConnectAsync();
                },
                "SQLite database connection test",
                false,
                ErrorSeverity.NonCritical,
                false);
        }

        /// <summary>
        /// Ensures the directory for the SQLite database exists
        /// </summary>
        private void EnsureDatabaseDirectoryExists(string connectionString)
        {
            try
            {
                // Extract file path from connection string
                var dbPath = ExtractDatabasePath(connectionString);
                if (!string.IsNullOrEmpty(dbPath))
                {
                    var directory = Path.GetDirectoryName(dbPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        _logger?.LogInformation($"Created database directory: {directory}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating database directory");
                // Don't throw - let EF Core handle the error
            }
        }

        /// <summary>
        /// Extracts the database file path from SQLite connection string
        /// </summary>
        private string ExtractDatabasePath(string connectionString)
        {
            try
            {
                // Simple extraction - look for "Data Source=" pattern
                var parts = connectionString.Split(';');
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
                    {
                        return trimmed.Substring("Data Source=".Length);
                    }
                }
            }
            catch
            {
                // Ignore parsing errors
            }

            return string.Empty;
        }

        /// <summary>
        /// Creates default data when database is first created
        /// </summary>
        private async Task CreateDefaultDataAsync(OmegaPlayerDbContext context)
        {
            try
            {
                // Create default profile
                await CreateDefaultProfileAsync(context);

                // Create default global config
                await CreateDefaultGlobalConfigAsync(context);

                _logger?.LogInformation("Created default application data");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating default data");
                throw;
            }
        }

        /// <summary>
        /// Ensures default data exists (for cases where database exists but data was lost)
        /// </summary>
        private async Task EnsureDefaultDataExistsAsync(OmegaPlayerDbContext context)
        {
            try
            {
                // Check and create default profile if missing
                if (!await context.Profiles.AnyAsync())
                {
                    await CreateDefaultProfileAsync(context);
                    _logger?.LogInformation("Restored missing default profile");
                }

                // Check and create global config if missing
                if (!await context.GlobalConfigs.AnyAsync())
                {
                    await CreateDefaultGlobalConfigAsync(context);
                    _logger?.LogInformation("Restored missing global configuration");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error ensuring default data exists");
                // Don't throw - this is not critical
            }
        }

        /// <summary>
        /// Creates the default profile
        /// </summary>
        private async Task CreateDefaultProfileAsync(OmegaPlayerDbContext context)
        {
            var defaultProfile = new OmegaPlayer.Infrastructure.Data.Entities.Profile
            {
                ProfileName = "Default",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Profiles.Add(defaultProfile);
            await context.SaveChangesAsync();

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
            await context.SaveChangesAsync();

            _logger?.LogInformation("Created default profile and configuration");
        }

        /// <summary>
        /// Creates the default global configuration
        /// </summary>
        private async Task CreateDefaultGlobalConfigAsync(OmegaPlayerDbContext context)
        {
            var defaultProfileId = await context.Profiles
                .Select(p => p.ProfileId)
                .FirstOrDefaultAsync();

            var globalConfig = new OmegaPlayer.Infrastructure.Data.Entities.GlobalConfig
            {
                LanguagePreference = "en",
                LastUsedProfile = defaultProfileId > 0 ? defaultProfileId : null
            };

            context.GlobalConfigs.Add(globalConfig);
            await context.SaveChangesAsync();

            _logger?.LogInformation("Created global configuration");
        }

        /// <summary>
        /// Gets database file information for debugging
        /// </summary>
        public async Task<DatabaseInfo> GetDatabaseInfoAsync(string connectionString)
        {
            var info = new DatabaseInfo();

            try
            {
                var dbPath = ExtractDatabasePath(connectionString);
                info.FilePath = dbPath;

                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    info.SizeInBytes = fileInfo.Length;
                    info.LastModified = fileInfo.LastWriteTime;
                    info.Exists = true;

                    // Test connection
                    info.IsAccessible = await TestConnectionAsync(connectionString);

                    // Get record counts if accessible
                    if (info.IsAccessible)
                    {
                        var optionsBuilder = new DbContextOptionsBuilder<OmegaPlayerDbContext>();
                        optionsBuilder.UseSqlite(connectionString);

                        using var context = new OmegaPlayerDbContext(optionsBuilder.Options);

                        info.ProfileCount = await context.Profiles.CountAsync();
                        info.TrackCount = await context.Tracks.CountAsync();
                        info.PlaylistCount = await context.Playlists.CountAsync();
                    }
                }
                else
                {
                    info.Exists = false;
                    info.IsAccessible = false;
                }
            }
            catch (Exception ex)
            {
                info.ErrorMessage = ex.Message;
            }

            return info;
        }
    }

    /// <summary>
    /// Information about the SQLite database file
    /// </summary>
    public class DatabaseInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public bool Exists { get; set; }
        public bool IsAccessible { get; set; }
        public long SizeInBytes { get; set; }
        public DateTime LastModified { get; set; }
        public int ProfileCount { get; set; }
        public int TrackCount { get; set; }
        public int PlaylistCount { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public string SizeFormatted => SizeInBytes > 0
            ? $"{SizeInBytes / 1024:N0} KB"
            : "0 KB";

        public string Status => !Exists
            ? "Not created"
            : !IsAccessible
                ? "Not accessible"
                : "Ready";
    }
}