using System;
using System.IO;

namespace OmegaMusicPlayer.Core
{
    /// <summary>
    /// Provides build configuration-specific settings and paths
    /// Automatically isolates development and release environments
    /// </summary>
    public static class AppConfiguration
    {
        /// <summary>
        /// Gets the application name with build suffix
        /// Debug builds use "OmegaMusicPlayerDev", Release builds use "OmegaMusicPlayer"
        /// </summary>
        public static string ApplicationName
        {
            get
            {
                #if DEBUG
                return "OmegaMusicPlayerDev";
                #else
                return "OmegaMusicPlayer";
                #endif
            }
        }

        /// <summary>
        /// Gets the application data directory path based on build configuration
        /// </summary>
        public static string ApplicationDataPath
        {
            get
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appDataPath, ApplicationName);
            }
        }

        /// <summary>
        /// Gets the database directory path
        /// </summary>
        public static string DatabasePath => ApplicationDataPath;

        /// <summary>
        /// Gets the media directory path
        /// </summary>
        public static string MediaPath => Path.Combine(ApplicationDataPath, "media");

        /// <summary>
        /// Gets the logs directory path
        /// </summary>
        public static string LogsPath => Path.Combine(ApplicationDataPath, "logs");

        /// <summary>
        /// Gets the track cover media directory path
        /// </summary>
        public static string TrackCoverPath => Path.Combine(MediaPath, "track_cover");

        /// <summary>
        /// Gets the album cover media directory path
        /// </summary>
        public static string AlbumCoverPath => Path.Combine(MediaPath, "album_cover");

        /// <summary>
        /// Gets the artist photo media directory path
        /// </summary>
        public static string ArtistPhotoPath => Path.Combine(MediaPath, "artist_photo");

        /// <summary>
        /// Indicates whether this is a debug build
        /// </summary>
        public static bool IsDebugBuild
        {
            get
            {
                #if DEBUG
                return true;
                #else
                return false;
                #endif
            }
        }

        /// <summary>
        /// Gets build configuration name for logging and diagnostics
        /// </summary>
        public static string BuildConfiguration
        {
            get
            {
                #if DEBUG
                return "Debug";
                #else
                return "Release";
                #endif
            }
        }

        /// <summary>
        /// Creates all necessary application directories
        /// </summary>
        public static void EnsureDirectoriesExist()
        {
            var directories = new[]
            {
                ApplicationDataPath,
                MediaPath,
                LogsPath,
                TrackCoverPath,
                AlbumCoverPath,
                ArtistPhotoPath
            };

            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }

        /// <summary>
        /// Gets diagnostic information about the current configuration
        /// </summary>
        public static string GetDiagnosticInfo()
        {
            return $"Application Configuration:\n" +
                   $"  Name: {ApplicationName}\n" +
                   $"  Build: {BuildConfiguration}\n" +
                   $"  Is Debug: {IsDebugBuild}\n" +
                   $"  App Data Path: {ApplicationDataPath}\n" +
                   $"  Database Path: {DatabasePath}\n" +
                   $"  Media Path: {MediaPath}\n" +
                   $"  Logs Path: {LogsPath}";
        }
    }
}