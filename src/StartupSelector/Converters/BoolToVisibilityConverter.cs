using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StartupSelector.Converters
{
    /// <summary>true → Visible, false/null → Collapsed. Set <see cref="Invert"/> to reverse.
    /// Non-bool values count as true when they are non-null (and non-empty for strings).</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var flag = value switch
            {
                bool b => b,
                string s => s.Length > 0,
                null => false,
                _ => true,
            };
            return flag ^ Invert ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is Visibility visibility && (visibility == Visibility.Visible) ^ Invert;
    }
}
