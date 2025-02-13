using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace DriveSync.WPF.Views.SettingsPanels
{
    public partial class GeneralSettingsPanel : UserControl, ISettingsPanel
    {
        public GeneralSettingsPanel()
        {
            InitializeComponent();
        }

        public void LoadSettings(AppSettings settings)
        {
            // If no path is set, attempt to auto-detect
            if (string.IsNullOrWhiteSpace(settings.RcloneExecutablePath) ||
                !File.Exists(settings.RcloneExecutablePath))
            {
                settings.RcloneExecutablePath = FindRclonePath();
            }

            RclonePathTextBox.Text = settings.RcloneExecutablePath;
            DefaultSyncModeCombo.SelectedIndex = settings.DefaultSyncMode switch
            {
                "Mirror" => 0,
                "Backup" => 1,
                "Move" => 2,
                _ => 0
            };
        }

        private string FindRclonePath()
        {
            // Possible locations to search for rclone.exe
            string[] searchLocations = new[]
            {
                // Current application directory
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe"),
                
                // Common installation paths
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "rclone", "rclone.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "rclone", "rclone.exe"),
                
                // Local app data
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rclone", "rclone.exe"),
                
                // System PATH
                FindInSystemPath("rclone.exe")
            };

            foreach (var path in searchLocations)
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    return path;
                }
            }

            // Fallback to default if no path found
            return "rclone";
        }

        private string FindInSystemPath(string executable)
        {
            return Environment.GetEnvironmentVariable("PATH")
                .Split(Path.PathSeparator)
                .Select(p => Path.Combine(p, executable))
                .FirstOrDefault(File.Exists);
        }

        public void SaveSettings(AppSettings settings)
        {
            settings.RcloneExecutablePath = RclonePathTextBox.Text;
            settings.DefaultSyncMode = ((ComboBoxItem)DefaultSyncModeCombo.SelectedItem).Content.ToString();
        }

        private void BrowseRclone_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|All files (*.*)|*.*",
                Title = "Select Rclone Executable"
            };
            if (dialog.ShowDialog() == true)
            {
                RclonePathTextBox.Text = dialog.FileName;
            }
        }
    }
}