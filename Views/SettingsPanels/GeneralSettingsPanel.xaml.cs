using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using DriveSync.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DriveSync.WPF.Views.SettingsPanels
{
    public partial class GeneralSettingsPanel : UserControl, ISettingsPanel
    {
        private readonly RcloneManager _rcloneManager;

        public GeneralSettingsPanel()
        {
            InitializeComponent();
            _rcloneManager = App.ServiceProvider.GetService<RcloneManager>();

            // Subscribe to RcloneManager path changes
            if (_rcloneManager != null)
            {
                _rcloneManager.RclonePathChanged += (sender, path) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        RclonePathTextBox.Text = path;
                    });
                };
            }
        }

        public void LoadSettings(AppSettings settings)
        {
            // Use the path from RcloneManager if available, otherwise fall back to settings
            RclonePathTextBox.Text = _rcloneManager?.CurrentRclonePath ?? settings.RcloneExecutablePath;

            // Map the saved DefaultSyncMode to the correct ComboBox index
            DefaultSyncModeCombo.SelectedIndex = settings.DefaultSyncMode switch
            {
                "Mirror Sync" => 0,
                "Tükrözéses szinkronizálás" => 0,
                "Backup (Copy)" => 1,
                "Biztonsági mentés (Másolás)" => 1,
                "Move Files" => 2,
                "Fájlok áthelyezése" => 2,
                _ => 0
            };
        }

        public void SaveSettings(AppSettings settings)
        {
            settings.RcloneExecutablePath = RclonePathTextBox.Text;

            // Save the non-localized version of the sync mode
            settings.DefaultSyncMode = ((ComboBoxItem)DefaultSyncModeCombo.SelectedItem).Content.ToString() switch
            {
                "Tükrözéses szinkronizálás" => "Mirror Sync",
                "Biztonsági mentés (Másolás)" => "Backup (Copy)",
                "Fájlok áthelyezése" => "Move Files",
                "Mirror Sync" => "Mirror Sync",
                "Backup (Copy)" => "Backup (Copy)",
                "Move Files" => "Move Files",
                _ => "Mirror Sync"
            };
        }

        private void BrowseRclone_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select Rclone Executable"
            };

            // If the current path exists and is a valid file path, set it as the initial directory
            if (!string.IsNullOrWhiteSpace(RclonePathTextBox.Text) &&
                File.Exists(RclonePathTextBox.Text))
            {
                dialog.InitialDirectory = Path.GetDirectoryName(RclonePathTextBox.Text);
                dialog.FileName = Path.GetFileName(RclonePathTextBox.Text);
            }
            else
            {
                // Fallback to some default directories if the current path is invalid
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            }

            if (dialog.ShowDialog() == true)
            {
                RclonePathTextBox.Text = dialog.FileName;
            }
        }
    }
}