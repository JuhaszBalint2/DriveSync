using System;
using System.Windows.Controls;
using DriveSync.WPF.Localization;
using DriveSync.WPF.ViewModels;

namespace DriveSync.WPF.Views.SettingsPanels
{
    public partial class InterfaceSettingsPanel : UserControl, ISettingsPanel
    {
        public InterfaceSettingsPanel()
        {
            InitializeComponent();
        }

        public void LoadSettings(AppSettings settings)
        {
            // Theme settings
            if (settings.UseSystemTheme)
            {
                ThemeCombo.SelectedIndex = 0; // System
            }
            else
            {
                ThemeCombo.SelectedIndex = settings.Theme == "Light" ? 1 : 2;
            }

            // Font size
            FontSizeTextBox.Text = settings.FontSize.ToString();

            // Language
            LanguageCombo.SelectedIndex = LocalizationManager.Instance.CurrentLanguage == AppLanguage.Hungarian ? 1 : 0;
        }

        public void SaveSettings(AppSettings settings)
        {
            // Theme settings
            settings.UseSystemTheme = ThemeCombo.SelectedIndex == 0;
            settings.Theme = ThemeCombo.SelectedIndex switch
            {
                0 => AppSettings.DetectSystemTheme(),
                1 => "Light",
                _ => "Dark"
            };

            // Font size
            if (int.TryParse(FontSizeTextBox.Text, out int fontSize))
            {
                settings.FontSize = Math.Max(8, Math.Min(fontSize, 72));
            }

            // Language
            LocalizationManager.Instance.CurrentLanguage = LanguageCombo.SelectedIndex == 0
                ? AppLanguage.English
                : AppLanguage.Hungarian;
        }
    }
}