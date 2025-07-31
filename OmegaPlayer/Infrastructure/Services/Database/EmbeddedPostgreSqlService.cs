using MysticMind.PostgresEmbed;
using System;
using System.Collections.Generic;
using System.IO;

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

        // Server configuration
        private const string POSTGRES_VERSION = "16.5.0";
        private const string POSTGRES_USER = "omega_app";
        private const string POSTGRES_DATABASE = "omega_player";
        private const int STARTUP_WAIT_TIME = 30000; // 30 seconds

        public string ConnectionString => _connectionString ?? throw new InvalidOperationException("PostgreSQL server is not running");
        public bool IsServerRunning => _isServerRunning;
        public int Port => _pgServer?.PgPort ?? 0;

        /// <summary>
        /// Starts the embedded PostgreSQL server synchronously
        /// </summary>
        public bool StartServer()
        {
            if (_isServerRunning)
            {
                return true;
            }

            // Get application data directory for database storage
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var omegaDbPath = Path.Combine(appDataPath, "OmegaPlayer");
            Directory.CreateDirectory(omegaDbPath);

            // Configure server parameters for optimal performance
            var serverParams = GetOptimalServerParameters();

            // Create and configure the PostgreSQL server
            _pgServer = new PgServer(
                pgVersion: POSTGRES_VERSION,
                pgUser: POSTGRES_USER,
                instanceId: Guid.Parse("dcd227f4-89b9-4d85-b7e9-180263ab03a9"), // Fixed GUID to prevent re-extraction
                dbDir: omegaDbPath,
                port: 15432,
                pgServerParams: serverParams,
                clearInstanceDirOnStop: false, // Keep data between sessions
                clearWorkingDirOnStart: false, // Don't clear existing data
                addLocalUserAccessPermission: true,
                startupWaitTime: STARTUP_WAIT_TIME
            );

            // Start the server
            _pgServer.Start();

            // Build connection string
            _connectionString = BuildConnectionString(_pgServer.PgPort);

            // Create database if it doesn't exist
            CreateDatabaseIfNotExists();

            _isServerRunning = true;
            return true;
        }

        /// <summary>
        /// Stops the embedded PostgreSQL server synchronously
        /// </summary>
        public void StopServer()
        {
            if (!_isServerRunning || _pgServer == null)
            {
                return;
            }

            _pgServer.Stop();
            _isServerRunning = false;
            _connectionString = null;
        }

        /// <summary>
        /// Gets optimal server parameters for OmegaPlayer usage
        /// </summary>
        private Dictionary<string, string> GetOptimalServerParameters()
        {
            return new Dictionary<string, string>
            {
                // Optimize for desktop application usage
                {"shared_buffers", "32MB"},           // Reasonable buffer size
                {"effective_cache_size", "128MB"},    // Assume moderate system memory
                {"maintenance_work_mem", "4MB"},     // For maintenance operations
                {"work_mem", "4MB"},                  // Per-query memory
                
                // Connection and performance settings
                {"max_connections", "50"},            // More than enough for single-user app
                
                // Reliability vs Performance balance
                {"synchronous_commit", "off"},       // Better performance, acceptable for music player
                {"wal_buffers", "8MB"},             // Write-ahead log buffers
                {"checkpoint_completion_target", "0.9"}, // Spread out checkpoint I/O
                
                // Logging and monitoring
                {"log_statement", "none"},           // No query logging for production
                {"log_min_duration_statement", "-1"}, // Disable slow query logging
                
                // Locale and encoding
                {"timezone", "UTC"},                 // Consistent timezone
                {"default_text_search_config", "pg_catalog.english"}, // English text search
            };
        }

        /// <summary>
        /// Builds the PostgreSQL connection string
        /// </summary>
        private string BuildConnectionString(int port)
        {
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
                throw;
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
            catch
            {
                return false;
            }
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

        public void Dispose()
        {
            try
            {
                if (_isServerRunning && _pgServer != null)
                {
                    _pgServer.Stop();
                    _isServerRunning = false;
                }

                _pgServer?.Dispose();
                _pgServer = null;
            }
            catch (Exception ex) { }
        }
    }
}