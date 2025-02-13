namespace OmegaPlayer.Core.Models
{
    public class GlobalConfig
    {
        public int ID { get; set; }
        public int? LastUsedProfile { get; set; }
        public string LanguagePreference { get; set; } = "en";
    }
}
