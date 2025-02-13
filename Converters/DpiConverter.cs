using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DriveSync.WPF.Converters
{
    public class DpiConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is double originalValue)
            {
                var dpiScale = VisualTreeHelper.GetDpi(Application.Current.MainWindow).DpiScaleX;
                return originalValue * (96.0 / (96.0 * dpiScale));
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}