using System.Globalization;
using System.Windows.Data;
using System;

namespace IgnaviorLauncher.ViewModels;

public class ScaleMultiplier : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double scale && parameter is string multiplier && double.TryParse(multiplier, out double factor))
        {
            return scale * factor;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}