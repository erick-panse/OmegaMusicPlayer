using Microsoft.Extensions.DependencyInjection;
using MysticMind.PostgresEmbed;
using OmegaPlayer.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace OmegaPlayer.Infrastructure.Services.Database
{
    /// <summary>
    /// Service to manage embedded PostgreSQL server lifecycle (Synchronous)
    /// Provides a portable PostgreSQL instance for OmegaPlayer
    /// </summary>
    public class EmbeddedPostgreSqlService : IDisposable
    {
        private PgServer? _pgServer;
        private bool _isServerRunning = false;
        private string? _connectionString;
        private readonly DatabaseErrorHandlingService _errorHandler;

        // Server configuration
        private const string POSTGRES_VERSION = "17.5.0";
        private const string POSTGRES_USER = "omega_app";
        private const string POSTGRES_DATABASE = "omega_player";
        private const int STARTUP_WAIT_TIME = 30000; // 30 seconds

        private const int DEFAULT_POSTGRES_PORT = 15432;
        private const int PORT_RANGE_START = 15433;
        private const int PORT_RANGE_END = 15500;

        public string ConnectionString => _connectionString ?? throw new InvalidOperationException("PostgreSQL server is not running");
        public bool IsServerRunning => _isServerRunning;
        public int Port => _pgServer?.PgPort ?? 0;
        public DatabaseErrorHandlingService ErrorHandler => _errorHandler;

        public EmbeddedPostgreSqlService()
        {
            var localizationService = App.ServiceProvider.GetRequiredService<LocalizationService>();
            _errorHandler = new DatabaseErrorHandlingService(localizationService);
        }

        /// <summary>
        /// Starts the embedded PostgreSQL server synchronously
        /// </summary>
        public DatabaseStartupResult StartServer()
        {
            if (_isServerRunning)
            {
                return DatabaseStartupResult.IsSuccess();
            }

            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var omegaDbPath = Path.Combine(appDataPath, "OmegaPlayer");

            var result = new DatabaseStartupResult();

            try
            {
                // Phase 1: Pre-flight checks
                var preflightError = _errorHandler.PerformPreFlightChecks(omegaDbPath);
                if (preflightError != null)
                {
                    result.Success = false;
                    result.Error = preflightError;
                    result.Phase = "Pre-flight Checks";
                    return result;
                }

                // Phase 2: Directory creation
                try
                {
                    Directory.CreateDirectory(omegaDbPath);
                }
                catch (Exception ex)
                {
                    var error = _errorHandler.AnalyzeException(ex, "Directory Creation");
                    result.Success = false;
                    result.Error = error;
                    result.Phase = "Directory Creation";
                    return result;
                }

                // Phase 3: Server configuration and creation
                PgServer tempServer = null;
                try
                {
                    // Validate binaries before trying to use them
                    var binaryError = _errorHandler.ValidatePostgreSQLBinaries(omegaDbPath);
                    if (binaryError != null)
                    {
                        result.Success = false;
                        result.Error = binaryError;
                        result.Phase = "Binary Validation";
                        return result;
                    }

                    int port = FindAvailablePort();
                    var serverParams = GetOptimalServerParameters();

                    tempServer = new PgServer(
                        pgVersion: POSTGRES_VERSION,
                        pgUser: POSTGRES_USER,
                        instanceId: Guid.Parse("dcd227f4-89b9-4d85-b7e9-180263ab03a9"), // Fixed GUID to prevent re-extraction
                        dbDir: omegaDbPath,
                        port: port,
                        pgServerParams: serverParams,
                        clearInstanceDirOnStop: false, // Keep data between sessions
                        clearWorkingDirOnStart: false, // Don't clear existing data
                        addLocalUserAccessPermission: true, // Critical for permission issues
                        startupWaitTime: STARTUP_WAIT_TIME
                    );
                }
                catch (Exception ex)
                {
                    // Clean up partial server instance
                    tempServer?.Dispose();

                    var error = _errorHandler.AnalyzeException(ex, "Server Configuration");
                    result.Success = false;
                    result.Error = error;
                    result.Phase = "Server Configuration";
                    return result;
                }

                // Phase 4: Server startup (most critical phase)
                try
                {
                    tempServer.Start();

                    // Only assign to field after successful startup
                    _pgServer = tempServer;
                    tempServer = null; // Prevent disposal in catch block
                }
                catch (Exception ex)
                {
                    // Clean up failed server instance
                    try
                    {
                        tempServer?.Stop();
                    }
                    catch
                    {
                        // Ignore stop errors during cleanup
                    }
                    finally
                    {
                        tempServer?.Dispose();
                    }

                    var error = _errorHandler.AnalyzeException(ex, "Server Startup");
                    result.Success = false;
                    result.Error = error;
                    result.Phase = "Server Startup";
                    return result;
                }

                // Phase 5: Connection string building
                try
                {
                    _connectionString = BuildConnectionString(_pgServer.PgPort);
                }
                catch (Exception ex)
                {
                    StopServer(false);

                    var error = _errorHandler.AnalyzeException(ex, "Connection String Building");
                    result.Success = false;
                    result.Error = error;
                    result.Phase = "Connection String Building";
                    return result;
                }

                // Phase 6: Database creation
                try
                {
                    CreateDatabaseIfNotExists();
                }
                catch (Exception ex)
                {
                    StopServer(false);

                    var error = _errorHandler.AnalyzeException(ex, "Database Creation");
                    result.Success = false;
                    result.Error = error;
                    result.Phase = "Database Creation";
                    return result;
                }

                _isServerRunning = true;
                result.Success = true;
                result.ConnectionString = _connectionString;
                return result;

            }
            catch (Exception ex)
            {
                // Catch-all cleanup
                StopServer();

                var error = _errorHandler.AnalyzeException(ex, "Unexpected Error");
                result.Success = false;
                result.Error = error;
                result.Phase = "Unexpected Error";
                return result;
            }
        }

        /// <summary>
        /// Stops the embedded PostgreSQL server synchronously
        /// </summary>
        public void StopServer(bool reportErrors = true)
        {
            if (!_isServerRunning && _pgServer == null)
            {
                return;
            }

            var errors = new List<Exception>();

            // Step 1: Stop the server process
            if (_pgServer != null)
            {
                try
                {
                    _pgServer.Stop();
                }
                catch (Exception ex)
                {
                    errors.Add(new Exception("Failed to stop PostgreSQL server process", ex));
                }
            }

            // Step 2: Dispose the server instance
            if (_pgServer != null)
            {
                try
                {
                    _pgServer.Dispose();
                }
                catch (Exception ex)
                {
                    errors.Add(new Exception("Failed to dispose PostgreSQL server instance", ex));
                }
                finally
                {
                    _pgServer = null;
                }
            }

            // Step 3: Clear state
            _isServerRunning = false;
            _connectionString = null;

            // Step 4: Report errors if requested
            if (reportErrors && errors.Count > 0)
            {
                var combinedException = new AggregateException("Multiple errors occurred during server shutdown", errors);
                throw combinedException;
            }
        }

        /// <summary>
        /// Gets optimal server parameters for OmegaPlayer usage
        /// </summary>
        private Dictionary<string, string> GetOptimalServerParameters()
        {
            return new Dictionary<string, string>
            {
                // Optimize for desktop application usage
                {"shared_buffers", "32MB"},
                {"effective_cache_size", "128MB"},
                {"maintenance_work_mem", "4MB"},
                {"work_mem", "4MB"},                  
                
                // Connection and performance settings
                {"max_connections", "50"},            
                
                // Reliability vs Performance balance
                {"synchronous_commit", "off"},
                {"wal_buffers", "8MB"},
                {"checkpoint_completion_target", "0.9"}, 
                
                // Logging and monitoring - reduced for production
                {"log_statement", "none"},
                {"log_min_duration_statement", "-1"}, 
                
                // Locale and encoding
                {"timezone", "UTC"},
                {"default_text_search_config", "pg_catalog.english"}, 
                
                // Additional stability settings
                {"log_destination", "stderr"},
                {"logging_collector", "off"},
                {"log_min_messages", "warning"} // Only log warnings and errors
            };
        }

        /// <summary>
        /// Builds the PostgreSQL connection string with pooling disabled to avoid connection issues
        /// </summary>
        private string BuildConnectionString(int port)
        {
            // Note: Pooling=false is recommended by MysticMind.PostgresEmbed documentation to avoid "connection was forcibly closed" issues
            //return $"Server=localhost;Port={port};User Id={POSTGRES_USER};Database={POSTGRES_DATABASE};Connection Idle Lifetime=300;Pooling=false;";
            return $"Server=localhost;Port={port};User Id={POSTGRES_USER};Database={POSTGRES_DATABASE};Pooling=true;Minimum Pool Size=1;Maximum Pool Size=10;Connection Idle Lifetime=300;";
        }

        /// <summary>
        /// Creates the application database if it doesn't exist
        /// </summary>
        private void CreateDatabaseIfNotExists()
        {
            try
            {
                // Connect to default postgres database first
                //var defaultConnString = $"Server=localhost;Port={_pgServer.PgPort};User Id={POSTGRES_USER};Database=postgres;Pooling=false;";
                var defaultConnString = $"Server=localhost;Port={_pgServer.PgPort};User Id={POSTGRES_USER};Database=postgres;";

                using var connection = new Npgsql.NpgsqlConnection(defaultConnString);
                connection.Open();

                // Check if our database exists
                var checkDbCommand = new Npgsql.NpgsqlCommand(
                    "SELECT 1 FROM pg_database WHERE datname = @dbname", connection);
                checkDbCommand.Parameters.AddWithValue("@dbname", POSTGRES_DATABASE);

                var exists = checkDbCommand.ExecuteScalar();

                if (exists == null)
                {
                    // Create the database
                    var createDbCommand = new Npgsql.NpgsqlCommand(
                        $"CREATE DATABASE \"{POSTGRES_DATABASE}\" WITH ENCODING 'UTF8'", connection);
                    createDbCommand.ExecuteNonQuery();
                }

                // Update connection string to use our database
                _connectionString = BuildConnectionString(_pgServer.PgPort);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to create or connect to the application database", ex);
            }
        }

        /// <summary>
        /// Tests the database connection synchronously
        /// </summary>
        public bool TestConnection()
        {
            if (!_isServerRunning || string.IsNullOrEmpty(_connectionString))
            {
                return false;
            }

            try
            {
                using var connection = new Npgsql.NpgsqlConnection(_connectionString);
                connection.Open();

                using var command = new Npgsql.NpgsqlCommand("SELECT 1", connection);
                var result = command.ExecuteScalar();

                return result != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Finds next available port starting from default
        /// </summary>
        public static int FindAvailablePort()
        {
            // Try default port first
            if (IsPortAvailable(DEFAULT_POSTGRES_PORT))
            {
                return DEFAULT_POSTGRES_PORT;
            }

            // Scan range for available port
            for (int port = PORT_RANGE_START; port <= PORT_RANGE_END; port++)
            {
                if (IsPortAvailable(port))
                {
                    return port;
                }
            }

            // Fallback to system-assigned port
            return GetSystemAssignedPort();
        }

        /// <summary>
        /// Checks if port is available (nothing listening)
        /// </summary>
        private static bool IsPortAvailable(int port)
        {
            try
            {
                // Check if port is in use
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                var listeners = properties.GetActiveTcpListeners();

                if (listeners.Any(l => l.Port == port))
                {
                    // If in use Check if its PostgreSQL
                    return IsOurPostgreSqlServerRunning(port) ? true : false;
                }

                // Double-check by trying to bind
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if our PostgreSQL server is running on the specified port before building _connectionString
        /// </summary>
        private static bool IsOurPostgreSqlServerRunning(int port)
        {
            try
            {
                // Try PostgreSQL-specific connection test
                var connectionString = $"Server=localhost;Port={port};User Id={POSTGRES_USER};Database=postgres;Pooling=false;CommandTimeout=3;";

                using var connection = new Npgsql.NpgsqlConnection(connectionString);
                connection.Open();

                using var command = new Npgsql.NpgsqlCommand("SELECT version()", connection);
                var result = command.ExecuteScalar() as string;

                // If we get a PostgreSQL version response, it's our server
                return !string.IsNullOrEmpty(result) &&
                       result.Contains("PostgreSQL", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Connection failed, not our PostgreSQL server
                return false;
            }
        }

        /// <summary>
        /// Gets system-assigned available port
        /// </summary>
        private static int GetSystemAssignedPort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return ((IPEndPoint)socket.LocalEndPoint).Port;
        }

        /// <summary>
        /// Gets server information for debugging synchronously
        /// </summary>
        public string GetServerInfo()
        {
            if (!_isServerRunning || _pgServer == null)
            {
                return "PostgreSQL server is not running";
            }

            try
            {
                using var connection = new Npgsql.NpgsqlConnection(_connectionString);
                connection.Open();

                using var command = new Npgsql.NpgsqlCommand("SELECT version()", connection);
                var version = command.ExecuteScalar() as string;

                return $"PostgreSQL Server\n" +
                       $"Version: {version}\n" +
                       $"Port: {_pgServer.PgPort}\n" +
                       $"Database: {POSTGRES_DATABASE}\n" +
                       $"User: {POSTGRES_USER}\n" +
                       $"Status: Running";
            }
            catch (Exception ex)
            {
                return $"Error getting server info: {ex.Message}";
            }
        }

        /// <summary>
        /// Creates a diagnostic report for the current server state
        /// </summary>
        public string CreateDiagnosticReport()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var omegaDbPath = Path.Combine(appDataPath, "OmegaPlayer");

            var error = new DatabaseErrorHandlingService.DatabaseError
            {
                Category = DatabaseErrorHandlingService.DatabaseErrorCategory.Unknown,
                UserFriendlyTitle = "Database Diagnostic Report",
                UserFriendlyMessage = "Current database state information",
                TechnicalDetails = GetServerInfo(),
                OriginalException = null
            };

            return _errorHandler.CreateDiagnosticReport(error, omegaDbPath);
        }

        public void Dispose()
        {
            try
            {
                StopServer(false); // Don't throw during disposal
            }
            catch
            {
                // Force cleanup on disposal errors
                try
                {
                    _pgServer?.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
                finally
                {
                    _pgServer = null;
                    _isServerRunning = false;
                    _connectionString = null;
                }
            }
        }
    }

    /// <summary>
    /// Result object for database startup operations
    /// </summary>
    public class DatabaseStartupResult
    {
        public bool Success { get; set; }
        public DatabaseErrorHandlingService.DatabaseError? Error { get; set; }
        public string? Phase { get; set; }
        public string? ConnectionString { get; set; }

        public static DatabaseStartupResult IsSuccess(string? connectionString = null)
        {
            return new DatabaseStartupResult
            {
                Success = true,
                ConnectionString = connectionString
            };
        }

        public static DatabaseStartupResult Failure(DatabaseErrorHandlingService.DatabaseError error, string phase)
        {
            return new DatabaseStartupResult
            {
                Success = false,
                Error = error,
                Phase = phase
            };
        }
    }
}