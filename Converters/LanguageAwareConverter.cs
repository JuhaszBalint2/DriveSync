using System;
using System.Globalization;
using System.Windows.Data;
using DriveSync.WPF.Localization;

namespace DriveSync.WPF.Converters
{
    public class LanguageAwareConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is AppLanguage language && parameter is string param)
            {
                return language switch
                {
                    AppLanguage.Hungarian => param switch
                    {
                        "LeftColumnMargin" => "0,0,16,0",
                        "RightColumnMargin" => "16,0,0,0",
                        "LabelWidth" => "160",
                        _ => parameter
                    },
                    AppLanguage.English => param switch
                    {
                        "LeftColumnMargin" => "0,0,12,0",
                        "RightColumnMargin" => "12,0,0,0",
                        "LabelWidth" => "140",
                        _ => parameter
                    },
                    _ => parameter
                };
            }
            return parameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}