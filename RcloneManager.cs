using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace DriveSync.Infrastructure.Services
{
    public class RcloneManager
    {
        private readonly ILogger<RcloneManager> _logger;
        private readonly IRcloneVersionService _versionService;
        private readonly string _baseDirectory;
        private string _currentRclonePath;
        private const int VERSION_HISTORY_LIMIT = 2;
        private SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;

        public event EventHandler<string> RclonePathChanged;
        public event EventHandler<double> DownloadProgress;
        public event EventHandler<string> InitializationError;

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

            _versionService.VersionCheckError += (sender, message) =>
                InitializationError?.Invoke(this, message);

            EnsureBaseDirectoryExists();
        }

        public async Task InitializeAsync()
        {
            try
            {
                await _initializationLock.WaitAsync();
                if (_isInitialized)
                {
                    return;
                }

                _logger.LogInformation("Initializing RcloneManager...");

                var (isUpdateAvailable, latestVersion, currentVersion) =
                    await _versionService.CheckRcloneVersion();

                string existingPath = GetExistingRclonePath(latestVersion);
                if (existingPath != null)
                {
                    _logger.LogInformation($"Using existing latest rclone v{latestVersion}");
                    CurrentRclonePath = existingPath;
                }
                else if (isUpdateAvailable)
                {
                    _logger.LogInformation($"Updating rclone from v{currentVersion} to v{latestVersion}");

                    string downloadPath = Path.Combine(
                        _baseDirectory,
                        $"rclone-v{latestVersion}-windows-amd64.zip"
                    );

                    var progress = new Progress<double>(p => DownloadProgress?.Invoke(this, p));
                    bool downloaded = await _versionService.DownloadLatestRclone(downloadPath, progress);

                    if (downloaded)
                    {
                        CurrentRclonePath = await ExtractRclone(downloadPath, latestVersion);
                        await CleanupOldVersionsAsync(latestVersion);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to download latest version, using existing rclone");
                        CurrentRclonePath = GetExistingRclonePath(currentVersion) ?? "rclone";
                    }
                }
                else
                {
                    CurrentRclonePath = GetExistingRclonePath(currentVersion) ?? "rclone";
                }

                _isInitialized = true;
                _logger.LogInformation("RcloneManager initialization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing RcloneManager");
                InitializationError?.Invoke(this, $"Initialization failed: {ex.Message}");
                CurrentRclonePath = "rclone"; // Fallback to system rclone
                throw;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        private async Task<string> ExtractRclone(string zipPath, string version)
        {
            string versionDirectory = Path.Combine(_baseDirectory, $"v{version}");
            string rcloneExecutable = Path.Combine(versionDirectory, "rclone.exe");

            try
            {
                _logger.LogInformation($"Extracting rclone v{version} to {versionDirectory}");

                if (!Directory.Exists(versionDirectory))
                {
                    Directory.CreateDirectory(versionDirectory);
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var rcloneEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith("rclone.exe", StringComparison.OrdinalIgnoreCase));

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

                // Verify the extracted file
                if (!File.Exists(rcloneExecutable))
                {
                    throw new FileNotFoundException("Failed to extract rclone.exe");
                }

                // Cleanup zip file after successful extraction
                try
                {
                    File.Delete(zipPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete zip file after extraction");
                }

                _logger.LogInformation($"Successfully extracted rclone v{version}");
                return rcloneExecutable;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error extracting rclone v{version}");

                // Cleanup on failure
                try
                {
                    if (Directory.Exists(versionDirectory))
                    {
                        Directory.Delete(versionDirectory, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                throw;
            }
        }

        private string GetExistingRclonePath(string version)
        {
            if (string.IsNullOrEmpty(version) || version == "0.0.0")
                return null;

            string rclonePath = Path.Combine(_baseDirectory, $"v{version}", "rclone.exe");

            if (File.Exists(rclonePath))
            {
                try
                {
                    // Verify the executable
                    using (var process = new System.Diagnostics.Process())
                    {
                        process.StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = rclonePath,
                            Arguments = "version",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        process.Start();
                        process.WaitForExit(5000); // 5 second timeout

                        if (process.ExitCode == 0)
                        {
                            return rclonePath;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Failed to verify rclone executable at {rclonePath}");
                    return null;
                }
            }

            return null;
        }

        private async Task CleanupOldVersionsAsync(string currentVersion)
        {
            try
            {
                _logger.LogInformation("Starting cleanup of old rclone versions");

                var directories = Directory.GetDirectories(_baseDirectory)
                    .Select(d => new DirectoryInfo(d))
                    .Where(d => d.Name.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(d => d.CreationTime)
                    .ToList();

                // Keep the current version and the specified number of previous versions
                var directoriesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    Path.Combine(_baseDirectory, $"v{currentVersion}")
                };

                // Add the most recent versions up to the limit
                directoriesToKeep.UnionWith(
                    directories
                        .Where(d => !d.Name.Equals($"v{currentVersion}", StringComparison.OrdinalIgnoreCase))
                        .Take(VERSION_HISTORY_LIMIT - 1)
                        .Select(d => d.FullName)
                );

                var deleteTasks = directories
                    .Where(d => !directoriesToKeep.Contains(d.FullName))
                    .Select(async d =>
                    {
                        try
                        {
                            await Task.Run(() => Directory.Delete(d.FullName, true));
                            _logger.LogInformation($"Cleaned up old version directory: {d.Name}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Could not delete old version directory: {d.Name}");
                        }
                    });

                await Task.WhenAll(deleteTasks);

                _logger.LogInformation("Cleanup of old rclone versions completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cleanup of old versions");
            }
        }

        private void EnsureBaseDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_baseDirectory))
                {
                    Directory.CreateDirectory(_baseDirectory);
                    _logger.LogInformation($"Created base directory: {_baseDirectory}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to create base directory: {_baseDirectory}");
                throw;
            }
        }

        public async Task<bool> ReinitializeAsync()
        {
            try
            {
                _isInitialized = false;
                await InitializeAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reinitialize RcloneManager");
                return false;
            }
        }
    }
}