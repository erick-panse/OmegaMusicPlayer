using Avalonia;
using Avalonia.Media;
using System;
using OmegaPlayer.Core.Utils;

namespace OmegaPlayer.Core.Services
{
    public class ThemeService
    {
        private readonly Application _app;
        private const double DARKER_FACTOR = 0.3;
        private const double DARKEST_FACTOR = 0.5;
        private const double LIGHTER_FACTOR = 0.2;
        private const double LIGHTEST_FACTOR = 0.4;

        public ThemeService(Application app)
        {
            _app = app;
        }

        public void ApplyTheme(ThemeColors colors)
        {
            var resources = _app.Resources;

            // ======== MAIN COLOR ========
            // Main Gradient (primary background)
            var mainGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };
            mainGradient.GradientStops.Add(new GradientStop(colors.MainStart, 0));
            mainGradient.GradientStops.Add(new GradientStop(colors.MainEnd, 0.7));
            resources["MainColor"] = mainGradient;

            // Solid variants based on average color
            var mainAvgColor = mainGradient.GetAverageColor();
            resources["MainColorSolid"] = mainAvgColor;
            resources["MainColorDarker"] = mainAvgColor.Darken(DARKER_FACTOR);
            resources["MainColorDarkest"] = mainAvgColor.Darken(DARKEST_FACTOR);
            resources["MainColorLighter"] = mainAvgColor.Lighten(LIGHTER_FACTOR);
            resources["MainColorLightest"] = mainAvgColor.Lighten(LIGHTEST_FACTOR);

            // Gradient variants
            resources["MainColorDarkerGradient"] = mainGradient.Darken(DARKER_FACTOR);
            resources["MainColorLighterGradient"] = mainGradient.Lighten(LIGHTER_FACTOR);

            // ======== SECONDARY COLOR ========
            // Secondary Gradient (panels, cards)
            var secondaryGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.7, RelativeUnit.Relative)
            };
            secondaryGradient.GradientStops.Add(new GradientStop(colors.SecondaryStart, 0));
            secondaryGradient.GradientStops.Add(new GradientStop(colors.SecondaryEnd, 0.7));
            resources["SecondaryColor"] = secondaryGradient;

            // Solid variants
            var secondaryAvgColor = secondaryGradient.GetAverageColor();
            resources["SecondaryColorSolid"] = secondaryAvgColor;
            resources["SecondaryColorDarker"] = secondaryAvgColor.Darken(DARKER_FACTOR);
            resources["SecondaryColorDarkest"] = secondaryAvgColor.Darken(DARKEST_FACTOR);
            resources["SecondaryColorLighter"] = secondaryAvgColor.Lighten(LIGHTER_FACTOR);
            resources["SecondaryColorLightest"] = secondaryAvgColor.Lighten(LIGHTEST_FACTOR);

            // Gradient variants
            resources["SecondaryColorDarkerGradient"] = secondaryGradient.Darken(DARKER_FACTOR);
            resources["SecondaryColorLighterGradient"] = secondaryGradient.Lighten(LIGHTER_FACTOR);

            // ======== ACCENT COLOR ========
            // Accent Gradient (interactive elements)
            var accentGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };
            accentGradient.GradientStops.Add(new GradientStop(colors.AccentStart, 0));
            accentGradient.GradientStops.Add(new GradientStop(colors.AccentEnd, 1));
            resources["AccentColor"] = accentGradient;

            // Solid variants
            var accentAvgColor = accentGradient.GetAverageColor();
            resources["AccentColorSolid"] = accentAvgColor;
            resources["AccentColorDarker"] = accentAvgColor.Darken(DARKER_FACTOR);
            resources["AccentColorDarkest"] = accentAvgColor.Darken(DARKEST_FACTOR);
            resources["AccentColorLighter"] = accentAvgColor.Lighten(LIGHTER_FACTOR);
            resources["AccentColorLightest"] = accentAvgColor.Lighten(LIGHTEST_FACTOR);

            // Gradient variants
            resources["AccentColorDarkerGradient"] = accentGradient.Darken(DARKER_FACTOR);
            resources["AccentColorLighterGradient"] = accentGradient.Lighten(LIGHTER_FACTOR);

            // ======== TEXT COLOR ========
            // Text Gradient
            var textGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };
            textGradient.GradientStops.Add(new GradientStop(colors.TextStart, 0));
            textGradient.GradientStops.Add(new GradientStop(colors.TextEnd, 1));
            resources["TextColor"] = textGradient;

            // Solid variants
            var textAvgColor = textGradient.GetAverageColor();
            resources["TextColorSolid"] = textAvgColor;
            resources["TextColorDarker"] = textAvgColor.Darken(DARKER_FACTOR);
            resources["TextColorDarkest"] = textAvgColor.Darken(DARKEST_FACTOR);
            resources["TextColorLighter"] = textAvgColor.Lighten(LIGHTER_FACTOR);
            resources["TextColorLightest"] = textAvgColor.Lighten(LIGHTEST_FACTOR);

            // Gradient variants
            resources["TextColorDarkerGradient"] = textGradient.Darken(DARKER_FACTOR);
            resources["TextColorLighterGradient"] = textGradient.Lighten(LIGHTER_FACTOR);

            // Additional utility colors based on main colors
            resources["ErrorColor"] = Color.Parse("#FF4444");
            resources["WarningColor"] = Color.Parse("#FFBB33");
            resources["SuccessColor"] = Color.Parse("#00C851");

            // UI State colors 
            resources["ActiveElementBackground"] = resources["AccentColor"];
            resources["HoverElementBackground"] = resources["SecondaryColorLighter"];
            resources["PressedElementBackground"] = resources["AccentColorDarker"];
            resources["DisabledElementBackground"] = resources["MainColorDarker"];
            resources["DisabledElementForeground"] = textAvgColor.Darken(0.5);
        }

        public void ApplyPresetTheme(PresetTheme theme)
        {
            var colors = GetPresetThemeColors(theme);
            ApplyTheme(colors);
        }

        private ThemeColors GetPresetThemeColors(PresetTheme theme)
        {
            return theme switch
            {
                PresetTheme.Dark => new ThemeColors
                {
                    // Dark theme
                    MainStart = Color.Parse("#1A1A1A"),
                    MainEnd = Color.Parse("#262626"),
                    SecondaryStart = Color.Parse("#333333"),
                    SecondaryEnd = Color.Parse("#444444"),
                    AccentStart = Color.Parse("#38BDF8"),
                    AccentEnd = Color.Parse("#34D399"),
                    TextStart = Color.Parse("#BEE5F7"),
                    TextEnd = Color.Parse("#A9D4C3")
                },
                PresetTheme.Light => new ThemeColors
                {
                    // Light theme
                    MainStart = Color.Parse("#FFFBF5"),
                    MainEnd = Color.Parse("#F8F5F0"),
                    SecondaryStart = Color.Parse("#F0EAE2"),
                    SecondaryEnd = Color.Parse("#E8C69B"),
                    AccentStart = Color.Parse("#F59E0B"),
                    AccentEnd = Color.Parse("#EA580C"),
                    TextStart = Color.Parse("#44403C"),
                    TextEnd = Color.Parse("#292524")
                },
                PresetTheme.DarkNeon => new ThemeColors
                {
                    // Dark Neon theme
                    MainStart = Color.Parse("#08142E"),
                    MainEnd = Color.Parse("#0D1117"),
                    SecondaryStart = Color.Parse("#0f0c29"),
                    SecondaryEnd = Color.Parse("#302b63"),
                    AccentStart = Color.Parse("#0000FF"),
                    AccentEnd = Color.Parse("#EE82EE"),
                    TextStart = Color.Parse("#7F00FF"),
                    TextEnd = Color.Parse("#E100FF")
                },
                PresetTheme.Sunset => new ThemeColors
                {
                    // Tropical Light theme
                    MainStart = Color.Parse("#800080"),
                    MainEnd = Color.Parse("#FFA500"),
                    SecondaryStart = Color.Parse("#FF0453"),
                    SecondaryEnd = Color.Parse("#FF9E00"),
                    AccentStart = Color.Parse("#FFFB00"),
                    AccentEnd = Color.Parse("#09FFF4"),
                    TextStart = Color.Parse("#FFFB00"),
                    TextEnd = Color.Parse("#FFF000")
                },
                _ => throw new ArgumentException("Unknown theme preset", nameof(theme))
            };
        }
    }

public class ThemeColors
    {
        public Color MainStart { get; set; }
        public Color MainEnd { get; set; }
        public Color SecondaryStart { get; set; }
        public Color SecondaryEnd { get; set; }
        public Color AccentStart { get; set; }
        public Color AccentEnd { get; set; }
        public Color TextStart { get; set; }
        public Color TextEnd { get; set; }
    }

    public enum PresetTheme
    {
        Dark,
        Light, 
        DarkNeon,
        Sunset,
        Custom
    }
}
