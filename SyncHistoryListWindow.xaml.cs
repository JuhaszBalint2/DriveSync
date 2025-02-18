using System.Collections.ObjectModel;
using System.Windows;
using DriveSync.WPF.ViewModels;

namespace DriveSync.WPF.Views
{
    public partial class SyncHistoryListWindow : ModernWindowBase
    {
        public ObservableCollection<MainViewModel.SyncHistoryItem> SyncHistory { get; }

        public SyncHistoryListWindow(ObservableCollection<MainViewModel.SyncHistoryItem> syncHistory)
        {
            InitializeComponent();
            SyncHistory = syncHistory;
            DataContext = this;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}