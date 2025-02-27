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
        private readonly string _latestVersion;

        public string UpdateMessage { get; set; }

        public UpdateAvailableWindow(string currentVersion, string latestVersion)
        {
            InitializeComponent();

            _versionService = App.ServiceProvider.GetService<IRcloneVersionService>();
            _rcloneManager = App.ServiceProvider.GetService<RcloneManager>();
            _logger = App.ServiceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger<UpdateAvailableWindow>();

            _currentVersion = currentVersion;
            _latestVersion = latestVersion;

            DataContext = this;
            UpdateMessage = $"Current Version: {currentVersion}\nLatest Version: {latestVersion}";

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
                _logger?.LogInformation($"Starting update from {_currentVersion} to {_latestVersion}");
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
                    $"rclone-v{_latestVersion}-windows-amd64.zip"
                );

                var progress = new Progress<double>(p =>
                {
                    Dispatcher.Invoke(() => {
                        _logger?.LogInformation($"Download progress: {p}%");
                        // You can update a progress bar here if needed
                    });
                });

                _logger?.LogInformation($"Downloading rclone v{_latestVersion} to {downloadPath}");
                bool downloaded = await _versionService.DownloadLatestRclone(downloadPath, progress);

                if (downloaded)
                {
                    _logger?.LogInformation("Download completed, reinitializing RcloneManager");
                    // Reinitialize RcloneManager with new version
                    bool reinitialized = await _rcloneManager.ReinitializeAsync();

                    if (reinitialized)
                    {
                        _logger?.LogInformation("Update successful!");
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        _logger?.LogWarning("Reinitialization failed, attempting rollback");
                        // Attempt rollback
                        await HandleUpdateFailure();
                    }
                }
                else
                {
                    _logger?.LogWarning("Download failed, attempting rollback");
                    await HandleUpdateFailure();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during rclone update");
                await HandleUpdateFailure();
            }
        }

        private async Task HandleUpdateFailure()
        {
            try
            {
                _logger?.LogInformation($"Attempting to rollback to version {_currentVersion}");
                // Attempt to rollback to previous version
                bool rolledBack = await _versionService.RollbackToVersion(_currentVersion);

                if (rolledBack)
                {
                    _logger?.LogInformation("Rollback successful");
                    MessageBox.Show(
                        "Update failed. The previous version has been restored.",
                        "Update Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    // Continue application instead of shutting down
                    DialogResult = false;
                    Close();
                }
                else
                {
                    _logger?.LogError("Rollback failed");
                    MessageBox.Show(
                        "Update failed and rollback was unsuccessful. The application will now close.",
                        "Critical Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );

                    DialogResult = false;
                    Close();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during update rollback");
                MessageBox.Show(
                    "A critical error occurred during update. The application will now close.",
                    "Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );

                DialogResult = false;
                Close();
            }
        }
    }
}