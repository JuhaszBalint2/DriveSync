using System;
using System.Windows.Controls;

namespace DriveSync.WPF.Views.SettingsPanels
{
    public partial class PerformanceSettingsPanel : UserControl, ISettingsPanel
    {
        public PerformanceSettingsPanel()
        {
            InitializeComponent();
        }

        public void LoadSettings(AppSettings settings)
        {
            TransfersTextBox.Text = settings.MaxTransfers.ToString();
            CheckersTextBox.Text = settings.CheckerThreads.ToString();
            BufferSizeTextBox.Text = settings.BufferSize.ToString();
            UseMemoryMappingCheckBox.IsChecked = settings.UseMmap;

            // Convert bandwidth settings from KB/s to MiB/s for display
            double bwLimitMiB = (settings.BandwidthLimit / 1024.0);
            BandwidthLimitTextBox.Text = bwLimitMiB.ToString("F2");
            CutoffModeCombo.SelectedIndex = settings.CutoffMode == "hard" ? 0 : 1;
        }

        public void SaveSettings(AppSettings settings)
        {
            if (int.TryParse(TransfersTextBox.Text, out int transfers))
                settings.MaxTransfers = Math.Max(1, Math.Min(transfers, 32));

            if (int.TryParse(CheckersTextBox.Text, out int checkers))
                settings.CheckerThreads = Math.Max(1, Math.Min(checkers, 64));

            if (int.TryParse(BufferSizeTextBox.Text, out int bufferSize))
                settings.BufferSize = Math.Max(1, Math.Min(bufferSize, 1024));

            settings.UseMmap = UseMemoryMappingCheckBox.IsChecked ?? false;

            // Convert bandwidth from MiB/s to KB/s for storage
            if (double.TryParse(BandwidthLimitTextBox.Text, out double bwLimitMiB))
                settings.BandwidthLimit = (int)(bwLimitMiB * 1024);

            settings.CutoffMode = CutoffModeCombo.SelectedIndex == 0 ? "hard" : "soft";
        }
    }
}