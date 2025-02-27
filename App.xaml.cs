using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using DriveSync.Infrastructure.Services;
using DriveSync.WPF.ViewModels;
using DriveSync.WPF.Views;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace DriveSync.WPF
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDPIAware();

        [DllImport("shcore.dll", SetLastError = true)]
        private static extern int SetProcessDpiAwareness(PROCESS_DPI_AWARENESS awareness);

        private enum PROCESS_DPI_AWARENESS
        {
            Process_DPI_Unaware = 0,
            Process_System_DPI_Aware = 1,
            Process_Per_Monitor_DPI_Aware = 2
        }

        public App()
        {
            if (Environment.OSVersion.Version >= new Version(6, 3, 0))
            {
                try
                {
                    SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);
                }
                catch (Exception)
                {
                    SetProcessDPIAware();
                }
            }
            else if (Environment.OSVersion.Version >= new Version(6, 0))
            {
                SetProcessDPIAware();
            }

            InitializeComponent();
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            var settings = AppSettings.Load();
            if (settings.UseSystemTheme)
            {
                SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
            }
        }

        private void ConfigureServices(ServiceCollection services)
        {
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Register RcloneVersionService first
            services.AddSingleton<IRcloneVersionService, RcloneVersionService>();

            // Then register RcloneManager that depends on it
            services.AddSingleton<RcloneManager>();

            // Register other services
            services.AddSingleton<IRcloneService, RcloneService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Set shutdown mode before any windows are shown
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (Environment.OSVersion.Version >= new Version(10, 0, 15063))
            {
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
            }

            var rcloneManager = ServiceProvider.GetService<RcloneManager>();
            var versionService = ServiceProvider.GetService<IRcloneVersionService>();
            var logger = ServiceProvider.GetService<ILoggerFactory>().CreateLogger<App>();

            try
            {
                // Check for rclone updates
                logger.LogInformation("Checking for rclone updates...");
                var checkResult = await versionService.CheckForUpdate();
                bool isUpdateAvailable = checkResult.IsUpdateAvailable;
                string latestVersion = checkResult.LatestVersion;
                string currentVersion = checkResult.CurrentVersion;

                // Check if the current version exists locally
                string baseDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DriveSync",
                    "RcloneVersions"
                );

                // Check paths for both current and latest versions
                string currentVersionPath = !string.IsNullOrEmpty(currentVersion)
                    ? Path.Combine(baseDirectory, $"v{currentVersion}", "rclone.exe")
                    : string.Empty;

                string latestVersionPath = !string.IsNullOrEmpty(latestVersion)
                    ? Path.Combine(baseDirectory, $"v{latestVersion}", "rclone.exe")
                    : string.Empty;

                bool currentVersionExists = !string.IsNullOrEmpty(currentVersionPath) && File.Exists(currentVersionPath);
                bool latestVersionExists = !string.IsNullOrEmpty(latestVersionPath) && File.Exists(latestVersionPath);

                logger.LogInformation($"Version check: Current: {currentVersion} (exists: {currentVersionExists}), " +
                                    $"Latest: {latestVersion} (exists: {latestVersionExists})");

                // We need to download the latest version if:
                // 1. An update is available (newer version exists and not installed), OR
                // 2. We don't have ANY version installed locally
                bool needToDownload = isUpdateAvailable || (!currentVersionExists && !latestVersionExists);

                if (needToDownload)
                {
                    logger.LogInformation($"Download needed: {(isUpdateAvailable ? "Update available" : "No local version found")}");

                    // Show blocking update window
                    var updateWindow = new UpdateAvailableWindow(currentVersion, latestVersion);
                    updateWindow.Owner = null; // No owner since MainWindow isn't created yet
                    bool? result = updateWindow.ShowDialog();

                    logger.LogInformation($"Update dialog result: {result}");

                    if (result != true)
                    {
                        logger.LogWarning("Download was not successful");
                        // If we have no version at all, we can't continue
                        if (!currentVersionExists && !latestVersionExists)
                        {
                            logger.LogError("No local version available, application will now exit");
                            MessageBox.Show(
                                "Failed to download required files. Please check your internet connection and try again.",
                                "Critical Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                            Shutdown();
                            return;
                        }
                    }
                    else
                    {
                        logger.LogInformation("Download was successful, continuing with startup");
                    }
                }
                else
                {
                    logger.LogInformation("No download needed, using existing version");
                }

                // Continue with normal startup
                logger.LogInformation("Initializing rclone manager...");
                await rcloneManager.InitializeAsync();

                var settings = AppSettings.Load();
                var mainViewModel = ServiceProvider.GetService<MainViewModel>();

                if (mainViewModel != null)
                {
                    // Apply theme settings
                    string themeToApply = settings.UseSystemTheme ?
                        AppSettings.DetectSystemTheme() :
                        settings.Theme;

                    logger.LogInformation($"Applying theme: {themeToApply}");
                    mainViewModel.ApplyTheme(themeToApply);
                }

                // Show the main window
                logger.LogInformation("Starting main window...");
                var mainWindow = ServiceProvider.GetService<MainWindow>();
                MainWindow = mainWindow;

                // Show main window
                mainWindow?.Show();

                // After showing the main window, set shutdown mode to close when main window closes
                ShutdownMode = ShutdownMode.OnMainWindowClose;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during startup");
                MessageBox.Show(
                    $"A critical error occurred during startup: {ex.Message}\n\n" +
                    "The application will now close.",
                    "Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Shutdown();
            }
        }

        private string ExtractVersionFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "unknown";

            var match = Regex.Match(path, @"v(\d+\.\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : "unknown";
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Color)
            {
                var settings = AppSettings.Load();
                if (settings.UseSystemTheme)
                {
                    Dispatcher.Invoke(() =>
                    {
                        var mainViewModel = ServiceProvider.GetService<MainViewModel>();
                        string systemTheme = AppSettings.DetectSystemTheme();
                        mainViewModel?.ApplyTheme(systemTheme);
                    });
                }
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            base.OnExit(e);
        }
    }
}