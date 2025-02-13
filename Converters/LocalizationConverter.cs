using System;
using System.Globalization;
using System.Windows.Data;
using DriveSync.Infrastructure.Services;
using DriveSync.WPF.Localization;

namespace DriveSync.WPF.Converters
{
    public class LocalizationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle DisplayName to localization conversion
            if (value is string displayName)
            {
                return displayName switch
                {
                    "Mirror Sync" => LocalizationManager.Instance["SyncModeOption1"],
                    "Backup (Copy)" => LocalizationManager.Instance["SyncModeOption2"],
                    "Move Files" => LocalizationManager.Instance["SyncModeOption3"],
                    _ => displayName
                };
            }
            // Handle SyncType to localization conversion
            if (value is SyncType syncType)
            {
                return syncType switch
                {
                    SyncType.Mirror => LocalizationManager.Instance["SyncModeOption1"],
                    SyncType.Backup => LocalizationManager.Instance["SyncModeOption2"],
                    SyncType.Move => LocalizationManager.Instance["SyncModeOption3"],
                    _ => LocalizationManager.Instance["SyncModeOption1"]
                };
            }
            // Default case: return the original value
            return value?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}