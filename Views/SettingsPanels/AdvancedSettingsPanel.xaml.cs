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
            RetryCountTextBox.Text = settings.RetryCount.ToString();
            ConnectTimeoutTextBox.Text = settings.ConnectTimeout.ToString();

            // Advanced Transfer Settings
            FastListCheckBox.IsChecked = settings.FastList;
            NoTraversalCheckBox.IsChecked = settings.NoTraverse;
        }

        public void SaveSettings(AppSettings settings)
        {
            // Error Handling Settings
            if (int.TryParse(RetryCountTextBox.Text, out int retryCount))
            {
                settings.RetryCount = Math.Max(0, Math.Min(retryCount, 100));
            }

            if (int.TryParse(ConnectTimeoutTextBox.Text, out int timeout))
            {
                settings.ConnectTimeout = Math.Max(1, Math.Min(timeout, 3600));
            }

            // Advanced Transfer Settings
            settings.FastList = FastListCheckBox.IsChecked ?? false;
            settings.NoTraverse = NoTraversalCheckBox.IsChecked ?? false;
        }
    }
}