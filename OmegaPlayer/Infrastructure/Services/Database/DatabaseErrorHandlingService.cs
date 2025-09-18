using System;
using System.ComponentModel;
using System.Diagnostics;
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

        public enum BinaryFailureReason
        {
            Unknown,
            Missing,
            Corrupted,
            Permissions,
            Dependencies
        }

        public class BinaryValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
            public BinaryFailureReason FailureReason { get; set; }

            public static BinaryValidationResult Valid() =>
                new BinaryValidationResult { IsValid = true };

            public static BinaryValidationResult Failed(string error, BinaryFailureReason reason = BinaryFailureReason.Unknown) =>
                new BinaryValidationResult
                {
                    IsValid = false,
                    ErrorMessage = error,
                    FailureReason = reason
                };
        }

        /// <summary>
        /// Database error information
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

                // Validate PostgreSQL binaries if they exist
                var binaryError = ValidatePostgreSQLBinaries(databasePath);
                if (binaryError != null) return binaryError;

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

        /// <summary>
        /// Validates PostgreSQL binaries are functional, not just present
        /// </summary>
        public DatabaseError ValidatePostgreSQLBinaries(string databasePath)
        {
            try
            {
                var binaryPath = FindPostgreSQLBinary(databasePath);
                if (string.IsNullOrEmpty(binaryPath))
                {
                    return null; // No binaries found - will trigger download
                }

                // Test if binary is executable and functional
                var validationResult = TestBinaryExecution(binaryPath);
                if (!validationResult.IsValid)
                {
                    return CreateBinaryCorruptionError(validationResult);
                }

                return null; // Binaries are valid
            }
            catch (Exception ex)
            {
                return CreateGenericError("Binary Validation Failed",
                    "Unable to validate PostgreSQL binaries.", ex);
            }
        }

        /// <summary>
        /// Finds the PostgreSQL binary in expected locations
        /// </summary>
        private string FindPostgreSQLBinary(string databasePath)
        {
            const string instanceId = "dcd227f4-89b9-4d85-b7e9-180263ab03a9";

            var possiblePaths = new[]
            {
                // Actual MysticMind.PostgresEmbed structure
                Path.Combine(databasePath, "pg_embed", instanceId, "bin", "postgres.exe"),  // Windows
                Path.Combine(databasePath, "pg_embed", instanceId, "bin", "postgres"),     // Linux/Mac
        
                // Alternative structures
                Path.Combine(databasePath, "pgsql", "bin", "postgres.exe"),                // Windows fallback
                Path.Combine(databasePath, "pgsql", "bin", "postgres"),                   // Linux/Mac fallback
                Path.Combine(databasePath, "bin", "postgres.exe"),                        // Direct bin folder Windows
                Path.Combine(databasePath, "bin", "postgres"),                           // Direct bin folder Linux/Mac
                
                // Search in any subdirectories with GUID pattern
                GetPostgresBinaryFromGuidFolders(databasePath)
            };

            return possiblePaths.Where(p => !string.IsNullOrEmpty(p)).FirstOrDefault(File.Exists);
        }

        /// <summary>
        /// Searches for postgres binary in any GUID-named folders under pg_embed
        /// </summary>
        private string GetPostgresBinaryFromGuidFolders(string databasePath)
        {
            try
            {
                var pgEmbedPath = Path.Combine(databasePath, "pg_embed");

                if (!Directory.Exists(pgEmbedPath))
                    return null;

                // Look for folders that match GUID pattern
                var guidFolders = Directory.GetDirectories(pgEmbedPath)
                    .Where(dir => IsGuidFolder(Path.GetFileName(dir)));

                foreach (var guidFolder in guidFolders)
                {
                    var windowsBinary = Path.Combine(guidFolder, "bin", "postgres.exe");
                    var unixBinary = Path.Combine(guidFolder, "bin", "postgres");

                    if (File.Exists(windowsBinary))
                        return windowsBinary;
                    if (File.Exists(unixBinary))
                        return unixBinary;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if folder name looks like a GUID
        /// </summary>
        private bool IsGuidFolder(string folderName)
        {
            return Guid.TryParse(folderName, out _);
        }

        /// <summary>
        /// Tests if PostgreSQL binary can execute and return version info
        /// </summary>
        private BinaryValidationResult TestBinaryExecution(string binaryPath)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = binaryPath,
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(binaryPath)
                };

                using var process = new Process { StartInfo = processInfo };

                // Test if process can start
                if (!process.Start())
                {
                    return BinaryValidationResult.Failed(_localizationService["BinaryExecution_StartupFailed"]);
                }

                // Set timeout to prevent hanging
                if (!process.WaitForExit(5000)) // 5 second timeout
                {
                    process.Kill();
                    return BinaryValidationResult.Failed(_localizationService["BinaryExecution_Timeout"]);
                }

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                // Check for successful version output
                if (process.ExitCode == 0 && output.Contains("postgres", StringComparison.OrdinalIgnoreCase))
                {
                    return BinaryValidationResult.Valid();
                }

                // Analyze specific error patterns
                if (!string.IsNullOrEmpty(error))
                {
                    if (error.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                        return BinaryValidationResult.Failed(_localizationService["BinaryExecution_PermissionDenied"], BinaryFailureReason.Permissions);

                    if (error.Contains("not found", StringComparison.OrdinalIgnoreCase))
                        return BinaryValidationResult.Failed(_localizationService["BinaryExecution_DependenciesMissing"], BinaryFailureReason.Dependencies);
                }

                return BinaryValidationResult.Failed(
                    string.Format(_localizationService["BinaryExecution_UnexpectedExit"], process.ExitCode, error));
            }
            catch (UnauthorizedAccessException)
            {
                return BinaryValidationResult.Failed(_localizationService["BinaryExecution_PermissionDenied"], BinaryFailureReason.Permissions);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 2) // File not found
            {
                return BinaryValidationResult.Failed(_localizationService["BinaryExecution_DependenciesMissing"], BinaryFailureReason.Missing);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 193) // Invalid Win32 application
            {
                return BinaryValidationResult.Failed(_localizationService["BinaryExecution_InvalidFormat"], BinaryFailureReason.Corrupted);
            }
            catch (Exception ex)
            {
                return BinaryValidationResult.Failed(
                    string.Format(_localizationService["BinaryExecution_Error"], ex.Message));
            }
        }

        /// <summary>
        /// Creates appropriate error for binary corruption scenarios
        /// </summary>
        private DatabaseError CreateBinaryCorruptionError(BinaryValidationResult validationResult)
        {
            var category = validationResult.FailureReason switch
            {
                BinaryFailureReason.Permissions => DatabaseErrorCategory.Permissions,
                BinaryFailureReason.Dependencies => DatabaseErrorCategory.Dependencies,
                BinaryFailureReason.Corrupted => DatabaseErrorCategory.Dependencies,
                _ => DatabaseErrorCategory.Unknown
            };

            var (title, message, steps) = GetCorruptionErrorDetails(validationResult.FailureReason);

            return new DatabaseError
            {
                Category = category,
                UserFriendlyTitle = title,
                UserFriendlyMessage = message,
                TechnicalDetails = $"Binary validation failed: {validationResult.ErrorMessage}",
                TroubleshootingSteps = steps,
                IsRecoverable = false,
                OriginalException = null
            };
        }

        /// <summary>
        /// Gets error details for specific corruption types
        /// </summary>
        private (string title, string message, string[] steps) GetCorruptionErrorDetails(BinaryFailureReason reason)
        {
            return reason switch
            {
                BinaryFailureReason.Corrupted => (
                    _localizationService["DatabaseError_CorruptedBinaries_Title"],
                    _localizationService["DatabaseError_CorruptedBinaries_Message"],
                    new[] {
                _localizationService["Troubleshoot_DeleteAppDataFolder"],
                _localizationService["Troubleshoot_ReinstallFromFreshDownload"],
                    }
                ),
                BinaryFailureReason.Dependencies => (
                    _localizationService["DatabaseError_Dependencies_Title"],
                    _localizationService["DatabaseError_Dependencies_Message"],
                    new[] {
                _localizationService["Troubleshoot_InstallVCRedistLatest"],
                _localizationService["Troubleshoot_RestartComputer"],
                _localizationService["Troubleshoot_InstallNetFramework"]
                    }
                ),
                BinaryFailureReason.Permissions => (
                    _localizationService["DatabaseError_Permissions_Title"],
                    _localizationService["DatabaseError_Permissions_Message"],
                    new[] {
                _localizationService["Troubleshoot_RunAsAdmin"],
                _localizationService["Troubleshoot_AddToExclusions"],
                _localizationService["Troubleshoot_CheckAppDataNotProtected"],
                _localizationService["Troubleshoot_DisableFolderProtection"],
                _localizationService["Troubleshoot_ContactAdmin"]
                    }
                ),
                _ => (
                    _localizationService["DatabaseError_Unknown_Title"],
                    _localizationService["DatabaseError_Unknown_Message"],
                    new[] {
                _localizationService["Troubleshoot_RestartApp"],
                _localizationService["Troubleshoot_RunAsAdmin"],
                _localizationService["Troubleshoot_RestartComputer"]
                    }
                )
            };
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
                const string instanceId = "dcd227f4-89b9-4d85-b7e9-180263ab03a9";

                // Check if PostgreSQL binaries already exist
                var possibleBinaryPaths = new[]
                {
                    // Actual MysticMind.PostgresEmbed structure
                    Path.Combine(databasePath, "pg_embed", instanceId, "bin", "postgres.exe"),  // Windows
                    Path.Combine(databasePath, "pg_embed", instanceId, "bin", "postgres"),     // Linux/Mac
                    
                    // Alternative structures
                    Path.Combine(databasePath, "pgsql", "bin", "postgres.exe"),     // Windows
                    Path.Combine(databasePath, "pgsql", "bin", "postgres"),        // Linux/Mac
                    Path.Combine(databasePath, "bin", "postgres.exe"),             // Alternative Windows
                    Path.Combine(databasePath, "bin", "postgres"),                 // Alternative Linux/Mac

                    // Search in any subdirectories with GUID pattern
                    GetPostgresBinaryFromGuidFolders(databasePath)
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