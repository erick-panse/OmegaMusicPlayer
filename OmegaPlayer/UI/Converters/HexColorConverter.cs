using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OmegaPlayer.UI.Converters
{
    /// <summary>
    /// Converts between color formats
    /// - Converts from 8-character hex (#FFRRGGBB) to 6-character hex (#RRGGBB)
    /// - Converts from 6-character hex (#RRGGBB) back to 8-character hex (#FFRRGGBB) if needed
    /// </summary>
    public class HexColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hexColor)
            {
                // If it's already in #RRGGBB format, return as is
                if (hexColor.Length == 7) // # + 6 characters
                {
                    return hexColor.ToUpper();
                }

                // If it's in #AARRGGBB format, convert to #RRGGBB
                if (hexColor.Length == 9) // # + 8 characters
                {
                    return "#" + hexColor.Substring(3).ToUpper(); // Skip the # and the AA part
                }
            }

            // Return a default color if value is not in expected format
            return "#FFFFFF";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string hexColor && hexColor.Length == 7) // # + 6 characters
            {
                // Convert #RRGGBB to #FFRRGGBB (fully opaque)
                return "#FF" + hexColor.Substring(1).ToUpper();
            }

            // Return the original value if it's not in expected format
            return value;
        }
    }
}