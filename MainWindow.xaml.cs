using System.Windows.Controls;
using DriveSync.WPF.Localization;
using DriveSync.WPF.ViewModels;

namespace DriveSync.WPF.Views
{
    public partial class MainWindow : ModernWindowBase
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            base.DataContext = viewModel;

            // Initialize language ComboBox
            InitializeLanguageSelection();
        }

        private void InitializeLanguageSelection()
        {
            if (LanguageComboBox != null)
            {
                // Set initial selection based on current language
                LanguageComboBox.SelectedIndex = LocalizationManager.Instance.CurrentLanguage == AppLanguage.Hungarian ? 1 : 0;
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex >= 0)
            {
                LocalizationManager.Instance.CurrentLanguage = comboBox.SelectedIndex == 1 ? AppLanguage.Hungarian : AppLanguage.English;
            }
        }
    }
}