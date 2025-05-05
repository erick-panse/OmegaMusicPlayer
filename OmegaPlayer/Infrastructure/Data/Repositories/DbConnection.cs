using Npgsql;
using System;
using System.Threading.Tasks;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using System.Collections.Generic;
using System.Data;

namespace OmegaPlayer.Infrastructure.Data.Repositories
{
    /// <summary>
    /// Manages database connections with robust error handling, retry logic, and connection pooling.
    /// </summary>
    public class DbConnection : IDisposable
    {
        public NpgsqlConnection dbConn { get; private set; }

        private readonly IErrorHandlingService _errorHandlingService;

        // Retry configuration
        private static readonly int MAX_RETRIES = 3;
        private static readonly TimeSpan INITIAL_RETRY_DELAY = TimeSpan.FromSeconds(1);
        private static readonly double RETRY_BACKOFF_FACTOR = 2.0;

        // Connection pooling settings
        private static readonly int CONNECTION_TIMEOUT_SECONDS = 30;
        private static readonly int COMMAND_TIMEOUT_SECONDS = 60;

        // Connection string parameters
        private static readonly Dictionary<string, string> CONNECTION_PARAMS = new Dictionary<string, string>
        {
            { "Pooling", "true" },
            { "Minimum Pool Size", "1" },
            { "Maximum Pool Size", "20" },
            { "Connection Idle Lifetime", "300" }, // 5 minutes
            { "Connection Pruning Interval", "60" }, // 1 minute
            { "Timeout", CONNECTION_TIMEOUT_SECONDS.ToString() },
            { "Command Timeout", COMMAND_TIMEOUT_SECONDS.ToString() },
            { "Enlist", "false" } // No automatic transaction enlistment
        };

        // Connection status tracking
        private bool _isConnectionOpen = false;
        private DateTime _lastConnectionAttempt = DateTime.MinValue;
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

                string connectionString = GetConnectionString();

                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("Database connection string not found in environment variables.");
                }

                dbConn = new NpgsqlConnection(connectionString);
                OpenConnectionWithRetry();
            }
            catch (Exception ex)
            {
                HandleConnectionError(ex);

                // Mark connection failed for circuit breaker
                RecordConnectionFailure();

                throw; // Needs to throw as repositories expect a working connection
            }
        }

        /// <summary>
        /// Gets the connection string with appropriate parameters.
        /// </summary>
        private string GetConnectionString()
        {
            string baseConnectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");

            if (string.IsNullOrEmpty(baseConnectionString))
                return null;

            // Don't modify connection string if it already has parameters
            if (baseConnectionString.Contains(";"))
                return baseConnectionString;

            // Add connection parameters
            var parameters = new List<string>();
            foreach (var param in CONNECTION_PARAMS)
            {
                parameters.Add($"{param.Key}={param.Value}");
            }

            return $"{baseConnectionString};{string.Join(";", parameters)}";
        }

        /// <summary>
        /// Opens a database connection with retry logic.
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

                    _lastConnectionAttempt = DateTime.Now;
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
        /// Records a connection failure for circuit breaker pattern.
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
        /// Checks if we're in global failure mode (circuit breaker pattern).
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
        /// Handles connection errors with specific error messages based on exception type.
        /// </summary>
        private void HandleConnectionError(Exception ex)
        {
            // Only log if error service is available
            if (_errorHandlingService != null)
            {
                string details = ex switch
                {
                    NpgsqlException npgEx => GetDetailedNpgsqlErrorMessage(npgEx),
                    InvalidOperationException => "Missing or invalid connection string. Check your environment variables.",
                    TimeoutException => "Database connection timed out. The server might be overloaded or unreachable.",
                    _ => ex.Message
                };

                _errorHandlingService.LogError(
                    ErrorSeverity.Critical,
                    "Failed to establish database connection",
                    details,
                    ex,
                    true); // Show notification for connection failures
            }
        }

        /// <summary>
        /// Gets a detailed error message for Npgsql exceptions.
        /// </summary>
        private string GetDetailedNpgsqlErrorMessage(NpgsqlException ex)
        {
            try
            {
                // Extract useful information from the exception
                var sqlState = ex.SqlState ?? "Unknown";
                var message = ex.Message;

                // Check if there are inner exceptions
                if (ex.InnerException != null)
                {
                    message += $" Inner error: {ex.InnerException.Message}";
                }

                // Friendly messages based on SQL state codes
                switch (sqlState)
                {
                    case "08001": // Connection exception
                        return "Could not connect to the database server. Please check if the server is running and network connectivity is available.";

                    case "28P01": // Invalid password
                        return "Authentication failed. Check that the database credentials are correct.";

                    case "3D000": // Invalid catalog name
                        return "The specified database does not exist or user does not have access.";

                    case "57P03": // Cannot connect now
                        return "The database server is currently not accepting connections. It may be starting up or shutting down.";

                    case "53300": // Too many connections
                        return "The database server has too many connections. Please try again later.";

                    default:
                        return $"Database error code: {sqlState}, Message: {message}";
                }
            }
            catch
            {
                // Fallback if parsing fails
                return $"Database error: {ex.Message}";
            }
        }

        /// <summary>
        /// Validates that the connection is still usable.
        /// </summary>
        public bool ValidateConnection()
        {
            if (_isConnectionOpen && dbConn.State == ConnectionState.Open)
            {
                try
                {
                    // Execute a simple query to test the connection
                    using (var cmd = new NpgsqlCommand("SELECT 1", dbConn))
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
        /// Creates and prepares a command with proper parameter validation.
        /// </summary>
        public NpgsqlCommand CreateCommand(string query, Dictionary<string, object> parameters = null)
        {
            try
            {
                // Ensure connection is open
                if (!_isConnectionOpen || dbConn.State != ConnectionState.Open)
                {
                    OpenConnectionWithRetry();
                }

                var cmd = new NpgsqlCommand(query, dbConn);
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
        /// Disposes of the database connection safely with proper error handling.
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
        /// Static method to reset the circuit breaker manually.
        /// </summary>
        public static void ResetCircuitBreaker()
        {
            lock (_globalConnectionLock)
            {
                _isGlobalFailureMode = false;
                _globalFailureModeUntil = DateTime.MinValue;
            }
        }
    }
}