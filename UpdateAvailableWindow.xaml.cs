using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using DriveSync.Infrastructure.Services;
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public UpdateAvailableWindow(string currentVersion, string targetVersion, bool isFallbackVersion = false)
        {
            InitializeComponent();

            _versionService = App.ServiceProvider.GetService<IRcloneVersionService>();
            _rcloneManager = App.ServiceProvider.GetService<RcloneManager>();
            _logger = App.ServiceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger<UpdateAvailableWindow>();

            _currentVersion = currentVersion;
            _targetVersion = targetVersion;
            _isFallbackVersion = isFallbackVersion;

            // Check if this is an initial install (no local version)
            string baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DriveSync",
                "RcloneVersions"
            );

            // Check if any version exists
            bool anyVersionExists = Directory.Exists(baseDirectory) &&
                Directory.GetDirectories(baseDirectory, "v*").Length > 0;

            _isInitialInstall = !anyVersionExists;

            DataContext = this;

            // Set initial update message
            UpdateMessage = _isInitialInstall
                ? $"Rclone needs to be installed.\nDownloading version: {targetVersion}"
                : (_isFallbackVersion
                    ? $"Latest version download failed. Trying alternative version.\nDownloading version: {targetVersion}"
                    : $"Updating rclone\nCurrent Version: {currentVersion}\nLatest Version: {targetVersion}");

            // Prevent window from being closed by user
            Closing += (s, e) =>
            {
                if (DialogResult != true)
                {
                    e.Cancel = true;
                }
            };

            Loaded += UpdateAvailableWindow_Loaded;
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
                        UpdateMessage = $"Downloading: {p:F1}%";
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
                        UpdateMessage = "Download complete. Preparing initialization...";
                    });

                    _logger?.LogInformation("Download completed, preparing initialization");

                    // Detailed initialization steps
                    Dispatcher.Invoke(() => {
                        UpdateMessage = "Verifying downloaded files...";
                    });
                    bool fileValidated = await Task.Run(() => _versionService.ValidateRcloneFile(downloadPath));

                    if (!fileValidated)
                    {
                        throw new Exception("File validation failed");
                    }

                    Dispatcher.Invoke(() => {
                        UpdateMessage = "Extracting rclone files...";
                    });

                    Dispatcher.Invoke(() => {
                        UpdateMessage = "Initializing rclone manager...";
                    });
                    bool reinitialized = await _rcloneManager.ReinitializeAsync();

                    if (reinitialized)
                    {
                        _logger?.LogInformation("Installation/Update successful!");
                        Dispatcher.Invoke(() => {
                            UpdateMessage = "Initialization complete!";
                        });
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        _logger?.LogWarning("Reinitialization failed");
                        Dispatcher.Invoke(() => {
                            UpdateMessage = "Initialization failed. Retrying...";
                        });

                        MessageBox.Show(
                            "The download was successful but failed to initialize. Please try restarting the application.",
                            "Initialization Error",
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
                    MessageBox.Show(
                        "Failed to download the required files. Please check your internet connection and try again.",
                        "Download Error",
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
                MessageBox.Show(
                    $"An error occurred during download: {ex.Message}",
                    "Download Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                DialogResult = false;
                Close();
            }
        }
    }
}