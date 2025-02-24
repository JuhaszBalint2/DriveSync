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

                    // Safely extract the operation, defaulting to empty string if not found
                    string operation = jsonElement.TryGetProperty("Operation", out var operationElement)
                        ? operationElement.GetString()?.ToUpper() ?? string.Empty
                        : string.Empty;

                    IconChar iconChar = operation switch
                    {
                        "COPYING FILES" or "MÁSOLÁS" => IconChar.Copy,
                        "DELETING FILES" or "TÖRLÉS" => IconChar.TrashAlt,
                        "SKIPPING FILES" or "KIHAGYÁS" => IconChar.Ban,
                        "MOVING FILES" or "ÁTHELYEZÉS" => IconChar.FileImport,
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