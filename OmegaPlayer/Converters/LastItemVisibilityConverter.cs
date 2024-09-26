
using Avalonia.Data.Converters;
using OmegaPlayer.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OmegaPlayer.Converters
{
    public class LastItemVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var currentArtist = value as Artists;
            var allArtists = parameter as IEnumerable<Artists>;  // Expecting a list of artists

            if (allArtists != null && currentArtist != null)
            {
                return allArtists.Last() != currentArtist;  // Hide if this is the last artist
            }

            // Default to true if something goes wrong
            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
