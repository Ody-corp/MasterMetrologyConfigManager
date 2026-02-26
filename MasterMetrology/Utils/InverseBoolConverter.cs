using System;
using System.Globalization;
using System.Windows.Data;

namespace MasterMetrology.Utils
{
    public sealed class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? b : Binding.DoNothing;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? b : Binding.DoNothing;
    }
}