using System;
using System.IO;
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

        public UpdateAvailableWindow(string currentVersion, string latestVersion)
        {
            InitializeComponent();

            _versionService = App.ServiceProvider.GetService<IRcloneVersionService>();
            _rcloneManager = App.ServiceProvider.GetService<RcloneManager>();
            _logger = App.ServiceProvider.GetService<ILoggerFactory>()
                .CreateLogger<UpdateAvailableWindow>();

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

        public string UpdateMessage { get; set; }

        private async void UpdateAvailableWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DriveSync",
                    "RcloneVersions"
                );

                Directory.CreateDirectory(baseDirectory);

                string downloadPath = Path.Combine(
                    baseDirectory,
                    $"rclone-v{_latestVersion}-windows-amd64.zip"
                );

                var progress = new Progress<double>(p =>
                {
                    Dispatcher.Invoke(() => {
                        // You can update a progress bar here if needed
                    });
                });

                bool downloaded = await _versionService.DownloadLatestRclone(downloadPath, progress);

                if (downloaded)
                {
                    // Reinitialize RcloneManager with new version
                    bool reinitialized = await _rcloneManager.ReinitializeAsync();

                    if (reinitialized)
                    {
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        // Attempt rollback
                        await HandleUpdateFailure();
                    }
                }
                else
                {
                    await HandleUpdateFailure();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rclone update");
                await HandleUpdateFailure();
            }
        }

        private async Task HandleUpdateFailure()
        {
            try
            {
                // Attempt to rollback to previous version
                bool rolledBack = await _versionService.RollbackToVersion(_currentVersion);

                if (rolledBack)
                {
                    MessageBox.Show(
                        "Update failed. The previous version has been restored.",
                        "Update Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                }
                else
                {
                    MessageBox.Show(
                        "Update failed and rollback was unsuccessful. Please reinstall the application.",
                        "Critical Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }

                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during update rollback");
                MessageBox.Show(
                    "A critical error occurred. The application will now close.",
                    "Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Application.Current.Shutdown();
            }
        }
    }
}