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
                var (isUpdateAvailable, latestVersion, currentVersion) = await versionService.CheckForUpdate();

                if (isUpdateAvailable)
                {
                    // Show blocking update window
                    var updateWindow = new UpdateAvailableWindow(currentVersion, latestVersion);
                    updateWindow.Owner = null;
                    bool? updateResult = updateWindow.ShowDialog();

                    if (updateResult != true)
                    {
                        // If update fails, attempt to rollback
                        bool rolledBack = await versionService.RollbackToVersion(currentVersion);

                        if (!rolledBack)
                        {
                            // If rollback fails, show critical error and shutdown
                            MessageBox.Show(
                                "Update failed and rollback was unsuccessful. Please reinstall the application.",
                                "Critical Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error
                            );
                            Shutdown();
                            return;
                        }
                    }
                }

                // Continue with normal startup
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
                var mainWindow = ServiceProvider.GetService<MainWindow>();
                mainWindow?.Show();
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