// Replace the ENTIRE contents of your existing Converters/BoolToSymbolConverter.cs file with this:

using Avalonia.Data.Converters;
using Avalonia.Media;
using System;
using System.Globalization;

namespace JagexAccountSwitcher.Converters
{
    public class BoolToSymbolConverter : IValueConverter
    {
        public string TrueValue { get; set; } = "✓";
        public string FalseValue { get; set; } = "✗";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolVal)
                return boolVal ? TrueValue : FalseValue;
            return FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToColorConverter : IValueConverter
    {
        public object TrueValue { get; set; } = Brushes.Green;
        public object FalseValue { get; set; } = Brushes.Red;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolVal)
            {
                var selectedValue = boolVal ? TrueValue : FalseValue;
                
                // If it's a string color name, convert to brush
                if (selectedValue is string colorName)
                {
                    return ConvertStringToBrush(colorName);
                }
                
                return selectedValue;
            }
            return FalseValue;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private IBrush ConvertStringToBrush(string colorName)
        {
            // Common color mappings
            return colorName.ToLowerInvariant() switch
            {
                "green" => Brushes.Green,
                "red" => Brushes.Red,
                "orange" => Brushes.Orange,
                "white" => Brushes.White,
                "gray" or "grey" => Brushes.Gray,
                "blue" => Brushes.Blue,
                "yellow" => Brushes.Yellow,
                "purple" => Brushes.Purple,
                "cyan" => Brushes.Cyan,
                "magenta" => Brushes.Magenta,
                "black" => Brushes.Black,
                _ => Brushes.White
            };
        }
    }
}
