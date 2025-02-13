using System;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;

namespace DriveSync.WPF.Views.SettingsPanels
{
    public partial class SecuritySettingsPanel : UserControl, ISettingsPanel
    {
        public SecuritySettingsPanel()
        {
            InitializeComponent();
        }

        public void LoadSettings(AppSettings settings)
        {
            // Simplified security settings based on the revised AppSettings
            RequirePasswordCheckBox.IsChecked = settings.RequirePassword;
            RequirePasswordForSettingsCheckBox.IsChecked = settings.RequirePasswordForSettings;
            RequirePasswordForSyncCheckBox.IsChecked = settings.RequirePasswordForSync;
        }

        public void SaveSettings(AppSettings settings)
        {
            // Update security-related settings
            settings.RequirePassword = RequirePasswordCheckBox.IsChecked ?? false;
            settings.RequirePasswordForSettings = RequirePasswordForSettingsCheckBox.IsChecked ?? false;
            settings.RequirePasswordForSync = RequirePasswordForSyncCheckBox.IsChecked ?? false;
        }

        private void GenerateKey_Click(object sender, RoutedEventArgs e)
        {
            // Removed encryption key generation as it's no longer supported
            MessageBox.Show("Encryption key generation is no longer supported in this version.",
                "Feature Removed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ResetPassword_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset the application password?",
                "Reset Password",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Reset password-related settings
                RequirePasswordCheckBox.IsChecked = false;
                RequirePasswordForSettingsCheckBox.IsChecked = false;
                RequirePasswordForSyncCheckBox.IsChecked = false;
            }
        }
    }
}