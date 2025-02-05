namespace OmegaPlayer.Core.Models
{
    public class ProfileConfig
    {
        public int ID { get; set; }
        public int ProfileID { get; set; }
        public float DefaultPlaybackSpeed { get; set; } = 1.0f;
        public string EqualizerPresets { get; set; } = "{}";
        public int LastVolume { get; set; } = 50;
        public string Theme { get; set; } = "dark";
        public string MainColor { get; set; } = "#1a1a1a";
        public string SecondaryColor { get; set; } = "#333333";
        public string OutputDevice { get; set; }
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
