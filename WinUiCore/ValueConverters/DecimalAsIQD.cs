using Microsoft.UI.Xaml.Data;

using System;

namespace WinUiCore.ValueConverters
{
    public class DecimalAsIQD : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is IConvertible)
            {
                decimal decimalValue = System.Convert.ToDecimal(value);
                return decimalValue.ToString("N0") + " د.ع";
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string stringValue && decimal.TryParse(stringValue, out decimal result))
            {
                return result;
            }
            return value;
        }
    }
}
