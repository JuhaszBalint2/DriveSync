using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
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

            // Set initial update message based on installation type and localization
            UpdateUIMessages();

            // Initially, show only the countdown and update panel
            LocalizationPanel.Visibility = Visibility.Collapsed;
            UpdatePanel.Visibility = Visibility.Visible;
            CountdownTextBlock.Visibility = Visibility.Visible;

            // Initialize countdown
            InitializeCountdown();

            // Add the Loaded event handler
            Loaded += (s, e) =>
            {
                // We don't want to start the download immediately on load
                // Only the countdown should start now
            };
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

        // New method to handle the close button click
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ShowCloseWarningDialog();
        }

        // Method to display the custom warning dialog
        // Method to display the custom warning dialog
        private void ShowCloseWarningDialog()
        {
            // Determine if using dark theme for proper styling
            var settings = AppSettings.Load();
            bool isDarkTheme = settings.GetEffectiveTheme().Equals("Dark", StringComparison.OrdinalIgnoreCase);

            // Create the styled message box window
            var dialog = new Window
            {
                Width = 450,
                Height = 220,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            // Set up the dialog content
            var mainBorder = new Border
            {
                Background = isDarkTheme ? new SolidColorBrush(Color.FromRgb(48, 48, 48)) : new SolidColorBrush(Color.FromRgb(250, 250, 250)),
                BorderBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(97, 97, 97)) : new SolidColorBrush(Color.FromRgb(221, 221, 221)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Opacity = 0.3,
                    BlurRadius = 15,
                    ShadowDepth = 2
                }
            };

            var contentGrid = new Grid
            {
                Margin = new Thickness(20)
            };

            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Warning icon
            var iconBorder = new Border
            {
                Width = 48,
                Height = 48,
                VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Create a warning triangle icon using a Canvas and shapes
            var canvas = new Canvas
            {
                Width = 32,
                Height = 32
            };

            var triangle = new Polygon
            {
                Points = new PointCollection { new Point(16, 0), new Point(32, 32), new Point(0, 32) },
                Fill = new SolidColorBrush(Color.FromRgb(255, 204, 0))
            };

            var exclamation = new TextBlock
            {
                Text = "!",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Canvas.SetLeft(exclamation, 13);
            Canvas.SetTop(exclamation, 4);

            canvas.Children.Add(triangle);
            canvas.Children.Add(exclamation);
            iconBorder.Child = canvas;

            Grid.SetRow(iconBorder, 0);
            Grid.SetRowSpan(iconBorder, 2);

            // Message text based on current language - Now bold but with original positioning
            var messageText = LocalizationManager.Instance.CurrentLanguage == AppLanguage.English
                ? "The application may not function correctly until it is updated."
                : "Az alkalmazás esetleg nem fog megfelelően működni, amíg nem frissítik.";

            var messageTextBlock = new TextBlock
            {
                Text = messageText,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(60, 0, 0, 15),
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(messageTextBlock, 0);

            // Countdown Text - Centered with bold and seconds underlined
            var countdownTextBlock = new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = isDarkTheme ? Brushes.White : new SolidColorBrush(Color.FromRgb(33, 33, 33)),
                Margin = new Thickness(0, 10, 0, 15)
            };
            Grid.SetRow(countdownTextBlock, 1);

            // OK Button
            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                Background = isDarkTheme ? new SolidColorBrush(Color.FromRgb(64, 64, 64)) : new SolidColorBrush(Color.FromRgb(225, 225, 225)),
                Foreground = isDarkTheme ? Brushes.White : Brushes.Black,
                BorderThickness = new Thickness(1),
                BorderBrush = isDarkTheme ? new SolidColorBrush(Color.FromRgb(97, 97, 97)) : new SolidColorBrush(Color.FromRgb(173, 173, 173))
            };
            Grid.SetRow(okButton, 2);

            okButton.Click += (s, e) =>
            {
                dialog.Close();
                AbortUpdateAndCleanup();
            };

            contentGrid.Children.Add(iconBorder);
            contentGrid.Children.Add(messageTextBlock);
            contentGrid.Children.Add(countdownTextBlock);
            contentGrid.Children.Add(okButton);

            mainBorder.Child = contentGrid;
            dialog.Content = mainBorder;

            // Create countdown timer
            var countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };

            int secondsRemaining = 4;

            countdownTimer.Tick += (s, e) =>
            {
                secondsRemaining--;

                // Create text with formatted seconds (bold and underlined)
                if (LocalizationManager.Instance.CurrentLanguage == AppLanguage.English)
                {
                    countdownTextBlock.Inlines.Clear();
                    countdownTextBlock.Inlines.Add("Closing in ");
                    var secondsRun = new Run(secondsRemaining.ToString())
                    {
                        TextDecorations = TextDecorations.Underline
                    };
                    countdownTextBlock.Inlines.Add(secondsRun);
                    countdownTextBlock.Inlines.Add(" seconds...");
                }
                else // Hungarian
                {
                    countdownTextBlock.Inlines.Clear();
                    countdownTextBlock.Inlines.Add("Bezárás ");
                    var secondsRun = new Run(secondsRemaining.ToString())
                    {
                        TextDecorations = TextDecorations.Underline
                    };
                    countdownTextBlock.Inlines.Add(secondsRun);
                    countdownTextBlock.Inlines.Add(" másodperc múlva...");
                }

                if (secondsRemaining <= 0)
                {
                    countdownTimer.Stop();
                    dialog.Close();
                    AbortUpdateAndCleanup();
                }
            };

            // Set initial countdown text with formatted seconds (bold and underlined)
            if (LocalizationManager.Instance.CurrentLanguage == AppLanguage.English)
            {
                countdownTextBlock.Inlines.Clear();
                countdownTextBlock.Inlines.Add("Closing in ");
                var secondsRun = new Run(secondsRemaining.ToString())
                {
                    TextDecorations = TextDecorations.Underline
                };
                countdownTextBlock.Inlines.Add(secondsRun);
                countdownTextBlock.Inlines.Add(" seconds...");
            }
            else // Hungarian
            {
                countdownTextBlock.Inlines.Clear();
                countdownTextBlock.Inlines.Add("Bezárás ");
                var secondsRun = new Run(secondsRemaining.ToString())
                {
                    TextDecorations = TextDecorations.Underline
                };
                countdownTextBlock.Inlines.Add(secondsRun);
                countdownTextBlock.Inlines.Add(" másodperc múlva...");
            }

            // Start countdown when dialog is shown
            dialog.Loaded += (s, e) => countdownTimer.Start();

            // Show the dialog
            dialog.ShowDialog();
        }

        // Method to abort the update and cleanup rclone versions
        private async void AbortUpdateAndCleanup()
        {
            try
            {
                string rcloneVersionsPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DriveSync",
                    "RcloneVersions"
                );

                _logger?.LogInformation($"Deleting rclone versions directory: {rcloneVersionsPath}");

                // Queue the deletion process to run asynchronously
                await Task.Run(() =>
                {
                    if (System.IO.Directory.Exists(rcloneVersionsPath))
                    {
                        try
                        {
                            // Use a more aggressive approach to ensure files are deleted
                            foreach (var dir in System.IO.Directory.GetDirectories(rcloneVersionsPath))
                            {
                                try
                                {
                                    // Try to make files writable before deletion
                                    foreach (var file in System.IO.Directory.GetFiles(dir, "*", System.IO.SearchOption.AllDirectories))
                                    {
                                        try
                                        {
                                            System.IO.File.SetAttributes(file, System.IO.FileAttributes.Normal);
                                        }
                                        catch
                                        {
                                            // Continue even if setting attributes fails
                                        }
                                    }

                                    System.IO.Directory.Delete(dir, true);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, $"Error deleting directory: {dir}");
                                }
                            }

                            foreach (var file in System.IO.Directory.GetFiles(rcloneVersionsPath))
                            {
                                try
                                {
                                    System.IO.File.SetAttributes(file, System.IO.FileAttributes.Normal);
                                    System.IO.File.Delete(file);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, $"Error deleting file: {file}");
                                }
                            }

                            // Then try to delete the main directory
                            try
                            {
                                System.IO.Directory.Delete(rcloneVersionsPath, true);
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, $"Error deleting main directory: {rcloneVersionsPath}");

                                // If we can't delete the directory, try to at least rename it to indicate it's no longer valid
                                try
                                {
                                    string invalidDir = System.IO.Path.Combine(
                                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                        "DriveSync",
                                        "RcloneVersions_Invalid_" + DateTime.Now.ToString("yyyyMMddHHmmss")
                                    );

                                    System.IO.Directory.Move(rcloneVersionsPath, invalidDir);
                                }
                                catch
                                {
                                    // Ignore if rename fails
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error cleaning up directory structure");
                        }
                    }
                });

                // Close the application completely
                DialogResult = false;
                Close();

                // Shutdown the application entirely
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during cleanup");

                // Close the application even if cleanup fails
                DialogResult = false;
                Close();

                // Shutdown the application entirely
                Application.Current.Shutdown();
            }
        }

        // Check if a specific version exists on disk
        private bool CheckIfVersionExists(string version)
        {
            if (string.IsNullOrEmpty(version)) return false;

            string baseDirectory = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DriveSync",
                "RcloneVersions"
            );

            string versionPath = System.IO.Path.Combine(baseDirectory, $"v{version}", "rclone.exe");
            return System.IO.File.Exists(versionPath);
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
                ShowLanguagePanel();
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

        private void ShowLanguagePanel()
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

            // Start the update process immediately after language selection
            StartUpdateProcess();
        }

        private void StartUpdateProcess()
        {
            // Directly start the update process after language selection
            UpdateAvailableWindow_Loaded(this, null);
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

                string baseDirectory = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DriveSync",
                    "RcloneVersions"
                );

                if (!System.IO.Directory.Exists(baseDirectory))
                {
                    System.IO.Directory.CreateDirectory(baseDirectory);
                }

                string downloadPath = System.IO.Path.Combine(
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