using System;
using System.Globalization;
using System.Windows.Data;
using System.Text.Json;
using FontAwesome.Sharp;

namespace DriveSync.WPF.Converters
{
    public class SyncOperationIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string jsonString)
            {
                try
                {
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

                    // Check for move-related operations
                    if (jsonElement.TryGetProperty("Operation", out var operationElement))
                    {
                        string operation = operationElement.GetString()?.ToUpper() ?? string.Empty;

                        // Detect move operations more comprehensively
                        if (operation.Contains("MOVE") ||
                            operation.Contains("ÁTHELYEZÉS") ||
                            (jsonElement.TryGetProperty("Description", out var descElement) &&
                             descElement.GetString()?.Contains("move", StringComparison.OrdinalIgnoreCase) == true))
                        {
                            return IconChar.FileImport;
                        }
                    }

                    // Fallback to existing operation detection
                    string fallbackOperation = jsonElement.TryGetProperty("Operation", out var operationFallback)
                        ? operationFallback.GetString()?.ToUpper() ?? string.Empty
                        : string.Empty;

                    IconChar iconChar = fallbackOperation switch
                    {
                        "COPYING FILES" or "MÁSOLÁS" or "COPY" => IconChar.Copy,
                        "DELETING FILES" or "TÖRLÉS" or "DELETE" => IconChar.TrashAlt,
                        "SKIPPING FILES" or "KIHAGYÁS" or "SKIP" => IconChar.Ban,
                        "MOVING FILES" or "ÁTHELYEZÉS" or "MOVE" => IconChar.FileImport,
                        _ => IconChar.QuestionCircle
                    };

                    System.Diagnostics.Debug.WriteLine($"Convert input: {jsonString}, Selected icon: {iconChar}");
                    return iconChar;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in converter: {ex.Message}");
                    return IconChar.QuestionCircle;
                }
            }
            System.Diagnostics.Debug.WriteLine("Convert input is not a string");
            return IconChar.QuestionCircle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}