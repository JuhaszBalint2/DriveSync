using System;
using System.Windows;
using System.Windows.Controls;
using DriveSync.WPF.Views.SettingsPanels;
using Microsoft.Extensions.Logging;
using DriveSync.WPF.Localization;
using System.Linq;

namespace DriveSync.WPF.Views
{
    public partial class SettingsWindow : ModernWindowBase
    {
        private readonly ILogger<SettingsWindow> _logger;
        private AppSettings _settings;
        private readonly LocalizationManager _localizationManager;

        // Settings panels
        private readonly GeneralSettingsPanel _generalPanel;
        private readonly InterfaceSettingsPanel _interfacePanel;
        private readonly SecuritySettingsPanel _securityPanel;
        private readonly SyncSettingsPanel _syncPanel;
        private readonly PerformanceSettingsPanel _performancePanel;
        private readonly AdvancedSettingsPanel _advancedPanel;

        // Track currently displayed panel
        private UserControl _currentPanel;

        public SettingsWindow(ILogger<SettingsWindow> logger)
        {
            InitializeComponent();
            _logger = logger;
            _settings = AppSettings.Load();
            _localizationManager = LocalizationManager.Instance;

            // Initialize all panels
            _generalPanel = new GeneralSettingsPanel();
            _interfacePanel = new InterfaceSettingsPanel();
            _securityPanel = new SecuritySettingsPanel();
            _syncPanel = new SyncSettingsPanel();
            _performancePanel = new PerformanceSettingsPanel();
            _advancedPanel = new AdvancedSettingsPanel();

            // Apply current theme
            ApplyCurrentTheme();

            LoadPanelSettings();
            InitializeNavigation();
        }

        private void ApplyCurrentTheme()
        {
            var settings = AppSettings.Load();
            string effectiveTheme = settings.GetEffectiveTheme();

            // Remove existing theme dictionaries
            var appResources = Application.Current.Resources.MergedDictionaries;
            var existingThemes = appResources
                .Where(d => d.Source != null &&
                    (d.Source.ToString().Contains("LightTheme.xaml") ||
                     d.Source.ToString().Contains("DarkTheme.xaml")))
                .ToList();

            foreach (var theme in existingThemes)
            {
                appResources.Remove(theme);
            }

            // Load the appropriate theme
            string themePath = effectiveTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase)
                ? "pack://application:,,,/Themes/DarkTheme.xaml"
                : "pack://application:,,,/Themes/LightTheme.xaml";

            var newTheme = new ResourceDictionary { Source = new Uri(themePath, UriKind.Absolute) };
            appResources.Add(newTheme);
        }

        private void LoadPanelSettings()
        {
            try
            {
                _generalPanel.LoadSettings(_settings);
                _interfacePanel.LoadSettings(_settings);
                _securityPanel.LoadSettings(_settings);
                _syncPanel.LoadSettings(_settings);
                _performancePanel.LoadSettings(_settings);
                _advancedPanel.LoadSettings(_settings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading settings into panels");
                MessageBox.Show(
                    "Failed to load some settings. Some values may be set to defaults.",
                    "Settings Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void InitializeNavigation()
        {
            // Select first item by default
            if (SettingsNavigation.Items.Count > 0)
            {
                SettingsNavigation.SelectedIndex = 0;
            }

            // Set window title
            Title = _localizationManager["ApplicationSettings"];
        }

        private void SettingsNavigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentPanel != null)
            {
                SettingsPanels.Children.Remove(_currentPanel);
            }

            // Get the selected item
            var listBoxItem = SettingsNavigation.SelectedItem as ListBoxItem;
            if (listBoxItem == null) return;

            // Find the text content in the Grid's TextBlock
            var textBlock = listBoxItem.Content as TextBlock;
            string selectedText = textBlock?.Text ?? string.Empty;

            // Determine localization key based on the selected panel
            string localizationKey = selectedText switch
            {
                var s when s == _localizationManager["PanelGeneral"] => "PanelGeneral",
                var s when s == _localizationManager["PanelInterface"] => "PanelInterface",
                var s when s == _localizationManager["PanelSecurity"] => "PanelSecurity",
                var s when s == _localizationManager["PanelSync"] => "PanelSync",
                var s when s == _localizationManager["PanelPerformance"] => "PanelPerformance",
                var s when s == _localizationManager["PanelAdvanced"] => "PanelAdvanced",
                _ => "ApplicationSettings"
            };

            // Update header
            SettingsHeader.Text = _localizationManager[localizationKey];

            // Determine which panel to show based on selection
            _currentPanel = SettingsNavigation.SelectedIndex switch
            {
                0 => _generalPanel,
                1 => _interfacePanel,
                2 => _securityPanel,
                3 => _syncPanel,
                4 => _performancePanel,
                5 => _advancedPanel,
                _ => _generalPanel
            };

            if (_currentPanel != null)
            {
                SettingsPanels.Children.Add(_currentPanel);
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger.LogInformation("Saving settings...");

                // Save settings from all panels
                _generalPanel.SaveSettings(_settings);
                _interfacePanel.SaveSettings(_settings);
                _securityPanel.SaveSettings(_settings);
                _syncPanel.SaveSettings(_settings);
                _performancePanel.SaveSettings(_settings);
                _advancedPanel.SaveSettings(_settings);

                // Save to file
                _settings.Save();

                // Apply theme changes
                ApplyCurrentTheme();

                _logger.LogInformation("Settings saved successfully");

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving settings");
                MessageBox.Show(
                    $"Failed to save settings: {ex.Message}\n\nPlease check the application logs for more details.",
                    "Settings Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _logger.LogInformation("Settings changes cancelled");
            DialogResult = false;
            Close();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                _localizationManager["ConfirmReset"],
                _localizationManager["Reset"],
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    _logger.LogInformation("Resetting settings to defaults...");

                    _settings = new AppSettings();
                    LoadPanelSettings();
                    ApplyCurrentTheme();

                    _logger.LogInformation("Settings reset successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error resetting settings");
                    MessageBox.Show(
                        "Failed to reset settings to defaults. Please try again.",
                        "Reset Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _logger.LogInformation("Settings window closed");
        }
    }
}