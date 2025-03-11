using Avalonia.Data.Converters;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Infrastructure.Services;
using System;
using System.Globalization;
using System.Diagnostics;

namespace OmegaPlayer.UI.Converters
{
    /// <summary>
    /// Converter that formats a value using a localized format string.
    /// </summary>
    public class LocalizedFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null)
                    return string.Empty;

                // Get the format key from the parameter
                string formatKey = parameter?.ToString();
                if (string.IsNullOrEmpty(formatKey))
                {
                    Debug.WriteLine("Warning: No format key provided to LocalizedFormatConverter");
                    return value.ToString();
                }

                // Get the localization service
                var localizationService = App.ServiceProvider.GetRequiredService<LocalizationService>();

                // Get the format string from the localization service
                string formatString = localizationService[formatKey];

                // If format string not found, fall back to the key itself
                if (formatString == formatKey)
                {
                    Debug.WriteLine($"Warning: No localized format found for key: {formatKey}");
                    formatString = "{0} " + formatKey;
                }

                // Apply the format string to the value
                return string.Format(formatString, value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in LocalizedFormatConverter: {ex.Message}");
                return $"{value} ({parameter})";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}