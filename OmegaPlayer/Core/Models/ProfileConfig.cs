namespace OmegaPlayer.Core.Models
{
    public class ProfileConfig
    {
        public int ID { get; set; }
        public int ProfileID { get; set; }
        public string EqualizerPresets { get; set; } = "{}";
        public int LastVolume { get; set; } = 50;
        public string Theme { get; set; } = "{}"; // Will store complete theme config as JSON
        public bool DynamicPause { get; set; } = false;
        public string[] BlacklistDirectory { get; set; }
        public string ViewState { get; set; } = "{\"tracks\": \"grid\"}";
        public string SortingState { get; set; } = "{\"library\": {\"field\": \"title\", \"order\": \"asc\"}}";
    }
}
