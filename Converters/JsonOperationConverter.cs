using System;
using System.Text.Json;
using System.Windows.Data;
using System.Globalization;
using DriveSync.WPF.Localization;

namespace DriveSync.WPF.Converters
{
    public class JsonOperationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value is string textValue)
                {
                    // Check if the value is a scanning message
                    if (textValue.Equals(LocalizationManager.Instance["ScanningForChanges"], StringComparison.OrdinalIgnoreCase) ||
                        textValue.Contains("Változások keresése", StringComparison.OrdinalIgnoreCase) ||
                        textValue.Contains("Scanning for changes", StringComparison.OrdinalIgnoreCase))
                    {
                        return LocalizationManager.Instance["ScanningOperation"];
                    }

                    // Regular JSON parsing for operation
                    var obj = JsonSerializer.Deserialize<JsonElement>(textValue);
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
                if (value is string textValue)
                {
                    // Check if the value is a scanning message
                    if (textValue.Equals(LocalizationManager.Instance["ScanningForChanges"], StringComparison.OrdinalIgnoreCase) ||
                        textValue.Contains("Változások keresése", StringComparison.OrdinalIgnoreCase) ||
                        textValue.Contains("Scanning for changes", StringComparison.OrdinalIgnoreCase))
                    {
                        return textValue;
                    }

                    // Regular JSON parsing for filename
                    var obj = JsonSerializer.Deserialize<JsonElement>(textValue);
                    return obj.GetProperty("Filename").GetString() ?? string.Empty;
                }
            }
            catch { }
            return value as string ?? string.Empty;
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
                if (value is string textValue)
                {
                    // Check if the value is a scanning message
                    if (textValue.Equals(LocalizationManager.Instance["ScanningForChanges"], StringComparison.OrdinalIgnoreCase) ||
                        textValue.Contains("Változások keresése", StringComparison.OrdinalIgnoreCase) ||
                        textValue.Contains("Scanning for changes", StringComparison.OrdinalIgnoreCase))
                    {
                        return DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                    }

                    // Regular JSON parsing for timestamp
                    var obj = JsonSerializer.Deserialize<JsonElement>(textValue);
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