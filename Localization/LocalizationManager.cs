using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Data;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Diagnostics;

namespace DriveSync.WPF.Localization
{
    public enum AppLanguage
    {
        English,
        Hungarian
    }

    public class LocalizationSettings
    {
        public AppLanguage Language { get; set; } = AppLanguage.English;
    }

    public class LocalizationManager : INotifyPropertyChanged
    {
        private static LocalizationManager instance;
        private Dictionary<string, string> englishTranslations;
        private Dictionary<string, string> hungarianTranslations;
        private AppLanguage currentLanguage = AppLanguage.English;
        private readonly string settingsPath;

        public static LocalizationManager Instance
        {
            get
            {
                instance ??= new LocalizationManager();
                return instance;
            }
        }

        // In LocalizationManager.cs
        public AppLanguage CurrentLanguage
        {
            get => currentLanguage;
            set
            {
                if (currentLanguage != value)
                {
                    currentLanguage = value;

                    // Add detailed logging
                    Console.WriteLine($"Language changed to: {currentLanguage}");
                    Debug.WriteLine($"Language changed to: {currentLanguage}");

                    SaveSettings();
                    OnPropertyChanged(string.Empty); // Notify all bindings
                }
            }
        }

        private LocalizationManager()
        {
            settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "localization.json");
            LoadSettings();
            InitializeTranslations();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsPath))
                {
                    var json = File.ReadAllText(settingsPath);
                    var settings = JsonSerializer.Deserialize<LocalizationSettings>(json);
                    currentLanguage = settings.Language;
                }
            }
            catch
            {
                currentLanguage = AppLanguage.English;
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new LocalizationSettings { Language = currentLanguage };
                var json = JsonSerializer.Serialize(settings);
                File.WriteAllText(settingsPath, json);
            }
            catch
            {
                // Log error if needed
            }
        }

        private void SafeAddTranslation(Dictionary<string, string> translations, string key, string value)
        {
            if (!translations.ContainsKey(key))
            {
                translations[key] = value;
            }
        }

        private void InitializeTranslations()
        {
            englishTranslations = new Dictionary<string, string>();
            hungarianTranslations = new Dictionary<string, string>();

            AddEnglishTranslations();
            AddHungarianTranslations();
        }

        private void AddEnglishTranslations()
        {
            // Global and Common Elements
            SafeAddTranslation(englishTranslations, "ApplicationTitle", "DriveSync");
            SafeAddTranslation(englishTranslations, "Settings", "Settings");
            SafeAddTranslation(englishTranslations, "Schedule", "Schedule");
            SafeAddTranslation(englishTranslations, "Browse", "Browse...");
            SafeAddTranslation(englishTranslations, "Cancel", "Cancel");
            SafeAddTranslation(englishTranslations, "Save", "Save");
            SafeAddTranslation(englishTranslations, "CreateTask", "Create Task");


            // Main Window
            SafeAddTranslation(englishTranslations, "SyncConfiguration", "Sync Configuration");
            SafeAddTranslation(englishTranslations, "Source", "Source");
            SafeAddTranslation(englishTranslations, "Target", "Target");
            SafeAddTranslation(englishTranslations, "SyncMode", "Sync Mode");
            SafeAddTranslation(englishTranslations, "SyncNow", "Sync Now");
            SafeAddTranslation(englishTranslations, "SyncHistory", "Sync History");
            SafeAddTranslation(englishTranslations, "SyncModeOption1", "Mirror Sync");
            SafeAddTranslation(englishTranslations, "SyncModeOption2", "Backup (Copy)");
            SafeAddTranslation(englishTranslations, "SyncModeOption3", "Move Files");
            SafeAddTranslation(englishTranslations, "StartTime", "Start Time");

            // Remote loading status messages
            SafeAddTranslation(englishTranslations, "RemotesLoadedMessage", "{0} remote(s) loaded");
            SafeAddTranslation(englishTranslations, "NoRemotesFound", "No remotes found. Please configure rclone.");
            SafeAddTranslation(englishTranslations, "RemotesLoadError", "Failed to load remotes. Check your rclone configuration.");
            SafeAddTranslation(englishTranslations, "PleaseSelectSourceRemoteFirst", "Please select a source remote first.");
            SafeAddTranslation(englishTranslations, "PleaseSelectTargetRemoteFirst", "Please select a target remote first.");
            SafeAddTranslation(englishTranslations, "SourceRemoteSelected", "Source remote selected: {0}");
            SafeAddTranslation(englishTranslations, "TargetRemoteSelected", "Target remote selected: {0}");
            SafeAddTranslation(englishTranslations, "SelectedSourceRemote", "Selected Source Remote:");
            SafeAddTranslation(englishTranslations, "SelectedTargetRemote", "Selected Target Remote:");
            SafeAddTranslation(englishTranslations, "SelectedSourcePath", "Selected source path: {0}");
            SafeAddTranslation(englishTranslations, "SelectedTargetPath", "Selected target path: {0}");

            // Scheduled Sync Window
            SafeAddTranslation(englishTranslations, "ScheduleSyncTitle", "Schedule DriveSync");
            SafeAddTranslation(englishTranslations, "SourceRemote", "Source Remote");
            SafeAddTranslation(englishTranslations, "TargetRemote", "Target Remote");
            SafeAddTranslation(englishTranslations, "StartDate", "Start Date");
            SafeAddTranslation(englishTranslations, "Repeat", "Repeat");

            // Repeat Options
            SafeAddTranslation(englishTranslations, "OneTimeSyncOption", "One Time Sync");
            SafeAddTranslation(englishTranslations, "DailySyncOption", "Daily Sync");
            SafeAddTranslation(englishTranslations, "WeeklySyncOption", "Weekly Sync");
            SafeAddTranslation(englishTranslations, "MonthlySyncOption", "Monthly Sync");
            SafeAddTranslation(englishTranslations, "OneTime", "One Time");
            SafeAddTranslation(englishTranslations, "Daily", "Daily");
            SafeAddTranslation(englishTranslations, "Weekly", "Weekly");
            SafeAddTranslation(englishTranslations, "Monthly", "Monthly");

            // Tab Headers
            SafeAddTranslation(englishTranslations, "General", "General");
            SafeAddTranslation(englishTranslations, "Conditions", "Conditions");
            SafeAddTranslation(englishTranslations, "Settings", "Settings");

            // Power and Idle Settings
            SafeAddTranslation(englishTranslations, "PowerSettingsSection", "Power Settings");
            SafeAddTranslation(englishTranslations, "OnACPowerStart", "Start only if on AC power");
            SafeAddTranslation(englishTranslations, "StopOnBattery", "Stop if switched to battery");
            SafeAddTranslation(englishTranslations, "WakeToRun", "Wake computer to run");

            // Network Settings
            SafeAddTranslation(englishTranslations, "NetworkSettingsSection", "Network Settings");
            SafeAddTranslation(englishTranslations, "RunOnlyIfNetworkAvailable", "Run only if network available");
            SafeAddTranslation(englishTranslations, "AnyNetwork", "Any network");

            // Additional and Instance Handling
            SafeAddTranslation(englishTranslations, "ForceStopIfNotEnding", "Force stop if doesn't end");

            //SettingsWindow Dark and Light Theme
            SafeAddTranslation(englishTranslations, "Light", "Light");
            SafeAddTranslation(englishTranslations, "Dark", "Dark");
            SafeAddTranslation(englishTranslations, "System", "System");

            // Directory Browser Dialog translations
            SafeAddTranslation(englishTranslations, "SelectDirectoryFromRemote", "Select Directory from {0}");
            SafeAddTranslation(englishTranslations, "SelectDirectory", "Select Directory");
            SafeAddTranslation(englishTranslations, "LoadingDirectories", "Loading directories...");
            SafeAddTranslation(englishTranslations, "Select", "Select");

            SafeAddTranslation(englishTranslations, "AppearanceHeader", "Appearance");
            SafeAddTranslation(englishTranslations, "ThemeMode", "Theme Mode");
            SafeAddTranslation(englishTranslations, "FontSettings", "Font Settings");
            SafeAddTranslation(englishTranslations, "Language", "Language");
            SafeAddTranslation(englishTranslations, "English", "English");
            SafeAddTranslation(englishTranslations, "Hungarian", "Magyar");

            SafeAddTranslation(englishTranslations, "RcloneConfiguration", "Rclone Configuration");
            SafeAddTranslation(englishTranslations, "RcloneExecutablePath", "Rclone Executable Path");
            SafeAddTranslation(englishTranslations, "BrowseRclone", "Browse...");
            SafeAddTranslation(englishTranslations, "DefaultSyncMode", "Default Sync Mode");
            SafeAddTranslation(englishTranslations, "StartupBehavior", "Startup Behavior");
            SafeAddTranslation(englishTranslations, "LaunchAtStartup", "Launch at Startup");
            SafeAddTranslation(englishTranslations, "MinimizeToTray", "Minimize to Tray");
            SafeAddTranslation(englishTranslations, "RestartRequired", "Restart Required");

            SafeAddTranslation(englishTranslations, "PanelGeneral", "General");
            SafeAddTranslation(englishTranslations, "PanelInterface", "Interface");
            SafeAddTranslation(englishTranslations, "PanelSecurity", "Security");
            SafeAddTranslation(englishTranslations, "PanelSync", "Sync");
            SafeAddTranslation(englishTranslations, "PanelPerformance", "Performance");
            SafeAddTranslation(englishTranslations, "PanelAdvanced", "Advanced");

            SafeAddTranslation(englishTranslations, "ApplicationSettings", "Application Settings");
            SafeAddTranslation(englishTranslations, "Reset", "Reset");
            SafeAddTranslation(englishTranslations, "Cancel", "Cancel");
            SafeAddTranslation(englishTranslations, "SaveSettings", "Save Settings");

            SafeAddTranslation(englishTranslations, "FileEncryption", "File Encryption");
            SafeAddTranslation(englishTranslations, "EnableFileEncryption", "Enable File Encryption");
            SafeAddTranslation(englishTranslations, "EncryptionKey", "Encryption Key");
            SafeAddTranslation(englishTranslations, "GenerateKey", "Generate Key");
            SafeAddTranslation(englishTranslations, "Authentication", "Authentication");
            SafeAddTranslation(englishTranslations, "RequirePasswordOnStartup", "Require Password on Startup");
            SafeAddTranslation(englishTranslations, "RequirePasswordForSettings", "Require Password for Settings");
            SafeAddTranslation(englishTranslations, "RequirePasswordForManualSync", "Require Password for Manual Sync");
            SafeAddTranslation(englishTranslations, "ApplicationPassword", "Application Password");
            SafeAddTranslation(englishTranslations, "ResetPassword", "Reset Password");
            SafeAddTranslation(englishTranslations, "PasswordResetNote", "Note: If you forget the password, you will need to reinstall the application.");

            SafeAddTranslation(englishTranslations, "SyncBehavior", "Sync Behavior");
            SafeAddTranslation(englishTranslations, "EnableAutomaticSync", "Enable Automatic Sync");
            SafeAddTranslation(englishTranslations, "SyncInterval", "Sync Interval (Hours)");
            SafeAddTranslation(englishTranslations, "SyncOnStartup", "Sync on Application Startup");
            SafeAddTranslation(englishTranslations, "SyncOnShutdown", "Sync on Application Shutdown");

            SafeAddTranslation(englishTranslations, "FileFiltering", "File Filtering");
            SafeAddTranslation(englishTranslations, "ExcludedPaths", "Excluded Paths (comma-separated)");
            SafeAddTranslation(englishTranslations, "AllowedPaths", "Allowed Paths (comma-separated)");
            SafeAddTranslation(englishTranslations, "VerifyTransfers", "Verify Transfers");
            SafeAddTranslation(englishTranslations, "PreserveFileTimes", "Preserve File Timestamps");
            SafeAddTranslation(englishTranslations, "PreserveFilePermissions", "Preserve File Permissions");

            SafeAddTranslation(englishTranslations, "BandwidthThrottling", "Bandwidth Throttling");
            SafeAddTranslation(englishTranslations, "UploadLimit", "Upload Limit (KB/s)");
            SafeAddTranslation(englishTranslations, "DownloadLimit", "Download Limit (KB/s)");

            SafeAddTranslation(englishTranslations, "TransferSettings", "Transfer Settings");
            SafeAddTranslation(englishTranslations, "MaxParallelTransfers", "Max Parallel Transfers");
            SafeAddTranslation(englishTranslations, "EnableDirectoryCaching", "Enable Directory Caching");
            SafeAddTranslation(englishTranslations, "CacheExpiration", "Cache Expiration (Hours)");

            SafeAddTranslation(englishTranslations, "TransferSettings", "Transfer Settings");
            SafeAddTranslation(englishTranslations, "MaxParallelTransfers", "Max Parallel Transfers");
            SafeAddTranslation(englishTranslations, "ParallelTransfers", "Parallel Transfers");
            SafeAddTranslation(englishTranslations, "CheckWorkers", "Check Workers");
            SafeAddTranslation(englishTranslations, "BufferSize", "Buffer Size (MiB)");
            SafeAddTranslation(englishTranslations, "UseMemoryMapping", "Use Memory Mapping");
            SafeAddTranslation(englishTranslations, "BandwidthLimitMiBs", "Bandwidth Limit (MiB/s, 0 = unlimited)");
            SafeAddTranslation(englishTranslations, "CutoffMode", "Cutoff Mode");
            SafeAddTranslation(englishTranslations, "ErrorHandling", "Error Handling");
            SafeAddTranslation(englishTranslations, "RetryCount", "Retry Count");
            SafeAddTranslation(englishTranslations, "ConnectionTimeout", "Connection Timeout (seconds)");
            SafeAddTranslation(englishTranslations, "AdvancedTransferOptions", "Advanced Transfer Options");
            SafeAddTranslation(englishTranslations, "DisableDirectoryTraversal", "Disable Directory Traversal (--no-traverse)");
            SafeAddTranslation(englishTranslations, "PerformanceWarning", "These settings can significantly impact performance. Adjust with caution.");
            SafeAddTranslation(englishTranslations, "TransferSecurity", "Transfer Security");
            SafeAddTranslation(englishTranslations, "VerifyChecksumFlag", "Verify Checksum (--checksum)");
            SafeAddTranslation(englishTranslations, "CompareModTimeFlag", "Compare Mod-time (--ignore-times)");
            SafeAddTranslation(englishTranslations, "VerifyTransfersFlag", "Verify Transfers (--size-only)");
            SafeAddTranslation(englishTranslations, "SecurityPerformanceNote", "These settings affect sync performance. Enable verification options only when needed.");

            // Operation flags and tooltips
            SafeAddTranslation(englishTranslations, "FastListFlag", "Fast List (--fast-list)");
            SafeAddTranslation(englishTranslations, "FastListTooltip", "Use recursive list if available. Uses more memory but fewer transactions.");
            SafeAddTranslation(englishTranslations, "UseServerModTimeFlag", "Use Server ModTime (--use-server-modtime)");
            SafeAddTranslation(englishTranslations, "UseServerModTimeTooltip", "Use server modified time instead of object metadata");
            SafeAddTranslation(englishTranslations, "CheckAccessFlag", "Check Access (--check-access)");
            SafeAddTranslation(englishTranslations, "CheckAccessTooltip", "Check whether the source and destination have proper access permissions");
            SafeAddTranslation(englishTranslations, "CreateEmptyDirsFlag", "Create Empty Source Dirs (--create-empty-src-dirs)");
            SafeAddTranslation(englishTranslations, "CreateEmptyDirsTooltip", "Create empty source directories on destination");

            // Security-related translations
            SafeAddTranslation(englishTranslations, "VerifyChecksumTooltip", "Skip based on checksum & size, not mod-time & size");
            SafeAddTranslation(englishTranslations, "CompareModTimeTooltip", "Compare modification times for file changes");
            SafeAddTranslation(englishTranslations, "VerifyTransfersTooltip", "Skip based on sizes only, not mod-time");

            // Sync behavior translations
            SafeAddTranslation(englishTranslations, "DeleteEmptySourceDirsFlag", "Delete Empty Source Dirs (--delete-empty-src-dirs)");
            SafeAddTranslation(englishTranslations, "DeleteEmptySourceDirsTooltip", "Delete empty source directories after sync");
            SafeAddTranslation(englishTranslations, "IgnoreExistingFlag", "Ignore Existing (--ignore-existing)");
            SafeAddTranslation(englishTranslations, "IgnoreExistingTooltip", "Skip all files that exist on destination");
            SafeAddTranslation(englishTranslations, "CompareSizeOnlyFlag", "Compare Size Only (--size-only)");
            SafeAddTranslation(englishTranslations, "CompareSizeOnlyTooltip", "Skip based on sizes only, not mod-time");
            SafeAddTranslation(englishTranslations, "PreservePermissionsFlag", "Preserve Permissions (--perms)");
            SafeAddTranslation(englishTranslations, "PreservePermissionsTooltip", "Preserve file permissions when possible");
            SafeAddTranslation(englishTranslations, "PreserveTimestampsFlag", "Preserve Timestamps (--times)");
            SafeAddTranslation(englishTranslations, "PreserveTimestampsTooltip", "Preserve file modification times");
            SafeAddTranslation(englishTranslations, "ExcludedPathsTooltip", "Comma-separated list of paths to exclude (e.g., *.tmp, cache/*)");
            SafeAddTranslation(englishTranslations, "AllowedPathsTooltip", "Comma-separated list of paths to include (e.g., *.jpg, docs/*)");
            SafeAddTranslation(englishTranslations, "FilterSyntaxNote", "Use rclone filter syntax (e.g., *.tmp, folder/*, !important.txt)");

            // Performance settings translations
            SafeAddTranslation(englishTranslations, "ParallelTransfersTooltip", "Number of file transfers to run in parallel");
            SafeAddTranslation(englishTranslations, "CheckWorkersTooltip", "Number of workers for checking");
            SafeAddTranslation(englishTranslations, "BufferSizeTooltip", "Buffer size for copying files");
            SafeAddTranslation(englishTranslations, "UseMemoryMappingTooltip", "Use memory mapping for reads if possible");
            SafeAddTranslation(englishTranslations, "BandwidthLimitTooltip", "Set bandwidth limit in MiB/s (0 for unlimited)");
            SafeAddTranslation(englishTranslations, "CutoffModeTooltip", "How to handle bandwidth limit");
            SafeAddTranslation(englishTranslations, "CutoffModeHard", "Hard");
            SafeAddTranslation(englishTranslations, "CutoffModeSoft", "Soft");
            SafeAddTranslation(englishTranslations, "BandwidthControl", "Bandwidth Control");

            SafeAddTranslation(englishTranslations, "LanguageNoteRestart", "Note: Application will use selected language after restart");

            // In AddEnglishTranslations method
            SafeAddTranslation(englishTranslations, "DisableDirectoryTraversalTooltip", "Don't traverse destination file system on copy");

            SafeAddTranslation(englishTranslations, "SyncHistory", "Sync History");
            SafeAddTranslation(englishTranslations, "NoSyncHistoryAvailable", "No sync history available.");

            SafeAddTranslation(englishTranslations, "ProgressPercentage", "{0}% complete");
            SafeAddTranslation(englishTranslations, "StartingSync", "Starting sync...");
            SafeAddTranslation(englishTranslations, "PreparingToSync", "Preparing to sync...");
            SafeAddTranslation(englishTranslations, "SyncInitializing", "INITIALIZING");
            SafeAddTranslation(englishTranslations, "SyncCompleted", "Sync completed");
            SafeAddTranslation(englishTranslations, "SyncCompletedSuccess", "Sync completed successfully");
            SafeAddTranslation(englishTranslations, "SyncCancelled", "Sync operation cancelled.");
            SafeAddTranslation(englishTranslations, "SyncFailed", "Sync failed: {0}");
            SafeAddTranslation(englishTranslations, "CalculatingProgress", "Calculating...");
            SafeAddTranslation(englishTranslations, "CalculatingTime", "Calculating time...");
            SafeAddTranslation(englishTranslations, "ZeroSpeed", "0 B/s");
            SafeAddTranslation(englishTranslations, "InvalidSourceTarget", "Please select valid source and target settings.");
            SafeAddTranslation(englishTranslations, "WaitingForSync", "Waiting for sync...");
            SafeAddTranslation(englishTranslations, "TransferSpeed", "Transfer Speed");
            SafeAddTranslation(englishTranslations, "TimeRemaining", "Time Remaining");
            SafeAddTranslation(englishTranslations, "CheckOperation", "Checking files");
            SafeAddTranslation(englishTranslations, "CopyOperation", "Copying files");
            SafeAddTranslation(englishTranslations, "DeleteOperation", "Deleting files");
            SafeAddTranslation(englishTranslations, "SkipOperation", "Skipping files");
            SafeAddTranslation(englishTranslations, "FileVerificationCheck", "File verification check: {0}");
            SafeAddTranslation(englishTranslations, "NoFilesToTransfer", "No files to transfer");
            SafeAddTranslation(englishTranslations, "ScanningOperation", "SCANNING");
            SafeAddTranslation(englishTranslations, "SyncOperation", "SYNC");
            SafeAddTranslation(englishTranslations, "SkippingOperation", "SKIPPING");
            SafeAddTranslation(englishTranslations, "DeletingOperation", "DELETING");
            SafeAddTranslation(englishTranslations, "CopyingOperation", "COPYING");
            SafeAddTranslation(englishTranslations, "ScanningForChanges", "Scanning for changes...");
            SafeAddTranslation(englishTranslations, "skipping", "Skipping...");
            SafeAddTranslation(englishTranslations, "Copying", "Copying...");
            SafeAddTranslation(englishTranslations, "Deleted", "Deleted...");
            SafeAddTranslation(englishTranslations, "SkipOperation", "Skipping...");
        }

        private void AddHungarianTranslations()
        {
            // Global and Common Elements
            SafeAddTranslation(hungarianTranslations, "ApplicationTitle", "DriveSync");
            SafeAddTranslation(hungarianTranslations, "Settings", "Beállítások");
            SafeAddTranslation(hungarianTranslations, "Schedule", "Ütemezés");
            SafeAddTranslation(hungarianTranslations, "Browse", "Tallózás...");
            SafeAddTranslation(hungarianTranslations, "Cancel", "Mégse");
            SafeAddTranslation(hungarianTranslations, "Save", "Mentés");
            SafeAddTranslation(hungarianTranslations, "CreateTask", "Létrehozás");

            // Main Window
            SafeAddTranslation(hungarianTranslations, "SyncConfiguration", "Szinkronizálási beállítások");
            SafeAddTranslation(hungarianTranslations, "Source", "Forrás");
            SafeAddTranslation(hungarianTranslations, "Target", "Cél");
            SafeAddTranslation(hungarianTranslations, "SyncMode", "Szinkronizálási mód");
            SafeAddTranslation(hungarianTranslations, "SyncNow", "Szinkronizálás most");
            SafeAddTranslation(hungarianTranslations, "SyncHistory", "Szinkronizálási előzmények");
            SafeAddTranslation(hungarianTranslations, "SyncModeOption1", "Tükrözéses szinkronizálás");
            SafeAddTranslation(hungarianTranslations, "SyncModeOption2", "Biztonsági mentés (Másolás)");
            SafeAddTranslation(hungarianTranslations, "SyncModeOption3", "Fájlok áthelyezése");
            SafeAddTranslation(hungarianTranslations, "StartTime", "Kezdési idő");

            // Remote loading status messages
            SafeAddTranslation(hungarianTranslations, "RemotesLoadedMessage", "{0} felhő tárhely betöltve");
            SafeAddTranslation(hungarianTranslations, "NoRemotesFound", "Nem találhatók felhő tárhelyek. Konfiguráljon rclone-t.");
            SafeAddTranslation(hungarianTranslations, "RemotesLoadError", "Nem sikerült betölteni a felhő tárhelyeket. Ellenőrizze az rclone konfigurációt.");
            SafeAddTranslation(hungarianTranslations, "PleaseSelectSourceRemoteFirst", "Először válasszon forrás tárolót.");
            SafeAddTranslation(hungarianTranslations, "PleaseSelectTargetRemoteFirst", "Először válasszon céloldali tárolót.");
            SafeAddTranslation(hungarianTranslations, "SourceRemoteSelected", "Forrás tárhely kiválasztva: {0}");
            SafeAddTranslation(hungarianTranslations, "TargetRemoteSelected", "Cél tárhely kiválasztva: {0}");
            SafeAddTranslation(hungarianTranslations, "SelectedSourceRemote", "Kiválasztott forrás tárhely:");
            SafeAddTranslation(hungarianTranslations, "SelectedTargetRemote", "Kiválasztott cél tárhely:");
            SafeAddTranslation(hungarianTranslations, "SelectedSourcePath", "Forrás útvonal kiválasztva: {0}");
            SafeAddTranslation(hungarianTranslations, "SelectedTargetPath", "Cél útvonal kiválasztva: {0}");

            // Scheduled Sync Window
            SafeAddTranslation(hungarianTranslations, "ScheduleSyncTitle", "DriveSync Ütemezése");
            SafeAddTranslation(hungarianTranslations, "SourceRemote", "Forrás Remote");
            SafeAddTranslation(hungarianTranslations, "TargetRemote", "Cél Remote");
            SafeAddTranslation(hungarianTranslations, "StartDate", "Kezdés Dátuma");
            SafeAddTranslation(hungarianTranslations, "Repeat", "Ismétlés");

            // Repeat Options
            SafeAddTranslation(hungarianTranslations, "OneTimeSyncOption", "Egyszeri");
            SafeAddTranslation(hungarianTranslations, "DailySyncOption", "Napi szinkron");
            SafeAddTranslation(hungarianTranslations, "WeeklySyncOption", "Heti szinkron");
            SafeAddTranslation(hungarianTranslations, "MonthlySyncOption", "Havi szinkron");
            SafeAddTranslation(hungarianTranslations, "OneTime", "Egyszeri");
            SafeAddTranslation(hungarianTranslations, "Daily", "Naponta");
            SafeAddTranslation(hungarianTranslations, "Weekly", "Hetente");
            SafeAddTranslation(hungarianTranslations, "Monthly", "Havonta");

            // Tab Headers
            SafeAddTranslation(hungarianTranslations, "General", "Általános");
            SafeAddTranslation(hungarianTranslations, "Conditions", "Feltételek");
            SafeAddTranslation(hungarianTranslations, "Settings", "Beállítások");

            // Power and Idle Settings
            SafeAddTranslation(hungarianTranslations, "PowerSettingsSection", "Energiagazdálkodás");
            SafeAddTranslation(hungarianTranslations, "OnACPowerStart", "Csak hálózati tápellátás esetén");
            SafeAddTranslation(hungarianTranslations, "StopOnBattery", "Leállítás akkumulátorra váltáskor");
            SafeAddTranslation(hungarianTranslations, "WakeToRun", "Számítógép felébresztése futtatáshoz");
            SafeAddTranslation(hungarianTranslations, "IdleSettingsSection", "Tétlenségi beállítások");
            SafeAddTranslation(hungarianTranslations, "RunOnlyIfIdle", "Futtatás csak tétlen állapotban");
            SafeAddTranslation(hungarianTranslations, "IdleDuration", "Tétlenségi időtartam");
            SafeAddTranslation(hungarianTranslations, "Minutes", "perc");
            SafeAddTranslation(hungarianTranslations, "AdditionalSettingsSection", "További beállítások");
            SafeAddTranslation(hungarianTranslations, "StartWhenMissed", "Indítás kihagyás esetén");
            SafeAddTranslation(hungarianTranslations, "AllowDemandStart", "Igény szerinti indítás engedélyezése");

            // Network Settings
            SafeAddTranslation(hungarianTranslations, "NetworkSettingsSection", "Hálózati beállítások");
            SafeAddTranslation(hungarianTranslations, "RunOnlyIfNetworkAvailable", "Futtatás csak ha van hálózat");
            SafeAddTranslation(hungarianTranslations, "AnyNetwork", "Bármely hálózat");

            // Additional and Instance Handling
            SafeAddTranslation(hungarianTranslations, "ForceStopIfNotEnding", "Kényszerített leállítás, ha nem fejeződik be");

            SafeAddTranslation(hungarianTranslations, "TaskSettingsSection", "Feladat beállítások");
            SafeAddTranslation(hungarianTranslations, "DisableTask", "Feladat letiltása");
            SafeAddTranslation(hungarianTranslations, "HiddenTask", "Rejtett feladat");
            SafeAddTranslation(hungarianTranslations, "StopAfter", "Leállítás után");

            SafeAddTranslation(hungarianTranslations, "InstanceHandlingSection", "Példánykezelés");
            SafeAddTranslation(hungarianTranslations, "IfTaskRunning", "Ha a feladat már fut");

            // Instance Handling Options
            SafeAddTranslation(hungarianTranslations, "QueueInstances", "Sorba állítás");
            SafeAddTranslation(hungarianTranslations, "StopExistingInstances", "Meglévő leállítása");
            SafeAddTranslation(hungarianTranslations, "IgnoreNewInstances", "Új figyelmen kívül hagyása");
            SafeAddTranslation(hungarianTranslations, "ParallelInstances", "Párhuzamos");

            // Application Settings
            SafeAddTranslation(hungarianTranslations, "ApplicationSettings", "Alkalmazás Beállítások");
            SafeAddTranslation(hungarianTranslations, "RcloneExecutablePath", "Rclone Program Útvonala");
            SafeAddTranslation(hungarianTranslations, "DefaultSyncMode", "Alapértelmezett Sync");

            // Notification Settings Section
            SafeAddTranslation(hungarianTranslations, "NotificationSettings", "Értesítési Beállítások");
            SafeAddTranslation(hungarianTranslations, "ShowNotifications", "Értesítések Megjelenítése");
            SafeAddTranslation(hungarianTranslations, "EnableSound", "Hang Engedélyezése");
            SafeAddTranslation(hungarianTranslations, "SoundPath", "Hang Útvonala");

            // Bandwidth Throttling Section
            SafeAddTranslation(hungarianTranslations, "BandwidthThrottling", "Sávszélesség Korlátozás");
            SafeAddTranslation(hungarianTranslations, "UploadLimit", "Feltöltési Korlát");
            SafeAddTranslation(hungarianTranslations, "DownloadLimit", "Letöltési Korlát");

            // File Filters Section
            SafeAddTranslation(hungarianTranslations, "FileFilters", "Fájl Szűrők"); SafeAddTranslation(hungarianTranslations, "ExclusionPatterns", "Kizárási Minták");

            // User Interface Section
            SafeAddTranslation(hungarianTranslations, "UserInterface", "Felhasználói Felület");
            SafeAddTranslation(hungarianTranslations, "Theme", "Téma");
            SafeAddTranslation(hungarianTranslations, "FontSize", "Betűméret");

            // Advanced Options Section
            SafeAddTranslation(hungarianTranslations, "AdvancedOptions", "Speciális Beállítások");
            SafeAddTranslation(hungarianTranslations, "RetryCount", "Újrapróbálkozások Száma");
            SafeAddTranslation(hungarianTranslations, "ErrorReporting", "Hibajelentés");

            // Common Buttons and Labels
            SafeAddTranslation(hungarianTranslations, "Cancel", "Mégse");
            SafeAddTranslation(hungarianTranslations, "Save", "Mentés");

            // Theme Options
            // Theme values - make sure they're in the correct section and properly capitalized
            SafeAddTranslation(hungarianTranslations, "Light", "Világos");
            SafeAddTranslation(hungarianTranslations, "Dark", "Sötét");
            SafeAddTranslation(hungarianTranslations, "System", "Rendszer");

            // Sync Mode Values
            SafeAddTranslation(hungarianTranslations, "Mirror", "Tükrözés");
            SafeAddTranslation(hungarianTranslations, "Backup", "Biztonsági mentés");
            SafeAddTranslation(hungarianTranslations, "Move", "Áthelyezés");

            // Default Repeat
            SafeAddTranslation(hungarianTranslations, "DefaultRepeat", "Alapértelmezett Ismétlés");

            //SettingsWindow Dark and Light Theme translation 
            SafeAddTranslation(hungarianTranslations, "Light", "Világos");
            SafeAddTranslation(hungarianTranslations, "Dark", "Sötét");

            // Directory Browser Dialog translations
            SafeAddTranslation(hungarianTranslations, "SelectDirectoryFromRemote", "Könyvtár kiválasztása innen: {0}");
            SafeAddTranslation(hungarianTranslations, "SelectDirectory", "Könyvtár kiválasztása");
            SafeAddTranslation(hungarianTranslations, "LoadingDirectories", "Könyvtárak betöltése...");
            SafeAddTranslation(hungarianTranslations, "Select", "Kiválaszt");

            // LogViewerWindow button translations
            SafeAddTranslation(hungarianTranslations, "CopyLog", "Napló másolása");
            SafeAddTranslation(hungarianTranslations, "Export", "Exportálás");
            SafeAddTranslation(hungarianTranslations, "Close", "Bezárás");

            // AdvancedSettingsPanel
            SafeAddTranslation(hungarianTranslations, "LoggingAndErrorReporting", "Naplózás és Hibajelentés");
            SafeAddTranslation(hungarianTranslations, "EnableErrorReporting", "Hibajelentés Engedélyezése");
            SafeAddTranslation(hungarianTranslations, "LogVerbosity", "Naplózás Részletessége");
            SafeAddTranslation(hungarianTranslations, "MaxLogSize", "Maximális Napló Méret (MB)");
            SafeAddTranslation(hungarianTranslations, "MaxLogFiles", "Maximális Napló Fájlok");
            SafeAddTranslation(hungarianTranslations, "EnableDetailedLogging", "Részletes Naplózás Engedélyezése");
            SafeAddTranslation(hungarianTranslations, "BackupAndRecovery", "Biztonsági mentés és Helyreállítás");
            SafeAddTranslation(hungarianTranslations, "KeepBackupHistory", "Biztonsági mentési előzmények megőrzése");
            SafeAddTranslation(hungarianTranslations, "MaxBackupVersions", "Maximális Biztonsági mentés Verziók");
            SafeAddTranslation(hungarianTranslations, "BackupRetention", "Biztonsági mentés megőrzése (Nap)");
            SafeAddTranslation(hungarianTranslations, "CompressBackups", "Biztonsági mentések tömörítése");
            SafeAddTranslation(hungarianTranslations, "CompressionLevel", "Tömörítés Szintje");

            // SecuritySettingsPanel
            SafeAddTranslation(hungarianTranslations, "Authentication", "Hitelesítés");
            SafeAddTranslation(hungarianTranslations, "RequirePasswordOnStartup", "Jelszó szükséges indításkor");
            SafeAddTranslation(hungarianTranslations, "RequirePasswordForSettings", "Jelszó szükséges a beállításokhoz");
            SafeAddTranslation(hungarianTranslations, "RequirePasswordForManualSync", "Jelszó szükséges a manuális szinkronizáláshoz");
            SafeAddTranslation(hungarianTranslations, "ApplicationPassword", "Alkalmazás Jelszó");
            SafeAddTranslation(hungarianTranslations, "PasswordResetNote", "Megjegyzés: Ha elfelejti a jelszót, újra kell telepítenie az alkalmazást.");

            // SyncSettingsPanel
            SafeAddTranslation(hungarianTranslations, "FileFiltering", "Fájl Szűrés");
            SafeAddTranslation(hungarianTranslations, "ExcludedPaths", "Kizárt Útvonalak (vesszővel elválasztva)");
            SafeAddTranslation(hungarianTranslations, "AllowedPaths", "Engedélyezett Útvonalak (vesszővel elválasztva)");

            SafeAddTranslation(hungarianTranslations, "AppearanceHeader", "Megjelenés");
            SafeAddTranslation(hungarianTranslations, "ThemeMode", "Téma Mód");
            SafeAddTranslation(hungarianTranslations, "FontSettings", "Betűtípus Beállítások");
            SafeAddTranslation(hungarianTranslations, "Language", "Nyelv");
            SafeAddTranslation(hungarianTranslations, "English", "English");
            SafeAddTranslation(hungarianTranslations, "Hungarian", "Magyar");

            SafeAddTranslation(hungarianTranslations, "RcloneConfiguration", "Rclone Konfiguráció");
            SafeAddTranslation(hungarianTranslations, "RcloneExecutablePath", "Rclone Végrehajtható Útvonal");
            SafeAddTranslation(hungarianTranslations, "BrowseRclone", "Tallózás...");
            SafeAddTranslation(hungarianTranslations, "DefaultSyncMode", "Alapértelmezett Szinkronizáció");
            SafeAddTranslation(hungarianTranslations, "StartupBehavior", "Indítási Viselkedés");
            SafeAddTranslation(hungarianTranslations, "LaunchAtStartup", "Indítás rendszerindításkor");
            SafeAddTranslation(hungarianTranslations, "MinimizeToTray", "Lehúzás a tálcára");
            SafeAddTranslation(hungarianTranslations, "RestartRequired", "Újraindítás szükséges");

            SafeAddTranslation(hungarianTranslations, "PanelGeneral", "Általános");
            SafeAddTranslation(hungarianTranslations, "PanelInterface", "Felület");
            SafeAddTranslation(hungarianTranslations, "PanelSecurity", "Biztonság");
            SafeAddTranslation(hungarianTranslations, "PanelSync", "Szinkronizáció");
            SafeAddTranslation(hungarianTranslations, "PanelPerformance", "Teljesítmény");
            SafeAddTranslation(hungarianTranslations, "PanelAdvanced", "Speciális");

            SafeAddTranslation(hungarianTranslations, "ApplicationSettings", "Alkalmazás Beállítások");
            SafeAddTranslation(hungarianTranslations, "Reset", "Visszaállítás");
            SafeAddTranslation(hungarianTranslations, "Cancel", "Mégse");
            SafeAddTranslation(hungarianTranslations, "SaveSettings", "Beállítások Mentése");

            SafeAddTranslation(hungarianTranslations, "FileEncryption", "Fájl Titkosítás");
            SafeAddTranslation(hungarianTranslations, "EnableFileEncryption", "Fájl Titkosítás Engedélyezése");
            SafeAddTranslation(hungarianTranslations, "EncryptionKey", "Titkosítási Kulcs");
            SafeAddTranslation(hungarianTranslations, "GenerateKey", "Kulcs Generálása");
            SafeAddTranslation(hungarianTranslations, "Authentication", "Hitelesítés");
            SafeAddTranslation(hungarianTranslations, "RequirePasswordOnStartup", "Jelszó szükséges indításkor");
            SafeAddTranslation(hungarianTranslations, "RequirePasswordForSettings", "Jelszó szükséges a beállításokhoz");
            SafeAddTranslation(hungarianTranslations, "RequirePasswordForManualSync", "Jelszó szükséges manuális szinkronizáláshoz");
            SafeAddTranslation(hungarianTranslations, "ApplicationPassword", "Alkalmazás Jelszó");
            SafeAddTranslation(hungarianTranslations, "ResetPassword", "Jelszó Visszaállítása");
            SafeAddTranslation(hungarianTranslations, "PasswordResetNote", "Megjegyzés: Ha elfelejti a jelszót, újra kell telepítenie az alkalmazást.");

            SafeAddTranslation(hungarianTranslations, "SyncBehavior", "Szinkronizáció Viselkedése");
            SafeAddTranslation(hungarianTranslations, "EnableAutomaticSync", "Automatikus Szinkronizáció Engedélyezése");
            SafeAddTranslation(hungarianTranslations, "SyncInterval", "Szinkronizáció Gyakorisága (Órák)");
            SafeAddTranslation(hungarianTranslations, "SyncOnStartup", "Szinkronizáció Indításkor");
            SafeAddTranslation(hungarianTranslations, "SyncOnShutdown", "Szinkronizáció Leállításkor");

            SafeAddTranslation(hungarianTranslations, "FileFiltering", "Fájl Szűrés");
            SafeAddTranslation(hungarianTranslations, "ExcludedPaths", "Kizárt Útvonalak (vesszővel elválasztva)");
            SafeAddTranslation(hungarianTranslations, "IncludedPaths", "Tartalmazott Útvonalak (vesszővel elválasztva)");
            SafeAddTranslation(hungarianTranslations, "VerifyTransfers", "Átvitelek Ellenőrzése");
            SafeAddTranslation(hungarianTranslations, "PreserveFileTimes", "Fájl Időbélyegek Megőrzése");
            SafeAddTranslation(hungarianTranslations, "PreserveFilePermissions", "Fájl Jogosultságok Megőrzése");

            SafeAddTranslation(hungarianTranslations, "BandwidthThrottling", "Sávszélesség Korlátozás");
            SafeAddTranslation(hungarianTranslations, "UploadLimit", "Feltöltési Korlát (KB/s)");
            SafeAddTranslation(hungarianTranslations, "DownloadLimit", "Letöltési Korlát (KB/s)");

            SafeAddTranslation(hungarianTranslations, "TransferSettings", "Átviteli Beállítások");
            SafeAddTranslation(hungarianTranslations, "MaxParallelTransfers", "Maximális Párhuzamos Átvitelek");
            SafeAddTranslation(hungarianTranslations, "EnableDirectoryCaching", "Könyvtár Gyorsítótár Engedélyezése");
            SafeAddTranslation(hungarianTranslations, "CacheExpiration", "Gyorsítótár Lejárata (Órák)");

            SafeAddTranslation(hungarianTranslations, "TransferSettings", "Átviteli Beállítások");
            SafeAddTranslation(hungarianTranslations, "MaxParallelTransfers", "Maximális Párhuzamos Átvitelek");
            SafeAddTranslation(hungarianTranslations, "ParallelTransfers", "Párhuzamos Átvitelek");
            SafeAddTranslation(hungarianTranslations, "CheckWorkers", "Ellenőrző Szálak");
            SafeAddTranslation(hungarianTranslations, "BufferSize", "Puffer Méret (MiB)");
            SafeAddTranslation(hungarianTranslations, "UseMemoryMapping", "Memória Leképezés Használata");
            SafeAddTranslation(hungarianTranslations, "BandwidthLimitMiBs", "Sávszélesség Korlát (MiB/s, 0 = korlátlan)");
            SafeAddTranslation(hungarianTranslations, "CutoffMode", "Megszakítási Mód");
            SafeAddTranslation(hungarianTranslations, "ErrorHandling", "Hibaellenőrzés");
            SafeAddTranslation(hungarianTranslations, "RetryCount", "Újrapróbálkozások Száma");
            SafeAddTranslation(hungarianTranslations, "ConnectionTimeout", "Kapcsolat Időtúllépés (másodperc)");
            SafeAddTranslation(hungarianTranslations, "AdvancedTransferOptions", "Speciális Átviteli Beállítások");
            SafeAddTranslation(hungarianTranslations, "DisableDirectoryTraversal", "Könyvtár Bejárás Kikapcsolása (--no-traverse)");
            SafeAddTranslation(hungarianTranslations, "PerformanceWarning", "Ezek a beállítások jelentősen befolyásolhatják a teljesítményt. Óvatosan módosítsa.");
            SafeAddTranslation(hungarianTranslations, "TransferSecurity", "Átviteli Biztonság");
            SafeAddTranslation(hungarianTranslations, "VerifyChecksumFlag", "Ellenőrzőösszeg Ellenőrzése (--checksum)");
            SafeAddTranslation(hungarianTranslations, "CompareModTimeFlag", "Módosítási Idő Összehasonlítása (--ignore-times)");
            SafeAddTranslation(hungarianTranslations, "VerifyTransfersFlag", "Átvitelek Ellenőrzése (--size-only)");
            SafeAddTranslation(hungarianTranslations, "SecurityPerformanceNote", "Ezek a beállítások befolyásolják a szinkronizálás teljesítményét. Csak szükség esetén engedélyezze az ellenőrzési opciókat.");

            SafeAddTranslation(hungarianTranslations, "FastListFlag", "Gyors Lista (--fast-list)");
            SafeAddTranslation(hungarianTranslations, "FastListTooltip", "Rekurzív lista használata, ha elérhető. Több memóriát használ, de kevesebb tranzakciót.");
            SafeAddTranslation(hungarianTranslations, "UseServerModTimeFlag", "Szerver Módosítási Idő (--use-server-modtime)");
            SafeAddTranslation(hungarianTranslations, "UseServerModTimeTooltip", "Szerver módosítási idő használata objektum metaadat helyett");
            SafeAddTranslation(hungarianTranslations, "CheckAccessFlag", "Hozzáférés Ellenőrzése (--check-access)");
            SafeAddTranslation(hungarianTranslations, "CheckAccessTooltip", "Forrás és cél megfelelő hozzáférési jogosultságainak ellenőrzése");
            SafeAddTranslation(hungarianTranslations, "CreateEmptyDirsFlag", "Üres Forrás Könyvtárak Létrehozása (--create-empty-src-dirs)");
            SafeAddTranslation(hungarianTranslations, "CreateEmptyDirsTooltip", "Üres forrás könyvtárak létrehozása a célhelyen");

            SafeAddTranslation(hungarianTranslations, "VerifyChecksumTooltip", "Ellenőrzőösszeg és méret alapján kihagy, nem módosítási idő és méret alapján");
            SafeAddTranslation(hungarianTranslations, "CompareModTimeTooltip", "Módosítási idők összehasonlítása a fájlváltozásokhoz");
            SafeAddTranslation(hungarianTranslations, "VerifyTransfersTooltip", "Csak méret alapján hagy ki, nem módosítási idő alapján");

            SafeAddTranslation(hungarianTranslations, "Authentication", "Hitelesítés");
            SafeAddTranslation(hungarianTranslations, "TransferSecurity", "Átviteli Biztonság");
            SafeAddTranslation(hungarianTranslations, "SecurityPerformanceNote", "Ezek a beállítások befolyásolják a szinkronizálás teljesítményét. Az ellenőrzési opciókat csak szükség esetén engedélyezze.");

            SafeAddTranslation(hungarianTranslations, "DeleteEmptySourceDirsFlag", "Üres Forrás Könyvtárak Törlése (--delete-empty-src-dirs)");
            SafeAddTranslation(hungarianTranslations, "DeleteEmptySourceDirsTooltip", "Üres forrás könyvtárak törlése szinkronizálás után");
            SafeAddTranslation(hungarianTranslations, "IgnoreExistingFlag", "Meglévők Kihagyása (--ignore-existing)");
            SafeAddTranslation(hungarianTranslations, "IgnoreExistingTooltip", "Minden meglévő fájl kihagyása a célhelyen");
            SafeAddTranslation(hungarianTranslations, "CompareSizeOnlyFlag", "Csak Méret Összehasonlítása (--size-only)");
            SafeAddTranslation(hungarianTranslations, "CompareSizeOnlyTooltip", "Kihagyás csak méret alapján, nem módosítási idő alapján");
            SafeAddTranslation(hungarianTranslations, "PreservePermissionsFlag", "Jogosultságok Megőrzése (--perms)");
            SafeAddTranslation(hungarianTranslations, "PreservePermissionsTooltip", "Fájl jogosultságok megőrzése, ha lehetséges");
            SafeAddTranslation(hungarianTranslations, "PreserveTimestampsFlag", "Időbélyegek Megőrzése (--times)");
            SafeAddTranslation(hungarianTranslations, "PreserveTimestampsTooltip", "Fájl módosítási idők megőrzése");
            SafeAddTranslation(hungarianTranslations, "ExcludedPathsTooltip", "Vesszővel elválasztott lista a kizárandó útvonalakról (pl., *.tmp, cache/*)");
            SafeAddTranslation(hungarianTranslations, "AllowedPathsTooltip", "Vesszővel elválasztott lista az engedélyezett útvonalakról (pl., *.jpg, docs/*)");
            SafeAddTranslation(hungarianTranslations, "FilterSyntaxNote", "Használja az rclone szűrő szintaxist (pl., *.tmp, folder/*, !important.txt)");

            SafeAddTranslation(hungarianTranslations, "ParallelTransfersTooltip", "Párhuzamosan futó fájlátvitelek száma");
            SafeAddTranslation(hungarianTranslations, "CheckWorkersTooltip", "Ellenőrző folyamatok száma");
            SafeAddTranslation(hungarianTranslations, "BufferSizeTooltip", "Pufferméret a fájlmásoláshoz");
            SafeAddTranslation(hungarianTranslations, "UseMemoryMappingTooltip", "Memória leképezés használata olvasáshoz, ha lehetséges");
            SafeAddTranslation(hungarianTranslations, "BandwidthLimitTooltip", "Sávszélesség korlát MiB/s-ban (0 = korlátlan)");
            SafeAddTranslation(hungarianTranslations, "CutoffModeTooltip", "Sávszélesség korlát kezelési módja");
            SafeAddTranslation(hungarianTranslations, "CutoffModeHard", "Szigorú");
            SafeAddTranslation(hungarianTranslations, "CutoffModeSoft", "Rugalmas");
            SafeAddTranslation(hungarianTranslations, "BandwidthControl", "Sávszélesség Vezérlés");
            SafeAddTranslation(hungarianTranslations, "ParallelTransfers", "Párhuzamos Átvitelek");
            SafeAddTranslation(hungarianTranslations, "CheckWorkers", "Ellenőrző Szálak");
            SafeAddTranslation(hungarianTranslations, "BufferSize", "Puffer Méret (MiB)");
            SafeAddTranslation(hungarianTranslations, "UseMemoryMapping", "Memória Leképezés Használata");
            SafeAddTranslation(hungarianTranslations, "BandwidthLimitMiBs", "Sávszélesség Korlát (MiB/s, 0 = korlátlan)");

            // Advanced options section
            SafeAddTranslation(hungarianTranslations, "ErrorHandling", "Hibakezelés");
            SafeAddTranslation(hungarianTranslations, "RetryCount", "Újrapróbálkozások Száma");
            SafeAddTranslation(hungarianTranslations, "ConnectionTimeout", "Kapcsolat Időtúllépés (másodperc)");
            SafeAddTranslation(hungarianTranslations, "AdvancedTransferOptions", "Speciális Átviteli Beállítások");
            SafeAddTranslation(hungarianTranslations, "UseFastList", "Gyors Lista Használata");
            SafeAddTranslation(hungarianTranslations, "DisableDirectoryTraversal", "Könyvtár Bejárás Letiltása");
            SafeAddTranslation(hungarianTranslations, "PerformanceWarning", "Ezek a beállítások jelentősen befolyásolhatják a teljesítményt. Óvatosan módosítsa.");

            SafeAddTranslation(hungarianTranslations, "LanguageNoteRestart", "Megjegyzés: Az alkalmazás a kiválasztott nyelvet az újraindítás után fogja használni");

            // In AddHungarianTranslations method
            SafeAddTranslation(hungarianTranslations, "DisableDirectoryTraversalTooltip", "Ne járja be a célrendszer fájlrendszerét másolás közben");

            SafeAddTranslation(hungarianTranslations, "SyncHistory", "Szinkronizálási előzmények");
            SafeAddTranslation(hungarianTranslations, "NoSyncHistoryAvailable", "Nincs szinkronizálási előzmény.");

            // Progress text
            SafeAddTranslation(hungarianTranslations, "ProgressPercentage", "{0}% kész");
            SafeAddTranslation(hungarianTranslations, "StartingSync", "Szinkronizálás indítása...");
            SafeAddTranslation(hungarianTranslations, "PreparingToSync", "Szinkronizálás előkészítése...");
            SafeAddTranslation(hungarianTranslations, "SyncInitializing", "ELŐKÉSZÍTÉS");
            SafeAddTranslation(hungarianTranslations, "SyncCompleted", "Szinkronizálás befejezve");
            SafeAddTranslation(hungarianTranslations, "SyncCompletedSuccess", "A szinkronizálás sikeresen befejeződött");
            SafeAddTranslation(hungarianTranslations, "SyncCancelled", "Szinkronizálás megszakítva.");
            SafeAddTranslation(hungarianTranslations, "SyncFailed", "Szinkronizálás sikertelen: {0}");
            SafeAddTranslation(hungarianTranslations, "CalculatingProgress", "Számítás...");
            SafeAddTranslation(hungarianTranslations, "CalculatingTime", "Idő számítása...");
            SafeAddTranslation(hungarianTranslations, "ZeroSpeed", "0 B/s");
            SafeAddTranslation(hungarianTranslations, "InvalidSourceTarget", "Kérem válasszon érvényes forrást és célt.");
            SafeAddTranslation(hungarianTranslations, "WaitingForSync", "Várakozás szinkronizálásra...");
            SafeAddTranslation(hungarianTranslations, "TransferSpeed", "Átviteli Sebesség");
            SafeAddTranslation(hungarianTranslations, "TimeRemaining", "Hátralévő Idő");
            SafeAddTranslation(hungarianTranslations, "CheckOperation", "Fájlok ellenőrzése");
            SafeAddTranslation(hungarianTranslations, "CopyOperation", "Fájlok másolása");
            SafeAddTranslation(hungarianTranslations, "DeleteOperation", "Fájlok törlése");
            SafeAddTranslation(hungarianTranslations, "SkipOperation", "Fájlok kihagyása");
            SafeAddTranslation(hungarianTranslations, "FileVerificationCheck", "Fájl ellenőrzés: {0}");
            SafeAddTranslation(hungarianTranslations, "NoFilesToTransfer", "Nincs átvitelre váró fájl");
            SafeAddTranslation(hungarianTranslations, "ScanningOperation", "KERESÉS");
            SafeAddTranslation(hungarianTranslations, "SyncOperation", "SZINKRONIZÁLÁS");
            SafeAddTranslation(hungarianTranslations, "SkippingOperation", "KIHAGYÁS");
            SafeAddTranslation(hungarianTranslations, "DeletingOperation", "TÖRLÉS");
            SafeAddTranslation(hungarianTranslations, "CopyingOperation", "MÁSOLÁS");
            SafeAddTranslation(hungarianTranslations, "ScanningForChanges", "Változások keresése...");
            SafeAddTranslation(hungarianTranslations, "ScanningForChanges", "Változások keresése...");
            SafeAddTranslation(hungarianTranslations, "Calculating", "Számítás...");
            SafeAddTranslation(hungarianTranslations, "CalculatingTime", "Idő számítása...");
            SafeAddTranslation(hungarianTranslations, "Deleted", "Törölve...");
            SafeAddTranslation(hungarianTranslations, "Copied", "Másolva...");
            SafeAddTranslation(hungarianTranslations, "Skipped", "Kihagyva...");
            SafeAddTranslation(hungarianTranslations, "CheckingFiles", "Fájlok ellenőrzése...");
            SafeAddTranslation(hungarianTranslations, "SyncComplete", "Szinkronizálás kész");
            SafeAddTranslation(hungarianTranslations, "Ready", "Kész");
            SafeAddTranslation(hungarianTranslations, "PercentComplete", "{0}% kész");
            SafeAddTranslation(hungarianTranslations, "ProcessedItem", "{0} feldolgozva");
            SafeAddTranslation(hungarianTranslations, "TimeLeft", "{0} van hátra");
            SafeAddTranslation(hungarianTranslations, "TransferSpeed", "Átviteli sebesség");
            SafeAddTranslation(hungarianTranslations, "RemainingTime", "Hátralévő idő");
            SafeAddTranslation(hungarianTranslations, "Copying", "Másolás...");
            SafeAddTranslation(hungarianTranslations, "ScanningForChanges", "változások keresése...");
            SafeAddTranslation(hungarianTranslations, "Calculating", "Számítás...");
            SafeAddTranslation(hungarianTranslations, "Calculating... van hátra", "Számítás... van hátra");
            SafeAddTranslation(hungarianTranslations, "Scanning for changes", "Változások keresése");
            SafeAddTranslation(hungarianTranslations, "Deleted", "Törölve");
            SafeAddTranslation(hungarianTranslations, "skipping", "Kihagyás...");
            SafeAddTranslation(hungarianTranslations, "Copying", "Másolás...");
            SafeAddTranslation(hungarianTranslations, "Deleted", "Törölve...");
            SafeAddTranslation(hungarianTranslations, "SkipOperation", "Kihagyva...");

            SafeAddTranslation(hungarianTranslations, "skipping", "Kihagyás...");
            SafeAddTranslation(hungarianTranslations, "Skipping", "Kihagyás...");
            SafeAddTranslation(hungarianTranslations, "SKIPPING", "Kihagyás...");


        }

        public string GetString(string key)
        {
            var translations = CurrentLanguage == AppLanguage.Hungarian ? hungarianTranslations : englishTranslations;
            return translations.TryGetValue(key, out string value) ? value : key;
        }

        public string this[string key] => GetString(key);

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class LocExtension : IValueConverter
    {
        private readonly string key;

        public LocExtension(string resourceKey)
        {
            key = resourceKey;
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return LocalizationManager.Instance[key];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}