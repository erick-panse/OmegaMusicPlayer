using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace OmegaPlayer.UI.Converters
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
            if (value is string hexColor)
            {
                try
                {
                    // Parse the string color into a Color object
                    return Color.Parse(hexColor);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error converting color string to Color: {ex.Message}");
                    return Colors.White; // Default if parse fails
                }
            }

            // If not a string, return a default color
            return Colors.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                try
                {
                    // Convert Color back to string in the expected format
                    return color.ToString().ToUpper();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error converting Color to string: {ex.Message}");
                    return "#FFFFFFFF"; // Default if conversion fails
                }
            }

            // Return default if not a Color
            return "#FFFFFFFF";
        }
    }
}