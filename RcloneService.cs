using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DriveSync.WPF.Localization;
using Microsoft.Extensions.Logging;
using DriveSync.WPF;

namespace DriveSync.Infrastructure.Services
{
    public class SyncProgress
    {
        private string GetThemeColor(string colorType)
        {
            var settings = AppSettings.Load();
            string theme = settings.GetEffectiveTheme();

            return (theme, colorType) switch
            {
                // Dark Theme Colors
                ("Dark", "Speed") => "#FFFFFF",    // White for all elements in Dark Theme
                ("Dark", "ETA") => "#FFFFFF",      // White
                ("Dark", "File") => "#FFFFFF",     // White
                ("Dark", "Date") => "#FFFFFF",     // White

                // Light Theme Colors
                ("Light", "Speed") => "#0078D4",   // Primary color (blue)
                ("Light", "ETA") => "#0078D4",     // Primary color (blue)
                ("Light", "File") => "#0078D4",    // Primary color (blue)
                ("Light", "Date") => "#0078D4",    // Primary color (blue)

                _ => "#757575"  // Fallback neutral color
            };
        }

        public string SpeedColor => GetThemeColor("Speed");
        public string ETAColor => GetThemeColor("ETA");
        public string FileColor => GetThemeColor("File");
        public string DateColor => GetThemeColor("Date");

        public double PercentComplete { get; set; }
        public string Speed { get; set; }
        public string TimeRemaining { get; set; }
        public string CurrentFile { get; set; }
        public string CurrentOperation { get; set; }
        public bool IsScanning { get; set; } = false; // New property to track scanning state
    }


    public class RcloneService : IRcloneService
    {
        private readonly ILogger<RcloneService> _logger;
        private const string RcloneExecutable = "rclone";

        // Operation type constants remain unchanged.
        private const string OP_CHECK = "CHECK";
        private const string OP_COPY = "COPY";
        private const string OP_DELETE = "DELETE";
        private const string OP_SKIP = "SKIP";
        private const string OP_MOVE = "MOVE";

        public RcloneService(ILogger<RcloneService> logger)
        {
            _logger = logger;
        }

        public async Task<bool> ValidateRcloneInstallation()
        {
            try
            {
                var result = await ExecuteCommand("version");
                return result.Contains("rclone");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate rclone installation");
                return false;
            }
        }

        public async Task<string[]> ListRemotes()
        {
            try
            {
                var output = await ExecuteCommand("listremotes");
                return output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to list remotes");
                throw;
            }
        }

        public async Task<string[]> ListDirectories(string remote, string path = "")
        {
            try
            {
                var fullPath = string.IsNullOrEmpty(path) ? remote + ":" : $"{remote}:{path}";
                var output = await ExecuteCommand($"lsf {fullPath} --dirs-only");
                return output.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to list directories for {remote}:{path}");
                throw;
            }
        }

        public async Task<string> SyncDirectories(
            string sourceRemote,
            string sourcePath,
            string targetRemote,
            string targetPath,
            SyncType syncType,
            IProgress<SyncProgress> progress,
            CancellationToken cancellationToken)
        {
            var sourceFullPath = $"{sourceRemote}:{sourcePath}";
            var targetFullPath = $"{targetRemote}:{targetPath}";

            // Choose the appropriate command based on sync type.
            string commandVerb = syncType switch
            {
                SyncType.Mirror => "sync",
                SyncType.Backup => "copy",
                SyncType.Move => "move",
                _ => "sync"
            };

            var arguments = new StringBuilder();
            arguments.Append($"{commandVerb} \"{sourceFullPath}\" \"{targetFullPath}\" ");
            arguments.Append("--progress ");
            arguments.Append("--stats-one-line ");
            arguments.Append("--stats 1s ");
            arguments.Append("-vv "); // Very verbose output

            _logger.LogInformation($"Starting {commandVerb} with command: rclone {arguments}");

            var fullLog = new StringBuilder();
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = RcloneExecutable,
                Arguments = arguments.ToString(),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Create a constant for the scanning message
            string scanningMessage = LocalizationManager.Instance["ScanningForChanges"];

            // Initialize with explicit scanning state
            var syncProgress = new SyncProgress
            {
                PercentComplete = 0,
                CurrentOperation = LocalizationManager.Instance["ScanningOperation"],
                CurrentFile = scanningMessage, // Set this explicitly
                Speed = LocalizationManager.Instance["ZeroSpeed"],
                TimeRemaining = LocalizationManager.Instance["CalculatingProgress"],
                IsScanning = true
            };

            // Report initial state
            progress?.Report(syncProgress);

            var errorBuilder = new StringBuilder();
            bool operationStarted = false;
            DateTime lastUpdateTime = DateTime.Now;

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;
                fullLog.AppendLine(e.Data);
                _logger.LogDebug("rclone output: {line}", e.Data);

                // Check for the start of actual file operations
                if (syncProgress.IsScanning &&
                    (e.Data.Contains("Copied (new)", StringComparison.OrdinalIgnoreCase) ||
                     e.Data.Contains("Deleted", StringComparison.OrdinalIgnoreCase) ||
                     e.Data.Contains("Moved", StringComparison.OrdinalIgnoreCase)))
                {
                    syncProgress.IsScanning = false;
                    operationStarted = true;
                }

                // Process the line
                ProcessProgressOutput(e.Data, syncProgress, progress, syncType);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;
                fullLog.AppendLine(e.Data);
                _logger.LogDebug("rclone error: {line}", e.Data);
                ProcessProgressOutput(e.Data, syncProgress, progress, syncType);
                errorBuilder.AppendLine(e.Data);
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                while (!process.HasExited)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Periodically refresh the scanning message to ensure it's visible
                    if (syncProgress.IsScanning && !operationStarted &&
                        (DateTime.Now - lastUpdateTime).TotalMilliseconds > 500)
                    {
                        syncProgress.CurrentOperation = LocalizationManager.Instance["ScanningOperation"];
                        syncProgress.CurrentFile = scanningMessage;
                        progress.Report(syncProgress);
                        lastUpdateTime = DateTime.Now;
                    }

                    await Task.Delay(100, cancellationToken);
                }

                if (process.ExitCode != 0)
                {
                    var errorMessage = errorBuilder.ToString();
                    _logger.LogError($"Rclone {commandVerb} failed with exit code {process.ExitCode}: {errorMessage}");
                    throw new Exception($"Sync failed: {errorMessage}");
                }

                syncProgress.PercentComplete = 100;
                syncProgress.CurrentOperation = LocalizationManager.Instance["SyncComplete"];
                syncProgress.CurrentFile = LocalizationManager.Instance["SyncCompletedSuccess"];
                syncProgress.Speed = LocalizationManager.Instance["ZeroSpeed"];
                syncProgress.TimeRemaining = "-";
                syncProgress.IsScanning = false;
                progress.Report(syncProgress);

                _logger.LogInformation($"Successfully executed {commandVerb} from {sourceFullPath} to {targetFullPath}");
                return fullLog.ToString();
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                _logger.LogError(ex, $"Failed to execute {commandVerb} from {sourceFullPath} to {targetFullPath}");
                throw;
            }
        }




        private async Task<string> ExecuteCommand(string arguments)
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = RcloneExecutable,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    output.AppendLine(e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    error.AppendLine(e.Data);
                }
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"Command failed: {error}");
                }
                return output.ToString().TrimEnd();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to execute rclone command: {arguments}");
                throw;
            }
        }

        private void ProcessProgressOutput(string line, SyncProgress progressObj, IProgress<SyncProgress> reporter, SyncType syncType)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line))
                    return;

                _logger.LogDebug("=== START PROCESSING LINE ===");
                _logger.LogDebug("Raw input line: {line}", line);

                // If in scanning mode, maintain the scanning message for most log lines
                if (progressObj.IsScanning)
                {
                    // Don't change CurrentFile during scanning phase for most log lines
                    if (!line.Contains(" Copied ", StringComparison.OrdinalIgnoreCase) &&
                        !line.Contains(" Deleted ", StringComparison.OrdinalIgnoreCase) &&
                        !line.Contains(" Moved ", StringComparison.OrdinalIgnoreCase))
                    {
                        // Make sure CurrentFile is set to scanning message
                        if (progressObj.CurrentFile != LocalizationManager.Instance["ScanningForChanges"])
                        {
                            progressObj.CurrentFile = LocalizationManager.Instance["ScanningForChanges"];
                        }

                        // Only update progress, speed and remaining time during scanning
                        UpdateStatistics(line, progressObj, reporter);
                        _logger.LogDebug("=== END PROCESSING LINE (SCANNING) ===\n");
                        return;
                    }
                    else
                    {
                        // Transition from scanning to operation phase
                        progressObj.IsScanning = false;
                    }

                }

                // Process copy operations
                var copyRegex = new Regex(@"INFO\s+:\s+([^:]+):\s*Copied", RegexOptions.IgnoreCase);
                var copyMatch = copyRegex.Match(line);
                if (copyMatch.Success)
                {
                    string filename = copyMatch.Groups[1].Value.Trim();
                    progressObj.CurrentOperation = LocalizationManager.Instance["CopyOperation"];
                    CreateFileOperation(progressObj, "CopyOperation", filename, DateTime.Now);
                    reporter?.Report(progressObj);
                    return;
                }

                // Process delete operations
                var deletionRegex = new Regex(@"(\d{4}/\d{2}/\d{2}\s+\d{2}:\d{2}:\d{2})\s+INFO\s+:\s+([^:]+):\s*Deleted", RegexOptions.IgnoreCase);
                var deletionMatch = deletionRegex.Match(line);
                if (deletionMatch.Success)
                {
                    string timestamp = deletionMatch.Groups[1].Value;
                    string filename = deletionMatch.Groups[2].Value.Trim();

                    // For Move operation, always show as Move
                    if (syncType == SyncType.Move)
                    {
                        progressObj.CurrentOperation = LocalizationManager.Instance["MoveOperation"];
                        CreateFileOperation(progressObj, "MoveOperation", filename, DateTime.Parse(timestamp));
                        _logger.LogDebug("Move operation deletion detected: {filename}", filename);
                    }
                    else
                    {
                        progressObj.CurrentOperation = LocalizationManager.Instance["DeleteOperation"];
                        CreateFileOperation(progressObj, "DeleteOperation", filename, DateTime.Parse(timestamp));
                        _logger.LogDebug("Delete operation detected: {filename}", filename);
                    }

                    reporter?.Report(progressObj);
                    return;
                }

                // Global move operation detection
                if (line.Contains("rclone move ", StringComparison.OrdinalIgnoreCase))
                {
                    progressObj.CurrentOperation = LocalizationManager.Instance["MoveOperation"];
                    CreateFileOperation(progressObj, "MoveOperation", "Directory move in progress", DateTime.Now);
                    _logger.LogDebug("Global move operation detected");
                    reporter?.Report(progressObj);
                    return;
                }

                // Update statistics (percent, speed, time remaining)
                UpdateStatistics(line, progressObj, reporter);

                // Sync completion check
                if (line.Contains("There was nothing to transfer", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug("Move operation completed - nothing to transfer");
                    progressObj.PercentComplete = 100;
                    progressObj.CurrentOperation = LocalizationManager.Instance["SyncComplete"];
                    progressObj.CurrentFile = LocalizationManager.Instance["SyncCompletedSuccess"];
                    progressObj.Speed = LocalizationManager.Instance["ZeroSpeed"];
                    progressObj.TimeRemaining = "-";
                    reporter?.Report(progressObj);
                }

                _logger.LogDebug("=== END PROCESSING LINE ===\n");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing progress output: {line}", line);
            }
        }

        // Helper method to update statistics during both scanning and operation phases
        private void UpdateStatistics(string line, SyncProgress progressObj, IProgress<SyncProgress> reporter)
        {
            // Update percentage if available
            var percentRegex = new Regex(@"(?i)(\d+(?:\.\d+)?)\s*%");
            var percentMatch = percentRegex.Match(line);
            if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out double percent))
            {
                progressObj.PercentComplete = Math.Min(100, Math.Max(0, percent));
                _logger.LogDebug("Progress Update - Percentage: {percent}%", percent);
                reporter?.Report(progressObj);
            }

            // Update speed and time remaining if available
            var statsRegex = new Regex(@"(?:Transferred:.*?\s+)?\(*\s*([\d\.]+\s*\w+/s)\s*\)?(?:.*ETA[:\s]*([\dhms]+))?", RegexOptions.IgnoreCase);
            var statsMatch = statsRegex.Match(line);
            if (statsMatch.Success)
            {
                bool statsUpdated = false;
                if (statsMatch.Groups[1].Success && !string.IsNullOrWhiteSpace(statsMatch.Groups[1].Value))
                {
                    progressObj.Speed = statsMatch.Groups[1].Value.Trim();
                    _logger.LogDebug("Speed Updated: {speed}", progressObj.Speed);
                    statsUpdated = true;
                }
                if (statsMatch.Groups[2].Success && !string.IsNullOrWhiteSpace(statsMatch.Groups[2].Value))
                {
                    progressObj.TimeRemaining = statsMatch.Groups[2].Value.Trim();
                    _logger.LogDebug("Time Remaining Updated: {timeRemaining}", progressObj.TimeRemaining);
                    statsUpdated = true;
                }
                if (statsUpdated)
                {
                    reporter?.Report(progressObj);
                }
            }
        }


        private void CreateFileOperation(SyncProgress progressObj, string operation, string filename, DateTime timestamp)
        {
            try
            {
                // Special case for scanning - use plain text without JSON
                if (progressObj.IsScanning ||
                    operation.Contains("Scanning", StringComparison.OrdinalIgnoreCase) ||
                    operation.Contains("KERESÉS", StringComparison.OrdinalIgnoreCase))
                {
                    progressObj.CurrentFile = LocalizationManager.Instance["ScanningForChanges"];
                    return;
                }

                // For other operations, create JSON string for icon mapping
                string opKey = GetOperationKey(operation);
                string json = $"{{\"Operation\":\"{opKey}\",\"Filename\":\"{filename}\",\"Timestamp\":\"{timestamp:yyyy/MM/dd HH:mm:ss}\"}}";
                progressObj.CurrentFile = json;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating file operation JSON");
                progressObj.CurrentFile = filename; // Fallback to just the filename
            }
        }



        private string GetOperationKey(string operation)
        {
            // Map the operation name to appropriate localized value
            return operation switch
            {
                "MoveOperation" => LocalizationManager.Instance["MoveOperation"],
                "CopyOperation" => LocalizationManager.Instance["CopyOperation"],
                "DeleteOperation" => LocalizationManager.Instance["DeleteOperation"],
                "SkipOperation" => LocalizationManager.Instance["SkipOperation"],
                _ => operation // Return the original value if not matched
            };
        }
    }
}