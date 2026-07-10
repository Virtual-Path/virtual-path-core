using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace VirtualPathCore.Helpers;

public class BoolConverters
{
    public static readonly NegateConverter Not = new();

    public class NegateConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }
    }
}
