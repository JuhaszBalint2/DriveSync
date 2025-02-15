using System;
using System.Windows;
using DriveSync.WPF.ViewModels;

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