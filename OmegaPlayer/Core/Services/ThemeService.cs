using Avalonia;
using Avalonia.Media;
using System;
using OmegaPlayer.Core.Utils;
using OmegaPlayer.Core.Interfaces;
using OmegaPlayer.Core.Enums;
using CommunityToolkit.Mvvm.Messaging;
using OmegaPlayer.Core.Models;

namespace OmegaPlayer.Core.Services
{
    public class ThemeService
    {
        private readonly Application _app;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IMessenger _messenger;

        private const double DARKER_FACTOR = 0.3;
        private const double DARKEST_FACTOR = 0.5;
        private const double LIGHTER_FACTOR = 0.2;
        private const double LIGHTEST_FACTOR = 0.4;

        // Default colors to fall back to in case of error
        private static readonly Color DEFAULT_MAIN_COLOR = Color.Parse("#1A1A1A");
        private static readonly Color DEFAULT_SECONDARY_COLOR = Color.Parse("#333333");
        private static readonly Color DEFAULT_ACCENT_COLOR = Color.Parse("#38BDF8");
        private static readonly Color DEFAULT_TEXT_COLOR = Color.Parse("#FFFFFF");

        // Cache the last successfully applied theme for recovery
        private ThemeColors _lastSuccessfulTheme;

        public ThemeService(
            Application app,
            IErrorHandlingService errorHandlingService = null,
            IMessenger messenger = null)
        {
            _app = app;
            _errorHandlingService = errorHandlingService;
            _messenger = messenger;

            // Initialize with a safe fallback theme
            _lastSuccessfulTheme = GetPresetThemeColors(PresetTheme.Dark);
        }

        /// <summary>
        /// Applies a custom theme with error handling and fallback.
        /// </summary>
        public void ApplyTheme(ThemeColors colors)
        {
            try
            {
                if (colors == null)
                {
                    LogThemeError("Null theme colors provided", "Falling back to last successful theme.");
                    ApplyFallbackTheme();
                    return;
                }

                var resources = _app.Resources;

                // Validate colors before applying
                if (!ValidateThemeColors(colors))
                {
                    LogThemeError("Invalid theme colors", "One or more theme colors are invalid. Falling back to last successful theme.");
                    ApplyFallbackTheme();
                    return;
                }

                // ======== MAIN COLOR ========
                // Main Gradient (primary background)
                var mainGradient = CreateGradient(colors.MainStart, colors.MainEnd, 0.7);
                resources["MainColor"] = mainGradient;

                // Solid variants based on average color
                var mainAvgColor = GetSafeAverageColor(mainGradient, DEFAULT_MAIN_COLOR);
                resources["MainColorSolid"] = mainAvgColor;
                resources["MainColorDarker"] = mainAvgColor.Darken(DARKER_FACTOR);
                resources["MainColorDarkest"] = mainAvgColor.Darken(DARKEST_FACTOR);
                resources["MainColorLighter"] = mainAvgColor.Lighten(LIGHTER_FACTOR);
                resources["MainColorLightest"] = mainAvgColor.Lighten(LIGHTEST_FACTOR);

                // Gradient variants
                resources["MainColorDarkerGradient"] = SafeDarken(mainGradient, DARKER_FACTOR);
                resources["MainColorLighterGradient"] = SafeLighten(mainGradient, LIGHTER_FACTOR);

                // ======== SECONDARY COLOR ========
                // Secondary Gradient (panels, cards)
                var secondaryGradient = CreateGradient(
                    colors.SecondaryStart,
                    colors.SecondaryEnd,
                    0.7,
                    new RelativePoint(0, 0, RelativeUnit.Relative),
                    new RelativePoint(1, 0.7, RelativeUnit.Relative));
                resources["SecondaryColor"] = secondaryGradient;

                // Solid variants
                var secondaryAvgColor = GetSafeAverageColor(secondaryGradient, DEFAULT_SECONDARY_COLOR);
                resources["SecondaryColorSolid"] = secondaryAvgColor;
                resources["SecondaryColorDarker"] = secondaryAvgColor.Darken(DARKER_FACTOR);
                resources["SecondaryColorDarkest"] = secondaryAvgColor.Darken(DARKEST_FACTOR);
                resources["SecondaryColorLighter"] = secondaryAvgColor.Lighten(LIGHTER_FACTOR);
                resources["SecondaryColorLightest"] = secondaryAvgColor.Lighten(LIGHTEST_FACTOR);

                // Gradient variants
                resources["SecondaryColorDarkerGradient"] = SafeDarken(secondaryGradient, DARKER_FACTOR);
                resources["SecondaryColorLighterGradient"] = SafeLighten(secondaryGradient, LIGHTER_FACTOR);

                // ======== ACCENT COLOR ========
                // Accent Gradient (interactive elements)
                var accentGradient = CreateGradient(colors.AccentStart, colors.AccentEnd);
                resources["AccentColor"] = accentGradient;

                // Solid variants
                var accentAvgColor = GetSafeAverageColor(accentGradient, DEFAULT_ACCENT_COLOR);
                resources["AccentColorSolid"] = accentAvgColor;
                resources["AccentColorDarker"] = accentAvgColor.Darken(DARKER_FACTOR);
                resources["AccentColorDarkest"] = accentAvgColor.Darken(DARKEST_FACTOR);
                resources["AccentColorLighter"] = accentAvgColor.Lighten(LIGHTER_FACTOR);
                resources["AccentColorLightest"] = accentAvgColor.Lighten(LIGHTEST_FACTOR);

                // Gradient variants
                resources["AccentColorDarkerGradient"] = SafeDarken(accentGradient, DARKER_FACTOR);
                resources["AccentColorLighterGradient"] = SafeLighten(accentGradient, LIGHTER_FACTOR);

                // ======== TEXT COLOR ========
                // Text Gradient
                var textGradient = CreateGradient(colors.TextStart, colors.TextEnd);
                resources["TextColor"] = textGradient;

                // Solid variants
                var textAvgColor = GetSafeAverageColor(textGradient, DEFAULT_TEXT_COLOR);
                resources["TextColorSolid"] = textAvgColor;
                resources["TextColorDarker"] = textAvgColor.Darken(DARKER_FACTOR);
                resources["TextColorDarkest"] = textAvgColor.Darken(DARKEST_FACTOR);
                resources["TextColorLighter"] = textAvgColor.Lighten(LIGHTER_FACTOR);
                resources["TextColorLightest"] = textAvgColor.Lighten(LIGHTEST_FACTOR);

                // Gradient variants
                resources["TextColorDarkerGradient"] = SafeDarken(textGradient, DARKER_FACTOR);
                resources["TextColorLighterGradient"] = SafeLighten(textGradient, LIGHTER_FACTOR);

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

                // Store as last successful theme
                _lastSuccessfulTheme = new ThemeColors
                {
                    MainStart = colors.MainStart,
                    MainEnd = colors.MainEnd,
                    SecondaryStart = colors.SecondaryStart,
                    SecondaryEnd = colors.SecondaryEnd,
                    AccentStart = colors.AccentStart,
                    AccentEnd = colors.AccentEnd,
                    TextStart = colors.TextStart,
                    TextEnd = colors.TextEnd
                };

                // Notify that theme was applied successfully
                NotifyThemeApplied();
            }
            catch (Exception ex)
            {
                LogThemeError(
                    "Failed to apply custom theme",
                    "An exception occurred while applying the custom theme. Falling back to default theme.",
                    ex);
                ApplyFallbackTheme();
            }
        }

        /// <summary>
        /// Applies a preset theme with error handling and fallback.
        /// </summary>
        public void ApplyPresetTheme(PresetTheme theme)
        {
            try
            {
                var colors = GetPresetThemeColors(theme);
                ApplyTheme(colors);

                // Notify that a preset theme was applied
                if (_messenger != null)
                {
                    _messenger.Send(new ThemeUpdatedMessage(new ThemeConfiguration
                    {
                        ThemeType = theme
                    }));
                }
            }
            catch (Exception ex)
            {
                LogThemeError(
                    $"Failed to apply preset theme {theme}",
                    "An exception occurred while applying a preset theme. Falling back to Dark theme.",
                    ex);
                ApplyFallbackTheme();
            }
        }

        /// <summary>
        /// Gets the colors for a preset theme with error handling.
        /// </summary>
        private ThemeColors GetPresetThemeColors(PresetTheme theme)
        {
            try
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
                        // Sunset theme
                        MainStart = Color.Parse("#800080"),
                        MainEnd = Color.Parse("#FFA500"),
                        SecondaryStart = Color.Parse("#FF0453"),
                        SecondaryEnd = Color.Parse("#FF9E00"),
                        AccentStart = Color.Parse("#FFFB00"),
                        AccentEnd = Color.Parse("#09FFF4"),
                        TextStart = Color.Parse("#FFFB00"),
                        TextEnd = Color.Parse("#FFF000")
                    },
                    PresetTheme.Custom => ThemeConfiguration.GetDefaultCustomTheme().ToThemeColors(),
                    _ => new ThemeColors
                    {
                        // Fallback to dark theme
                        MainStart = Color.Parse("#1A1A1A"),
                        MainEnd = Color.Parse("#262626"),
                        SecondaryStart = Color.Parse("#333333"),
                        SecondaryEnd = Color.Parse("#444444"),
                        AccentStart = Color.Parse("#38BDF8"),
                        AccentEnd = Color.Parse("#34D399"),
                        TextStart = Color.Parse("#BEE5F7"),
                        TextEnd = Color.Parse("#A9D4C3")
                    }
                };
            }
            catch (Exception ex)
            {
                LogThemeError(
                    $"Error creating preset theme colors for {theme}",
                    "Falling back to default color values.",
                    ex);

                // Return safe default theme colors
                return new ThemeColors
                {
                    MainStart = DEFAULT_MAIN_COLOR,
                    MainEnd = DEFAULT_MAIN_COLOR,
                    SecondaryStart = DEFAULT_SECONDARY_COLOR,
                    SecondaryEnd = DEFAULT_SECONDARY_COLOR,
                    AccentStart = DEFAULT_ACCENT_COLOR,
                    AccentEnd = DEFAULT_ACCENT_COLOR,
                    TextStart = DEFAULT_TEXT_COLOR,
                    TextEnd = DEFAULT_TEXT_COLOR
                };
            }
        }

        /// <summary>
        /// Applies the fallback theme in case of errors.
        /// </summary>
        private void ApplyFallbackTheme()
        {
            try
            {
                // Try to use last successful theme first
                if (_lastSuccessfulTheme != null)
                {
                    ApplyTheme(_lastSuccessfulTheme);
                    return;
                }

                // If that fails, use hardcoded dark theme
                var darkTheme = GetPresetThemeColors(PresetTheme.Dark);
                var resources = _app.Resources;

                // Set a minimal set of critical resources directly
                resources["MainColor"] = new SolidColorBrush(DEFAULT_MAIN_COLOR);
                resources["MainColorSolid"] = new SolidColorBrush(DEFAULT_MAIN_COLOR);
                resources["SecondaryColor"] = new SolidColorBrush(DEFAULT_SECONDARY_COLOR);
                resources["SecondaryColorSolid"] = new SolidColorBrush(DEFAULT_SECONDARY_COLOR);
                resources["AccentColor"] = new SolidColorBrush(DEFAULT_ACCENT_COLOR);
                resources["AccentColorSolid"] = new SolidColorBrush(DEFAULT_ACCENT_COLOR);
                resources["TextColor"] = new SolidColorBrush(DEFAULT_TEXT_COLOR);
                resources["TextColorSolid"] = new SolidColorBrush(DEFAULT_TEXT_COLOR);
                resources["ErrorColor"] = new SolidColorBrush(Color.Parse("#FF4444"));
                resources["WarningColor"] = new SolidColorBrush(Color.Parse("#FFBB33"));
                resources["SuccessColor"] = new SolidColorBrush(Color.Parse("#00C851"));
            }
            catch (Exception ex)
            {
                // Last-resort error handling - just log the error, can't do much else
                if (_errorHandlingService != null)
                {
                    _errorHandlingService.LogError(
                        ErrorSeverity.Critical,
                        "Fatal theme error",
                        "Failed to apply fallback theme. UI may appear broken.",
                        ex,
                        true);
                }
            }
        }

        /// <summary>
        /// Creates a linear gradient with error handling.
        /// </summary>
        private LinearGradientBrush CreateGradient(
            Color startColor,
            Color endColor,
            double endPoint = 1.0,
            RelativePoint? startPoint = null,
            RelativePoint? endPointRelative = null)
        {
            try
            {
                var gradient = new LinearGradientBrush
                {
                    StartPoint = startPoint ?? new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = endPointRelative ?? new RelativePoint(1, 1, RelativeUnit.Relative)
                };

                gradient.GradientStops.Add(new GradientStop(startColor, 0));
                gradient.GradientStops.Add(new GradientStop(endColor, endPoint));

                return gradient;
            }
            catch (Exception)
            {
                // Return a safe solid color brush if gradient creation fails
                return new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = new GradientStops
                    {
                        new GradientStop(DEFAULT_MAIN_COLOR, 0),
                        new GradientStop(DEFAULT_MAIN_COLOR, 1)
                    }
                };
            }
        }

        /// <summary>
        /// Gets the average color from a gradient brush with error handling.
        /// </summary>
        private Color GetSafeAverageColor(LinearGradientBrush gradient, Color defaultColor)
        {
            try
            {
                return gradient.GetAverageColor();
            }
            catch (Exception)
            {
                return defaultColor;
            }
        }

        /// <summary>
        /// Darkens a gradient brush with error handling.
        /// </summary>
        private LinearGradientBrush SafeDarken(LinearGradientBrush gradient, double factor)
        {
            try
            {
                return gradient.Darken(factor);
            }
            catch (Exception)
            {
                return gradient;
            }
        }

        /// <summary>
        /// Lightens a gradient brush with error handling.
        /// </summary>
        private LinearGradientBrush SafeLighten(LinearGradientBrush gradient, double factor)
        {
            try
            {
                return gradient.Lighten(factor);
            }
            catch (Exception)
            {
                return gradient;
            }
        }

        /// <summary>
        /// Validates theme colors to ensure they are usable.
        /// </summary>
        private bool ValidateThemeColors(ThemeColors colors)
        {
            try
            {
                // Check that all colors are valid and not transparent
                // This helps prevent UI rendering issues
                return
                    IsValidColor(colors.MainStart) &&
                    IsValidColor(colors.MainEnd) &&
                    IsValidColor(colors.SecondaryStart) &&
                    IsValidColor(colors.SecondaryEnd) &&
                    IsValidColor(colors.AccentStart) &&
                    IsValidColor(colors.AccentEnd) &&
                    IsValidColor(colors.TextStart) &&
                    IsValidColor(colors.TextEnd);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a color is valid for use in the theme.
        /// </summary>
        private bool IsValidColor(Color color)
        {
            // Ensure color has some opacity to be visible
            return color.A > 0;
        }

        /// <summary>
        /// Logs theme-related errors with appropriate severity.
        /// </summary>
        private void LogThemeError(string message, string details, Exception ex = null)
        {
            if (_errorHandlingService != null)
            {
                _errorHandlingService.LogError(
                    ErrorSeverity.NonCritical,
                    message,
                    details,
                    ex,
                    true);
            }
        }

        /// <summary>
        /// Notifies that a theme was successfully applied.
        /// </summary>
        private void NotifyThemeApplied()
        {
            // No need to do anything if there's no messenger
            if (_messenger == null)
                return;

            try
            {
                // Send a notification that theme was updated
                _messenger.Send(new ThemeAppliedMessage());
            }
            catch (Exception ex)
            {
                // Log but don't crash if notification fails
                LogThemeError(
                    "Failed to send theme applied notification",
                    "The theme was applied successfully but notification to other components failed.",
                    ex);
            }
        }
    }

    /// <summary>
    /// Message sent when a theme is successfully applied.
    /// </summary>
    public class ThemeAppliedMessage
    {
        public DateTime AppliedTime { get; } = DateTime.Now;
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