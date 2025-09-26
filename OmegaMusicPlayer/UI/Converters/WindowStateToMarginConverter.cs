using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace OmegaMusicPlayer.UI.Converters
{
    public class WindowStateToMarginConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is WindowState windowState)
            {
                if (windowState == WindowState.Maximized)
                {
                    // If a parameter is provided, use it to determine the margin type
                    if (parameter is string param)
                    {
                        switch (param)
                        {
                            case "titleBar":
                                return new Thickness(8, 8, 8, 0);
                            case "navigation":
                                return new Thickness(8, 5, 8, 0);
                            case "content":
                                return new Thickness(8, 0, 8, 8);
                            case "track":
                                return new Thickness(8, 8, 8, 8);
                            default:
                                return new Thickness(0);
                        }
                    }
                }
                return new Thickness(0);
            }
            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}