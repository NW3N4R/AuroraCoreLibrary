using Microsoft.UI.Xaml.Data;

using System;

namespace WinUiCore.ValueConverters
{
    public class IntAsString : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return int.TryParse(value?.ToString(), out var i) ? i : 0;
        }
    }
}
