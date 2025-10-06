using Microsoft.UI.Xaml.Data;

using System;

namespace WinUiCore.ValueConverters
{
    public class BoolToYesNo : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool b)
            {
                return b ? "بەڵێ" : "نەخێر";
            }
            return "نەزانراو";
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string s)
            {
                return s == "بەڵێ";
            }
            return false;
        }
    }
}
