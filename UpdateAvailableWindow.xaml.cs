using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using DriveSync.Infrastructure.Services;
using DriveSync.WPF.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DriveSync.WPF.Views
{
    public partial class UpdateAvailableWindow : Window, INotifyPropertyChanged
    {
        private readonly IRcloneVersionService _versionService;
        private readonly RcloneManager _rcloneManager;
        private readonly ILogger<UpdateAvailableWindow> _logger;
        private readonly string _currentVersion;
        private readonly string _targetVersion;
        private readonly bool _isInitialInstall;
        private readonly bool _isFallbackVersion;
        private DispatcherTimer _countdownTimer;
        private int _remainingSeconds = 5;

        private string _updateMessage;
        public string UpdateMessage
        {
            get => _updateMessage;
            set
            {
                if (_updateMessage != value)
                {
                    _updateMessage = value;
                    OnPropertyChanged(nameof(UpdateMessage));
                }
            }
        }

        private string _countdownMessage;
        public string CountdownMessage
        {
            get => _countdownMessage;
            set
            {
                if (_countdownMessage != value)
                {
                    _countdownMessage = value;
                    OnPropertyChanged(nameof(CountdownMessage));
                }
            }
        }

        private string _alternateLanguageText;
        public string AlternateLanguageText
        {
            get => _alternateLanguageText;
            set
            {
                if (_alternateLanguageText != value)
                {
                    _alternateLanguageText = value;
                    OnPropertyChanged(nameof(AlternateLanguageText));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public UpdateAvailableWindow(string currentVersion, string targetVersion, bool isFallbackVersion = false)
        {
            // Apply the current theme from the app settings
            ApplyCurrentTheme();

            InitializeComponent();

            _versionService = App.ServiceProvider.GetService<IRcloneVersionService>();
            _rcloneManager = App.ServiceProvider.GetService<RcloneManager>();
            _logger = App.ServiceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger<UpdateAvailableWindow>();

            _currentVersion = currentVersion;
            _targetVersion = targetVersion;
            _isFallbackVersion = isFallbackVersion;

            // Determine if this is an initial install (no current version)
            _isInitialInstall = string.IsNullOrEmpty(currentVersion) || currentVersion == "0.0.0" || !CheckIfVersionExists(currentVersion);

            // Set alternate language text based on current language
            UpdateAlternateLanguageText();

            // Listen for language changes and update the button text
            LocalizationManager.Instance.PropertyChanged += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "CurrentLanguage")
                {
                    UpdateAlternateLanguageText();
                }
            };

            DataContext = this;

            // Initialize countdown
            InitializeCountdown();

            // Set initial update message based on installation type and localization
            UpdateUIMessages();

            // Add the Loaded event handler
            Loaded += OnLoaded;
        }

        private void UpdateUIMessages()
        {
            // Dynamically set messages based on current language and installation type
            if (_isInitialInstall)
            {
                UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                    ? $"Rclone needs to be installed.\nDownloading version: {_targetVersion}"
                    : $"Rclone telepítése szükséges.\nVerziós letöltése: {_targetVersion}";

                SubtitleTextBlock.Text = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                    ? "Please wait while DriveSync prepares the update..."
                    : "Kérjük várjon, amíg a DriveSync előkészíti a frissítést...";
            }
            else if (_isFallbackVersion)
            {
                UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                    ? $"Latest version download failed. Trying alternative version.\nDownloading version: {_targetVersion}"
                    : $"A legújabb verzió letöltése sikertelen. Alternatív verzió próbálása.\nVerziós letöltése: {_targetVersion}";

                SubtitleTextBlock.Text = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                    ? "Please wait while DriveSync prepares the update..."
                    : "Kérjük várjon, amíg a DriveSync előkészíti a frissítést...";
            }
            else
            {
                UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                    ? $"Updating rclone\nCurrent Version: {_currentVersion}\nLatest Version: {_targetVersion}"
                    : $"Rclone frissítése\nJelenlegi verzió: {_currentVersion}\nLegújabb verzió: {_targetVersion}";

                SubtitleTextBlock.Text = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                    ? "Please wait while DriveSync prepares the update..."
                    : "Kérjük várjon, amíg a DriveSync előkészíti a frissítést...";
            }
        }

        private void UpdateAlternateLanguageText()
        {
            // Set the alternate language option based on current language
            AlternateLanguageText = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                ? "Magyar"
                : "English";

            // Also update other UI text elements when language changes
            UpdateUIMessages();
        }

        private void LanguageSwitchButton_Click(object sender, RoutedEventArgs e)
        {
            // Toggle language
            if (LocalizationManager.Instance.CurrentLanguage == AppLanguage.English)
            {
                LocalizationManager.Instance.CurrentLanguage = AppLanguage.Hungarian;
            }
            else
            {
                LocalizationManager.Instance.CurrentLanguage = AppLanguage.English;
            }

            // UpdateAlternateLanguageText() will be called via the PropertyChanged event
        }

        // Check if a specific version exists on disk
        private bool CheckIfVersionExists(string version)
        {
            if (string.IsNullOrEmpty(version)) return false;

            string baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DriveSync",
                "RcloneVersions"
            );

            string versionPath = Path.Combine(baseDirectory, $"v{version}", "rclone.exe");
            return File.Exists(versionPath);
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

        private void InitializeCountdown()
        {
            _countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _countdownTimer.Tick += CountdownTimer_Tick;
            _countdownTimer.Start();

            UpdateCountdownMessage();
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            _remainingSeconds--;

            if (_remainingSeconds <= 0)
            {
                _countdownTimer.Stop();
                ShowUpdatePanel();
            }
            else
            {
                UpdateCountdownMessage();
            }
        }

        private void UpdateCountdownMessage()
        {
            CountdownMessage = $"Choose language / Válasszon nyelvet ({_remainingSeconds} sec)";
        }

        private void ShowUpdatePanel()
        {
            LocalizationPanel.Visibility = Visibility.Visible;
            UpdatePanel.Visibility = Visibility.Collapsed;
            CountdownTextBlock.Visibility = Visibility.Collapsed;
        }

        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as System.Windows.Controls.Button;
            if (button == null) return;

            var selectedLanguage = button.Tag?.ToString();
            if (selectedLanguage == "English")
            {
                LocalizationManager.Instance.CurrentLanguage = AppLanguage.English;
            }
            else if (selectedLanguage == "Hungarian")
            {
                LocalizationManager.Instance.CurrentLanguage = AppLanguage.Hungarian;
            }

            LocalizationPanel.Visibility = Visibility.Collapsed;
            UpdatePanel.Visibility = Visibility.Visible;
            CountdownTextBlock.Visibility = Visibility.Collapsed;

            StartUpdateProcess();
        }

        private void StartUpdateProcess()
        {
            // The Loaded event handler is already assigned in the constructor
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateAvailableWindow_Loaded(sender, e);
        }

        private async void UpdateAvailableWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logger?.LogInformation($"Starting {(_isInitialInstall ? "installation" : "update")} of version {_targetVersion} (Fallback: {_isFallbackVersion})");

                // Immediately set up the progress bar to be determinate and start at 0
                Dispatcher.Invoke(() => {
                    DownloadProgressBar.IsIndeterminate = false;
                    DownloadProgressBar.Minimum = 0;
                    DownloadProgressBar.Maximum = 100;
                    DownloadProgressBar.Value = 0;
                });

                string baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DriveSync",
                    "RcloneVersions"
                );

                if (!Directory.Exists(baseDirectory))
                {
                    Directory.CreateDirectory(baseDirectory);
                }

                string downloadPath = Path.Combine(
                    baseDirectory,
                    $"rclone-v{_targetVersion}-windows-amd64.zip"
                );

                // Modify the progress to use a deterministic progress bar
                var progress = new Progress<double>(p =>
                {
                    Dispatcher.Invoke(() => {
                        _logger?.LogInformation($"Download progress: {p}%");

                        DownloadProgressBar.Value = p;
                        // Update the message with localized text
                        UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                            ? $"Downloading: {p:F1}%"
                            : $"Letöltés: {p:F1}%";
                    });
                });

                _logger?.LogInformation($"Downloading rclone v{_targetVersion} to {downloadPath}");

                bool downloaded;
                if (_isFallbackVersion)
                {
                    downloaded = await _versionService.DownloadSpecificVersion(_targetVersion, downloadPath, progress);
                }
                else
                {
                    downloaded = await _versionService.DownloadLatestRclone(downloadPath, progress);
                }

                if (downloaded)
                {
                    // Ensure progress bar reaches 100%
                    Dispatcher.Invoke(() => {
                        DownloadProgressBar.Value = 100;
                        // Localize the status message
                        UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                            ? "Download complete. Preparing initialization..."
                            : "Letöltés befejezve. Inicializálás előkészítése...";
                    });

                    _logger?.LogInformation("Download completed, preparing initialization");

                    // Detailed initialization steps
                    Dispatcher.Invoke(() => {
                        // Localize the status message
                        UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                            ? "Verifying downloaded files..."
                            : "Letöltött fájlok ellenőrzése...";
                    });

                    bool fileValidated = await Task.Run(() => _versionService.ValidateRcloneFile(downloadPath));

                    if (!fileValidated)
                    {
                        throw new Exception("File validation failed");
                    }

                    Dispatcher.Invoke(() => {
                        // Localize the status message
                        UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                            ? "Extracting rclone files..."
                            : "Rclone fájlok kicsomagolása...";
                    });

                    Dispatcher.Invoke(() => {
                        // Localize the status message
                        UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                            ? "Initializing rclone manager..."
                            : "Rclone kezelő inicializálása...";
                    });

                    bool reinitialized = await _rcloneManager.ReinitializeAsync();

                    if (reinitialized)
                    {
                        _logger?.LogInformation("Installation/Update successful!");
                        Dispatcher.Invoke(() => {
                            // Localize the status message
                            UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                                ? "Initialization complete!"
                                : "Inicializálás befejezve!";
                        });

                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        _logger?.LogWarning("Reinitialization failed");
                        Dispatcher.Invoke(() => {
                            // Localize the status message
                            UpdateMessage = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                                ? "Initialization failed. Retrying..."
                                : "Az inicializálás sikertelen. Újrapróbálkozás...";
                        });

                        // Localize the message box text
                        string messageText = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                            ? "The download was successful but failed to initialize. Please try restarting the application."
                            : "A letöltés sikeres volt, de az inicializálás sikertelen. Kérjük, indítsa újra az alkalmazást.";

                        string messageTitle = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                            ? "Initialization Error"
                            : "Inicializálási hiba";

                        MessageBox.Show(
                            messageText,
                            messageTitle,
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );

                        DialogResult = false;
                        Close();
                    }
                }
                else
                {
                    _logger?.LogWarning("Download failed");

                    // Localize the message box text
                    string messageText = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                        ? "Failed to download the required files. Please check your internet connection and try again."
                        : "A szükséges fájlok letöltése sikertelen. Kérjük, ellenőrizze az internetkapcsolatot és próbálja újra.";

                    string messageTitle = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                        ? "Download Error"
                        : "Letöltési hiba";

                    MessageBox.Show(
                        messageText,
                        messageTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    DialogResult = false;
                    Close();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during download");

                // Localize the message box text
                string messageText = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                    ? $"An error occurred during download: {ex.Message}"
                    : $"Hiba történt a letöltés során: {ex.Message}";

                string messageTitle = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                    ? "Download Error"
                    : "Letöltési hiba";

                MessageBox.Show(
                    messageText,
                    messageTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                DialogResult = false;
                Close();
            }
        }
    }
}