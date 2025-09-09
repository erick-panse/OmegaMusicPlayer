using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;

namespace OmegaPlayer.Infrastructure.Services.Database
{
    /// <summary>
    /// Error handling service for database initialization issues
    /// </summary>
    public class DatabaseErrorHandlingService
    {
        private const long MINIMUM_DISK_SPACE_BYTES = 500 * 1024 * 1024; // 500MB

        /// <summary>
        /// Database error categories for user-friendly messages
        /// </summary>
        public enum DatabaseErrorCategory
        {
            NetworkDownload,
            Permissions,
            Locale,
            Dependencies,
            Security,
            DiskSpace,
            PortConflict,
            ProcessFailure,
            PathCharacters,
            Unknown
        }

        /// <summary>
        /// Comprehensive database error information
        /// </summary>
        public class DatabaseError
        {
            public DatabaseErrorCategory Category { get; set; }
            public string UserFriendlyTitle { get; set; }
            public string UserFriendlyMessage { get; set; }
            public string TechnicalDetails { get; set; }
            public string[] TroubleshootingSteps { get; set; }
            public bool IsRecoverable { get; set; }
            public Exception OriginalException { get; set; }
        }

        /// <summary>
        /// Performs pre-flight checks before database initialization
        /// </summary>
        public DatabaseError PerformPreFlightChecks(string databasePath)
        {
            try
            {
                // Check disk space
                var diskSpaceError = CheckDiskSpace(databasePath);
                if (diskSpaceError != null) return diskSpaceError;

                // Check directory permissions
                var permissionError = CheckDirectoryPermissions(databasePath);
                if (permissionError != null) return permissionError;

                // Check path characters
                var pathError = CheckPathCharacters(databasePath);
                if (pathError != null) return pathError;

                // Check network connectivity (only if we need to download binaries)
                var networkError = CheckNetworkConnectivity(databasePath);
                if (networkError != null) return networkError;

                return null; // All checks passed
            }
            catch (Exception ex)
            {
                return CreateGenericError("Pre-flight Check Failed",
                    "Unable to verify system requirements for database setup.", ex);
            }
        }

        /// <summary>
        /// Analyzes and categorizes database initialization exceptions
        /// </summary>
        public DatabaseError AnalyzeException(Exception exception, string context = "")
        {
            if (exception == null)
                return CreateGenericError("Unknown Error", "An unknown error occurred during database initialization.", null);

            var message = exception.Message?.ToLowerInvariant() ?? "";
            var stackTrace = exception.StackTrace ?? "";
            var innerMessage = exception.InnerException?.Message?.ToLowerInvariant() ?? "";

            // Network/Download issues
            if (IsNetworkError(message, innerMessage, stackTrace))
            {
                return new DatabaseError
                {
                    Category = DatabaseErrorCategory.NetworkDownload,
                    UserFriendlyTitle = "Database Download Failed",
                    UserFriendlyMessage = "OmegaPlayer couldn't download the required database components. This usually indicates a network connectivity issue.",
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        "Check your internet connection",
                        "Temporarily disable firewall or antivirus",
                        "Try running OmegaPlayer as administrator",
                        "Check if your organization blocks downloads",
                        "Restart your computer and try again"
                    },
                    IsRecoverable = true,
                    OriginalException = exception
                };
            }

            // Permission issues
            if (IsPermissionError(message, innerMessage, stackTrace))
            {
                return new DatabaseError
                {
                    Category = DatabaseErrorCategory.Permissions,
                    UserFriendlyTitle = "Insufficient Permissions",
                    UserFriendlyMessage = "OmegaPlayer doesn't have the required permissions to set up the database. This commonly happens with restricted user accounts.",
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        "Right-click OmegaPlayer and select 'Run as administrator'",
                        "Ensure your user account has full control over the AppData folder",
                        "Check that no other program is using the database folder",
                        "Temporarily disable any folder protection software",
                        "Contact your system administrator if on a managed computer"
                    },
                    IsRecoverable = true,
                    OriginalException = exception
                };
            }

            // Locale issues
            if (IsLocaleError(message, innerMessage, stackTrace))
            {
                return new DatabaseError
                {
                    Category = DatabaseErrorCategory.Locale,
                    UserFriendlyTitle = "System Locale Configuration Issue",
                    UserFriendlyMessage = "The database couldn't initialize due to system locale settings. This can happen on systems with non-standard language configurations.",
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        "Ensure your system has a valid locale setting",
                        "In Windows, check Region settings in Control Panel",
                        "Try changing your system locale to English (US) temporarily",
                        "Restart OmegaPlayer after changing locale settings",
                    },
                    IsRecoverable = true,
                    OriginalException = exception
                };
            }

            // Security software interference
            if (IsSecuritySoftwareError(message, innerMessage, stackTrace))
            {
                return new DatabaseError
                {
                    Category = DatabaseErrorCategory.Security,
                    UserFriendlyTitle = "Security Software Interference",
                    UserFriendlyMessage = "Your antivirus or security software may be preventing OmegaPlayer from setting up the database.",
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        "Add OmegaPlayer to your antivirus exclusions list",
                        "Temporarily disable real-time protection",
                        "Add the OmegaPlayer installation folder to Windows Defender exclusions",
                        "Check if Windows SmartScreen is blocking the application",
                        "Try running with antivirus completely disabled (temporarily)"
                    },
                    IsRecoverable = true,
                    OriginalException = exception
                };
            }

            // Dependency issues
            if (IsDependencyError(message, innerMessage, stackTrace))
            {
                return new DatabaseError
                {
                    Category = DatabaseErrorCategory.Dependencies,
                    UserFriendlyTitle = "Missing System Components",
                    UserFriendlyMessage = "OmegaPlayer requires certain system components that appear to be missing from your computer.",
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        "Install Visual C++ Redistributable for Visual Studio 2013",
                        "Install the latest Windows updates",
                        "Download and install .NET Framework 4.8 or later",
                        "Run Windows System File Checker (sfc /scannow)",
                        "Restart your computer after installing components"
                    },
                    IsRecoverable = true,
                    OriginalException = exception
                };
            }

            // Path/Character issues
            if (IsPathCharacterError(message, innerMessage, stackTrace))
            {
                return new DatabaseError
                {
                    Category = DatabaseErrorCategory.PathCharacters,
                    UserFriendlyTitle = "Invalid Path Characters",
                    UserFriendlyMessage = "The database setup path contains characters that aren't supported. This can happen with special characters in folder names.",
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        "Ensure your username doesn't contain special characters",
                        "Avoid installing in folders with non-English characters",
                        "Try moving OmegaPlayer to a simple path like C:\\OmegaPlayer",
                        "Check that your AppData folder path is valid",
                    },
                    IsRecoverable = false,
                    OriginalException = exception
                };
            }

            // Process failure
            if (IsProcessFailureError(message, innerMessage, stackTrace))
            {
                return new DatabaseError
                {
                    Category = DatabaseErrorCategory.ProcessFailure,
                    UserFriendlyTitle = "Database Process Failed",
                    UserFriendlyMessage = "The database server process couldn't start properly. This might be due to system resources or conflicting software.",
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        "Close other applications to free up system resources",
                        "Restart your computer and try again",
                        "Check if there is any port between 15432 and 15500 is available",
                        "Run OmegaPlayer as administrator",
                    },
                    IsRecoverable = true,
                    OriginalException = exception
                };
            }

            // Generic error for unrecognized issues
            return CreateGenericError("Database Initialization Failed",
                "An unexpected error occurred while setting up the database.", exception);
        }

        /// <summary>
        /// Creates diagnostic information for support
        /// </summary>
        public string CreateDiagnosticReport(DatabaseError error, string databasePath)
        {
            try
            {
                var report = $"=== OmegaPlayer Database Initialization Error Report ===\n";
                report += $"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n";
                report += $"Category: {error.Category}\n";
                report += $"Title: {error.UserFriendlyTitle}\n";
                report += $"Database Path: {databasePath}\n";
                report += $"OS: {Environment.OSVersion}\n";
                report += $"User: {Environment.UserName}\n";
                report += $"Machine: {Environment.MachineName}\n";
                report += $"Working Directory: {Environment.CurrentDirectory}\n";
                report += $"App Domain Base: {AppDomain.CurrentDomain.BaseDirectory}\n\n";

                report += $"=== Error Details ===\n";
                report += $"Message: {error.OriginalException?.Message}\n";
                report += $"Type: {error.OriginalException?.GetType().Name}\n";
                report += $"Stack Trace:\n{error.OriginalException?.StackTrace}\n\n";

                if (error.OriginalException?.InnerException != null)
                {
                    report += $"=== Inner Exception ===\n";
                    report += $"Message: {error.OriginalException.InnerException.Message}\n";
                    report += $"Type: {error.OriginalException.InnerException.GetType().Name}\n";
                    report += $"Stack Trace:\n{error.OriginalException.InnerException.StackTrace}\n\n";
                }

                // System information
                report += $"=== System Information ===\n";
                report += $"Disk Space Available: {GetDiskSpace(databasePath)}\n";
                report += $"Network Available: {IsNetworkAvailable()}\n";
                report += $"Is Administrator: {IsRunningAsAdministrator()}\n";
                report += $"Temp Directory: {Path.GetTempPath()}\n";

                return report;
            }
            catch (Exception ex)
            {
                return $"Error creating diagnostic report: {ex.Message}";
            }
        }

        #region Private Helper Methods

        private DatabaseError CheckDiskSpace(string path)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path));
                if (drive.AvailableFreeSpace < MINIMUM_DISK_SPACE_BYTES)
                {
                    return new DatabaseError
                    {
                        Category = DatabaseErrorCategory.DiskSpace,
                        UserFriendlyTitle = "Insufficient Disk Space",
                        UserFriendlyMessage = $"OmegaPlayer needs at least 500 MB of free space, but only {drive.AvailableFreeSpace / 1024 / 1024} MB is available.",
                        TechnicalDetails = $"Required: {MINIMUM_DISK_SPACE_BYTES / 1024 / 1024} MB, Available: {drive.AvailableFreeSpace / 1024 / 1024} MB",
                        TroubleshootingSteps = new[]
                        {
                            "Free up disk space by deleting unnecessary files",
                            "Empty your Recycle Bin",
                            "Run Disk Cleanup utility",
                            "Move large files to another drive",
                            "Consider uninstalling unused programs"
                        },
                        IsRecoverable = false,
                        OriginalException = null
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                return CreateGenericError("Disk Space Check Failed", "Cannot verify available disk space.", ex);
            }
        }

        private DatabaseError CheckDirectoryPermissions(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path) ?? path;

                // Try to create a test file
                var testFile = Path.Combine(directory, $"omega_test_{Guid.NewGuid():N}.tmp");

                Directory.CreateDirectory(directory);
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);

                return null; // Permissions OK
            }
            catch (UnauthorizedAccessException)
            {
                return new DatabaseError
                {
                    Category = DatabaseErrorCategory.Permissions,
                    UserFriendlyTitle = "Permission Denied",
                    UserFriendlyMessage = "OmegaPlayer doesn't have permission to create files in the required directory.",
                    TechnicalDetails = $"Cannot write to: {path}",
                    TroubleshootingSteps = new[]
                    {
                        "Run OmegaPlayer as administrator",
                        "Check folder permissions in File Explorer",
                        "Ensure antivirus isn't blocking file creation",
                        "Try a different installation location"
                    },
                    IsRecoverable = true,
                    OriginalException = null
                };
            }
            catch (Exception ex)
            {
                return CreateGenericError("Permission Check Failed", "Cannot verify directory permissions.", ex);
            }
        }

        private DatabaseError CheckPathCharacters(string path)
        {
            var invalidChars = Path.GetInvalidPathChars();
            var invalidFileChars = Path.GetInvalidFileNameChars();

            if (path.Any(c => invalidChars.Contains(c)) ||
                path.Contains("..") ||
                path.Any(c => c > 127)) // Non-ASCII characters
            {
                return new DatabaseError
                {
                    Category = DatabaseErrorCategory.PathCharacters,
                    UserFriendlyTitle = "Invalid Path Characters",
                    UserFriendlyMessage = "The installation path contains characters that may cause problems.",
                    TechnicalDetails = $"Problematic path: {path}",
                    TroubleshootingSteps = new[]
                    {
                        "Install OmegaPlayer in a path with only English characters",
                        "Avoid spaces and special characters in the path",
                        "Try installing in C:\\OmegaPlayer instead"
                    },
                    IsRecoverable = false,
                    OriginalException = null
                };
            }
            return null;
        }

        private DatabaseError CheckNetworkConnectivity(string databasePath)
        {
            try
            {
                // First, check if we actually need network connectivity
                if (!DoesRequireNetworkDownload(databasePath))
                {
                    // PostgreSQL binaries already exist, no network needed
                    return null;
                }

                // We need to download binaries, so check network
                if (!NetworkInterface.GetIsNetworkAvailable())
                {
                    return new DatabaseError
                    {
                        Category = DatabaseErrorCategory.NetworkDownload,
                        UserFriendlyTitle = "No Network Connection",
                        UserFriendlyMessage = "OmegaPlayer needs an internet connection to download database components during first-time setup.",
                        TechnicalDetails = "No network interfaces are available and PostgreSQL binaries need to be downloaded",
                        TroubleshootingSteps = new[]
                        {
                            "Check your internet connection",
                            "Ensure Wi-Fi or Ethernet is connected",
                            "Try accessing a website in your browser",
                            "Restart your network adapter",
                            "Contact your network administrator"
                        },
                        IsRecoverable = true,
                        OriginalException = null
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                return CreateGenericError("Network Check Failed", "Cannot verify network connectivity.", ex);
            }
        }

        /// <summary>
        /// Checks if we need to download PostgreSQL binaries (first-time setup)
        /// </summary>
        private bool DoesRequireNetworkDownload(string databasePath)
        {
            try
            {
                // Check if PostgreSQL binaries already exist
                var possibleBinaryPaths = new[]
                {
                    Path.Combine(databasePath, "pgsql", "bin", "postgres.exe"),     // Windows
                    Path.Combine(databasePath, "pgsql", "bin", "postgres"),        // Linux/Mac
                    Path.Combine(databasePath, "bin", "postgres.exe"),             // Alternative Windows
                    Path.Combine(databasePath, "bin", "postgres"),                 // Alternative Linux/Mac
                };

                // If any postgres binary exists, we probably don't need to download
                return !possibleBinaryPaths.Any(File.Exists);
            }
            catch
            {
                // If we can't check, assume we need to download (safer approach)
                return true;
            }
        }

        private bool IsNetworkError(string message, string innerMessage, string stackTrace)
        {
            var networkKeywords = new[] { "download", "network", "timeout", "connection", "dns", "proxy", "firewall", "timed out", "unreachable" };
            return networkKeywords.Any(keyword =>
                message.Contains(keyword) || innerMessage.Contains(keyword) || stackTrace.Contains(keyword));
        }

        private bool IsPermissionError(string message, string innerMessage, string stackTrace)
        {
            var permissionKeywords = new[] { "permission", "access", "denied", "unauthorized", "forbidden", "initdb: could not change permissions" };
            return permissionKeywords.Any(keyword =>
                message.Contains(keyword) || innerMessage.Contains(keyword) || stackTrace.Contains(keyword));
        }

        private bool IsLocaleError(string message, string innerMessage, string stackTrace)
        {
            var localeKeywords = new[] { "locale", "initdb crashes", "text search configuration", "encoding" };
            return localeKeywords.Any(keyword =>
                message.Contains(keyword) || innerMessage.Contains(keyword) || stackTrace.Contains(keyword));
        }

        private bool IsSecuritySoftwareError(string message, string innerMessage, string stackTrace)
        {
            var securityKeywords = new[] { "virus", "blocked", "quarantine", "smartscreen", "defender", "execution", "trust" };
            return securityKeywords.Any(keyword =>
                message.Contains(keyword) || innerMessage.Contains(keyword) || stackTrace.Contains(keyword));
        }

        private bool IsDependencyError(string message, string innerMessage, string stackTrace)
        {
            var dependencyKeywords = new[] { "msvcr120", "redistributable", "library", "dll", "large negative number", "dependency" };
            return dependencyKeywords.Any(keyword =>
                message.Contains(keyword) || innerMessage.Contains(keyword) || stackTrace.Contains(keyword));
        }

        private bool IsPathCharacterError(string message, string innerMessage, string stackTrace)
        {
            var pathKeywords = new[] { "special character", "path", "invalid", "character", "unicode" };
            return pathKeywords.Any(keyword =>
                message.Contains(keyword) || innerMessage.Contains(keyword) || stackTrace.Contains(keyword));
        }

        private bool IsProcessFailureError(string message, string innerMessage, string stackTrace)
        {
            var processKeywords = new[] { "process", "start", "server", "postgres", "failed to start", "exit code" };
            return processKeywords.Any(keyword =>
                message.Contains(keyword) || innerMessage.Contains(keyword) || stackTrace.Contains(keyword));
        }

        private DatabaseError CreateGenericError(string title, string message, Exception exception)
        {
            return new DatabaseError
            {
                Category = DatabaseErrorCategory.Unknown,
                UserFriendlyTitle = title,
                UserFriendlyMessage = message,
                TechnicalDetails = exception?.ToString() ?? "No additional details available",
                TroubleshootingSteps = new[]
                {
                    "Restart OmegaPlayer and try again",
                    "Run OmegaPlayer as administrator",
                    "Restart your computer",
                    "Check Windows Event Log for additional errors",
                },
                IsRecoverable = true,
                OriginalException = exception
            };
        }

        private string GetDiskSpace(string path)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path));
                return $"{drive.AvailableFreeSpace / 1024 / 1024} MB available";
            }
            catch
            {
                return "Unknown";
            }
        }

        private bool IsNetworkAvailable()
        {
            return NetworkInterface.GetIsNetworkAvailable();
        }

        private bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}