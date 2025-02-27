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
        private SemaphoreSlim _initializationLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;
        private const int MAX_RETRIES = 3;
        private const int RETRY_DELAY_MS = 1000;

        public event EventHandler<string> RclonePathChanged;
        public event EventHandler<double> DownloadProgress;
        public event EventHandler<string> InitializationError;
        public event EventHandler<string> StatusMessage;
        public event EventHandler<(string Message, bool IsError)> OperationResult;

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

            // Subscribe to version service events
            _versionService.VersionCheckError += (sender, message) =>
                InitializationError?.Invoke(this, message);

            _versionService.ErrorOccurred += (sender, error) =>
            {
                string userMessage = GetUserFriendlyErrorMessage(error.ErrorType, error.Message);
                OperationResult?.Invoke(this, (userMessage, true));
            };

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
                StatusMessage?.Invoke(this, "Checking rclone version...");

                var (isUpdateAvailable, latestVersion, currentVersion) =
                    await _versionService.CheckRcloneVersion();

                string existingPath = GetExistingRclonePath(latestVersion);
                if (existingPath != null)
                {
                    _logger.LogInformation($"Using existing latest rclone v{latestVersion}");
                    CurrentRclonePath = existingPath;
                    StatusMessage?.Invoke(this, $"Using rclone v{latestVersion}");
                }
                else if (isUpdateAvailable)
                {
                    _logger.LogInformation($"Updating rclone from v{currentVersion} to v{latestVersion}");
                    StatusMessage?.Invoke(this, $"Downloading rclone v{latestVersion}...");

                    string downloadPath = Path.Combine(
                        _baseDirectory,
                        $"rclone-v{latestVersion}-windows-amd64.zip"
                    );

                    var progress = new Progress<double>(p =>
                    {
                        DownloadProgress?.Invoke(this, p);
                        StatusMessage?.Invoke(this, $"Downloading rclone v{latestVersion}: {p:F1}%");
                    });

                    bool downloaded = await DownloadAndExtractWithRetry(downloadPath, latestVersion, progress);
                    if (downloaded)
                    {
                        string extractedPath = Path.Combine(
                            _baseDirectory,
                            $"v{latestVersion}",
                            "rclone.exe"
                        );

                        if (await ValidateExtractedFile(extractedPath))
                        {
                            CurrentRclonePath = extractedPath;
                            await CleanupOldVersionsAsync(latestVersion);
                            StatusMessage?.Invoke(this, $"Successfully updated to rclone v{latestVersion}");
                            OperationResult?.Invoke(this, ($"Updated to rclone v{latestVersion}", false));
                        }
                        else
                        {
                            StatusMessage?.Invoke(this, "Error validating downloaded version");
                            CurrentRclonePath = GetExistingRclonePath(currentVersion) ?? "rclone";
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Failed to download latest version, trying second latest version...");
                        StatusMessage?.Invoke(this, "Update failed, trying earlier version...");

                        // Try to get available releases
                        var availableReleases = await _versionService.GetAvailableReleases(5);
                        if (availableReleases.Count > 1)
                        {
                            string secondLatestVersion = availableReleases[1]; // Index 1 is the second latest

                            _logger.LogInformation($"Trying to download second latest version: v{secondLatestVersion}");
                            StatusMessage?.Invoke(this, $"Downloading rclone v{secondLatestVersion}...");

                            string secondDownloadPath = Path.Combine(
                                _baseDirectory,
                                $"rclone-v{secondLatestVersion}-windows-amd64.zip"
                            );

                            bool secondDownloaded = await DownloadSecondLatestVersion(secondDownloadPath, secondLatestVersion, progress);
                            if (secondDownloaded)
                            {
                                string secondExtractedPath = Path.Combine(
                                    _baseDirectory,
                                    $"v{secondLatestVersion}",
                                    "rclone.exe"
                                );

                                if (await ValidateExtractedFile(secondExtractedPath))
                                {
                                    CurrentRclonePath = secondExtractedPath;
                                    await CleanupOldVersionsAsync(secondLatestVersion);
                                    StatusMessage?.Invoke(this, $"Successfully downloaded rclone v{secondLatestVersion}");
                                    OperationResult?.Invoke(this, ($"Installed rclone v{secondLatestVersion}", false));
                                }
                                else
                                {
                                    // Fall back to current version or system rclone if validation fails
                                    StatusMessage?.Invoke(this, "Error validating downloaded version");
                                    CurrentRclonePath = GetExistingRclonePath(currentVersion) ?? "rclone";
                                }
                            }
                            else
                            {
                                // Fall back to current version or system rclone if download fails
                                _logger.LogWarning("Failed to download second latest version, using existing rclone");
                                StatusMessage?.Invoke(this, "Update failed, using existing version");
                                CurrentRclonePath = GetExistingRclonePath(currentVersion) ?? "rclone";
                            }
                        }
                        else
                        {
                            // Fall back to current version or system rclone if no second latest available
                            _logger.LogWarning("No second latest version available, using existing rclone");
                            StatusMessage?.Invoke(this, "Update failed, using existing version");
                            CurrentRclonePath = GetExistingRclonePath(currentVersion) ?? "rclone";
                        }
                    }
                }
                else
                {
                    CurrentRclonePath = GetExistingRclonePath(currentVersion) ?? "rclone";
                    StatusMessage?.Invoke(this, $"Using rclone v{currentVersion}");
                }

                _isInitialized = true;
                _logger.LogInformation("RcloneManager initialization completed successfully");
            }
            catch (RcloneVersionException ex)
            {
                _logger.LogError(ex, "RcloneVersionException during initialization");
                HandleVersionError(ex);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing RcloneManager");
                InitializationError?.Invoke(this, $"Initialization failed: {ex.Message}");
                StatusMessage?.Invoke(this, "Error during initialization");
                CurrentRclonePath = "rclone"; // Fallback to system rclone
                throw;
            }
            finally
            {
                _initializationLock.Release();
            }
        }

        private async Task<bool> DownloadAndExtractWithRetry(string downloadPath, string version, IProgress<double> progress)
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    if (await _versionService.DownloadLatestRclone(downloadPath, progress))
                    {
                        string extractPath = Path.Combine(_baseDirectory, $"v{version}");
                        return await ExtractRclone(downloadPath, extractPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Download attempt {attempt} failed");
                    if (attempt == MAX_RETRIES)
                    {
                        OperationResult?.Invoke(this, ($"Failed to download after {MAX_RETRIES} attempts", true));
                        return false;
                    }
                    await Task.Delay(RETRY_DELAY_MS * attempt);
                }
            }
            return false;
        }

        private async Task<bool> DownloadSecondLatestVersion(string downloadPath, string version, IProgress<double> progress)
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    if (await _versionService.DownloadSpecificVersion(version, downloadPath, progress))
                    {
                        string extractPath = Path.Combine(_baseDirectory, $"v{version}");
                        return await ExtractRclone(downloadPath, extractPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Download attempt {attempt} for version {version} failed");
                    if (attempt == MAX_RETRIES)
                    {
                        OperationResult?.Invoke(this, ($"Failed to download version {version} after {MAX_RETRIES} attempts", true));
                        return false;
                    }
                    await Task.Delay(RETRY_DELAY_MS * attempt);
                }
            }
            return false;
        }

        private async Task<bool> ExtractRclone(string zipPath, string extractPath)
        {
            try
            {
                _logger.LogInformation($"Extracting rclone to {extractPath}");
                StatusMessage?.Invoke(this, "Extracting rclone...");

                if (!Directory.Exists(extractPath))
                {
                    Directory.CreateDirectory(extractPath);
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    var rcloneEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith("rclone.exe", StringComparison.OrdinalIgnoreCase));

                    if (rcloneEntry == null)
                    {
                        throw new FileNotFoundException("rclone.exe not found in zip archive");
                    }

                    string rcloneExecutable = Path.Combine(extractPath, "rclone.exe");
                    if (File.Exists(rcloneExecutable))
                    {
                        File.Delete(rcloneExecutable);
                    }

                    await Task.Run(() => rcloneEntry.ExtractToFile(rcloneExecutable));
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

                _logger.LogInformation("Successfully extracted rclone");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting rclone");
                OperationResult?.Invoke(this, ("Error extracting rclone", true));

                // Cleanup on failure
                try
                {
                    if (Directory.Exists(extractPath))
                    {
                        Directory.Delete(extractPath, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }

                throw;
            }
        }

        public async Task<bool> RollbackToVersion(string version)
        {
            try
            {
                StatusMessage?.Invoke(this, $"Rolling back to version {version}...");

                if (await _versionService.RollbackToVersion(version))
                {
                    string rollbackPath = Path.Combine(_baseDirectory, $"v{version}", "rclone.exe");
                    if (File.Exists(rollbackPath))
                    {
                        CurrentRclonePath = rollbackPath;
                        StatusMessage?.Invoke(this, $"Successfully rolled back to v{version}");
                        OperationResult?.Invoke(this, ($"Rolled back to v{version}", false));
                        return true;
                    }
                }

                StatusMessage?.Invoke(this, "Rollback failed");
                OperationResult?.Invoke(this, ("Rollback failed", true));
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rolling back to version {version}");
                StatusMessage?.Invoke(this, "Rollback failed");
                OperationResult?.Invoke(this, ($"Error during rollback: {ex.Message}", true));
                return false;
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

        private async Task<bool> ValidateExtractedFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                OperationResult?.Invoke(this, ("Extracted file not found", true));
                return false;
            }

            try
            {
                // Verify the file can run
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = filePath,
                        Arguments = "version",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    var tcs = new TaskCompletionSource<bool>();
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                    cts.Token.Register(() => tcs.TrySetResult(false));

                    process.Start();
                    var processTask = process.WaitForExitAsync(cts.Token);

                    if (await Task.WhenAny(processTask, tcs.Task) == tcs.Task)
                    {
                        try { process.Kill(); } catch { }
                        OperationResult?.Invoke(this, ("Validation timeout", true));
                        return false;
                    }

                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating extracted file");
                OperationResult?.Invoke(this, ("Error validating file", true));
                return false;
            }
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

                // Keep the current version and the previous version
                var directoriesToKeep = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    Path.Combine(_baseDirectory, $"v{currentVersion}")
                };

                // Add the most recent version
                if (directories.Count > 0 &&
                    !directories[0].FullName.Equals(directoriesToKeep.First(), StringComparison.OrdinalIgnoreCase))
                {
                    directoriesToKeep.Add(directories[0].FullName);
                }

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

        private void HandleVersionError(RcloneVersionException ex)
        {
            string userMessage = GetUserFriendlyErrorMessage(ex.ErrorType, ex.Message);
            InitializationError?.Invoke(this, userMessage);
            StatusMessage?.Invoke(this, userMessage);
            OperationResult?.Invoke(this, (userMessage, true));
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

        private string GetUserFriendlyErrorMessage(RcloneErrorType errorType, string technicalMessage)
        {
            return errorType switch
            {
                RcloneErrorType.NetworkError => "Unable to connect to the update server. Please check your internet connection.",
                RcloneErrorType.DownloadError => "Failed to download the update. Please try again later.",
                RcloneErrorType.ValidationError => "The downloaded update appears to be invalid or corrupted.",
                RcloneErrorType.ExtractionError => "Failed to extract the update files.",
                RcloneErrorType.VersionCheckError => "Unable to check for updates at this time.",
                _ => $"An unexpected error occurred: {technicalMessage}"
            };
        }
    }
}