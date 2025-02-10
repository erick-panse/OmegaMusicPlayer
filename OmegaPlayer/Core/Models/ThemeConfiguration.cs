using System.Text.Json;
using Avalonia.Media;
using OmegaPlayer.Core.Services;

namespace OmegaPlayer.Core.Models
{
    public class ThemeUpdatedMessage
    {
        public ThemeConfiguration NewTheme { get; }

        public ThemeUpdatedMessage(ThemeConfiguration theme)
        {
            NewTheme = theme;
        }
    }
    public class ThemeConfiguration
    {
        public PresetTheme ThemeType { get; set; } = PresetTheme.Dark;

        // Main gradient colors
        public string MainStartColor { get; set; } = "#08142E";
        public string MainEndColor { get; set; } = "#0D1117";

        // Secondary gradient colors
        public string SecondaryStartColor { get; set; } = "#41295a";
        public string SecondaryEndColor { get; set; } = "#2F0743";

        // Accent gradient colors
        public string AccentStartColor { get; set; } = "#0000FF";
        public string AccentEndColor { get; set; } = "#EE82EE";

        // Text gradient colors
        public string TextStartColor { get; set; } = "#61045F";
        public string TextEndColor { get; set; } = "#aa0744";

        public ThemeColors ToThemeColors()
        {
            return new ThemeColors
            {
                MainStart = Color.Parse(MainStartColor),
                MainEnd = Color.Parse(MainEndColor),
                SecondaryStart = Color.Parse(SecondaryStartColor),
                SecondaryEnd = Color.Parse(SecondaryEndColor),
                AccentStart = Color.Parse(AccentStartColor),
                AccentEnd = Color.Parse(AccentEndColor),
                TextStart = Color.Parse(TextStartColor),
                TextEnd = Color.Parse(TextEndColor)
            };
        }

        public static ThemeConfiguration FromJson(string json)
        {
            return string.IsNullOrEmpty(json)
                ? new ThemeConfiguration()
                : JsonSerializer.Deserialize<ThemeConfiguration>(json);
        }

        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}