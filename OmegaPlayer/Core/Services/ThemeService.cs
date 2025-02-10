using Avalonia;
using Avalonia.Media;
using System;

namespace OmegaPlayer.Core.Services
{
    public class ThemeService
    {
        private readonly Application _app;

        public ThemeService(Application app)
        {
            _app = app;
        }

        public void ApplyTheme(ThemeColors colors)
        {
            var resources = _app.Resources;

            // Main Gradient (primary background)
            var mainGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };
            mainGradient.GradientStops.Add(new GradientStop(colors.MainStart, 0));
            mainGradient.GradientStops.Add(new GradientStop(colors.MainEnd, 0.5));
            resources["MainColor"] = mainGradient;

            // Secondary Gradient (panels, cards)
            var secondaryGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 0.7, RelativeUnit.Relative)
            };
            secondaryGradient.GradientStops.Add(new GradientStop(colors.SecondaryStart, 0));
            secondaryGradient.GradientStops.Add(new GradientStop(colors.SecondaryEnd, 0.7));
            resources["SecondaryColor"] = secondaryGradient;

            // Accent Gradient (interactive elements)
            var accentGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };
            accentGradient.GradientStops.Add(new GradientStop(colors.AccentStart, 0));
            accentGradient.GradientStops.Add(new GradientStop(colors.AccentEnd, 1));
            resources["TextColor"] = accentGradient;

            // Text Gradient
            var textGradient = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative)
            };
            textGradient.GradientStops.Add(new GradientStop(colors.TextStart, 0));
            textGradient.GradientStops.Add(new GradientStop(colors.TextEnd, 1));
            resources["Text"] = textGradient;
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
                    MainStart = Color.Parse("#08142E"),
                    MainEnd = Color.Parse("#0D1117"),
                    SecondaryStart = Color.Parse("#41295a"),
                    SecondaryEnd = Color.Parse("#2F0743"),
                    AccentStart = Color.Parse("Blue"),
                    AccentEnd = Color.Parse("Violet"),
                    TextStart = Color.Parse("61045F"),
                    TextEnd = Color.Parse("aa0744")
                },
                PresetTheme.Light => new ThemeColors
                {
                    // Light theme
                    MainStart = Color.Parse("#F0F2F5"),
                    MainEnd = Color.Parse("#E4E6E9"),
                    SecondaryStart = Color.Parse("#E6E9F0"),
                    SecondaryEnd = Color.Parse("#D5D8DC"),
                    AccentStart = Color.Parse("#6495ED"),
                    AccentEnd = Color.Parse("#9370DB"),
                    TextStart = Color.Parse("#4A4A4A"),
                    TextEnd = Color.Parse("#2A2A2A")
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
        Custom
    }
}