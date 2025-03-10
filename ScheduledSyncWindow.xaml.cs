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
                // Validate inputs
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

                    // Create new task with basic settings
                    var td = ts.NewTask();
                    td.RegistrationInfo.Description =
                        $"Scheduled DriveSync ({SelectedSyncMode.Value}) from {SelectedSourceRemote}:{SourcePath} to {SelectedTargetRemote}:{TargetPath}";
                    td.Principal.LogonType = TaskLogonType.InteractiveToken;
                    td.Principal.RunLevel = TaskRunLevel.Highest;

                    // Set up trigger
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

                    // Configure settings
                    td.Settings.Enabled = true;
                    td.Settings.Hidden = false;
                    td.Settings.RunOnlyIfIdle = false;
                    td.Settings.RunOnlyIfNetworkAvailable = false;
                    td.Settings.StartWhenAvailable = false;
                    td.Settings.WakeToRun = false;
                    td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
                    td.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew;
                    // Build command and arguments
                    string commandVerb = SelectedSyncMode.Value switch
                    {
                        SyncType.Mirror => "sync",
                        SyncType.Backup => "copy",
                        SyncType.Move => "move",
                        _ => "sync"
                    };

                    // Create log file path in the same directory as rclone
                    string logPath = Path.Combine(Path.GetDirectoryName(rclonePath), "rclone_task.log");

                    // Construct the full PowerShell command
                    string psScript = $"-WindowStyle Hidden -Command \"& {{& '{rclonePath}' {commandVerb} '{SelectedSourceRemote}:{SourcePath.TrimStart('/')}' '{SelectedTargetRemote}:{TargetPath.TrimStart('/')}' --progress --stats-one-line --stats 1s --quiet > '{logPath}' 2>&1}}\"";

                    var action = new ExecAction(
                        "powershell.exe",
                        psScript,
                        Path.GetDirectoryName(rclonePath));

                    // Add action to task
                    td.Actions.Add(action);

                    // Robust XML modification for WindowStyle
                    try
                    {
                        XDocument taskXml = XDocument.Parse(td.XmlText);
                        XNamespace execNs = taskXml.Root.Name.Namespace;

                        // Find or create the Actions element
                        var actionsElement = taskXml.Descendants(execNs + "Actions").FirstOrDefault();
                        if (actionsElement != null)
                        {
                            // Find the first Exec element within Actions
                            var execNode = actionsElement.Descendants(execNs + "Exec").FirstOrDefault();
                            if (execNode != null)
                            {
                                try
                                {
                                    // Attempt to add WindowStyle, but handle potential exceptions
                                    var windowStyleElement = new XElement(execNs + "WindowStyle");
                                    windowStyleElement.Value = "7";  // SW_SHOWMINNOACTIVE
                                    execNode.Add(windowStyleElement);
                                }
                                catch (Exception xmlEx)
                                {
                                    _logger.LogWarning($"Could not add WindowStyle: {xmlEx.Message}");
                                }
                            }
                        }

                        // Safely update the XML text
                        try
                        {
                            td.XmlText = taskXml.ToString();
                        }
                        catch (Exception xmlUpdateEx)
                        {
                            _logger.LogError($"Error updating task XML: {xmlUpdateEx.Message}");
                        }
                    }
                    catch (Exception xmlParseEx)
                    {
                        _logger.LogError($"Error parsing task XML: {xmlParseEx.Message}");
                    }
                    // Update settings via XML
                    XDocument settingsDoc = XDocument.Parse(td.XmlText);
                    XNamespace settingsNs = settingsDoc.Root?.Name.Namespace ?? "http://schemas.microsoft.com/windows/2004/02/mit/task";
                    XElement settingsElem = settingsDoc.Descendants(settingsNs + "Settings").FirstOrDefault();

                    if (settingsElem != null)
                    {
                        if (OnACPowerCheckBox.IsChecked == true)
                            settingsElem.SetElementValue(settingsNs + "DisallowStartIfOnBatteries", "true");
                        if (StopOnBatteryCheckBox.IsChecked == true)
                            settingsElem.SetElementValue(settingsNs + "StopIfGoingOnBatteries", "true");
                        if (WakeToRunCheckBox.IsChecked == true)
                            settingsElem.SetElementValue(settingsNs + "WakeToRun", "true");

                        if (RunOnlyIfIdleCheckBox.IsChecked == true)
                        {
                            settingsElem.SetElementValue(settingsNs + "RunOnlyIfIdle", "true");
                            if (IdleDurationCombo.SelectedItem is ComboBoxItem selectedItem &&
                                int.TryParse(selectedItem.Content.ToString(), out int idleMins) && idleMins > 0)
                            {
                                XElement idleSettings = settingsElem.Element(settingsNs + "IdleSettings");
                                if (idleSettings == null)
                                {
                                    idleSettings = new XElement(settingsNs + "IdleSettings");
                                    settingsElem.Add(idleSettings);
                                }
                                idleSettings.SetElementValue(settingsNs + "Duration", $"PT{idleMins}M");
                            }
                        }

                        if (RunOnlyIfNetworkAvailableCheckBox.IsChecked == true)
                        {
                            settingsElem.SetElementValue(settingsNs + "RunOnlyIfNetworkAvailable", "true");
                            string selectedNetwork = NetworkCombo.SelectedItem?.ToString() ?? "Any network";
                            if (!string.Equals(selectedNetwork, "Any network", StringComparison.OrdinalIgnoreCase))
                            {
                                XElement networkSettings = settingsElem.Element(settingsNs + "NetworkSettings");
                                if (networkSettings == null)
                                {
                                    networkSettings = new XElement(settingsNs + "NetworkSettings");
                                    settingsElem.Add(networkSettings);
                                }
                                networkSettings.SetElementValue(settingsNs + "Name", selectedNetwork);
                            }
                        }

                        if (AllowDemandStartCheckBox.IsChecked == false)
                            settingsElem.SetElementValue(settingsNs + "DisallowDemandStart", "true");

                        var selectedInstances = (MultipleInstancesCombo.SelectedItem as ComboBoxItem)?.Content?.ToString();
                        switch (selectedInstances)
                        {
                            case "Queue": td.Settings.MultipleInstances = TaskInstancesPolicy.Queue; break;
                            case "StopExisting": td.Settings.MultipleInstances = TaskInstancesPolicy.StopExisting; break;
                            case "IgnoreNew": td.Settings.MultipleInstances = TaskInstancesPolicy.IgnoreNew; break;
                            case "Parallel": td.Settings.MultipleInstances = TaskInstancesPolicy.Parallel; break;
                        }

                        if (StopAfterCheckBox.IsChecked == true && int.TryParse(ExecutionTimeLimitBox.Text, out int limitMins))
                            td.Settings.ExecutionTimeLimit = TimeSpan.FromMinutes(limitMins);

                        if (ForceKillCheckBox.IsChecked == true)
                            settingsElem.SetElementValue(settingsNs + "DisallowHardTerminate", "false");

                        if (StartWhenAvailableCheckBox.IsChecked == true)
                            settingsElem.SetElementValue(settingsNs + "StartWhenAvailable", "true");

                        if (DisableTaskCheckBox.IsChecked == true)
                            settingsElem.SetElementValue(settingsNs + "Enabled", "false");

                        if (HiddenTaskCheckBox.IsChecked == true)
                            settingsElem.SetElementValue(settingsNs + "Hidden", "true");

                        td.XmlText = settingsDoc.ToString();
                    }

                    _logger.LogInformation("Final task XML before registration:");
                    _logger.LogInformation(td.XmlText);

                    // Register the task
                    string taskName = $"DriveSync_Scheduled_{Guid.NewGuid()}";
                    ts.RootFolder.RegisterTaskDefinition(
                        taskName,
                        td,
                        TaskCreation.Create,
                        null,
                        null,
                        TaskLogonType.InteractiveToken
                    );

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
                _logger.LogError($"Exception details: {ex.ToString()}");
                _logger.LogError($"Exception message: {ex.Message}");
                if (ex.InnerException != null)
                {
                    _logger.LogError($"Inner exception: {ex.InnerException.Message}");
                }

                // Check for specific exceptions
                if (ex is UnauthorizedAccessException)
                {
                    ThemedMessageBox.Show(
                        LocalizationManager.Instance["ScheduledTaskCreationFailedPermissions"],
                        LocalizationManager.Instance["Error"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
                else if (ex is ArgumentException)
                {
                    ThemedMessageBox.Show(
                        LocalizationManager.Instance["ScheduledTaskCreationFailedArguments"],
                        LocalizationManager.Instance["Error"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
                else
                {
                    ThemedMessageBox.Show(
                        string.Format(LocalizationManager.Instance["ScheduledTaskCreationFailed"], ex.Message),
                        LocalizationManager.Instance["Error"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
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