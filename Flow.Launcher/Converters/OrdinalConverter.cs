using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace Flow.Launcher.Converters;

public class OrdinalConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ListBoxItem listBoxItem
            || ItemsControl.ItemsControlFromItemContainer(listBoxItem) is not ListBox listBox)
        {
            return 0;
        }

        var res = listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem) + 1;
        return res == 10 ? 0 : res;  // 10th item => HOTKEY+0
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new InvalidOperationException();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length == 0)
            return string.Empty;

        string modifiers = "Alt";
        ListBoxItem listBoxItem = null;
        int topIndex = 0;

        if (values.Length >= 3)
        {
            if (values[0] is string mod && !string.IsNullOrEmpty(mod))
                modifiers = mod;

            listBoxItem = values[1] as ListBoxItem;

            if (values[2] is int ti)
                topIndex = ti;
            else if (values[2] != null && int.TryParse(values[2].ToString(), out var parsed))
                topIndex = parsed;
        }
        else if (values.Length == 2)
        {
            listBoxItem = values[0] as ListBoxItem;

            if (values[1] is int ti)
                topIndex = ti;
            else if (values[1] != null && int.TryParse(values[1].ToString(), out var parsed))
                topIndex = parsed;
        }
        else if (values.Length == 1)
        {
            listBoxItem = values[0] as ListBoxItem;
        }

        if (listBoxItem == null || ItemsControl.ItemsControlFromItemContainer(listBoxItem) is not ListBox listBox)
            return string.Empty;

        var itemIndex = listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);
        if (itemIndex < 0)
            return string.Empty;

        var relativeIndex = itemIndex - topIndex;
        var res = relativeIndex + 1;
        var digit = res == 10 ? 0 : res;

        if (values.Length >= 3)
            return $"{modifiers}+{digit}";

        return digit;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new InvalidOperationException();
}
