using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdiQuickLaunchLib.Converter;

public class InvertBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Check if the value is a boolean and is True
        if (value is bool boolean && boolean)
        {
            // True means Collapsed (we hide the TextBlock when selected)
            return Visibility.Collapsed;
        }
        
        // False or non-boolean means Visible (we show the TextBlock when not selected)
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Optional: Implement ConvertBack if you need two-way binding with visibility
        // If Visibility is Visible, return False (not selected)
        if (value is Visibility visibility && visibility == Visibility.Visible)
        {
            return false;
        }
        
        // If Visibility is Collapsed, return True (selected)
        return true;
    }
}