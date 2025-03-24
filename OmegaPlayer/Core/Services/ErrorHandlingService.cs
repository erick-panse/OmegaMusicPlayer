using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Enums;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Models;
using OmegaPlayer.Infrastructure.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OmegaPlayer.Core.Services
{
    /// <summary>
    /// Implementation of error handling service
    /// </summary>
    public class ErrorHandlingService : IErrorHandlingService
    {
        private readonly IMessenger _messenger;
        private readonly LocalizationService _localizationService;
        private readonly string _logDirectory;
        private readonly object _logLock = new object();
        private const int MaxLogFileSize = 5 * 1024 * 1024; // 5MB
        private const int MaxLogFiles = 5;

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

            // Log application start
            LogInfo("Application started", $"OmegaPlayer {GetAppVersion()} initialized");
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

            // Debug output in development
            Debug.WriteLine($"[{severity}] {message}");
            if (exception != null)
            {
                Debug.WriteLine($"Exception: {exception.Message}");
            }

            // Show notification if requested
            if (showNotification)
            {
                _messenger.Send(new ErrorOccurredMessage(severity, message, details, exception));
            }

            // Special handling for critical errors
            if (severity == ErrorSeverity.Critical)
            {
                // For critical errors, we might want to take additional actions
                // such as attempting recovery, saving application state, etc.
                // This would depend on specific application requirements
            }
        }

        public void LogInfo(string message, string details = null)
        {
            LogToFile(ErrorSeverity.Info, message, details, null);
            Debug.WriteLine($"[INFO] {message}");
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
    }
}