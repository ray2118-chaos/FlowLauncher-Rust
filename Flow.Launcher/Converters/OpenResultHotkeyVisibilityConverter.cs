using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class OpenResultHotkeyVisibilityConverter : IValueConverter, IMultiValueConverter
{
    private const int MaxVisibleHotkeys = 5;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var number = int.MaxValue;

        if (value is ListBoxItem listBoxItem
            && ItemsControl.ItemsControlFromItemContainer(listBoxItem) is ListBox listBox)
            number = listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem) + 1;

        return (number >= 1 && number <= MaxVisibleHotkeys) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new InvalidOperationException();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2)
            return Visibility.Collapsed;

        if (values[0] is not ListBoxItem listBoxItem
            || ItemsControl.ItemsControlFromItemContainer(listBoxItem) is not ListBox listBox)
        {
            return Visibility.Collapsed;
        }

        int topIndex = 0;
        if (values[1] is int ti)
            topIndex = ti;
        else if (values[1] != null && int.TryParse(values[1].ToString(), out var parsed))
            topIndex = parsed;

        var itemIndex = listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);
        if (itemIndex < 0)
            return Visibility.Collapsed;

        var relativeIndex = itemIndex - topIndex;
        return (relativeIndex >= 0 && relativeIndex < MaxVisibleHotkeys) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new InvalidOperationException();
}
