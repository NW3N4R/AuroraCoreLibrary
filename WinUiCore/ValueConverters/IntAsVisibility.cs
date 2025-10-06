using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

using System;

namespace WinUiCore.ValueConverters
{
    internal class IntAsVisibility : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is int intValue)
            {
                return intValue == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is int intValue)
            {
                return intValue != 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }
    }
}
