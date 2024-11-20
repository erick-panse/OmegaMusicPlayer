using Avalonia.Data.Converters;
using OmegaPlayer.Features.Library.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OmegaPlayer.UI.Converters
{
    public class ArtistsConcatenationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<Artists> artists)
            {
                return string.Join(", ", artists.Select(a => a.ArtistName));
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
