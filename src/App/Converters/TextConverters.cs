using System.Collections.Generic;
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

/// <summary>
/// Splits a free-text prerequisite description ("PRO192, MAD101" or
/// "MGT101 or MKG101") into individual course-code tags for pill rendering.
///
/// The subject list stores this as one string - see Course.PrerequisiteText -
/// so the tag split happens on display rather than in the data model.
/// </summary>
public sealed class PrerequisiteTagsConverter : IValueConverter
{
    private static readonly char[] Separators = [',', ';', '/'];

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        return text
            .Replace(" or ", ",", StringComparison.OrdinalIgnoreCase)
            .Replace(" and ", ",", StringComparison.OrdinalIgnoreCase)
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException("PrerequisiteTagsConverter is one-way.");
}

/// <summary>
/// Looks up a course's major codes for the "Ngành Áp Dụng" column: values[0] is
/// the row's CourseId, values[1] is SubjectAdminViewModel.MajorCodesByCourseId.
///
/// A MultiBinding rather than a plain converter because a DataGrid cell has no
/// other way to reach a dictionary that lives on the ViewModel, not the row.
/// </summary>
public sealed class CourseMajorCodesConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is [int courseId, IDictionary<int, string> map] && map.TryGetValue(courseId, out var display))
        {
            return display;
        }

        return "-";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException("CourseMajorCodesConverter is one-way.");
}
