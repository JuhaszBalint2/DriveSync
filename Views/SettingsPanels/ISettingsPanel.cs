namespace DriveSync.WPF.Views.SettingsPanels
{
    public interface ISettingsPanel
    {
        void LoadSettings(AppSettings settings);
        void SaveSettings(AppSettings settings);
    }
}