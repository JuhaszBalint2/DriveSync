using System;
using System.Text.Json;
using System.Windows.Data;
using System.Globalization;

namespace DriveSync.WPF.Converters
{
    public class JsonOperationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string json)
                {
                    var obj = JsonSerializer.Deserialize<JsonElement>(json);
                    return obj.GetProperty("Operation").GetString() ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class JsonFilenameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string json)
                {
                    var obj = JsonSerializer.Deserialize<JsonElement>(json);
                    return obj.GetProperty("Filename").GetString() ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class JsonTimestampConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string json)
                {
                    var obj = JsonSerializer.Deserialize<JsonElement>(json);
                    return obj.GetProperty("Timestamp").GetString() ?? string.Empty;
                }
            }
            catch { }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}