using System;
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
        public PresetTheme ThemeType { get; set; }

        // Main gradient colors
        public string? MainStartColor { get; set; }
        public string? MainEndColor { get; set; }

        // Secondary gradient colors
        public string? SecondaryStartColor { get; set; }
        public string? SecondaryEndColor { get; set; }

        // Accent gradient colors
        public string? AccentStartColor { get; set; }
        public string? AccentEndColor { get; set; }

        // Text gradient colors
        public string? TextStartColor { get; set; }
        public string? TextEndColor { get; set; }

        public string ToJson()
        {
            // Always include color values to preserve them
            return JsonSerializer.Serialize(this);
        }

        public static ThemeConfiguration FromJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return GetDefaultTheme();

            try
            {
                var config = JsonSerializer.Deserialize<ThemeConfiguration>(json);
                return config ?? GetDefaultTheme();
            }
            catch
            {
                return GetDefaultTheme();
            }
        }

        private static ThemeConfiguration GetDefaultTheme()
        {
            return new ThemeConfiguration
            {
                ThemeType = PresetTheme.Dark
            };
        }
        public static ThemeConfiguration GetDefaultCustomTheme()
        {
            return new ThemeConfiguration
            {
                ThemeType = PresetTheme.Custom,
                MainStartColor = "#08142E",
                MainEndColor = "#0D1117",
                SecondaryStartColor = "#41295a",
                SecondaryEndColor = "#2F0743",
                AccentStartColor = "#0000FF",
                AccentEndColor = "#EE82EE",
                TextStartColor = "#61045F",
                TextEndColor = "#aa0744"
            };
        }

        // Helper method to convert to ThemeColors
        public ThemeColors ToThemeColors()
        {
            if (ThemeType != PresetTheme.Custom)
            {
                throw new InvalidOperationException("Cannot convert preset theme to theme colors");
            }

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
    }
}