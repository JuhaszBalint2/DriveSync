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
            SetComboBoxValueByContent(TransfersTextBox, settings.MaxTransfers.ToString());
            SetComboBoxValueByContent(CheckersTextBox, settings.CheckerThreads.ToString());
            SetComboBoxValueByContent(BufferSizeTextBox, settings.BufferSize.ToString());
            UseMemoryMappingCheckBox.IsChecked = settings.UseMmap;

            // Convert bandwidth settings from KB/s to MiB/s for display
            double bwLimitMiB = (settings.BandwidthLimit / 1024.0);
            SetComboBoxValueByContent(BandwidthLimitTextBox, Math.Round(bwLimitMiB).ToString());
            CutoffModeCombo.SelectedIndex = settings.CutoffMode == "hard" ? 0 : 1;
        }

        public void SaveSettings(AppSettings settings)
        {
            if (TransfersTextBox.SelectedItem is ComboBoxItem transfersItem &&
                int.TryParse(transfersItem.Content.ToString(), out int transfers))
                settings.MaxTransfers = Math.Max(1, Math.Min(transfers, 32));

            if (CheckersTextBox.SelectedItem is ComboBoxItem checkersItem &&
                int.TryParse(checkersItem.Content.ToString(), out int checkers))
                settings.CheckerThreads = Math.Max(1, Math.Min(checkers, 64));

            if (BufferSizeTextBox.SelectedItem is ComboBoxItem bufferItem &&
                int.TryParse(bufferItem.Content.ToString(), out int bufferSize))
                settings.BufferSize = Math.Max(1, Math.Min(bufferSize, 1024));

            settings.UseMmap = UseMemoryMappingCheckBox.IsChecked ?? false;

            // Convert bandwidth from MiB/s to KB/s for storage
            if (BandwidthLimitTextBox.SelectedItem is ComboBoxItem bwItem &&
                double.TryParse(bwItem.Content.ToString(), out double bwLimitMiB))
                settings.BandwidthLimit = (int)(bwLimitMiB * 1024);

            settings.CutoffMode = CutoffModeCombo.SelectedIndex == 0 ? "hard" : "soft";
        }

        private void SetComboBoxValueByContent(ComboBox comboBox, string value)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Content.ToString() == value)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            // If exact match not found, find the closest value
            if (double.TryParse(value, out double doubleValue))
            {
                ComboBoxItem closestItem = null;
                double closestDifference = double.MaxValue;

                foreach (ComboBoxItem item in comboBox.Items)
                {
                    if (double.TryParse(item.Content.ToString(), out double itemValue))
                    {
                        double difference = Math.Abs(itemValue - doubleValue);
                        if (difference < closestDifference)
                        {
                            closestDifference = difference;
                            closestItem = item;
                        }
                    }
                }

                if (closestItem != null)
                {
                    comboBox.SelectedItem = closestItem;
                }
                else
                {
                    // Default to first item if no match found
                    comboBox.SelectedIndex = 0;
                }
            }
            else
            {
                // Default to first item if value is not a number
                comboBox.SelectedIndex = 0;
            }
        }
    }
}