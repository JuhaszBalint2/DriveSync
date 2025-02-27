using DriveSync.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DriveSync.WPF
{
    /// <summary>
    /// Interaction logic for UpdateAvailableWindow.xaml
    /// </summary>
    public partial class UpdateAvailableWindow : Window
    {
        private readonly IRcloneVersionService _versionService;
        private readonly ILogger<UpdateAvailableWindow> _logger;

        public UpdateAvailableWindow(string currentVersion, string latestVersion)
        {
            InitializeComponent();

            _versionService = App.ServiceProvider.GetService<IRcloneVersionService>();
            _logger = App.ServiceProvider.GetService<ILoggerFactory>()
                .CreateLogger<UpdateAvailableWindow>();

            DataContext = this;
            UpdateMessage = $"Current Version: {currentVersion}\nLatest Version: {latestVersion}";

            Loaded += UpdateAvailableWindow_Loaded;
        }

        public string UpdateMessage { get; set; }

        private async void UpdateAvailableWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string downloadPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DriveSync",
                    "RcloneVersions",
                    $"rclone-v{latestVersion}-windows-amd64.zip"
                );

                var progress = new Progress<double>(p =>
                {
                    // Update progress if needed
                });

                bool downloaded = await _versionService.DownloadLatestRclone(downloadPath, progress);

                if (downloaded)
                {
                    // Show success message and close
                    MessageBox.Show(
                        "Rclone has been successfully updated. You can now use the application.",
                        "Update Complete",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show(
                        "Failed to download the update. Please try again later.",
                        "Update Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                    DialogResult = false;
                    Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during rclone update");
                MessageBox.Show(
                    $"An error occurred during update: {ex.Message}",
                    "Update Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                DialogResult = false;
                Close();
            }
        }
    }
}