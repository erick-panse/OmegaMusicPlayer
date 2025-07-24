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

        // Add recursion guard flags
        private bool _isApplyingTheme = false;
        private bool _isApplyingFallback = false;
        private int _recoveryAttempts = 0;
        private const int MAX_RECOVERY_ATTEMPTS = 2;

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
            // Guard against recursive calls
            if (_isApplyingTheme)
            {
                // If we're already applying a theme and get called again, it's likely a recursion issue
                // Log but don't throw to avoid stack overflow
                _errorHandlingService?.LogError(
                    ErrorSeverity.NonCritical,
                    "Recursive theme application detected",
                    "Prevented potential stack overflow in theme service",
                    null,
                    false);
                return;
            }

            try
            {
                _isApplyingTheme = true;
                _recoveryAttempts = 0;

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

                // Only call ApplyFallbackTheme if we haven't exceeded maximum recovery attempts
                if (_recoveryAttempts < MAX_RECOVERY_ATTEMPTS)
                {
                    _recoveryAttempts++;
                    ApplyFallbackTheme();
                }
                else
                {
                    LogThemeError(
                        "Maximum theme recovery attempts exceeded",
                        "Unable to apply a valid theme after multiple attempts. UI may appear broken.",
                        null);
                }
            }
            finally
            {
                _isApplyingTheme = false;
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

                // Only call ApplyFallbackTheme if we haven't exceeded maximum recovery attempts
                if (_recoveryAttempts < MAX_RECOVERY_ATTEMPTS)
                {
                    _recoveryAttempts++;
                    ApplyFallbackTheme();
                }
                else
                {
                    LogThemeError(
                        "Maximum theme recovery attempts exceeded",
                        "Unable to apply a valid theme after multiple attempts. UI may appear broken.",
                        null);
                }
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
                        SecondaryStart = Color.Parse("#312786"),
                        SecondaryEnd = Color.Parse("#433C8B"),
                        AccentStart = Color.Parse("#3403FF"),
                        AccentEnd = Color.Parse("#FF0073"),
                        TextStart = Color.Parse("#F904D2"),
                        TextEnd = Color.Parse("#033EF9")
                    },
                    PresetTheme.Sunset => new ThemeColors
                    {
                        // Sunset theme
                        MainStart = Color.Parse("#D28A5C"),
                        MainEnd = Color.Parse("#FF9DA1"),
                        SecondaryStart = Color.Parse("#FFA9F1"),
                        SecondaryEnd = Color.Parse("#FF9862"),
                        AccentStart = Color.Parse("#85FED5"),
                        AccentEnd = Color.Parse("#D6FF82"),
                        TextStart = Color.Parse("#64FF95"),
                        TextEnd = Color.Parse("#7BFFC3")
                    },
                    PresetTheme.Crimson => new ThemeColors
                    {
                        // Crimson theme
                        MainStart = Color.Parse("#0A0203"),
                        MainEnd = Color.Parse("#0D0404"),
                        SecondaryStart = Color.Parse("#160D0E"),
                        SecondaryEnd = Color.Parse("#7B3637"),
                        AccentStart = Color.Parse("#FF1543"),
                        AccentEnd = Color.Parse("#FF3638"),
                        TextStart = Color.Parse("#FF6B6E"),
                        TextEnd = Color.Parse("#FF7F8D")
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
            // Guard against recursive calls to ApplyFallbackTheme
            if (_isApplyingFallback)
            {
                LogThemeError(
                    "Recursive fallback theme application detected",
                    "Prevented potential stack overflow in theme fallback mechanism",
                    null);
                return;
            }

            try
            {
                _isApplyingFallback = true;

                // Try to use last successful theme first
                if (_lastSuccessfulTheme != null)
                {
                    ApplyTheme(_lastSuccessfulTheme);
                    _isApplyingFallback = false;
                    return;
                }

                // Apply resources directly instead of calling ApplyTheme again
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

                // Add the rest of derived colors with safe defaults
                resources["MainColorDarker"] = new SolidColorBrush(DEFAULT_MAIN_COLOR.Darken(DARKER_FACTOR));
                resources["MainColorDarkest"] = new SolidColorBrush(DEFAULT_MAIN_COLOR.Darken(DARKEST_FACTOR));
                resources["MainColorLighter"] = new SolidColorBrush(DEFAULT_MAIN_COLOR.Lighten(LIGHTER_FACTOR));
                resources["MainColorLightest"] = new SolidColorBrush(DEFAULT_MAIN_COLOR.Lighten(LIGHTEST_FACTOR));

                resources["SecondaryColorDarker"] = new SolidColorBrush(DEFAULT_SECONDARY_COLOR.Darken(DARKER_FACTOR));
                resources["SecondaryColorDarkest"] = new SolidColorBrush(DEFAULT_SECONDARY_COLOR.Darken(DARKEST_FACTOR));
                resources["SecondaryColorLighter"] = new SolidColorBrush(DEFAULT_SECONDARY_COLOR.Lighten(LIGHTER_FACTOR));
                resources["SecondaryColorLightest"] = new SolidColorBrush(DEFAULT_SECONDARY_COLOR.Lighten(LIGHTEST_FACTOR));

                resources["AccentColorDarker"] = new SolidColorBrush(DEFAULT_ACCENT_COLOR.Darken(DARKER_FACTOR));
                resources["AccentColorDarkest"] = new SolidColorBrush(DEFAULT_ACCENT_COLOR.Darken(DARKEST_FACTOR));
                resources["AccentColorLighter"] = new SolidColorBrush(DEFAULT_ACCENT_COLOR.Lighten(LIGHTER_FACTOR));
                resources["AccentColorLightest"] = new SolidColorBrush(DEFAULT_ACCENT_COLOR.Lighten(LIGHTEST_FACTOR));

                resources["TextColorDarker"] = new SolidColorBrush(DEFAULT_TEXT_COLOR.Darken(DARKER_FACTOR));
                resources["TextColorDarkest"] = new SolidColorBrush(DEFAULT_TEXT_COLOR.Darken(DARKEST_FACTOR));
                resources["TextColorLighter"] = new SolidColorBrush(DEFAULT_TEXT_COLOR.Lighten(LIGHTER_FACTOR));
                resources["TextColorLightest"] = new SolidColorBrush(DEFAULT_TEXT_COLOR.Lighten(LIGHTEST_FACTOR));

                // UI State colors
                resources["ActiveElementBackground"] = resources["AccentColor"];
                resources["HoverElementBackground"] = resources["SecondaryColorLighter"];
                resources["PressedElementBackground"] = resources["AccentColorDarker"];
                resources["DisabledElementBackground"] = resources["MainColorDarker"];
                resources["DisabledElementForeground"] = new SolidColorBrush(DEFAULT_TEXT_COLOR.Darken(0.5));

                // Gradient variants (use solid brushes as a fallback)
                resources["MainColorDarkerGradient"] = resources["MainColorDarker"];
                resources["MainColorLighterGradient"] = resources["MainColorLighter"];
                resources["SecondaryColorDarkerGradient"] = resources["SecondaryColorDarker"];
                resources["SecondaryColorLighterGradient"] = resources["SecondaryColorLighter"];
                resources["AccentColorDarkerGradient"] = resources["AccentColorDarker"];
                resources["AccentColorLighterGradient"] = resources["AccentColorLighter"];
                resources["TextColorDarkerGradient"] = resources["TextColorDarker"];
                resources["TextColorLighterGradient"] = resources["TextColorLighter"];
            }
            catch (Exception ex)
            {
                // Last-resort error handling - just log the error, can't do much else
                LogThemeError(
                    "Fatal theme error",
                    "Failed to apply fallback theme. UI may appear broken.",
                    ex);
            }
            finally
            {
                _isApplyingFallback = false;
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
            catch (Exception ex)
            {
                LogThemeError(
                    "Error creating gradient brush",
                    "Failed to create gradient. Using solid color instead.",
                    ex);

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
            catch (Exception ex)
            {
                LogThemeError(
                    "Error calculating average color",
                    "Failed to calculate average color from gradient. Using default color instead.",
                    ex);
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
            catch (Exception ex)
            {
                LogThemeError(
                    "Error darkening gradient",
                    "Failed to darken gradient. Using original gradient instead.",
                    ex);
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
            catch (Exception ex)
            {
                LogThemeError(
                    "Error lightening gradient",
                    "Failed to lighten gradient. Using original gradient instead.",
                    ex);
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
            catch (Exception ex)
            {
                LogThemeError(
                    "Error validating theme colors",
                    "An exception occurred while validating theme colors.",
                    ex);
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
                // Use NonCritical severity to avoid triggering the error recovery system which could cause recursive theme operations
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
        Crimson,
        Custom
    }
}