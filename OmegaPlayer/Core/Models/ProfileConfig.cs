namespace OmegaPlayer.Core.Models
{
    public class ProfileConfig
    {
        public int ID { get; set; }
        public int ProfileID { get; set; }
        public string EqualizerPresets { get; set; } = "{}";
        public int LastVolume { get; set; } = 50;
        public string Theme { get; set; } = "dark";
        public string MainStartColor { get; set; } = "#08142E";
        public string MainEndColor { get; set; } = "#0D1117";
        public string SecondaryStartColor { get; set; } = "#41295a";
        public string SecondaryEndColor { get; set; } = "#2F0743";
        public string AccentStartColor { get; set; } = "#0000FF";  // Blue
        public string AccentEndColor { get; set; } = "#EE82EE";    // Violet
        public string TextStartColor { get; set; } = "#61045F";
        public string TextEndColor { get; set; } = "#aa0744";
        public bool DynamicPause { get; set; } = false;
        public string[] BlacklistDirectory { get; set; }
        public string TrackSortingOrderState { get; set; } = "name_asc";
        public int? LastPlayedTrackID { get; set; }
        public int LastPlayedPosition { get; set; }
        public bool ShuffleEnabled { get; set; }
        public string RepeatMode { get; set; } = "none";
        public string LastQueueState { get; set; } = "{}";
        public string QueueState { get; set; } = "{}";
        public string ViewState { get; set; } = "{\"tracks\": \"grid\"}";
        public string SortingState { get; set; } = "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}";
    }
}
