using OmegaPlayer.Core.Enums;
using System;
using System.Threading.Tasks;

namespace OmegaPlayer.Core.Interfaces
{
    /// <summary>
    /// Interface for error handling service
    /// </summary>
    public interface IErrorHandlingService
    {
        /// <summary>
        /// Logs an error and optionally shows a notification
        /// </summary>
        /// <param name="severity">Error severity level</param>
        /// <param name="message">User-friendly error message</param>
        /// <param name="details">Additional error details</param>
        /// <param name="exception">Optional exception that caused the error</param>
        /// <param name="showNotification">Whether to show a notification to the user</param>
        void LogError(ErrorSeverity severity, string message, string details = null, Exception exception = null, bool showNotification = true);

        /// <summary>
        /// Logs informations, not to be mistaken with errors
        /// </summary>
        /// <param name="message">User-friendly error message</param>
        /// <param name="details">Additional error details</param>
        void LogInfo(string message, string details = null, bool showNotification = false);

        /// <summary>
        /// Wraps an action in a try-catch block with appropriate error handling
        /// </summary>
        void SafeExecute(Action action, string contextMessage, ErrorSeverity severity = ErrorSeverity.NonCritical, bool showNotification = true);

        /// <summary>
        /// Wraps an async task in a try-catch block with appropriate error handling
        /// </summary>
        Task SafeExecuteAsync(Func<Task> taskFunc, string contextMessage, ErrorSeverity severity = ErrorSeverity.NonCritical, bool showNotification = true);

        /// <summary>
        /// Wraps a function with a return value in a try-catch block
        /// </summary>
        T SafeExecute<T>(Func<T> func, string contextMessage, T defaultValue = default, ErrorSeverity severity = ErrorSeverity.NonCritical, bool showNotification = true);

        /// <summary>
        /// Wraps an async function with a return value in a try-catch block
        /// </summary>
        Task<T> SafeExecuteAsync<T>(Func<Task<T>> taskFunc, string contextMessage, T defaultValue = default, ErrorSeverity severity = ErrorSeverity.NonCritical, bool showNotification = true);
    }
}
