using System;
using System.Windows.Controls;

namespace DriveSync.WPF.Views.SettingsPanels
{
    public partial class AdvancedSettingsPanel : UserControl, ISettingsPanel
    {
        public AdvancedSettingsPanel()
        {
            InitializeComponent();
        }

        public void LoadSettings(AppSettings settings)
        {
            // Error Handling Settings
            SetComboBoxValueByContent(RetryCountTextBox, settings.RetryCount.ToString());
            SetComboBoxValueByContent(ConnectTimeoutTextBox, settings.ConnectTimeout.ToString());

            // Advanced Transfer Settings
            FastListCheckBox.IsChecked = settings.FastList;
            NoTraversalCheckBox.IsChecked = settings.NoTraverse;
        }

        public void SaveSettings(AppSettings settings)
        {
            // Error Handling Settings
            if (RetryCountTextBox.SelectedItem is ComboBoxItem retryItem &&
                int.TryParse(retryItem.Content.ToString(), out int retryCount))
            {
                settings.RetryCount = Math.Max(0, Math.Min(retryCount, 100));
            }

            if (ConnectTimeoutTextBox.SelectedItem is ComboBoxItem timeoutItem &&
                int.TryParse(timeoutItem.Content.ToString(), out int timeout))
            {
                settings.ConnectTimeout = Math.Max(1, Math.Min(timeout, 3600));
            }

            // Advanced Transfer Settings
            settings.FastList = FastListCheckBox.IsChecked ?? false;
            settings.NoTraverse = NoTraversalCheckBox.IsChecked ?? false;
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
            if (int.TryParse(value, out int intValue))
            {
                ComboBoxItem closestItem = null;
                int closestDifference = int.MaxValue;

                foreach (ComboBoxItem item in comboBox.Items)
                {
                    if (int.TryParse(item.Content.ToString(), out int itemValue))
                    {
                        int difference = Math.Abs(itemValue - intValue);
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