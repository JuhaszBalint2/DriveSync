using System;
using System.Windows;
using DriveSync.WPF.ViewModels;
using DriveSync.WPF.Localization;

namespace DriveSync.WPF.Views
{
    public partial class DirectoryBrowserDialog : ModernWindowBase
    {
        private readonly DirectoryBrowserViewModel _viewModel;

        public string SelectedPath => _viewModel.SelectedPath;

        public DirectoryBrowserDialog(DirectoryBrowserViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;

            // Add explicit check and warning if no remote is selected
            if (string.IsNullOrWhiteSpace(viewModel.RemoteName))
            {
                MessageBox.Show(
                    viewModel.IsSourceRemote
                        ? LocalizationManager.Instance["PleaseSelectSourceRemoteFirst"]
                        : LocalizationManager.Instance["PleaseSelectTargetRemoteFirst"],
                    LocalizationManager.Instance["Error"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                DialogResult = false;
                Close();
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}