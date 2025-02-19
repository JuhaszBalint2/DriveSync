using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using DriveSync.WPF.ViewModels;

namespace DriveSync.WPF.Views
{
    public partial class SyncHistoryListWindow : ModernWindowBase
    {
        public ObservableCollection<MainViewModel.SyncHistoryItem> SyncHistory { get; }

        public ICommand OpenSyncHistoryItemCommand { get; }

        public SyncHistoryListWindow(ObservableCollection<MainViewModel.SyncHistoryItem> syncHistory)
        {
            InitializeComponent();
            SyncHistory = syncHistory;

            // Create the command to open sync history item
            OpenSyncHistoryItemCommand = new RelayCommand<MainViewModel.SyncHistoryItem>(OpenSyncHistoryItem);

            DataContext = this;
        }

        private void OpenSyncHistoryItem(MainViewModel.SyncHistoryItem historyItem)
        {
            if (historyItem == null || string.IsNullOrWhiteSpace(historyItem.Log))
            {
                MessageBox.Show("No log available for this sync.", "Sync Log", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var logViewer = new LogViewerWindow(historyItem.Log, historyItem.Timestamp);
            logViewer.Owner = this;
            logViewer.ShowDialog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}