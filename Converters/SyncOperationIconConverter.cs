using System;
using System.Globalization;
using System.Windows.Data;
using System.Text.Json;
using FontAwesome.Sharp;
using DriveSync.WPF.Localization;

namespace DriveSync.WPF.Converters
{
    public class SyncOperationIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string jsonString)
            {
                // Special handling for scanning text
                if (jsonString.Equals(LocalizationManager.Instance["ScanningForChanges"], StringComparison.OrdinalIgnoreCase) ||
                    jsonString.Contains("SCANNING", StringComparison.OrdinalIgnoreCase) ||
                    jsonString.Contains("KERESÉS", StringComparison.OrdinalIgnoreCase) ||
                    jsonString.Contains("Változások keresése", StringComparison.OrdinalIgnoreCase) ||
                    jsonString.Contains("Scanning for changes", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"Scanning detected in: {jsonString}");
                    return IconChar.Search;
                }

                // Check if the string is valid JSON
                if (!jsonString.StartsWith("{") || !jsonString.EndsWith("}"))
                {
                    // If it's not a JSON string, handle it based on content
                    if (jsonString.Contains("COPY", StringComparison.OrdinalIgnoreCase) ||
                        jsonString.Contains("MÁSOLÁS", StringComparison.OrdinalIgnoreCase))
                        return IconChar.Copy;

                    if (jsonString.Contains("DELETE", StringComparison.OrdinalIgnoreCase) ||
                        jsonString.Contains("TÖRLÉS", StringComparison.OrdinalIgnoreCase))
                        return IconChar.TrashAlt;

                    if (jsonString.Contains("SKIP", StringComparison.OrdinalIgnoreCase) ||
                        jsonString.Contains("KIHAGYÁS", StringComparison.OrdinalIgnoreCase))
                        return IconChar.Ban;

                    if (jsonString.Contains("MOVE", StringComparison.OrdinalIgnoreCase) ||
                        jsonString.Contains("ÁTHELYEZÉS", StringComparison.OrdinalIgnoreCase))
                        return IconChar.FileImport;

                    if (jsonString.Contains("SCAN", StringComparison.OrdinalIgnoreCase) ||
                        jsonString.Contains("KERES", StringComparison.OrdinalIgnoreCase))
                        return IconChar.Search;

                    return IconChar.QuestionCircle;
                }

                try
                {
                    // Regular JSON parsing for other operations
                    var jsonElement = JsonSerializer.Deserialize<JsonElement>(jsonString);

                    // Check for operation type
                    if (jsonElement.TryGetProperty("Operation", out var operationElement))
                    {
                        string operation = operationElement.GetString()?.ToUpper() ?? string.Empty;

                        // Handle scanning operation
                        if (operation.Contains("SCAN") || operation.Contains("KERES"))
                        {
                            return IconChar.Search;
                        }

                        // Return appropriate icon for operation
                        IconChar iconChar = operation switch
                        {
                            var s when s.Contains("COPY") || s.Contains("MÁSOLÁS") => IconChar.Copy,
                            var s when s.Contains("DELETE") || s.Contains("TÖRLÉS") => IconChar.TrashAlt,
                            var s when s.Contains("SKIP") || s.Contains("KIHAGYÁS") => IconChar.Ban,
                            var s when s.Contains("MOVE") || s.Contains("ÁTHELYEZÉS") => IconChar.FileImport,
                            var s when s.Contains("SCAN") || s.Contains("KERES") => IconChar.Search,
                            _ => IconChar.QuestionCircle
                        };

                        System.Diagnostics.Debug.WriteLine($"Convert input: {jsonString}, Selected icon: {iconChar}");
                        return iconChar;
                    }
                }
                catch (Exception ex)
                {
                    // Handle JSON parsing errors by checking for scanning text
                    System.Diagnostics.Debug.WriteLine($"Error in converter: {ex.Message}");

                    // If it's not valid JSON, check directly for scanning-related text
                    if (jsonString.Contains("SCAN", StringComparison.OrdinalIgnoreCase) ||
                        jsonString.Contains("KERES", StringComparison.OrdinalIgnoreCase) ||
                        jsonString.Contains("Változások", StringComparison.OrdinalIgnoreCase) ||
                        jsonString.Contains("Scanning", StringComparison.OrdinalIgnoreCase))
                    {
                        return IconChar.Search;
                    }

                    return IconChar.QuestionCircle;
                }
            }

            // Handle non-string values or direct scanning text comparison
            if (value != null)
            {
                string valueStr = value.ToString();
                if (valueStr.Equals(LocalizationManager.Instance["ScanningForChanges"], StringComparison.OrdinalIgnoreCase) ||
                    valueStr.Contains("SCAN", StringComparison.OrdinalIgnoreCase) ||
                    valueStr.Contains("KERES", StringComparison.OrdinalIgnoreCase) ||
                    valueStr.Contains("Változás", StringComparison.OrdinalIgnoreCase) ||
                    valueStr.Contains("Scanning", StringComparison.OrdinalIgnoreCase))
                {
                    return IconChar.Search;
                }
            }

            System.Diagnostics.Debug.WriteLine("Convert input is not recognized: " + (value?.ToString() ?? "null"));
            return IconChar.QuestionCircle;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}