using System.Windows.Controls;
using DriveSync.WPF.Localization;
using DriveSync.WPF.ViewModels;
using Microsoft.Extensions.Logging;

namespace DriveSync.WPF.Views
{
    public partial class MainWindow : ModernWindowBase
    {
        private readonly MainViewModel _viewModel;
        private readonly ILogger<MainWindow> _logger;

        public MainWindow(MainViewModel viewModel, ILogger<MainWindow> logger = null)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _logger = logger;
            base.DataContext = viewModel;

            // Initialize language ComboBox
            InitializeLanguageSelection();

            // Log window opening
            _logger?.LogInformation("MainWindow initialized and opened");

            // Handle closing to prevent accidental shutdown
            Closing += (s, e) =>
            {
                _logger?.LogInformation("MainWindow closing");
            };
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