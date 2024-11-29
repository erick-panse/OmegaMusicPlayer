using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OmegaPlayer.UI.Converters
{
    public class BooleanToFontSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCollapsed && parameter is string sizeParams)
            {
                var sizes = sizeParams.Split(',');
                if (sizes.Length == 2)
                {
                    if (double.TryParse(sizes[0], out double expandedSize) &&
                        double.TryParse(sizes[1], out double collapsedSize))
                    {
                        return isCollapsed ? collapsedSize : expandedSize;
                    }
                }
            }
            return 16; // Default font size
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}