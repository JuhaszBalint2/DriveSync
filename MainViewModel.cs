using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DriveSync.Infrastructure.Services;
using DriveSync.WPF.Localization;
using DriveSync.WPF.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DriveSync.WPF.ViewModels
{
    public class SyncTypeOption : INotifyPropertyChanged
    {
        private string displayName;
        public string DisplayName
        {
            get => displayName;
            set
            {
                if (displayName != value)
                {
                    displayName = value;
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }
        public SyncType Value { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void UpdateLocalization()
        {
            DisplayName = Value switch
            {
                SyncType.Mirror => LocalizationManager.Instance["SyncModeOption1"],
                SyncType.Backup => LocalizationManager.Instance["SyncModeOption2"],
                SyncType.Move => LocalizationManager.Instance["SyncModeOption3"],
                _ => DisplayName
            };
        }
    }

    [ObservableObject]
    public partial class MainViewModel
    {
        // Dependency and core service fields
        private readonly IRcloneService _rcloneService;
        private readonly ILogger<MainViewModel> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly RcloneManager _rcloneManager;
        private CancellationTokenSource _syncCancellationTokenSource;

        // Progress tracking constants
        private double _lastReportedProgress;
        private DateTime _lastProgressUpdateTime;
        private const double MINIMUM_PROGRESS_STEP = 0.1;
        private const int MINIMUM_UPDATE_INTERVAL_MS = 100;

        // Status indicator color constants
        private static readonly SolidColorBrush ColorCheck = new(Colors.Blue);
        private static readonly SolidColorBrush ColorCopy = new(Colors.Green);
        private static readonly SolidColorBrush ColorDelete = new(Colors.Red);
        private static readonly SolidColorBrush ColorSkip = new(Colors.Orange);
        private static readonly SolidColorBrush ColorScanning = new(Colors.Purple);
        private static readonly SolidColorBrush ColorUpdate = new(Colors.Teal);

        // History file configuration
        private const string HistoryFileName = "syncHistory.json";

        // Observable collections and properties for UI binding
        [ObservableProperty]
        private ObservableCollection<string> availableRemotes = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string selectedSourceRemote;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string selectedTargetRemote;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string sourcePath;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsValid))]
        private string targetPath;

        // Status and progress properties
        [ObservableProperty]
        private string statusMessage = "Loading remotes...";

        [ObservableProperty]
        private string updateMessage = "";

        [ObservableProperty]
        private string currentSyncOperation;

        [ObservableProperty]
        private bool isSyncing;

        [ObservableProperty]
        private double progressValue;

        [ObservableProperty]
        private string currentFile = "";

        [ObservableProperty]
        private string currentSpeed = "";

        [ObservableProperty]
        private string remainingTime = "";

        [ObservableProperty]
        private string progressPercentage = "";

        [ObservableProperty]
        private ObservableCollection<SyncHistoryItem> syncHistory = new();

        [ObservableProperty]
        private Brush statusIndicatorBrush = new SolidColorBrush(Colors.Blue);

        [ObservableProperty]
        private ObservableCollection<SyncTypeOption> availableSyncModes = new();

        [ObservableProperty]
        private SyncTypeOption selectedSyncMode;

        [ObservableProperty]
        private string buttonText;

        [ObservableProperty]
        private bool isUpdateAvailable;

        [ObservableProperty]
        private bool isCheckingForUpdates;

        partial void OnIsSyncingChanged(bool value)
        {
            ButtonText = value ?
                LocalizationManager.Instance["Cancel"] :
                LocalizationManager.Instance["SyncNow"];
        }

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(SelectedSourceRemote) &&
            !string.IsNullOrWhiteSpace(SelectedTargetRemote) &&
            !string.IsNullOrWhiteSpace(SourcePath) &&
            !string.IsNullOrWhiteSpace(TargetPath);

        public MainViewModel(
            IRcloneService rcloneService,
            ILogger<MainViewModel> logger,
            ILoggerFactory loggerFactory,
            RcloneManager rcloneManager)
        {
            _rcloneService = rcloneService;
            _logger = logger;
            _loggerFactory = loggerFactory;
            _rcloneManager = rcloneManager;

            // Subscribe to RcloneManager events
            _rcloneManager.DownloadProgress += (sender, progress) =>
            {
                StatusMessage = $"Downloading rclone update: {progress:F1}%";
                StatusIndicatorBrush = ColorUpdate;
            };

            _rcloneManager.InitializationError += (sender, message) =>
            {
                StatusMessage = $"Rclone initialization error: {message}";
                StatusIndicatorBrush = ColorDelete;
            };

            _rcloneManager.RclonePathChanged += (sender, path) =>
            {
                string version = ExtractVersionFromPath(path);
                UpdateMessage = $"rclone v{version}";
                StatusMessage = $"{AvailableRemotes.Count} felhő tárhely betöltve"; // Changed from "Using rclone v{version}"
                StatusIndicatorBrush = ColorCheck;
            };

            AvailableSyncModes = new ObservableCollection<SyncTypeOption>
            {
                new SyncTypeOption { DisplayName = LocalizationManager.Instance["SyncModeOption1"], Value = SyncType.Mirror },
                new SyncTypeOption { DisplayName = LocalizationManager.Instance["SyncModeOption2"], Value = SyncType.Backup },
                new SyncTypeOption { DisplayName = LocalizationManager.Instance["SyncModeOption3"], Value = SyncType.Move }
            };

            ButtonText = LocalizationManager.Instance["SyncNow"];

            LocalizationManager.Instance.PropertyChanged += (s, e) =>
            {
                if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == "CurrentLanguage")
                {
                    UpdateSyncModeDisplayNames();
                    ButtonText = IsSyncing ?
                        LocalizationManager.Instance["Cancel"] :
                        LocalizationManager.Instance["SyncNow"];
                }
            };

            var settings = AppSettings.Load();

            // Determine the correct sync mode based on the saved DefaultSyncMode
            SelectedSyncMode = AvailableSyncModes
                .FirstOrDefault(x =>
                    x.DisplayName == settings.DefaultSyncMode ||
                    (x.Value == SyncType.Mirror && settings.DefaultSyncMode == "Mirror Sync") ||
                    (x.Value == SyncType.Backup && settings.DefaultSyncMode == "Backup (Copy)") ||
                    (x.Value == SyncType.Move && settings.DefaultSyncMode == "Move Files") ||
                    (x.Value == SyncType.Mirror && settings.DefaultSyncMode == "Tükrözéses szinkronizálás") ||
                    (x.Value == SyncType.Backup && settings.DefaultSyncMode == "Biztonsági mentés (Másolás)") ||
                    (x.Value == SyncType.Move && settings.DefaultSyncMode == "Fájlok áthelyezése")
                ) ?? AvailableSyncModes.First();

            // Detect system theme and update settings if needed
            string detectedTheme = AppSettings.DetectSystemTheme();
            if (!string.Equals(settings.Theme, detectedTheme, StringComparison.OrdinalIgnoreCase))
            {
                settings.Theme = detectedTheme;
                settings.Save();
            }

            _logger.LogInformation($"Applying initial theme from settings: {settings.Theme}");
            ApplyTheme(settings.Theme);
            LoadRemotesAsync();
            LoadHistory();
        }

        [RelayCommand]
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                IsCheckingForUpdates = true;
                StatusMessage = "Checking for rclone updates...";
                StatusIndicatorBrush = ColorScanning;

                await _rcloneManager.ReinitializeAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
                StatusMessage = "Failed to check for updates";
                StatusIndicatorBrush = ColorDelete;
            }
            finally
            {
                IsCheckingForUpdates = false;
            }
        }

        private string ExtractVersionFromPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "unknown";

            var match = System.Text.RegularExpressions.Regex.Match(path, @"v(\d+\.\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : "unknown";
        }

        private void LoadHistory()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HistoryFileName);
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var historyItems = JsonSerializer.Deserialize<ObservableCollection<SyncHistoryItem>>(json);
                    if (historyItems != null)
                    {
                        SyncHistory = historyItems;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading sync history");
            }
        }

        private void SaveHistory()
        {
            try
            {
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, HistoryFileName);
                string json = JsonSerializer.Serialize(SyncHistory);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving sync history");
            }
        }

        [RelayCommand]
        private void OpenSyncHistoryList()
        {
            if (SyncHistory == null || SyncHistory.Count == 0)
            {
                MessageBox.Show(
                    LocalizationManager.Instance["NoSyncHistoryAvailable"],
                    LocalizationManager.Instance["SyncHistory"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            // Create a new window to display sync history
            var historyListWindow = new SyncHistoryListWindow(SyncHistory);
            historyListWindow.Owner = Application.Current.MainWindow;
            historyListWindow.ShowDialog();
        }


        [RelayCommand]
        private void OpenSettings()
        {
            var settingsWindow = new SettingsWindow(
                App.ServiceProvider.GetService<ILogger<SettingsWindow>>()
            )
            {
                Owner = Application.Current.MainWindow
            };

            if (settingsWindow.ShowDialog() == true)
            {
                var settings = AppSettings.Load();
                var newDefault = AvailableSyncModes
                    .FirstOrDefault(x => x.DisplayName.Contains(settings.DefaultSyncMode, StringComparison.OrdinalIgnoreCase));
                if (newDefault != null)
                {
                    SelectedSyncMode = newDefault;
                }
                ApplyTheme(settings.Theme);
            }
        }

        private System.Windows.Threading.DispatcherTimer _statusMessageTimer;

        [RelayCommand]
        private async Task BrowseSourceAsync()
        {
            _statusMessageTimer?.Stop();

            if (string.IsNullOrWhiteSpace(SelectedSourceRemote))
            {
                StatusMessage = LocalizationManager.Instance["PleaseSelectSourceRemoteFirst"];
                StatusIndicatorBrush = ColorDelete;

                _statusMessageTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _statusMessageTimer.Tick += (s, e) =>
                {
                    if (StatusMessage == LocalizationManager.Instance["PleaseSelectSourceRemoteFirst"])
                    {
                        StatusMessage = string.Empty;
                        StatusIndicatorBrush = ColorCheck;
                    }
                    _statusMessageTimer.Stop();
                };
                _statusMessageTimer.Start();
                return;
            }

            try
            {
                var browserLogger = _loggerFactory.CreateLogger<DirectoryBrowserViewModel>();
                var viewModel = new DirectoryBrowserViewModel(_rcloneService, browserLogger, SelectedSourceRemote);
                var dialog = new DirectoryBrowserDialog(viewModel);
                if (dialog.ShowDialog() == true)
                {
                    if (StatusMessage == LocalizationManager.Instance["PleaseSelectSourceRemoteFirst"])
                    {
                        StatusMessage = string.Empty;
                        _statusMessageTimer?.Stop();
                    }

                    SourcePath = dialog.SelectedPath;
                    StatusMessage = string.Format(
                        LocalizationManager.Instance["SelectedSourcePath"],
                        SourcePath
                    );
                    StatusIndicatorBrush = ColorCheck;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing source directory");
                StatusMessage = "Error browsing directory. Please try again.";
                StatusIndicatorBrush = ColorDelete;
            }
        }

        [RelayCommand]
        private async Task BrowseTargetAsync()
        {
            _statusMessageTimer?.Stop();

            if (string.IsNullOrWhiteSpace(SelectedTargetRemote))
            {
                StatusMessage = LocalizationManager.Instance["PleaseSelectTargetRemoteFirst"];
                StatusIndicatorBrush = ColorDelete;

                _statusMessageTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(3)
                };
                _statusMessageTimer.Tick += (s, e) =>
                {
                    if (StatusMessage == LocalizationManager.Instance["PleaseSelectTargetRemoteFirst"])
                    {
                        StatusMessage = string.Empty;
                        StatusIndicatorBrush = ColorCheck;
                    }
                    _statusMessageTimer.Stop();
                };
                _statusMessageTimer.Start();
                return;
            }

            try
            {
                var browserLogger = _loggerFactory.CreateLogger<DirectoryBrowserViewModel>();
                var viewModel = new DirectoryBrowserViewModel(_rcloneService, browserLogger, SelectedTargetRemote);
                var dialog = new DirectoryBrowserDialog(viewModel);
                if (dialog.ShowDialog() == true)
                {
                    if (StatusMessage == LocalizationManager.Instance["PleaseSelectTargetRemoteFirst"])
                    {
                        StatusMessage = string.Empty;
                        _statusMessageTimer?.Stop();
                    }

                    TargetPath = dialog.SelectedPath;
                    StatusMessage = string.Format(
                        LocalizationManager.Instance["SelectedTargetPath"],
                        TargetPath
                    );
                    StatusIndicatorBrush = ColorCheck;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error browsing target directory");
                StatusMessage = "Error browsing directory. Please try again.";
                StatusIndicatorBrush = ColorDelete;
            }
        }

        partial void OnSelectedSourceRemoteChanged(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _statusMessageTimer?.Stop();

                if (StatusMessage == LocalizationManager.Instance["PleaseSelectSourceRemoteFirst"])
                {
                    StatusMessage = string.Empty;
                }

                StatusMessage = string.Format(
                    LocalizationManager.Instance["SourceRemoteSelected"],
                    value
                );
                StatusIndicatorBrush = ColorCheck;
            }
        }

        partial void OnSelectedTargetRemoteChanged(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                _statusMessageTimer?.Stop();

                if (StatusMessage == LocalizationManager.Instance["PleaseSelectTargetRemoteFirst"])
                {
                    StatusMessage = string.Empty;
                }

                StatusMessage = string.Format(
                    LocalizationManager.Instance["TargetRemoteSelected"],
                    value
                );
                StatusIndicatorBrush = ColorCheck;
            }
        }

        [RelayCommand]
        private async Task SyncAsync()
        {
            if (IsSyncing)
            {
                _syncCancellationTokenSource?.Cancel();
                return;
            }
            if (!IsValid)
            {
                StatusMessage = LocalizationManager.Instance["InvalidSourceTarget"];
                StatusIndicatorBrush = ColorDelete;
                return;
            }

            try
            {
                IsSyncing = true;
                StatusMessage = LocalizationManager.Instance["StartingSync"];
                ProgressValue = 0;
                ProgressPercentage = string.Format(LocalizationManager.Instance["ProgressPercentage"], "0");
                CurrentSpeed = LocalizationManager.Instance["ZeroSpeed"];
                RemainingTime = LocalizationManager.Instance["CalculatingProgress"];
                CurrentFile = LocalizationManager.Instance["PreparingToSync"];
                CurrentSyncOperation = LocalizationManager.Instance["SyncInitializing"];
                StatusIndicatorBrush = ColorScanning;
                _lastReportedProgress = 0;

                _syncCancellationTokenSource = new CancellationTokenSource();
                var progress = new Progress<SyncProgress>(OnSyncProgress);

                string syncLog = await _rcloneService.SyncDirectories(
                    SelectedSourceRemote,
                    SourcePath,
                    SelectedTargetRemote,
                    TargetPath,
                    SelectedSyncMode.Value,
                    progress,
                    _syncCancellationTokenSource.Token);

                var historyItem = new SyncHistoryItem
                {
                    Timestamp = DateTime.Now,
                    Description = $"Synced {SelectedSourceRemote}:{SourcePath} -> {SelectedTargetRemote}:{TargetPath} ({SelectedSyncMode.DisplayName})",
                    Log = syncLog
                };
                SyncHistory.Insert(0, historyItem);
                while (SyncHistory.Count > 7)
                {
                    SyncHistory.RemoveAt(SyncHistory.Count - 1);
                }
                SaveHistory();

                ProgressValue = 100;
                ProgressPercentage = string.Format(LocalizationManager.Instance["ProgressPercentage"], "100");
                CurrentFile = LocalizationManager.Instance["SyncCompleted"];
                StatusMessage = LocalizationManager.Instance["SyncCompletedSuccess"];
                StatusIndicatorBrush = ColorCheck;
            }
            catch (OperationCanceledException)
            {
                StatusMessage = LocalizationManager.Instance["SyncCancelled"];
                StatusIndicatorBrush = ColorSkip;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync failed");
                StatusMessage = string.Format(LocalizationManager.Instance["SyncFailed"], ex.Message);
                StatusIndicatorBrush = ColorDelete;
            }
            finally
            {
                IsSyncing = false;
                CurrentSyncOperation = "";
                _syncCancellationTokenSource?.Dispose();
                _syncCancellationTokenSource = null;
            }
        }

        [RelayCommand]
        private void OpenSyncHistoryItem(SyncHistoryItem historyItem)
        {
            if (historyItem == null || string.IsNullOrWhiteSpace(historyItem.Log))
            {
                MessageBox.Show("No log available for this sync.", "Sync Log", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var logViewer = new LogViewerWindow(historyItem.Log, historyItem.Timestamp);
            logViewer.Owner = Application.Current.MainWindow;
            logViewer.ShowDialog();
        }

        private void UpdateSyncModeDisplayNames()
        {
            if (AvailableSyncModes != null)
            {
                foreach (var mode in AvailableSyncModes)
                {
                    mode.UpdateLocalization();
                }
            }
        }

        private string TranslateLog(string rawLog)
        {
            if (string.IsNullOrWhiteSpace(rawLog))
                return "No details available.";

            string friendlyLog = rawLog;
            friendlyLog = friendlyLog.Replace("rclone", "File Sync Tool");
            friendlyLog = friendlyLog.Replace("sync", "Synchronization");
            friendlyLog = friendlyLog.Replace("copy", "Backup");
            friendlyLog = friendlyLog.Replace("move", "Move Files");
            friendlyLog = friendlyLog.Replace("CHECK", "Verification");
            friendlyLog = friendlyLog.Replace("COPY", "Copy");
            friendlyLog = friendlyLog.Replace("DELETE", "Delete");
            friendlyLog = friendlyLog.Replace("SKIP", "Skip");
            friendlyLog = friendlyLog.Replace("SCANNING", "Scanning for changes");
            friendlyLog = friendlyLog.Replace("COMPLETE", "Completed");
            return friendlyLog;
        }

        private async void LoadRemotesAsync()
        {
            try
            {
                var remotes = await _rcloneService.ListRemotes();
                AvailableRemotes.Clear();

                if (remotes != null && remotes.Any())
                {
                    foreach (var remote in remotes)
                    {
                        AvailableRemotes.Add(remote.TrimEnd(':'));
                    }

                    StatusMessage = string.Format(
                        LocalizationManager.Instance["RemotesLoadedMessage"],
                        AvailableRemotes.Count
                    );

                    StatusIndicatorBrush = ColorCheck;
                }
                else
                {
                    StatusMessage = LocalizationManager.Instance["NoRemotesFound"];
                    StatusIndicatorBrush = ColorDelete;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load remotes");
                StatusMessage = LocalizationManager.Instance["RemotesLoadError"];
                StatusIndicatorBrush = ColorDelete;
            }
        }

        [RelayCommand]
        private void ScheduleSync()
        {
            var scheduleWindow = new ScheduledSyncWindow(
                SelectedSourceRemote,
                SourcePath,
                SelectedTargetRemote,
                TargetPath,
                SelectedSyncMode.Value
            );
            scheduleWindow.ShowDialog();
        }

        private void OnSyncProgress(SyncProgress progress)
        {
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OnSyncProgress(progress));
                return;
            }

            try
            {
                var now = DateTime.Now;
                bool shouldUpdate = _lastReportedProgress == 0 ||
                                    progress.PercentComplete == 100 ||
                                    (now - _lastProgressUpdateTime).TotalMilliseconds >= MINIMUM_UPDATE_INTERVAL_MS ||
                                    Math.Abs(progress.PercentComplete - _lastReportedProgress) >= 0.1 ||
                                    progress.CurrentOperation != CurrentSyncOperation;

                if (shouldUpdate)
                {
                    string operationKey = progress.CurrentOperation?.ToUpper() ?? "SYNC";

                    // Update the operation display
                    CurrentSyncOperation = operationKey switch
                    {
                        "CHECK" => LocalizationManager.Instance["CheckOperation"],
                        "COPY" => LocalizationManager.Instance["CopyOperation"],
                        "DELETE" => LocalizationManager.Instance["DeleteOperation"],
                        "SKIP" => LocalizationManager.Instance["SkipOperation"],
                        "SCANNING" => LocalizationManager.Instance["ScanningOperation"],
                        _ => LocalizationManager.Instance["ScanningForChanges"]
                    };

                    // Set the appropriate status color
                    StatusIndicatorBrush = operationKey switch
                    {
                        "CHECK" => ColorCheck,
                        "COPY" => ColorCopy,
                        "DELETE" => ColorDelete,
                        "SKIP" => ColorSkip,
                        "SCANNING" => ColorScanning,
                        _ => ColorCheck
                    };

                    if (!string.IsNullOrWhiteSpace(progress.CurrentFile))
                    {
                        CurrentFile = progress.CurrentFile;
                        StatusMessage = $"{CurrentSyncOperation}: {CurrentFile}";
                    }

                    if (progress.PercentComplete >= 0 && progress.PercentComplete <= 100)
                    {
                        ProgressValue = progress.PercentComplete;
                        ProgressPercentage = string.Format(
                            LocalizationManager.Instance["PercentComplete"],
                            progress.PercentComplete.ToString("F1")
                        );
                        _lastReportedProgress = progress.PercentComplete;
                        _lastProgressUpdateTime = now;
                    }

                    CurrentSpeed = !string.IsNullOrWhiteSpace(progress.Speed)
                        ? progress.Speed
                        : LocalizationManager.Instance["ZeroSpeed"];

                    RemainingTime = !string.IsNullOrWhiteSpace(progress.TimeRemaining)
                        ? string.Format(LocalizationManager.Instance["TimeLeft"], progress.TimeRemaining)
                        : LocalizationManager.Instance["Calculating"];
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sync progress");
            }
        }

        public void ApplyTheme(string themeName)
        {
            var settings = AppSettings.Load();
            var appResources = Application.Current.Resources.MergedDictionaries;

            _logger.LogInformation($"ApplyTheme called with themeName: {themeName}");
            _logger.LogInformation($"Current settings - UseSystemTheme: {settings.UseSystemTheme}, Theme: {settings.Theme}");

            // Important: Do not detect system theme if a specific theme is requested
            string effectiveTheme = themeName;

            _logger.LogInformation($"Effective theme to be applied: {effectiveTheme}");

            // Remove existing theme dictionaries
            var existingThemes = appResources
                .Where(d => d.Source != null &&
                    (d.Source.ToString().Contains("LightTheme.xaml") ||
                     d.Source.ToString().Contains("DarkTheme.xaml")))
                .ToList();

            foreach (var theme in existingThemes)
            {
                _logger.LogInformation($"Removing theme: {theme.Source}");
                appResources.Remove(theme);
            }

            string themePath;
            if (effectiveTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase) ||
                effectiveTheme.Equals("Sötét", StringComparison.OrdinalIgnoreCase))
            {
                themePath = "pack://application:,,,/Themes/DarkTheme.xaml";
            }
            else
            {
                themePath = "pack://application:,,,/Themes/LightTheme.xaml";
            }

            try
            {
                var newTheme = new ResourceDictionary
                {
                    Source = new Uri(themePath, UriKind.Absolute)
                };

                _logger.LogInformation($"Loading theme from: {themePath}");

                appResources.Add(newTheme);

                // Only update settings if not using system theme
                if (!settings.UseSystemTheme)
                {
                    settings.Theme = effectiveTheme;
                    settings.Save();
                    _logger.LogInformation($"Saved theme settings - Theme: {effectiveTheme}, UseSystemTheme: false");
                }

                _logger.LogInformation($"Successfully applied {effectiveTheme} theme");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error applying {effectiveTheme} theme: {ex.Message}");
            }
        }

        // SyncHistoryItem class for tracking sync operations
        public class SyncHistoryItem
        {
            public DateTime Timestamp { get; set; }
            public string Description { get; set; }
            public string Log { get; set; }
        }
    }
}