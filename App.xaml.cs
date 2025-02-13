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

namespace DriveSync.WPF
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }

        // DPI-related Win32 API imports
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
            // Enable proper DPI scaling
            if (Environment.OSVersion.Version >= new Version(6, 3, 0))
            {
                // Windows 8.1 and above - Per Monitor DPI aware
                try
                {
                    SetProcessDpiAwareness(PROCESS_DPI_AWARENESS.Process_Per_Monitor_DPI_Aware);
                }
                catch (Exception)
                {
                    // Fallback to older DPI awareness method
                    SetProcessDPIAware();
                }
            }
            else if (Environment.OSVersion.Version >= new Version(6, 0))
            {
                // Windows Vista and above - System DPI aware
                SetProcessDPIAware();
            }

            InitializeComponent();
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Only subscribe to system theme changes if using system theme
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

            services.AddSingleton<IRcloneVersionService, RcloneVersionService>();
            services.AddSingleton<IRcloneService, RcloneService>();
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MainWindow>();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Enable DPI change awareness for WPF
            if (Environment.OSVersion.Version >= new Version(10, 0, 15063))
            {
                System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
            }

            var settings = AppSettings.Load();
            var mainViewModel = ServiceProvider.GetService<MainViewModel>();
            var logger = ServiceProvider.GetService<ILoggerFactory>().CreateLogger<App>();

            try
            {
                // Rest of your startup code remains the same
                if (mainViewModel != null)
                {
                    string themeToApply = settings.UseSystemTheme ?
                        AppSettings.DetectSystemTheme() :
                        settings.Theme;

                    logger.LogInformation($"OnStartup - UseSystemTheme: {settings.UseSystemTheme}, Theme: {settings.Theme}, Applying: {themeToApply}");
                    mainViewModel.ApplyTheme(themeToApply);
                }

                var versionService = ServiceProvider.GetService<IRcloneVersionService>();
                var (isUpdateAvailable, latestVersion, currentVersion) = await versionService.CheckRcloneVersion();

                if (mainViewModel != null)
                {
                    if (isUpdateAvailable)
                    {
                        mainViewModel.UpdateMessage = $"rclone v{currentVersion} → v{latestVersion} available";
                        var result = MessageBox.Show(
                            $"A new version of rclone is available.\n\n" +
                            $"Current version: {currentVersion}\n" +
                            $"Latest version: {latestVersion}\n\n" +
                            "Would you like to download the latest version?",
                            "Update Available",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information
                        );

                        if (result == MessageBoxResult.Yes)
                        {
                            string downloadPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "DriveSync",
                                $"rclone-v{latestVersion}-windows-amd64.zip"
                            );

                            Directory.CreateDirectory(Path.GetDirectoryName(downloadPath));

                            bool downloadSuccess = await versionService.DownloadLatestRclone(downloadPath);

                            if (downloadSuccess)
                            {
                                MessageBox.Show(
                                    $"Rclone {latestVersion} downloaded to {downloadPath}. " +
                                    "Please extract and replace your existing rclone executable.",
                                    "Download Complete",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Information
                                );
                            }
                            else
                            {
                                MessageBox.Show(
                                    "Failed to download the latest rclone version.",
                                    "Download Error",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error
                                );
                            }
                        }
                    }
                    else
                    {
                        mainViewModel.UpdateMessage = $"rclone v{currentVersion}";
                    }
                }

                // Show the main window after theme is applied
                var mainWindow = ServiceProvider.GetService<MainWindow>();
                mainWindow?.Show();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during startup");
                var mainWindow = ServiceProvider.GetService<MainWindow>();
                mainWindow?.Show();
            }
        }

        private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            // Check if the change is related to color settings and we're using system theme
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