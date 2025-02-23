using System;
using System.Globalization;
using System.Windows.Data;
using System.Text.Json;

namespace DriveSync.WPF.Converters
{
    public class SyncOperationIconConverter : IValueConverter
    {
        // Converters/SyncOperationIconConverter.cs
        // Converters/SyncOperationIconConverter.cs
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string jsonString)
            {
                try
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

                    // Safely extract the operation, defaulting to empty string if not found
                    string operation = jsonElement.TryGetProperty("Operation", out var operationElement)
                        ? operationElement.GetString()?.ToUpper() ?? string.Empty
                        : string.Empty;

                    string iconPath = operation switch
                    {
                        "COPYING FILES" or "MÁSOLÁS" => "M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0m-3.5-2.5a.5.5 0 0 0-.707 0L7 10.793 5.854 9.646a.5.5 0 1 0-.708.708l1.5 1.5a.5.5 0 0 0 .708 0l4.5-4.5a.5.5 0 0 0 0-.708",
                        "DELETING FILES" or "TÖRLÉS" => "M5.5 5.5A.5.5 0 0 1 6 6v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5m2.5 0a.5.5 0 0 1 .5.5v6a.5.5 0 0 1-1 0V6a.5.5 0 0 1 .5-.5m3 .5a.5.5 0 0 0-1 0v6a.5.5 0 0 0 1 0zM5 16a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1zm0 2a3 3 0 0 0 3-3V6a3 3 0 0 0-3-3H4a3 3 0 0 0-3 3v9a3 3 0 0 0 3 3z M4.5 4h10c.276 0 .5-.224.5-.5s-.224-.5-.5-.5h-10c-.276 0-.5.224-.5.5s.224.5.5.5",
                        "SKIPPING FILES" or "KIHAGYÁS" => "M16 8A8 8 0 1 1 0 8a8 8 0 0 1 16 0M5.354 4.646a.5.5 0 1 0-.708.708L7.293 8l-2.647 2.646a.5.5 0 0 0 .708.708L8 8.707l2.646 2.647a.5.5 0 0 0 .708-.708L8.707 8l2.647-2.646a.5.5 0 0 0-.708-.708L8 7.293z",
                        "MOVING FILES" or "ÁTHELYEZÉS" => "M7.646 4.646a.5.5 0 0 1 .708 0l6 6a.5.5 0 0 1-.708.708L8 5.707l-5.646 5.647a.5.5 0 0 1-.708-.708z",
                        _ => "M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14m0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16"
                    };

                    System.Diagnostics.Debug.WriteLine($"Convert input: {jsonString}, Selected icon path: {iconPath}");
                    return iconPath;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in converter: {ex.Message}");
                    return "M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14m0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16";
                }
            }
            System.Diagnostics.Debug.WriteLine("Convert input is not a string");
            return "M8 15A7 7 0 1 1 8 1a7 7 0 0 1 0 14m0 1A8 8 0 1 0 8 0a8 8 0 0 0 0 16";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}