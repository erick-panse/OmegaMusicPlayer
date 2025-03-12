using Avalonia.Data.Converters;
using Microsoft.Extensions.DependencyInjection;
using OmegaPlayer.Infrastructure.Services;
using OmegaPlayer.UI;
using System;
using System.Globalization;

namespace OmegaPlayer.UI.Converters
{
    public class ThemeNameConverter : IValueConverter
    {
        private LocalizationService _localizationService;

        private LocalizationService LocalizationService =>
            _localizationService ??= App.ServiceProvider.GetRequiredService<LocalizationService>();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            // Get the theme name (e.g., "Light", "Dark", "Custom")
            string themeName = value.ToString();

            // Map theme names to localization keys
            string key = themeName switch
            {
                "Light" => "ThemeLight",
                "Dark" => "ThemeDark",
                "Custom" => "ThemeCustom",
                _ => themeName
            };

            // Get the localized value
            string localizedName = LocalizationService[key];

            // If key wasn't found in translations, it returns the key itself
            // So we check if they're identical and use the original theme name as fallback
            return localizedName != key ? localizedName : themeName;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // This shouldn't be needed for this scenario, but implement if necessary
            throw new NotImplementedException();
        }
    }
}