using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace DriveSync.Infrastructure.Services
{
    public class RcloneManager
    {
        private readonly ILogger<RcloneManager> _logger;
        private readonly IRcloneVersionService _versionService;
        private readonly string _baseDirectory;
        private string _currentRclonePath;

        public event EventHandler<string> RclonePathChanged;

        public string CurrentRclonePath
        {
            get => _currentRclonePath;
            private set
            {
                if (_currentRclonePath != value)
                {
                    _currentRclonePath = value;
                    RclonePathChanged?.Invoke(this, value);
                }
            }
        }

        public RcloneManager(ILogger<RcloneManager> logger, IRcloneVersionService versionService)
        {
            _logger = logger;
            _versionService = versionService;
            _baseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DriveSync",
                "RcloneVersions"
            );
            Directory.CreateDirectory(_baseDirectory);
        }

        public async Task InitializeAsync()
        {
            try
            {
                // First check if we already have the latest version installed
                var (isUpdateAvailable, latestVersion, currentVersion) =
                    await _versionService.CheckRcloneVersion();

                string existingPath = GetExistingRclonePath(latestVersion);
                if (existingPath != null)
                {
                    // We already have the latest version, use it
                    _logger.LogInformation($"Using existing latest rclone v{latestVersion}");
                    CurrentRclonePath = existingPath;
                    return;
                }

                if (isUpdateAvailable)
                {
                    _logger.LogInformation($"Updating rclone from v{currentVersion} to v{latestVersion}");
                    string downloadPath = Path.Combine(
                        _baseDirectory,
                        $"rclone-v{latestVersion}-windows-amd64.zip"
                    );

                    bool downloaded = await _versionService.DownloadLatestRclone(downloadPath);
                    if (downloaded)
                    {
                        CurrentRclonePath = await ExtractRclone(downloadPath, latestVersion);
                        CleanupOldVersions(latestVersion);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to download latest version, using existing rclone");
                        CurrentRclonePath = GetExistingRclonePath(currentVersion) ?? "rclone";
                    }
                }
                else
                {
                    // If no update is available, use the current version
                    CurrentRclonePath = GetExistingRclonePath(currentVersion) ?? "rclone";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing rclone");
                CurrentRclonePath = "rclone"; // Fallback to system rclone
                throw;
            }
        }

        private async Task<string> ExtractRclone(string zipPath, string version)
        {
            string versionDirectory = Path.Combine(_baseDirectory, $"v{version}");
            string rcloneExecutable = Path.Combine(versionDirectory, "rclone.exe");

            try
            {
                Directory.CreateDirectory(versionDirectory);

                _logger.LogInformation("Extracting rclone");
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var rcloneEntry = archive.GetEntry($"rclone-v{version}-windows-amd64/rclone.exe");
                    if (rcloneEntry == null)
                    {
                        throw new FileNotFoundException("rclone.exe not found in zip archive");
                    }

                    if (File.Exists(rcloneExecutable))
                    {
                        File.Delete(rcloneExecutable);
                    }

                    rcloneEntry.ExtractToFile(rcloneExecutable);
                }

                // Cleanup zip file
                File.Delete(zipPath);

                return rcloneExecutable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting rclone v{version}");
                throw;
            }
        }

        private string GetExistingRclonePath(string version)
        {
            if (string.IsNullOrEmpty(version) || version == "0.0.0")
                return null;

            string rclonePath = Path.Combine(_baseDirectory, $"v{version}", "rclone.exe");
            return File.Exists(rclonePath) ? rclonePath : null;
        }

        private void CleanupOldVersions(string latestVersion)
        {
            try
            {
                var directories = Directory.GetDirectories(_baseDirectory);
                foreach (var dir in directories)
                {
                    if (!dir.Contains($"v{latestVersion}"))
                    {
                        try
                        {
                            Directory.Delete(dir, true);
                            _logger.LogInformation($"Cleaned up old version directory: {dir}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Could not delete old version directory: {dir}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old versions");
            }
        }
    }
}