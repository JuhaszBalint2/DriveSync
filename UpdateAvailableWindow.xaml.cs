using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using DriveSync.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DriveSync.WPF.Views
{
    public partial class UpdateAvailableWindow : Window
    {
        private readonly IRcloneVersionService _versionService;
        private readonly RcloneManager _rcloneManager;
        private readonly ILogger<UpdateAvailableWindow> _logger;
        private readonly string _currentVersion;
        private readonly string _targetVersion;
        private readonly bool _isInitialInstall;

        public string UpdateMessage { get; set; }

        public UpdateAvailableWindow(string currentVersion, string targetVersion)
        {
            InitializeComponent();

            _versionService = App.ServiceProvider.GetService<IRcloneVersionService>();
            _rcloneManager = App.ServiceProvider.GetService<RcloneManager>();
            _logger = App.ServiceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger<UpdateAvailableWindow>();

            _currentVersion = currentVersion;
            _targetVersion = targetVersion;

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

            // Set appropriate message based on whether this is initial install or update
            if (_isInitialInstall)
            {
                Title = "Rclone Installation";
                UpdateMessage = $"Rclone needs to be installed.\nDownloading version: {targetVersion}";
            }
            else
            {
                Title = "Rclone Update";
                UpdateMessage = $"Current Version: {currentVersion}\nLatest Version: {targetVersion}";
            }

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
                _logger?.LogInformation($"Starting {(_isInitialInstall ? "installation" : "update")} of version {_targetVersion}");
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

                var progress = new Progress<double>(p =>
                {
                    Dispatcher.Invoke(() => {
                        _logger?.LogInformation($"Download progress: {p}%");
                        // You can update a progress bar here if needed
                    });
                });

                _logger?.LogInformation($"Downloading rclone v{_targetVersion} to {downloadPath}");
                bool downloaded = await _versionService.DownloadLatestRclone(downloadPath, progress);

                if (downloaded)
                {
                    _logger?.LogInformation("Download completed, reinitializing RcloneManager");
                    // Reinitialize RcloneManager with new version
                    bool reinitialized = await _rcloneManager.ReinitializeAsync();

                    if (reinitialized)
                    {
                        _logger?.LogInformation("Installation/Update successful!");
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        _logger?.LogWarning("Reinitialization failed");
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