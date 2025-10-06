using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

using System;
using System.Linq;

namespace WinUiCore.ValueConverters
{
    public class NumericalTextBox
    {
        public static bool GetEnableNumberOnly(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableNumberOnlyProperty);
        }

        public static void SetEnableNumberOnly(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableNumberOnlyProperty, value);
        }

        public static readonly DependencyProperty EnableNumberOnlyProperty =
            DependencyProperty.RegisterAttached(
                "EnableNumberOnly",
                typeof(bool),
                typeof(NumericalTextBox),
                new PropertyMetadata(false, OnEnableNumberOnlyChanged));

        private static void OnEnableNumberOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is TextBox textBox)
            {
                bool isEnabled = (bool)e.NewValue;

                if (isEnabled)
                {
                    textBox.TextChanging += NumberTextBoxWithSingleDot;
                } else
                {
                    textBox.TextChanging -= NumberTextBoxWithSingleDot;

                    // ✅ Clean invalid characters when disabling
                    textBox.Text = CleanInvalidCharacters(textBox.Text);
                }
            }
        }

        private static void NumberTextBoxWithSingleDot(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            int caretIndex = sender.SelectionStart;
            string filteredText = CleanInvalidCharacters(sender.Text);

            if (sender.Text != filteredText)
            {
                sender.Text = filteredText;
                sender.SelectionStart = caretIndex > filteredText.Length ? filteredText.Length : caretIndex;
            }
        }

        private static string CleanInvalidCharacters(string input)
        {
            char[] englishNums = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.' };

            string result = new string(input.Where(c => englishNums.Contains(c)).ToArray());

            int firstDotIndex = result.IndexOf('.');
            if (firstDotIndex != -1)
            {
                result = result.Substring(0, firstDotIndex + 1) + result.Substring(firstDotIndex + 1).Replace(".", "");
            }

            return result;
        }
    }
}