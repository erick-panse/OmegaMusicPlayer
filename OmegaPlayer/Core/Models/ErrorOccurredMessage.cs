using OmegaPlayer.Core.Enums;
using System;

namespace OmegaPlayer.Core.Models
{
    /// <summary>
    /// Message sent when an error occurs in the application
    /// </summary>
    public class ErrorOccurredMessage
    {
        public ErrorSeverity Severity { get; }
        public string Message { get; }
        public string Details { get; }
        public Exception Exception { get; }
        public TimeSpan DisplayDuration { get; }

        public ErrorOccurredMessage(
            ErrorSeverity severity,
            string message,
            string details = null,
            Exception exception = null,
            TimeSpan? displayDuration = null)
        {
            Severity = severity;
            Message = message;
            Details = details;
            Exception = exception;
            DisplayDuration = displayDuration ?? (severity == ErrorSeverity.Critical
                ? TimeSpan.FromSeconds(15)
                : TimeSpan.FromSeconds(5));
        }
    }

}
