using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using DevCLT.WindowsApp.Services;

namespace DevCLT.WindowsApp.Converters;

/// <summary>
/// Converts a hotkey string (e.g. "Ctrl+Alt+I") into a horizontal StackPanel
/// with individual kbd-styled Border+TextBlock elements for each key.
/// </summary>
public class HotkeyToKbdConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var keyCombo = value as string;
        if (string.IsNullOrWhiteSpace(keyCombo))
            return new StackPanel();

        var parts = HotkeyService.SplitForDisplay(keyCombo);
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0)
        };

        foreach (var part in parts)
        {
            var text = new TextBlock { Text = part };
            text.SetResourceReference(FrameworkElement.StyleProperty, "KbdText");

            var border = new Border { Child = text };
            border.SetResourceReference(FrameworkElement.StyleProperty, "KbdBorder");

            panel.Children.Add(border);
        }

        return panel;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
