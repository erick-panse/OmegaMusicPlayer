using CommunityToolkit.Mvvm.Messaging;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using OmegaMusicPlayer.Core.Models;
using OmegaMusicPlayer.Infrastructure.Services;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaMusicPlayer.Core.Services
{
    /// <summary>
    /// Implementation of error handling service with automatic log cleanup
    /// </summary>
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly IMessenger _messenger;
        private readonly LocalizationService _localizationService;
        private readonly string _logDirectory;
        private readonly object _logLock = new object();
        private const int MaxLogFileSize = 8 * 1024 * 1024; // 8MB
        private const int MaxLogFiles = 10;
        private const int LogRetentionDays = 5; // Keep logs for 5 days

        public ErrorHandlingService(IMessenger messenger, LocalizationService localizationService)
        {
            _messenger = messenger;
            _localizationService = localizationService;

            // Set up log directory
            _logDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "logs");

            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            // Clean up old logs on service initialization
            CleanupOldLogs();

            // Log application start
            LogInfo("Application started", $"OmegaMusicPlayer {GetAppVersion()} initialized");
        }

        /// <summary>
        /// Cleans up log files older than the retention period
        /// </summary>
        private void CleanupOldLogs()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-LogRetentionDays);
                var deletedCount = 0;
                var totalSize = 0L;

                // Get all log-related files in the directory
                var logFiles = Directory.GetFiles(_logDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => IsLogFile(file))
                    .ToList();

                foreach (var logFile in logFiles)
                {
                    try
                    {
                        var fileInfo = new FileInfo(logFile);

                        // Check if file is older than retention period
                        if (fileInfo.CreationTime < cutoffDate || fileInfo.LastWriteTime < cutoffDate)
                        {
                            totalSize += fileInfo.Length;
                            File.Delete(logFile);
                            deletedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log cleanup failure but don't throw - we don't want to crash during cleanup
                        LogToFile(ErrorSeverity.NonCritical,
                            "Log cleanup warning",
                            $"Failed to delete old log file: {logFile}",
                            ex);
                    }
                }

                // Log cleanup results if any files were deleted
                if (deletedCount > 0)
                {
                    var sizeMB = totalSize / (1024.0 * 1024.0);
                    LogToFile(ErrorSeverity.Info,
                        "Log cleanup completed",
                        $"Deleted {deletedCount} old log files, freed {sizeMB:F2} MB of disk space",
                        null);

                    (int fileCount, long totalSizeBytes, string oldestDate) = GetLogDirectoryInfo();

                    LogToFile(ErrorSeverity.Info,
                        "Current Log details",
                        $"Current log folder contains {fileCount} log files, {totalSizeBytes:F2} MB of disk space used, oldest date is: {oldestDate}",
                        null);


                }
            }
            catch (Exception ex)
            {
                // Even if cleanup fails, we should continue - log it but don't throw
                LogToFile(ErrorSeverity.NonCritical,
                    "Log cleanup failed",
                    "Failed to clean up old log files",
                    ex);
            }
        }

        /// <summary>
        /// Determines if a file is a log file that should be subject to cleanup
        /// </summary>
        private bool IsLogFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath).ToLowerInvariant();
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Check for various log file patterns
            return (extension == ".log" || extension == ".txt") && (
                fileName.StartsWith("omega-player-") ||           // Regular application logs
                fileName.StartsWith("database-error-") ||         // Database error logs  
                fileName.StartsWith("diagnostic-report-") ||      // Diagnostic reports
                fileName.StartsWith("unhandled-error-") ||        // Unhandled exception logs
                fileName.Contains("error") ||                     // General error logs
                fileName.Contains("crash") ||                     // Crash logs
                fileName.Contains("exception")                    // Exception logs
            );
        }

        private string GetAppVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version.ToString();
            }
            catch
            {
                return "Unknown Version";
            }
        }

        private string GetCurrentLogFilePath()
        {
            var date = DateTime.Now.ToString("yyyy-MM-dd");
            return Path.Combine(_logDirectory, $"omega-player-{date}.log");
        }

        private void LogToFile(ErrorSeverity severity, string message, string details, Exception exception)
        {
            lock (_logLock)
            {
                try
                {
                    var logPath = GetCurrentLogFilePath();
                    var logFile = new FileInfo(logPath);

                    // Check if we need to rotate logs
                    if (logFile.Exists && logFile.Length > MaxLogFileSize)
                    {
                        RotateLogFiles();
                    }

                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var log = $"[{timestamp}] [{severity}] {message}";

                    if (!string.IsNullOrEmpty(details))
                    {
                        log += $"\nDetails: {details}";
                    }

                    if (exception != null)
                    {
                        log += $"\nException: {exception.GetType().Name}: {exception.Message}";
                        log += $"\nStack Trace: {exception.StackTrace}";

                        if (exception.InnerException != null)
                        {
                            log += $"\nInner Exception: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}";
                            log += $"\nInner Stack Trace: {exception.InnerException.StackTrace}";
                        }
                    }

                    log += "\n\n";

                    File.AppendAllText(logPath, log);
                }
                catch
                {
                    // We're already in the error handler, just swallow the exception to prevent infinite loops
                }
            }
        }

        private void RotateLogFiles()
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "omega-player-*.log")
                    .OrderByDescending(f => new FileInfo(f).CreationTime)
                    .ToList();

                // Remove oldest files if we exceed the max
                while (logFiles.Count >= MaxLogFiles)
                {
                    var oldest = logFiles.Last();
                    File.Delete(oldest);
                    logFiles.Remove(oldest);
                }

                // Rename current log file
                var currentLog = GetCurrentLogFilePath();
                if (File.Exists(currentLog))
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");
                    var archivedPath = Path.Combine(_logDirectory, $"omega-player-{timestamp}.log");
                    File.Move(currentLog, archivedPath);
                }
            }
            catch
            {
                // Just try our best with log rotation, don't crash if it fails
            }
        }

        public void LogError(ErrorSeverity severity, string message, string details = null, Exception exception = null, bool showNotification = true)
        {
            // Log to file
            LogToFile(severity, message, details, exception);

            // Show notification if requested
            if (showNotification)
            {
                _messenger.Send(new ErrorOccurredMessage(severity, message, details, exception));
            }
        }

        public void LogInfo(string message, string details = null, bool showNotification = false)
        {
            LogToFile(ErrorSeverity.Info, message, details, null);

            // Show notification if requested
            if (showNotification)
            {
                _messenger.Send(new ErrorOccurredMessage(ErrorSeverity.Info, message, details));
            }
        }

        public void SafeExecute(Action action, string contextMessage, ErrorSeverity severity = ErrorSeverity.NonCritical, bool showNotification = true)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LogError(severity,
                    $"{_localizationService["ErrorOccurred"]}: {contextMessage}",
                    contextMessage,
                    ex,
                    showNotification);
            }
        }

        public async Task SafeExecuteAsync(Func<Task> taskFunc, string contextMessage, ErrorSeverity severity = ErrorSeverity.NonCritical, bool showNotification = true)
        {
            try
            {
                await taskFunc();
            }
            catch (Exception ex)
            {
                LogError(severity,
                    $"{_localizationService["ErrorOccurred"]}: {contextMessage}",
                    contextMessage,
                    ex,
                    showNotification);
            }
        }

        public T SafeExecute<T>(Func<T> func, string contextMessage, T defaultValue = default, ErrorSeverity severity = ErrorSeverity.NonCritical, bool showNotification = true)
        {
            try
            {
                return func();
            }
            catch (Exception ex)
            {
                LogError(severity,
                    $"{_localizationService["ErrorOccurred"]}: {contextMessage}",
                    contextMessage,
                    ex,
                    showNotification);

                return defaultValue;
            }
        }

        public async Task<T> SafeExecuteAsync<T>(Func<Task<T>> taskFunc, string contextMessage, T defaultValue = default, ErrorSeverity severity = ErrorSeverity.NonCritical, bool showNotification = true)
        {
            try
            {
                return await taskFunc();
            }
            catch (Exception ex)
            {
                LogError(severity,
                    $"{_localizationService["ErrorOccurred"]}: {contextMessage}",
                    contextMessage,
                    ex,
                    showNotification);

                return defaultValue;
            }
        }

        /// <summary>
        /// Gets information about current log directory status
        /// </summary>
        public (int FileCount, long TotalSizeBytes, string OldestLogDate) GetLogDirectoryInfo()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                    return (0, 0, "N/A");

                var logFiles = Directory.GetFiles(_logDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(file => IsLogFile(file))
                    .Select(f => new FileInfo(f))
                    .ToList();

                var fileCount = logFiles.Count;
                var totalSize = logFiles.Sum(f => f.Length);
                var oldestDate = logFiles.Count > 0 ?
                    logFiles.Min(f => f.CreationTime).ToString("yyyy-MM-dd") :
                    "N/A";

                return (fileCount, totalSize, oldestDate);
            }
            catch
            {
                return (0, 0, "Error");
            }
        }
    }
}