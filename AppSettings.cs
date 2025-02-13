using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;

namespace DriveSync.WPF
{
    public class AppSettings
    {
        private static readonly string SettingsFile = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        // Application Configuration
        public string RcloneExecutablePath { get; set; } = "rclone";
        public string DefaultSyncMode { get; set; } = "Mirror";
        public bool LaunchAtStartup { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;

        // Interface Configuration
        public string Theme { get; set; } = "Light";
        public bool UseSystemTheme { get; set; } = true;
        public int FontSize { get; set; } = 14;
        public string Language { get; set; } = "English";

        // Security Settings
        public bool RequirePassword { get; set; } = false;
        public bool RequirePasswordForSettings { get; set; } = false;
        public bool RequirePasswordForSync { get; set; } = false;

        // Sync Behavior Settings
        public bool DeleteEmptyDirs { get; set; } = false;
        public bool IgnoreExisting { get; set; } = false;
        public bool CompareSize { get; set; } = true;
        public bool PreservePermissions { get; set; } = true;
        public bool PreserveTimes { get; set; } = true;
        public string[] ExcludedPaths { get; set; } = Array.Empty<string>();
        public string[] IncludedPaths { get; set; } = Array.Empty<string>();

        // Transfer Security Settings
        public bool Checksum { get; set; } = false;
        public bool IgnoreTimes { get; set; } = false;
        public bool CreateEmptyDirs { get; set; } = true;


        // Rclone-specific Transfer Configuration
        public int MaxTransfers { get; set; } = 4;
        public int CheckerThreads { get; set; } = 8;
        public int BufferSize { get; set; } = 16;
        public bool UseMmap { get; set; } = true;
        public int RetryCount { get; set; } = 3;
        public int ConnectTimeout { get; set; } = 60;
        public bool FastList { get; set; } = true;
        public bool UseServerModTime { get; set; } = true;

        public bool NoTraverse { get; set; } = false;


        // Bandwidth Configuration
        public int BandwidthLimit { get; set; } = 0;
        public string CutoffMode { get; set; } = "hard";

        public string GetEffectiveTheme()
        {
            if (UseSystemTheme)
            {
                return DetectSystemTheme();
            }
            return Theme;
        }

        public static string DetectSystemTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object appsUseLightTheme = key.GetValue("AppsUseLightTheme");
                        return appsUseLightTheme?.ToString() == "0" ? "Dark" : "Light";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error detecting system theme: {ex.Message}");
            }
            return "Light";
        }

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);

                    if (settings.UseSystemTheme)
                    {
                        settings.Theme = DetectSystemTheme();
                    }

                    settings.ValidateAndFixSettings();
                    return settings;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
            }

            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                ValidateAndFixSettings();
                string directory = Path.GetDirectoryName(SettingsFile);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(SettingsFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings: {ex.Message}");
                throw;
            }
        }

        private void ValidateAndFixSettings()
        {
            // Interface settings validation
            FontSize = Math.Max(8, Math.Min(FontSize, 72));

            // Transfer settings validation
            MaxTransfers = Math.Max(1, Math.Min(MaxTransfers, 32));
            CheckerThreads = Math.Max(1, Math.Min(CheckerThreads, 64));
            BufferSize = Math.Max(1, Math.Min(BufferSize, 1024));
            RetryCount = Math.Max(0, Math.Min(RetryCount, 100));
            ConnectTimeout = Math.Max(1, Math.Min(ConnectTimeout, 3600));

            // Bandwidth settings validation
            BandwidthLimit = Math.Max(0, BandwidthLimit);
            CutoffMode = CutoffMode.ToLower() == "soft" ? "soft" : "hard";

            // Array validations
            ExcludedPaths ??= Array.Empty<string>();
            IncludedPaths ??= Array.Empty<string>();
        }
    }
}