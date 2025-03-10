using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.TaskScheduler;
using DriveSync.Infrastructure.Services;
using DriveSync.WPF.ViewModels;
using DriveSync.WPF.Localization;
using System.Text.RegularExpressions;

namespace DriveSync.WPF.Views
{
    public partial class ScheduledSyncWindow : ModernWindowBase, INotifyPropertyChanged
    {
        // Dependency and core service fields
        private readonly IRcloneService _rcloneService;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<ScheduledSyncWindow> _logger;

        // Backing fields for bound properties
        private string _selectedSourceRemote;
        private string _sourcePath;
        private string _selectedTargetRemote;
        private string _targetPath;
        private SyncModeOption _selectedSyncMode;
        private ObservableCollection<SyncModeOption> _availableSyncModes;
        private ObservableCollection<string> _availableRemotes;

        // Properties bound to the UI
        public string SelectedSourceRemote
        {
            get => _selectedSourceRemote;
            set { _selectedSourceRemote = value; OnPropertyChanged(nameof(SelectedSourceRemote)); }
        }

        public string SourcePath
        {
            get => _sourcePath;
            set { _sourcePath = value; OnPropertyChanged(nameof(SourcePath)); }
        }

        public string SelectedTargetRemote
        {
            get => _selectedTargetRemote;
            set { _selectedTargetRemote = value; OnPropertyChanged(nameof(SelectedTargetRemote)); }
        }

        public string TargetPath
        {
            get => _targetPath;
            set { _targetPath = value; OnPropertyChanged(nameof(TargetPath)); }
        }

        public SyncModeOption SelectedSyncMode
        {
            get => _selectedSyncMode;
            set { _selectedSyncMode = value; OnPropertyChanged(nameof(SelectedSyncMode)); }
        }

        public ObservableCollection<SyncModeOption> AvailableSyncModes
        {
            get => _availableSyncModes;
            set { _availableSyncModes = value; OnPropertyChanged(nameof(AvailableSyncModes)); }
        }

        public ObservableCollection<string> AvailableRemotes
        {
            get => _availableRemotes;
            set { _availableRemotes = value; OnPropertyChanged(nameof(AvailableRemotes)); }
        }
        // INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Constructor
        public ScheduledSyncWindow(string initialSourceRemote, string initialSourcePath,
            string initialTargetRemote, string initialTargetPath, SyncType initialSyncType)
        {
            InitializeComponent();
            DataContext = this;

            // Access DI via the static App.ServiceProvider property
            _rcloneService = App.ServiceProvider.GetService<IRcloneService>();
            _loggerFactory = App.ServiceProvider.GetService<ILoggerFactory>();
            _logger = _loggerFactory.CreateLogger<ScheduledSyncWindow>();

            // Initialize properties
            SelectedSourceRemote = initialSourceRemote;
            SourcePath = initialSourcePath;
            SelectedTargetRemote = initialTargetRemote;
            TargetPath = initialTargetPath;

            AvailableSyncModes = new ObservableCollection<SyncModeOption>
            {
                new SyncModeOption { DisplayName = "Mirror Sync", Value = SyncType.Mirror },
                new SyncModeOption { DisplayName = "Backup (Copy)", Value = SyncType.Backup },
                new SyncModeOption { DisplayName = "Move Files", Value = SyncType.Move }
            };
            SelectedSyncMode = AvailableSyncModes.FirstOrDefault(x => x.Value == initialSyncType)
                ?? AvailableSyncModes.First();

            // Initialize remotes collection and load remotes
            AvailableRemotes = new ObservableCollection<string>();
            LoadRemotesAsync();

            LoadNetworkInterfaces();
            LoadTimeOptions();

            StartDatePicker.SelectedDate = DateTime.Today;
        }

        private string GetRclonePath()
        {
            try
            {
                var possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rclone.exe"),
                    @"C:\rclone\rclone-v1.64.0-windows-amd64\rclone.exe",
                    @"C:\rclone\rclone.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "rclone", "rclone.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        "rclone", "rclone.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "rclone", "rclone.exe")
                };

                foreach (var path in possiblePaths)
                {
                    _logger.LogInformation($"Checking for rclone at: {path}");
                    if (File.Exists(path))
                    {
                        _logger.LogInformation($"Found rclone at: {path}");
                        return path;
                    }
                }

                var searchedPaths = string.Join("\n", possiblePaths);
                var errorMessage = $"rclone.exe not found. Searched in:\n{searchedPaths}\n\n" +
                                 "Please ensure rclone is installed and in one of these locations, " +
                                 "or copy rclone.exe to the application directory.";

                _logger.LogError(errorMessage);
                MessageBox.Show(errorMessage, "Rclone Not Found", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                throw new FileNotFoundException(errorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while trying to locate rclone.exe");
                throw;
            }
        }
        private async void LoadRemotesAsync()
        {
            try
            {
                var remotes = await _rcloneService.ListRemotes();
                AvailableRemotes.Clear();

                if (remotes != null && remotes.Any())
                {
                    foreach (var r in remotes)
                    {
                        AvailableRemotes.Add(r.TrimEnd(':'));
                    }
                }
                else
                {
                    _logger.LogWarning("No remotes found for scheduling.");
                    MessageBox.Show("No remotes found. Check your rclone config.",
                                    "No Remotes",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading remotes in ScheduledSyncWindow");
                MessageBox.Show("Error loading remotes. See logs for details.",
                                "Error",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        private void LoadNetworkInterfaces()
        {
            if (NetworkCombo == null)
                return;

            NetworkCombo.Items.Clear();
            NetworkCombo.Items.Add("Any network");

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    var ipProps = nic.GetIPProperties();
                    var ipv4Props = ipProps.GetIPv4Properties();
                    if (ipv4Props == null)
                        continue;

                    int ifIndex = ipv4Props.Index;
                    if (ifIndex == 12 || ifIndex == 5)
                    {
                        string displayName = nic.Name;
                        NetworkCombo.Items.Add(displayName);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error retrieving ifIndex for adapter {nic.Name}");
                }
            }
            NetworkCombo.SelectedIndex = 0;
        }

        private void LoadTimeOptions()
        {
            if (StartTimeCombo == null)
                return;

            StartTimeCombo.Items.Clear();
            for (int hour = 0; hour < 24; hour++)
            {
                for (int minute = 0; minute < 60; minute += 15)
                {
                    TimeSpan time = new TimeSpan(hour, minute, 0);
                    string timeString = time.ToString(@"hh\:mm");
                    StartTimeCombo.Items.Add(timeString);
                }
            }
            int defaultIndex = StartTimeCombo.Items.IndexOf("08:00");
            StartTimeCombo.SelectedIndex = defaultIndex >= 0 ? defaultIndex : 0;

            if (IdleDurationCombo != null)
            {
                var defaultIdleDurationItem = IdleDurationCombo.Items
                    .Cast<ComboBoxItem>()
                    .FirstOrDefault(item => item.Content.ToString() == "10");

                if (defaultIdleDurationItem != null)
                {
                    IdleDurationCombo.SelectedItem = defaultIdleDurationItem;
                }
            }
        }

        private void BrowseSource_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedSourceRemote))
            {
                ThemedMessageBox.Show(
                    LocalizationManager.Instance["PleaseSelectSourceRemoteFirst"],
                    LocalizationManager.Instance["Error"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            var logger = _loggerFactory.CreateLogger<DirectoryBrowserViewModel>();
            var vm = new DirectoryBrowserViewModel(_rcloneService, logger, SelectedSourceRemote, true);
            var dialog = new DirectoryBrowserDialog(vm);
            if (dialog.ShowDialog() == true)
            {
                SourcePath = dialog.SelectedPath;
            }
        }

        private void BrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SelectedTargetRemote))
            {
                ThemedMessageBox.Show(
                    LocalizationManager.Instance["PleaseSelectTargetRemoteFirst"],
                    LocalizationManager.Instance["Error"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
                return;
            }

            var logger = _loggerFactory.CreateLogger<DirectoryBrowserViewModel>();
            var vm = new DirectoryBrowserViewModel(_rcloneService, logger, SelectedTargetRemote, false);
            var dialog = new DirectoryBrowserDialog(vm);
            if (dialog.ShowDialog() == true)
            {
                TargetPath = dialog.SelectedPath;
            }
        }

        private void CreateTaskButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Input validation
                if (string.IsNullOrWhiteSpace(SelectedSourceRemote) || string.IsNullOrWhiteSpace(SourcePath))
                {
                    ThemedMessageBox.Show(
                        LocalizationManager.Instance["PleaseSelectSourceRemoteFirst"],
                        LocalizationManager.Instance["Error"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }
                if (string.IsNullOrWhiteSpace(SelectedTargetRemote) || string.IsNullOrWhiteSpace(TargetPath))
                {
                    ThemedMessageBox.Show(
                        LocalizationManager.Instance["PleaseSelectTargetRemoteFirst"],
                        LocalizationManager.Instance["Error"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }
                if (!StartDatePicker.SelectedDate.HasValue)
                {
                    ThemedMessageBox.Show(
                        LocalizationManager.Instance["PleaseSelectValidStartDate"],
                        LocalizationManager.Instance["Error"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }
                var startDate = StartDatePicker.SelectedDate.Value;
                string timeString = StartTimeCombo.SelectedItem?.ToString();
                if (!TimeSpan.TryParseExact(timeString, "hh\\:mm", CultureInfo.InvariantCulture, out var startTime))
                {
                    ThemedMessageBox.Show(
                        LocalizationManager.Instance["InvalidStartTime"],
                        LocalizationManager.Instance["Error"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );
                    return;
                }
                var startBoundary = startDate.Date + startTime;

                using (TaskService ts = new TaskService())
                {
                    string rclonePath = GetRclonePath();
                    _logger.LogInformation($"Using rclone path: {rclonePath}");

                    // Create descriptive task name
                    string CreateDescriptiveTaskName()
                    {
                        // Base format: DriveSync_[SyncMode]_[SourceRemote]_to_[TargetRemote]_[Timestamp]
                        string syncModeShorthand = SelectedSyncMode.Value switch
                        {
                            SyncType.Mirror => "Mirror",
                            SyncType.Backup => "Backup",
                            SyncType.Move => "Move",
                            _ => "Sync"
                        };

                        // Sanitize remote names to remove special characters and spaces
                        string sanitizedSourceRemote = Regex.Replace(SelectedSourceRemote, @"[^\w\-]", "_");
                        string sanitizedTargetRemote = Regex.Replace(SelectedTargetRemote, @"[^\w\-]", "_");

                        // Truncate remote names if they're too long
                        sanitizedSourceRemote = sanitizedSourceRemote.Length > 20
                            ? sanitizedSourceRemote.Substring(0, 20)
                            : sanitizedSourceRemote;

                        sanitizedTargetRemote = sanitizedTargetRemote.Length > 20
                            ? sanitizedTargetRemote.Substring(0, 20)
                            : sanitizedTargetRemote;

                        // Create timestamp in a compact format
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

                        // Construct the task name
                        string taskName = $"DriveSync_{syncModeShorthand}_{sanitizedSourceRemote}_to_{sanitizedTargetRemote}_{timestamp}";

                        // Ensure the task name is valid (Windows has some restrictions on task names)
                        taskName = new string(taskName.Select(c =>
                                            char.IsLetterOrDigit(c) || c == '_' || c == '-'
                                            ? c
                                            : '_').ToArray());

                        return taskName;
                    }

                    // Create new task definition
                    var td = ts.NewTask();

                    // Set basic task properties
                    string taskName = CreateDescriptiveTaskName();
                    td.RegistrationInfo.Description = $"Scheduled DriveSync ({SelectedSyncMode.Value}) from {SelectedSourceRemote}:{SourcePath} to {SelectedTargetRemote}:{TargetPath}";
                    td.Principal.LogonType = TaskLogonType.InteractiveToken;
                    td.Principal.RunLevel = TaskRunLevel.Highest;

                    // Configure trigger based on selected repeat type
                    var selectedRepeat = (RepeatTypeCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "OneTime";
                    switch (selectedRepeat)
                    {
                        case "Daily":
                            td.Triggers.Add(new DailyTrigger { StartBoundary = startBoundary, DaysInterval = 1 });
                            break;
                        case "Weekly":
                            td.Triggers.Add(new WeeklyTrigger
                            {
                                StartBoundary = startBoundary,
                                DaysOfWeek = (DaysOfTheWeek)(1 << (int)startDate.DayOfWeek),
                                WeeksInterval = 1
                            });
                            break;
                        case "Monthly":
                            td.Triggers.Add(new MonthlyTrigger
                            {
                                StartBoundary = startBoundary,
                                DaysOfMonth = new int[] { startDate.Day }
                            });
                            break;
                        default:
                            td.Triggers.Add(new TimeTrigger { StartBoundary = startBoundary });
                            break;
                    }

                    // Configure task settings
                    td.Settings.Enabled = !DisableTaskCheckBox.IsChecked.GetValueOrDefault();
                    td.Settings.Hidden = HiddenTaskCheckBox.IsChecked.GetValueOrDefault();

                    // Network and idle settings
                    td.Settings.RunOnlyIfIdle = RunOnlyIfIdleCheckBox.IsChecked.GetValueOrDefault();
                    td.Settings.RunOnlyIfNetworkAvailable = RunOnlyIfNetworkAvailableCheckBox.IsChecked.GetValueOrDefault();

                    if (RunOnlyIfIdleCheckBox.IsChecked.GetValueOrDefault() &&
                        IdleDurationCombo.SelectedItem is ComboBoxItem selectedItem &&
                        int.TryParse(selectedItem.Content.ToString(), out int idleMins))
                    {
                        td.Settings.IdleSettings.IdleDuration = TimeSpan.FromMinutes(idleMins);
                    }

                    // Multiple instance handling
                    td.Settings.MultipleInstances = MultipleInstancesCombo.SelectedIndex switch
                    {
                        0 => TaskInstancesPolicy.Queue,
                        1 => TaskInstancesPolicy.StopExisting,
                        2 => TaskInstancesPolicy.IgnoreNew,
                        3 => TaskInstancesPolicy.Parallel,
                        _ => TaskInstancesPolicy.IgnoreNew
                    };

                    // Execution time limit
                    if (StopAfterCheckBox.IsChecked.GetValueOrDefault() &&
                        int.TryParse(ExecutionTimeLimitBox.Text, out int limitMins))
                    {
                        td.Settings.ExecutionTimeLimit = TimeSpan.FromMinutes(limitMins);
                    }

                    // Safely modify XML for power and battery settings
                    try
                    {
                        var taskXml = XDocument.Parse(td.XmlText);
                        XNamespace ns = taskXml.Root.Name.Namespace;

                        var settingsElement = taskXml.Descendants(ns + "Settings").FirstOrDefault();
                        if (settingsElement != null)
                        {
                            // Power settings
                            if (OnACPowerCheckBox.IsChecked.GetValueOrDefault())
                            {
                                AddOrUpdateXmlElement(settingsElement, ns, "DisallowStartIfOnBatteries", "true");
                            }
                            if (StopOnBatteryCheckBox.IsChecked.GetValueOrDefault())
                            {
                                AddOrUpdateXmlElement(settingsElement, ns, "StopIfGoingOnBatteries", "true");
                            }
                            if (WakeToRunCheckBox.IsChecked.GetValueOrDefault())
                            {
                                AddOrUpdateXmlElement(settingsElement, ns, "WakeToRun", "true");
                            }
                        }

                        // Update the task definition with modified XML
                        td.XmlText = taskXml.ToString();
                    }
                    catch (Exception xmlEx)
                    {
                        _logger.LogError(xmlEx, "Error modifying task XML for power settings");
                    }

                    // Determine command verb
                    string commandVerb = SelectedSyncMode.Value switch
                    {
                        SyncType.Mirror => "sync",
                        SyncType.Backup => "copy",
                        SyncType.Move => "move",
                        _ => "sync"
                    };

                    // Create log file path in the same directory as rclone
                    string logPath = Path.Combine(Path.GetDirectoryName(rclonePath), "rclone_task.log");

                    // Construct PowerShell command
                    string psScript = $"-WindowStyle Hidden -Command \"& {{& '{rclonePath}' {commandVerb} '{SelectedSourceRemote}:{SourcePath.TrimStart('/')}' '{SelectedTargetRemote}:{TargetPath.TrimStart('/')}' --progress --stats-one-line --stats 1s --quiet > '{logPath}' 2>&1}}\"";

                    // Add PowerShell action
                    td.Actions.Add(new ExecAction(
                        "powershell.exe",
                        psScript,
                        Path.GetDirectoryName(rclonePath)
                    ));

                    // Register the task
                    ts.RootFolder.RegisterTaskDefinition(
                        taskName,
                        td,
                        TaskCreation.Create,
                        null,
                        null,
                        TaskLogonType.InteractiveToken
                    );

                    _logger.LogInformation($"Task {taskName} created successfully");

                    ThemedMessageBox.Show(
                        string.Format(LocalizationManager.Instance["ScheduledTaskCreatedSuccessfully"], taskName),
                        LocalizationManager.Instance["Success"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    DialogResult = true;
                    Close();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during scheduled task creation");

                ThemedMessageBox.Show(
                    $"An unexpected error occurred: {ex.Message}",
                    LocalizationManager.Instance["Error"],
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
        // Helper method to safely add or update XML elements
        private void AddOrUpdateXmlElement(XElement parentElement, XNamespace ns, string elementName, string value)
        {
            var existingElement = parentElement.Element(ns + elementName);
            if (existingElement != null)
            {
                existingElement.Value = value;
            }
            else
            {
                parentElement.Add(new XElement(ns + elementName, value));
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // Supporting class for sync mode options
        public class SyncModeOption
        {
            public string DisplayName { get; set; }
            public SyncType Value { get; set; }
        }
    }
}