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

                    // Check if it's actually valid JSON
                    if (!textValue.StartsWith("{") || !textValue.EndsWith("}"))
                    {
                        return textValue; // Return the raw text if it's not JSON
                    }

                    // Regular JSON parsing for operation
                    try
                    {
                        var obj = JsonSerializer.Deserialize<JsonElement>(textValue);
                        if (obj.TryGetProperty("Operation", out var opElement))
                        {
                            return opElement.GetString() ?? string.Empty;
                        }
                    }
                    catch (JsonException)
                    {
                        // If JSON parsing fails, just return the original text
                        return textValue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in converter: {ex.Message}");
            }
            return value?.ToString() ?? string.Empty;
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

                    // Check if it's actually valid JSON
                    if (!textValue.StartsWith("{") || !textValue.EndsWith("}"))
                    {
                        return textValue; // Return the raw text if it's not JSON
                    }

                    // Regular JSON parsing for filename
                    try
                    {
                        var obj = JsonSerializer.Deserialize<JsonElement>(textValue);
                        if (obj.TryGetProperty("Filename", out var filenameElement))
                        {
                            return filenameElement.GetString() ?? string.Empty;
                        }
                    }
                    catch (JsonException)
                    {
                        // If JSON parsing fails, just return the original text
                        return textValue;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in converter: {ex.Message}");
            }
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

                    // Check if it's actually valid JSON
                    if (!textValue.StartsWith("{") || !textValue.EndsWith("}"))
                    {
                        return DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"); // Return current time if it's not JSON
                    }

                    // Regular JSON parsing for timestamp
                    try
                    {
                        var obj = JsonSerializer.Deserialize<JsonElement>(textValue);
                        if (obj.TryGetProperty("Timestamp", out var timestampElement))
                        {
                            return timestampElement.GetString() ?? string.Empty;
                        }
                    }
                    catch (JsonException)
                    {
                        // If JSON parsing fails, return current time
                        return DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in converter: {ex.Message}");
            }
            return DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}