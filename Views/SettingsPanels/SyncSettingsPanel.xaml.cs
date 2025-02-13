using System;
using System.Linq;
using System.Windows.Controls;

namespace DriveSync.WPF.Views.SettingsPanels
{
    public partial class SyncSettingsPanel : UserControl, ISettingsPanel
    {
        public SyncSettingsPanel()
        {
            InitializeComponent();
        }

        public void LoadSettings(AppSettings settings)
        {
            // Load sync behavior settings
            DeleteEmptyDirsCheckBox.IsChecked = settings.DeleteEmptyDirs;
            IgnoreExistingCheckBox.IsChecked = settings.IgnoreExisting;
            CompareSizeCheckBox.IsChecked = settings.CompareSize;
            PreservePermissionsCheckBox.IsChecked = settings.PreservePermissions;
            PreserveTimestampsCheckBox.IsChecked = settings.PreserveTimes;

            // Load file filtering settings
            ExcludedPathsTextBox.Text = string.Join(", ", settings.ExcludedPaths);
            IncludedPathsTextBox.Text = string.Join(", ", settings.IncludedPaths);
        }

        public void SaveSettings(AppSettings settings)
        {
            // Save sync behavior settings
            settings.DeleteEmptyDirs = DeleteEmptyDirsCheckBox.IsChecked ?? false;
            settings.IgnoreExisting = IgnoreExistingCheckBox.IsChecked ?? false;
            settings.CompareSize = CompareSizeCheckBox.IsChecked ?? false;
            settings.PreservePermissions = PreservePermissionsCheckBox.IsChecked ?? false;
            settings.PreserveTimes = PreserveTimestampsCheckBox.IsChecked ?? false;

            // Save file filtering settings
            settings.ExcludedPaths = ExcludedPathsTextBox.Text
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            settings.IncludedPaths = IncludedPathsTextBox.Text
                .Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();
        }
    }
}