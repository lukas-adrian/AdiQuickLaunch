using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AdiQuickLaunchLib.Converter;

public class BooleanToVisibilityConverter: IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Check for 'invert' parameter
            bool invert = parameter != null && parameter.ToString().ToLower() == "invert";
                
            // If invert is true, Visible becomes Collapsed and vice versa.
            if (invert)
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        // Return Collapsed if the value is not a boolean
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // Conversion from Visibility back to bool is typically not needed for this scenario.
        if (value is Visibility visibility)
        {
            bool invert = parameter != null && parameter.ToString().ToLower() == "invert";
                
            bool boolValue = (visibility == Visibility.Visible);

            // Re-invert if the parameter was set
            if (invert)
            {
                boolValue = !boolValue;
            }
                
            return boolValue;
        }
            
        return DependencyProperty.UnsetValue;
    }
}