namespace OmegaPlayer.Core.Models
{
    public class GlobalConfig
    {
        public int ID { get; set; }
        public int? LastUsedProfile { get; set; }
        public string LanguagePreference { get; set; } = "en";

        // Window state properties
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public int? WindowX { get; set; }
        public int? WindowY { get; set; }
        public bool IsWindowMaximized { get; set; } = false;
    }
}
