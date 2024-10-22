
namespace OmegaPlayer.Models
{
    public class Config
    {
        // Playback Settings
        public int ConfigId { get; set; }
        public string DefaultPlaybackSpeed { get; set; } = "normal";
        public string EqualizerPresets { get; set; } = "none";
        public bool ReplayGain { get; set; } = false;

        // UI Preferences
        public string Theme { get; set; } = "light";
        public string MainColor { get; set; } = "#FFFFFF";
        public string SecondaryColor { get; set; } = "#000000";
        public string LayoutSettings { get; set; } = "grid";
        public int FontSize { get; set; } = 12;
        public string StylePreferences { get; set; }
        public bool ShowAlbumArt { get; set; } = true;
        public bool EnableAnimations { get; set; } = true;

        // Audio Output Settings
        public string OutputDevice { get; set; }
        public bool DynamicPause { get; set; } = false;

        // Library Management
        public int AutoRescanInterval { get; set; } = 0; // 0 means disabled
        public string IncludeFileTypes { get; set; }
        public string ExcludeFileTypes { get; set; }
        public string SortingOrderState { get; set; } = "track_name";
        public string SortPlaylistsState { get; set; } = "track_name";
        public string SortType { get; set; } = "ascending";

        // Queue/Playback Behavior
        public bool AutoPlayNext { get; set; } = true;
        public bool SaveQueue { get; set; } = true;
        public bool ShuffleState { get; set; } = false;
        public string RepeatMode { get; set; } = "none";
        public float Volume { get; set; } = 25;

        // Notifications
        public bool EnableTrackChangeNotifications { get; set; } = true;
        public string NotificationSettings { get; set; }

        // User-Specific Customization
        public int? LastUsedProfile { get; set; }
        public bool AutoLogin { get; set; } = false;
        public string LanguagePreference { get; set; } = "en";
    }
}
