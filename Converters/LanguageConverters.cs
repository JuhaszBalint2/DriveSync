using System;
using System.Globalization;
using System.Windows.Data;
using DriveSync.WPF.Localization;

namespace DriveSync.WPF.Converters
{
    public class EnglishSelectedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AppLanguage language)
            {
                return language == AppLanguage.English;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return AppLanguage.English;
            }
            return Binding.DoNothing;
        }
    }

    public class HungarianSelectedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AppLanguage language)
            {
                return language == AppLanguage.Hungarian;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
            {
                return AppLanguage.Hungarian;
            }
            return Binding.DoNothing;
        }
    }
}