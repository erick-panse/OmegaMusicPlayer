using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using NAudio.Wave;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Enums.PresetTheme;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Features.Playback.Services;
using OmegaMusicPlayer.Features.Playback.ViewModels;
using OmegaMusicPlayer.Infrastructure.Data.Repositories;
using OmegaMusicPlayer.Infrastructure.Data;
using OmegaMusicPlayer.Infrastructure.Services;
using OmegaMusicPlayer.Infrastructure.Services.Database;
using OmegaMusicPlayer.UI.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using OmegaMusicPlayer.Core.Models;

namespace OmegaMusicPlayer.Core.Services
{
    /// <summary>
    /// Provides recovery capabilities for handling serious application failures.
    /// </summary>
    public class ErrorRecoveryService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IDbContextFactory<OmegaMusicPlayerDbContext> _contextFactory;
        private readonly EmbeddedPostgreSqlService _embeddedPostgreSqlService;

        // Recovery state tracking
        private int _recoveryAttempts = 0;
        private DateTime _lastRecoveryAttempt = DateTime.MinValue;
        private bool _isRecoveryInProgress = false;
        private readonly object _recoveryLock = new object();

        // Recovery settings
        private const int MAX_RECOVERY_ATTEMPTS = 3;
        private static readonly TimeSpan RECOVERY_COOLDOWN = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan EMERGENCY_SHUTDOWN_DELAY = TimeSpan.FromSeconds(10);

        // Track services that have been recovered
        private readonly HashSet<string> _recoveredServices = new HashSet<string>();

        public ErrorRecoveryService(
            IServiceProvider serviceProvider,
            IErrorHandlingService errorHandlingService,
            IDbContextFactory<OmegaMusicPlayerDbContext> contextFactory,
            EmbeddedPostgreSqlService embeddedPostgreSqlService,
            IMessenger messenger)
        {
            _serviceProvider = serviceProvider;
            _errorHandlingService = errorHandlingService;
            _contextFactory = contextFactory;
            _embeddedPostgreSqlService = embeddedPostgreSqlService;

            // Subscribe to error messages to handle critical errors automatically
            messenger.Register<ErrorOccurredMessage>(this, (r, m) => HandleCriticalErrorMessage(m));
        }

        private void HandleCriticalErrorMessage(ErrorOccurredMessage m)
        {
            // Only handle critical errors
            if (m.Severity == ErrorSeverity.Critical)
            {
                // Run recovery asynchronously to avoid blocking UI thread
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Create an emergency backup
                        await CreateEmergencyBackupAsync();

                        // Determine which subsystem is affected
                        string affectedSubsystem = DetermineAffectedSubsystem(m.Message, m.Details, m.Exception);

                        // Try to recover the affected subsystem
                        if (!string.IsNullOrEmpty(affectedSubsystem))
                        {
                            await RecoverSubsystemAsync(affectedSubsystem);
                        }

                        // If it's a fatal error type, consider emergency shutdown
                        if (IsFatalError(m.Exception))
                        {
                            await InitiateEmergencyShutdownAsync(m.Message);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log the error but avoid recursive critical errors
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to handle critical error",
                            "An error occurred while trying to recover from a critical error.",
                            ex,
                            false);
                    }
                });
            }
        }

        /// <summary>
        /// Determines which subsystem is likely affected based on error information
        /// </summary>
        private string DetermineAffectedSubsystem(string message, string details, Exception exception)
        {
            if (exception != null)
            {
                var stackTrace = exception.StackTrace ?? "";
                var exceptionType = exception.GetType().Name;
                var exceptionMessage = exception.Message ?? "";

                // Check for database-related errors (updated for PostgreSQL)
                if (stackTrace.Contains("PostgresEmbed") ||
                    stackTrace.Contains("Npgsql") ||
                    stackTrace.Contains("DbContext") ||
                    exceptionType.Contains("Npgsql") ||
                    exceptionType.Contains("Postgres") ||
                    exceptionMessage.Contains("database") ||
                    message.Contains("database", StringComparison.OrdinalIgnoreCase))
                {
                    return "database";
                }

                // Check for profile-related errors
                if (stackTrace.Contains("ProfileManager") ||
                    stackTrace.Contains("Profile") ||
                    message.Contains("profile", StringComparison.OrdinalIgnoreCase))
                {
                    return "profile";
                }

                // Check for playback-related errors
                if (stackTrace.Contains("TrackControl") ||
                    stackTrace.Contains("Playback") ||
                    message.Contains("playback", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("audio", StringComparison.OrdinalIgnoreCase))
                {
                    return "playback";
                }

                // Check for UI-related errors
                if (stackTrace.Contains("ThemeService") ||
                    stackTrace.Contains("UI") ||
                    message.Contains("UI", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("theme", StringComparison.OrdinalIgnoreCase) ||
                    message.Contains("display", StringComparison.OrdinalIgnoreCase))
                {
                    return "ui";
                }
            }

            // Default to "all" if we can't determine a specific subsystem
            return "all";
        }

        /// <summary>
        /// Determines if the exception is a fatal error that might require application restart
        /// </summary>
        private bool IsFatalError(Exception exception)
        {
            if (exception == null) return false;

            // Check for critical system exceptions
            return exception is OutOfMemoryException ||
                   exception is StackOverflowException ||
                   exception is AccessViolationException ||
                   exception is System.Threading.ThreadAbortException ||
                   exception is IOException ||
                   exception is System.Runtime.InteropServices.ExternalException;
        }

        /// <summary>
        /// Attempts to recover from a critical error in a specific subsystem.
        /// </summary>
        public async Task<bool> RecoverSubsystemAsync(string subsystem)
        {
            try
            {
                // Prevent concurrent recovery attempts
                lock (_recoveryLock)
                {
                    if (_isRecoveryInProgress)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Recovery already in progress",
                            $"Ignoring recovery request for {subsystem} as another recovery is in progress.",
                            null,
                            false);
                        return false;
                    }

                    // Check if we're still in cooldown
                    if (DateTime.Now - _lastRecoveryAttempt < RECOVERY_COOLDOWN)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Recovery cooldown period active",
                            $"Recovery for {subsystem} was requested too soon after a previous recovery attempt.",
                            null,
                            true);
                        return false;
                    }

                    // Check if we've exceeded max attempts
                    if (_recoveryAttempts >= MAX_RECOVERY_ATTEMPTS)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Critical,
                            "Maximum recovery attempts exceeded",
                            "The application has reached the maximum number of recovery attempts and may need to be restarted.",
                            null,
                            true);
                        return false;
                    }

                    // Set recovery in progress
                    _isRecoveryInProgress = true;
                    _recoveryAttempts++;
                    _lastRecoveryAttempt = DateTime.Now;
                }

                // Notify user that recovery is starting
                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "System recovery initiated",
                    $"Attempting to recover from errors in the {subsystem} subsystem.",
                    null,
                    true);

                // Attempt subsystem-specific recovery
                bool success = false;
                switch (subsystem.ToLowerInvariant())
                {
                    case "database":
                        success = await RecoverDatabaseSubsystemAsync();
                        break;

                    case "profile":
                        success = await RecoverProfileSubsystemAsync();
                        break;

                    case "playback":
                        success = await RecoverPlaybackSubsystemAsync();
                        break;

                    case "ui":
                        success = await RecoverUISubsystemAsync();
                        break;

                    case "all":
                        success = await PerformFullSystemRecoveryAsync();
                        break;

                    default:
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Unknown subsystem for recovery",
                            $"Recovery was requested for unknown subsystem: {subsystem}",
                            null,
                            true);
                        success = false;
                        break;
                }

                // Update recovery state
                lock (_recoveryLock)
                {
                    _isRecoveryInProgress = false;

                    if (success)
                    {
                        _recoveredServices.Add(subsystem.ToLowerInvariant());
                    }
                }

                // Notify about recovery result
                if (success)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Info,
                        "System recovery successful",
                        $"Successfully recovered from errors in the {subsystem} subsystem.",
                        null,
                        true);
                }
                else
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "System recovery failed",
                        $"Failed to recover from errors in the {subsystem} subsystem.",
                        null,
                        true);
                }

                return success;
            }
            catch (Exception ex)
            {
                // Reset recovery state
                lock (_recoveryLock)
                {
                    _isRecoveryInProgress = false;
                }

                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Error during recovery process",
                    $"An unexpected error occurred while attempting to recover the {subsystem} subsystem.",
                    ex,
                    true);

                return false;
            }
        }

        /// <summary>
        /// Recovers from database-related errors using PostgreSQL + Entity Framework.
        /// </summary>
        private async Task<bool> RecoverDatabaseSubsystemAsync()
        {
            try
            {
                // 1. Test PostgreSQL server status
                if (!_embeddedPostgreSqlService.IsServerRunning)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "PostgreSQL server not running",
                        "The embedded PostgreSQL server is not running.",
                        null,
                        true);
                    return false;
                }

                // 2. Test database connection using Entity Framework
                try
                {
                    using var context = _contextFactory.CreateDbContext();
                    var canConnect = await context.Database.CanConnectAsync();

                    if (!canConnect)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Critical,
                            "Database connection test failed",
                            "Could not establish a working connection to the PostgreSQL database.",
                            null,
                            true);
                        return false;
                    }

                    // Test basic query
                    await context.GlobalConfigs.CountAsync();
                }
                catch (Exception ex)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "Database functionality test failed",
                        "Database connection exists but basic operations are failing.",
                        ex,
                        true);
                    return false;
                }

                // 3. Reset/rebuild any in-memory caches of database data
                var globalConfigService = _serviceProvider.GetService<GlobalConfigurationService>();
                if (globalConfigService != null)
                {
                    globalConfigService.InvalidateCache();
                }

                var profileConfigService = _serviceProvider.GetService<ProfileConfigurationService>();
                if (profileConfigService != null)
                {
                    profileConfigService.InvalidateCache();
                }

                // 4. Clear repository caches if they exist
                var allTracksRepo = _serviceProvider.GetService<AllTracksRepository>();
                if (allTracksRepo != null)
                {
                    allTracksRepo.InvalidateAllCaches();
                }

                return true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Database recovery failed",
                    "Failed to recover PostgreSQL database connection and services.",
                    ex,
                    true);
                return false;
            }
        }

        /// <summary>
        /// Recovers from profile-related errors.
        /// </summary>
        private async Task<bool> RecoverProfileSubsystemAsync()
        {
            try
            {
                // 1. Get required services
                var profileManager = _serviceProvider.GetService<ProfileManager>();
                var stateManager = _serviceProvider.GetService<StateManagerService>();

                if (profileManager == null || stateManager == null)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "Profile recovery failed",
                        "Required profile services are not available.",
                        null,
                        true);
                    return false;
                }

                // 2. First attempt to reset profile manager to a stable state
                await profileManager.ResetToStableState();

                // 3. Then reset state manager to defaults
                await stateManager.ResetStateToDefaults();

                return true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Profile recovery failed",
                    "Failed to recover profile and state management systems.",
                    ex,
                    true);
                return false;
            }
        }

        /// <summary>
        /// Recovers from playback-related errors.
        /// </summary>
        private async Task<bool> RecoverPlaybackSubsystemAsync()
        {
            try
            {
                // 1. Get required services
                var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
                var queueService = _serviceProvider.GetService<QueueService>();
                var audioMonitorService = _serviceProvider.GetService<AudioMonitorService>();

                if (trackControlVM == null)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "Playback recovery failed",
                        "Required playback services are not available.",
                        null,
                        true);
                    return false;
                }

                // 2. Stop any current playback
                trackControlVM.StopPlayback();

                // 3. Reset queue if available
                if (queueService != null)
                {
                    try
                    {
                        // Use the convenience method that doesn't require profile ID
                        await queueService.ClearQueue();

                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Queue cleared",
                            "Playback queue has been reset during recovery.",
                            null,
                            false);
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to clear queue during recovery",
                            ex.Message,
                            ex,
                            false);
                        // Continue with recovery - this is not critical
                    }
                }

                // 4. Reset audio monitor if available
                if (audioMonitorService != null)
                {
                    try
                    {
                        // Use the dedicated Reset method
                        audioMonitorService.Reset();

                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Audio monitor reset",
                            "Audio monitoring system has been reset during recovery.",
                            null,
                            false);
                    }
                    catch (Exception ex)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.NonCritical,
                            "Failed to reset audio monitoring during recovery",
                            ex.Message,
                            ex,
                            false);
                        // Continue with recovery - this is not critical
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Playback recovery failed",
                    "Failed to recover audio playback system.",
                    ex,
                    true);
                return false;
            }
        }

        /// <summary>
        /// Recovers from UI-related errors.
        /// </summary>
        private async Task<bool> RecoverUISubsystemAsync()
        {
            try
            {
                // 1. Get required services
                var themeService = _serviceProvider.GetService<ThemeService>();
                var toastService = _serviceProvider.GetService<ToastNotificationService>();

                // 2. Reset any themes to defaults
                if (themeService != null)
                {
                    themeService.ApplyPresetTheme(PresetTheme.Dark);
                }

                // 3. Clear any notifications
                if (toastService != null)
                {
                    toastService.ClearAllNotifications();
                }

                return true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "UI recovery failed",
                    "Failed to recover user interface components.",
                    ex,
                    true);
                return false;
            }
        }

        /// <summary>
        /// Performs a full system recovery.
        /// </summary>
        private async Task<bool> PerformFullSystemRecoveryAsync()
        {
            // Track failed subsystems
            var failedSubsystems = new List<string>();

            // 1. Recover database first as other subsystems depend on it
            if (!await RecoverDatabaseSubsystemAsync())
            {
                failedSubsystems.Add("database");
            }

            // 2. Recover profile system
            if (!await RecoverProfileSubsystemAsync())
            {
                failedSubsystems.Add("profile");
            }

            // 3. Recover playback system
            if (!await RecoverPlaybackSubsystemAsync())
            {
                failedSubsystems.Add("playback");
            }

            // 4. Recover UI
            if (!await RecoverUISubsystemAsync())
            {
                failedSubsystems.Add("ui");
            }

            // Check if all systems were recovered
            if (failedSubsystems.Count > 0)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Partial system recovery",
                    $"Failed to recover all subsystems. Still experiencing issues with: {string.Join(", ", failedSubsystems)}",
                    null,
                    true);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Creates a backup of critical application state for crash recovery.
        /// </summary>
        public async Task CreateEmergencyBackupAsync()
        {
            try
            {
                var backupPath = GetEmergencyBackupPath();
                var backupDir = Path.GetDirectoryName(backupPath);

                // Create directory if it doesn't exist
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                // Collect critical state
                var backup = new EmergencyBackup();

                // 1. Get profile state
                var profileManager = _serviceProvider.GetService<ProfileManager>();
                if (profileManager != null)
                {
                    var profile = await profileManager.GetCurrentProfileAsync();
                    backup.CurrentProfileId = profile.ProfileID;
                }

                // 2. Get playback state
                var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
                if (trackControlVM != null)
                {
                    backup.Volume = trackControlVM.TrackVolume;
                    backup.IsPlaying = trackControlVM.IsPlaying == PlaybackState.Playing ? true : false;
                }

                // Save to file
                using (var stream = new FileStream(backupPath, FileMode.Create))
                {
                    await JsonSerializer.SerializeAsync(stream, backup);
                }

                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Emergency backup created",
                    "Created emergency state backup that can be used to recover after a restart.",
                    null,
                    false);
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to create emergency backup",
                    "Could not save emergency backup for recovery after restart.",
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Tries to restore from emergency backup after a crash.
        /// </summary>
        public async Task<bool> TryRestoreFromBackupAsync()
        {
            try
            {
                var backupPath = GetEmergencyBackupPath();

                // Check if backup exists
                if (!File.Exists(backupPath))
                {
                    return false;
                }

                // Check if backup is recent (less than 5 minutes old)
                var fileInfo = new FileInfo(backupPath);
                if (DateTime.Now - fileInfo.LastWriteTime > TimeSpan.FromMinutes(5))
                {
                    // Backup is too old, delete it
                    File.Delete(backupPath);
                    return false;
                }

                // Read backup
                EmergencyBackup backup;
                using (var stream = new FileStream(backupPath, FileMode.Open))
                {
                    backup = await JsonSerializer.DeserializeAsync<EmergencyBackup>(stream);
                }

                if (backup == null)
                {
                    return false;
                }

                // Restore profile state
                if (backup.CurrentProfileId > 0)
                {
                    var profileManager = _serviceProvider.GetService<ProfileManager>();
                    var stateManager = _serviceProvider.GetService<StateManagerService>();

                    if (profileManager != null && stateManager != null)
                    {
                        await stateManager.LoadAndApplyState(true);
                    }
                }

                // Restore volume
                if (backup.Volume > 0)
                {
                    var trackControlVM = _serviceProvider.GetService<TrackControlViewModel>();
                    if (trackControlVM != null)
                    {
                        trackControlVM.TrackVolume = backup.Volume;
                        trackControlVM.SetVolume();
                    }
                }

                // Delete backup after successful restore
                File.Delete(backupPath);

                _errorHandlingService.LogError(
                    ErrorSeverity.Info,
                    "Emergency backup restored",
                    "Successfully restored application state from emergency backup.",
                    null,
                    true);

                return true;
            }
            catch (Exception ex)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    "Failed to restore from emergency backup",
                    "Could not restore application state from emergency backup.",
                    ex,
                    true);
                return false;
            }
        }

        /// <summary>
        /// Gets the path for emergency backup files.
        /// </summary>
        private string GetEmergencyBackupPath()
        {
            // Use centralized application data path (automatically separates dev/release)
            var recoveryDir = Path.Combine(AppConfiguration.ApplicationDataPath, "Recovery");

            // Ensure directory exists
            if (!Directory.Exists(recoveryDir))
            {
                Directory.CreateDirectory(recoveryDir);
            }

            // Include build suffix in filename for clarity
            var buildSuffix = AppConfiguration.IsDebugBuild ? "-dev" : "";
            return Path.Combine(recoveryDir, $"emergency_backup{buildSuffix}.json");
        }

        /// <summary>
        /// Initiates an emergency shutdown with a delay to allow user to see the message.
        /// </summary>
        public async Task InitiateEmergencyShutdownAsync(string reason)
        {
            try
            {
                // Log the emergency shutdown
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "EMERGENCY SHUTDOWN INITIATED",
                    $"The application will shutdown in {EMERGENCY_SHUTDOWN_DELAY.TotalSeconds} seconds. Reason: {reason}",
                    null,
                    true);

                // Create emergency backup
                await CreateEmergencyBackupAsync();

                // Wait to allow user to see the message
                await Task.Delay(EMERGENCY_SHUTDOWN_DELAY);

                // Exit application
                Environment.Exit(1);
            }
            catch (Exception ex)
            {
                // If even emergency shutdown fails, just exit immediately
                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Emergency shutdown procedure failed",
                    "Forcing immediate exit.",
                    ex,
                    false);

                Environment.Exit(2);
            }
        }
    }

    /// <summary>
    /// Data structure for emergency backup.
    /// </summary>
    public class EmergencyBackup
    {
        public int CurrentProfileId { get; set; }
        public float Volume { get; set; }
        public bool IsPlaying { get; set; }
        public DateTime BackupTime { get; set; } = DateTime.Now;
    }
}