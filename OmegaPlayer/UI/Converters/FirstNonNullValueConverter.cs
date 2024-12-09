using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OmegaPlayer.UI.Converters
{
    public class FirstNonNullValueConverter : IMultiValueConverter
    {
        public static readonly FirstNonNullValueConverter Instance = new();

        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            return values.FirstOrDefault(v => v != null);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
