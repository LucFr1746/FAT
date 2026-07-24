using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace App.Converters;

/// <summary>
/// Shows an element only when a string actually has content.
///
/// BooleanToVisibilityConverter cannot do this: handed a string it returns
/// Visible unconditionally, so an optional field ends up rendering a blank row
/// and its label whether or not there is anything to show.
/// </summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("StringToVisibilityConverter is one-way.");
}

/// <summary>
/// Shows an element only when a count is greater than zero.
///
/// Same reason as above: binding Visibility straight to a Count would leave the
/// element permanently visible.
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("CountToVisibilityConverter is one-way.");
}
