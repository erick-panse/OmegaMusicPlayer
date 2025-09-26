namespace OmegaMusicPlayer.Core.Enums
{
    /// <summary>
    /// Severity levels for application errors
    /// </summary>
    public enum ErrorSeverity
    {
        /// <summary>
        /// Critical errors that affect application stability or core functionality
        /// </summary>
        Critical,

        /// <summary>
        /// Errors that affect playback but not application stability
        /// </summary>
        Playback,

        /// <summary>
        /// UI-related errors or non-critical functionality issues
        /// </summary>
        NonCritical,

        /// <summary>
        /// Informational messages that don't represent errors
        /// </summary>
        Info
    }
}
