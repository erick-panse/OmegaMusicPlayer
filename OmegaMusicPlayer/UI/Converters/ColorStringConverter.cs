using Avalonia.Data.Converters;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;
using OmegaMusicPlayer.Core.Enums;
using OmegaMusicPlayer.Core.Interfaces;
using System;
using System.Globalization;

namespace OmegaMusicPlayer.UI.Converters
{
    /// <summary>
    /// Converts between string hex color representation and Avalonia.Media.Color objects
    /// - Handles conversion between different color formats for ColorPicker compatibility
    /// - Converts from "#FFRRGGBB" string format to Color object for ColorPicker
    /// - Converts from Color object back to "#FFRRGGBB" string format for storage
    /// </summary>
    public class ColorStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string hexColor)
                {
                    // Parse the string color into a Color object
                    return Color.Parse(hexColor);
                }

                // If not a string, return a default color
                return Colors.White;
            }
            catch (Exception ex)
            {
                var errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();
                if (errorHandlingService != null)
                {
                    errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Converting color string to Color",
                        ex.Message,
                        ex,
                        false);
                }
                return Colors.White; // Default if parse fails
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is Color color)
                {
                    // Convert Color back to string in the expected format
                    return color.ToString().ToUpper();
                }

                // Return default if not a Color
                return "#FFFFFFFF";
            }
            catch (Exception ex)
            {
                var errorHandlingService = App.ServiceProvider.GetService<IErrorHandlingService>();
                if (errorHandlingService != null)
                {
                    errorHandlingService.LogError(
                        ErrorSeverity.NonCritical,
                        "Converting Color to string",
                        ex.Message,
                        ex,
                        false);
                }
                return "#FFFFFFFF"; // Default if conversion fails
            }
        }
    }

}