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

        private readonly LocalizationService _localizationService;

        public DatabaseErrorHandlingService(LocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

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
                return CreateGenericError(_localizationService["PreFlight_Failed_Title"],
                    _localizationService["PreFlight_Failed_Message"], ex);
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
                    UserFriendlyTitle = _localizationService["DatabaseError_NetworkDownload_Title"],
                    UserFriendlyMessage = _localizationService["DatabaseError_NetworkDownload_Message"],
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        _localizationService["Troubleshoot_CheckInternet"],
                        _localizationService["Troubleshoot_RunAsAdmin"],
                        _localizationService["Troubleshoot_CheckOrganizationBlocks"],
                        _localizationService["Troubleshoot_RestartComputer"]
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
                    UserFriendlyTitle = _localizationService["DatabaseError_Permissions_Title"],
                    UserFriendlyMessage = _localizationService["DatabaseError_Permissions_Message"],
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        _localizationService["Troubleshoot_CheckPermissions"],
                        _localizationService["Troubleshoot_CheckAppDataPermissions"],
                        _localizationService["Troubleshoot_CheckOtherPrograms"],
                        _localizationService["Troubleshoot_DisableFolderProtection"],
                        _localizationService["Troubleshoot_ContactAdmin"]
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
                    UserFriendlyTitle = _localizationService["DatabaseError_Locale_Title"],
                    UserFriendlyMessage = _localizationService["DatabaseError_Locale_Message"],
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        _localizationService["Troubleshoot_CheckLocale"],
                        _localizationService["Troubleshoot_CheckRegionSettings"],
                        _localizationService["Troubleshoot_ChangeLocaleTemporary"],
                        _localizationService["Troubleshoot_RestartAfterLocale"],
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
                    UserFriendlyTitle = _localizationService["DatabaseError_Security_Title"],
                    UserFriendlyMessage = _localizationService["DatabaseError_Security_Message"],
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        _localizationService["Troubleshoot_AddToExclusions"],
                        _localizationService["Troubleshoot_DisableRealTimeProtection"],
                        _localizationService["Troubleshoot_AddFolderToDefender"],
                        _localizationService["Troubleshoot_CheckSmartScreen"],
                        _localizationService["Troubleshoot_DisableAntivirusTemporary"]
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
                    UserFriendlyTitle = _localizationService["DatabaseError_Dependencies_Title"],
                    UserFriendlyMessage = _localizationService["DatabaseError_Dependencies_Message"],
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        _localizationService["Troubleshoot_InstallVCRedist"],
                        _localizationService["Troubleshoot_InstallNetFramework"],
                        _localizationService["Troubleshoot_RestartAfterInstall"]
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
                    UserFriendlyTitle = _localizationService["DatabaseError_PathCharacters_Title"],
                    UserFriendlyMessage = _localizationService["DatabaseError_PathCharacters_Message"],
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        _localizationService["Troubleshoot_CheckUsername"],
                        _localizationService["Troubleshoot_AvoidSpecialChars"],
                        _localizationService["Troubleshoot_TrySimplePath"],
                        _localizationService["Troubleshoot_CheckAppDataPath"]
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
                    UserFriendlyTitle = _localizationService["DatabaseError_ProcessFailure_Title"],
                    UserFriendlyMessage = _localizationService["DatabaseError_ProcessFailure_Message"],
                    TechnicalDetails = $"Context: {context}\nError: {exception.Message}\nInner: {exception.InnerException?.Message}",
                    TroubleshootingSteps = new[]
                    {
                        _localizationService["Troubleshoot_RestartAndRetry"],
                        _localizationService["Troubleshoot_CheckPortAvailability"],
                        _localizationService["Troubleshoot_FreeDiskSpace"],
                        _localizationService["Troubleshoot_RunAsAdmin"]
                    },
                    IsRecoverable = true,
                    OriginalException = exception
                };
            }

            // Generic error for unrecognized issues
            return CreateGenericError(_localizationService["DatabaseError_Unknown_Title"],
                _localizationService["DatabaseError_Unknown_Message"], exception);
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
                        UserFriendlyTitle = _localizationService["DatabaseError_DiskSpace_Title"],
                        UserFriendlyMessage = string.Format(_localizationService["DatabaseError_DiskSpace_Message"]),
                        TechnicalDetails = $"Required: {MINIMUM_DISK_SPACE_BYTES / 1024 / 1024} MB, Available: {drive.AvailableFreeSpace / 1024 / 1024} MB",
                        TroubleshootingSteps = new[]
                        {
                            _localizationService["Troubleshoot_FreeDiskSpace"],
                            _localizationService["Troubleshoot_EmptyRecycleBin"],
                            _localizationService["Troubleshoot_RunDiskCleanup"],
                            _localizationService["Troubleshoot_MoveLargeFiles"],
                            _localizationService["Troubleshoot_UninstallUnusedPrograms"]
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
                        _localizationService["Troubleshoot_EmptyRecycleBin"],
                        _localizationService["Troubleshoot_CheckFolderPermissions"],
                        _localizationService["Troubleshoot_CheckAntivirus"],
                        _localizationService["Troubleshoot_InstallInDifferentLocation"]
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
                    UserFriendlyTitle = _localizationService["DatabaseError_PathCharacters_Title"],
                    UserFriendlyMessage = _localizationService["DatabaseError_InvalidPathCharacters_Message"],
                    TechnicalDetails = $"Problematic path: {path}",
                    TroubleshootingSteps = new[]
                    {
                        _localizationService["Troubleshoot_AvoidSpecialChars"],
                        _localizationService["Troubleshoot_TrySimplePath"]
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
                        UserFriendlyTitle = _localizationService["DatabaseError_NetworkDownload_Title"],
                        UserFriendlyMessage = _localizationService["DatabaseError_NetworkDownload_Message"],
                        TechnicalDetails = "No network interfaces are available and PostgreSQL binaries need to be downloaded",
                        TroubleshootingSteps = new[]
                        {
                            _localizationService["Troubleshoot_CheckInternet"]
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
                UserFriendlyTitle = _localizationService["DatabaseError_Unknown_Title"],
                UserFriendlyMessage = _localizationService["DatabaseError_Unknown_Message"],
                TechnicalDetails = exception?.ToString() ?? "No additional details available",
                TroubleshootingSteps = new[]
                {
                    _localizationService["Troubleshoot_RestartApp"],
                    _localizationService["Troubleshoot_RunAsAdmin"],
                    _localizationService["Troubleshoot_RestartComputer"],
                    _localizationService["Troubleshoot_CheckEventLog"]
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