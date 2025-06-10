using Microsoft.Data.Sqlite;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Data.Common;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Manages SQLite database connections with automatic setup
    /// Simple, reliable, and portable database solution
    /// </summary>
    public class DbConnection : IDisposable
    {
        public SqliteConnection dbConn { get; private set; }

        private readonly IErrorHandlingService _errorHandlingService;

        // Retry configuration
        private static readonly int MAX_RETRIES = 3;
        private static readonly TimeSpan INITIAL_RETRY_DELAY = TimeSpan.FromSeconds(1);
        private static readonly double RETRY_BACKOFF_FACTOR = 2.0;

        // Connection settings
        private static readonly int COMMAND_TIMEOUT_SECONDS = 60;

        // Connection status tracking
        private bool _isConnectionOpen = false;
        private int _consecutiveFailures = 0;
        private static readonly object _globalConnectionLock = new object();
        private static bool _isGlobalFailureMode = false;
        private static DateTime _globalFailureModeUntil = DateTime.MinValue;

        // Circuit breaker pattern settings
        private static readonly int FAILURES_THRESHOLD = 5;
        private static readonly TimeSpan CIRCUIT_BREAKER_RESET_TIME = TimeSpan.FromMinutes(2);

        public DbConnection(IErrorHandlingService errorHandlingService = null)
        {
            _errorHandlingService = errorHandlingService;

            try
            {
                // Check if we're in global failure mode
                if (IsInGlobalFailureMode())
                {
                    throw new InvalidOperationException("Database connections temporarily disabled due to repeated failures.");
                }

                string connectionString = GetOrCreateConnectionString();

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Could not establish database connection string.");
                }

                dbConn = new SqliteConnection(connectionString);
                OpenConnectionWithRetry();
                ConfigureSQLite();
            }
            catch (Exception ex)
            {
                HandleConnectionError(ex);
                RecordConnectionFailure();
                throw;
            }
        }

        /// <summary>
        /// Gets or creates a working SQLite connection string
        /// Priority: Environment variable (developers) → Local SQLite file (always)
        /// </summary>
        private string GetOrCreateConnectionString()
        {
            // 1. Try environment variable first (for developers/testing)
            //var envConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            //if (!string.IsNullOrEmpty(envConnectionString))
            //{
            //    return envConnectionString;
            //}

            // 2. Create SQLite database in application data folder
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var omegaPath = Path.Combine(appDataPath, "OmegaPlayer");

                // Ensure directory exists
                Directory.CreateDirectory(omegaPath);

                var dbFilePath = Path.Combine(omegaPath, "OmegaPlayer.db");

                var connectionString = $"Data Source={dbFilePath};";

                return connectionString;
            }
            catch (Exception ex)
            {
                LogError("Failed to create SQLite database path", ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// Configure SQLite for optimal performance and concurrency
        /// </summary>
        private void ConfigureSQLite()
        {
            try
            {
                // Enable WAL mode for better concurrency (readers don't block writers)
                using var walCommand = new SqliteCommand("PRAGMA journal_mode = WAL;", dbConn);
                walCommand.ExecuteNonQuery();

                // Set synchronous mode to NORMAL for good balance of safety and performance
                using var syncCommand = new SqliteCommand("PRAGMA synchronous = NORMAL;", dbConn);
                syncCommand.ExecuteNonQuery();

                // ADDED: Set busy timeout for lock conflicts
                using var busyCommand = new SqliteCommand("PRAGMA busy_timeout = 30000;", dbConn);
                busyCommand.ExecuteNonQuery();

                // Increase cache size for better performance (10MB)
                using var cacheCommand = new SqliteCommand("PRAGMA cache_size = -10000;", dbConn);
                cacheCommand.ExecuteNonQuery();

                // Enable foreign key constraints
                using var fkCommand = new SqliteCommand("PRAGMA foreign_keys = ON;", dbConn);
                fkCommand.ExecuteNonQuery();

                // ADDED: Optimize for concurrent access
                using var lockingCommand = new SqliteCommand("PRAGMA locking_mode = NORMAL;", dbConn);
                lockingCommand.ExecuteNonQuery();

                // ADDED: Set page size for better performance (4KB is optimal for most cases)
                using var pageCommand = new SqliteCommand("PRAGMA page_size = 4096;", dbConn);
                pageCommand.ExecuteNonQuery();

                // ADDED: Vacuum database occasionally for maintenance
                using var autoVacuumCommand = new SqliteCommand("PRAGMA auto_vacuum = INCREMENTAL;", dbConn);
                autoVacuumCommand.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                LogError("Failed to configure SQLite", ex.Message, ex);
                // Non-fatal - continue with default settings
            }
        }

        /// <summary>
        /// Opens a database connection with retry logic
        /// </summary>
        private void OpenConnectionWithRetry()
        {
            if (_isConnectionOpen)
                return;

            Exception lastException = null;
            TimeSpan delay = INITIAL_RETRY_DELAY;

            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    if (dbConn.State == ConnectionState.Open)
                    {
                        _isConnectionOpen = true;
                        return;
                    }

                    dbConn.Open();
                    _isConnectionOpen = true;

                    // Reset consecutive failures on success
                    _consecutiveFailures = 0;

                    return; // Success
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // Log each retry attempt
                    if (_errorHandlingService != null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Critical,
                            $"Database connection attempt {attempt} failed",
                            ex.Message,
                            ex,
                            false); // Don't show notification for retries
                    }

                    if (attempt < MAX_RETRIES)
                    {
                        // Exponential backoff with jitter
                        int jitterMs = new Random().Next(0, 500);
                        TimeSpan delayWithJitter = delay + TimeSpan.FromMilliseconds(jitterMs);
                        Task.Delay(delayWithJitter).Wait();

                        // Increase delay for next attempt
                        delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * RETRY_BACKOFF_FACTOR);
                    }
                }
            }

            // If we've exhausted retries, record the failure
            RecordConnectionFailure();

            // If we've exhausted retries, throw the last exception
            throw new InvalidOperationException($"Failed to connect to database after {MAX_RETRIES} attempts", lastException);
        }

        /// <summary>
        /// Records a connection failure for circuit breaker pattern
        /// </summary>
        private void RecordConnectionFailure()
        {
            _consecutiveFailures++;

            // If we've reached the failure threshold, engage the circuit breaker
            if (_consecutiveFailures >= FAILURES_THRESHOLD)
            {
                lock (_globalConnectionLock)
                {
                    _isGlobalFailureMode = true;
                    _globalFailureModeUntil = DateTime.Now + CIRCUIT_BREAKER_RESET_TIME;

                    if (_errorHandlingService != null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Critical,
                            "Database connection circuit breaker engaged",
                            $"Database connections will be temporarily disabled until {_globalFailureModeUntil}.",
                            null,
                            true);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if we're in global failure mode (circuit breaker pattern)
        /// </summary>
        private bool IsInGlobalFailureMode()
        {
            lock (_globalConnectionLock)
            {
                // If we're in failure mode but the time has elapsed, reset it
                if (_isGlobalFailureMode && DateTime.Now > _globalFailureModeUntil)
                {
                    _isGlobalFailureMode = false;

                    if (_errorHandlingService != null)
                    {
                        _errorHandlingService.LogError(
                            ErrorSeverity.Info,
                            "Database circuit breaker reset",
                            "Database connections will be attempted again.",
                            null,
                            true);
                    }

                    return false;
                }

                return _isGlobalFailureMode;
            }
        }

        /// <summary>
        /// Handles connection errors with specific error messages
        /// </summary>
        private void HandleConnectionError(Exception ex)
        {
            if (_errorHandlingService != null)
            {
                string details = ex switch
                {
                    SqliteException sqliteEx => GetDetailedSqliteErrorMessage(sqliteEx),
                    InvalidOperationException => "Could not create or access the SQLite database file. Check file permissions and available disk space.",
                    UnauthorizedAccessException => "Access denied to the database file location. Please run the application as administrator or check folder permissions.",
                    DirectoryNotFoundException => "Could not create the application data directory for the database.",
                    IOException => "Database file is locked or in use by another process. Please close other instances of the application.",
                    _ => ex.Message
                };

                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Failed to establish database connection",
                    details,
                    ex,
                    true);
            }
        }

        /// <summary>
        /// Gets a detailed error message for SQLite exceptions
        /// </summary>
        private string GetDetailedSqliteErrorMessage(SqliteException ex)
        {
            try
            {
                return ex.SqliteErrorCode switch
                {
                    14 => "Database file is locked or being used by another process. Please close other instances of the application.",
                    13 => "Database file is corrupted. Please contact support for recovery options.",
                    11 => "Database disk is full. Please free up disk space.",
                    10 => "Database I/O error. Please check your hard drive for errors.",
                    8 => "Database file cannot be written. Check file permissions.",
                    1 => "General database error. Please try restarting the application.",
                    _ => $"SQLite error (code {ex.SqliteErrorCode}): {ex.Message}"
                };
            }
            catch
            {
                return $"Database error: {ex.Message}";
            }
        }

        /// <summary>
        /// Validates that the connection is still usable
        /// </summary>
        public bool ValidateConnection()
        {
            if (_isConnectionOpen && dbConn.State == ConnectionState.Open)
            {
                try
                {
                    // Execute a simple query to test the connection
                    using (var cmd = new SqliteCommand("SELECT 1", dbConn))
                    {
                        cmd.CommandTimeout = 5; // Short timeout for validation
                        cmd.ExecuteScalar();
                        return true;
                    }
                }
                catch
                {
                    // Connection is not valid
                    _isConnectionOpen = false;
                    return false;
                }
            }

            // Connection is not open
            _isConnectionOpen = false;
            return false;
        }

        /// <summary>
        /// Creates and prepares a command with proper parameter validation
        /// </summary>
        public SqliteCommand CreateCommand(string query, Dictionary<string, object> parameters = null)
        {
            try
            {
                // Ensure connection is open
                if (!_isConnectionOpen || dbConn.State != ConnectionState.Open)
                {
                    OpenConnectionWithRetry();
                }

                var cmd = new SqliteCommand(query, dbConn);
                cmd.CommandTimeout = COMMAND_TIMEOUT_SECONDS;

                // Add parameters
                if (parameters != null)
                {
                    foreach (var param in parameters)
                    {
                        // Handle null values properly
                        if (param.Value == null)
                        {
                            cmd.Parameters.AddWithValue(param.Key, DBNull.Value);
                        }
                        else
                        {
                            cmd.Parameters.AddWithValue(param.Key, param.Value);
                        }
                    }
                }

                return cmd;
            }
            catch (Exception ex)
            {
                HandleConnectionError(ex);
                throw;
            }
        }

        /// <summary>
        /// Get database file information for debugging
        /// </summary>
        public string GetDatabaseInfo()
        {
            try
            {
                if (dbConn?.DataSource != null)
                {
                    var fileInfo = new FileInfo(dbConn.DataSource);
                    return $"Database: {fileInfo.FullName}\n" +
                           $"Size: {(fileInfo.Exists ? $"{fileInfo.Length / 1024} KB" : "Not created yet")}\n" +
                           $"Status: {(dbConn.State == ConnectionState.Open ? "Connected" : "Disconnected")}";
                }

                return "Database information not available";
            }
            catch (Exception ex)
            {
                return $"Error getting database info: {ex.Message}";
            }
        }

        /// <summary>
        /// Disposes of the database connection safely
        /// </summary>
        public void Dispose()
        {
            try
            {
                if (dbConn != null)
                {
                    if (dbConn.State == ConnectionState.Open)
                    {
                        dbConn.Close();
                    }
                    dbConn.Dispose();
                    dbConn = null;
                    _isConnectionOpen = false;
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw from Dispose
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Error while disposing database connection",
                    ex.Message,
                    ex,
                    false);
            }
        }

        /// <summary>
        /// Static method to reset the circuit breaker manually
        /// </summary>
        public static void ResetCircuitBreaker()
        {
            lock (_globalConnectionLock)
            {
                _isGlobalFailureMode = false;
                _globalFailureModeUntil = DateTime.MinValue;
            }
        }

        #region Logging Helper

        private void LogError(string title, string message, Exception? ex = null)
        {
            _errorHandlingService?.LogError(ErrorSeverity.NonCritical, title, message, ex, false);
        }

        public static implicit operator DbConnection(DbTransaction v)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}