using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DriveSync.WPF.Localization;
using Microsoft.Extensions.Logging;

namespace DriveSync.Infrastructure.Services
{
    public class SyncProgress
    {
        public double PercentComplete { get; set; }
        public string Speed { get; set; }
        public string TimeRemaining { get; set; }
        public string CurrentFile { get; set; }
        public string CurrentOperation { get; set; }
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

            var syncProgress = new SyncProgress
            {
                PercentComplete = 0,
                CurrentOperation = LocalizationManager.Instance["SyncInitializing"],
                CurrentFile = LocalizationManager.Instance["PreparingToSync"],
                Speed = LocalizationManager.Instance["ZeroSpeed"],
                TimeRemaining = LocalizationManager.Instance["CalculatingProgress"]
            };

            var errorBuilder = new StringBuilder();
            bool isFirstUpdate = true;

            process.OutputDataReceived += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;
                fullLog.AppendLine(e.Data);
                _logger.LogDebug("rclone output: {line}", e.Data);
                if (isFirstUpdate)
                {
                    syncProgress.CurrentOperation = LocalizationManager.Instance["ScanningOperation"];
                    syncProgress.CurrentFile = LocalizationManager.Instance["ScanningForChanges"];
                    progress.Report(syncProgress);
                    isFirstUpdate = false;
                }

                ProcessProgressOutput(e.Data, syncProgress, progress);
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(e.Data))
                    return;
                fullLog.AppendLine(e.Data);
                _logger.LogDebug("rclone error: {line}", e.Data);
                ProcessProgressOutput(e.Data, syncProgress, progress);
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

        private void ProcessProgressOutput(string line, SyncProgress progressObj, IProgress<SyncProgress> reporter)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line))
                    return;
                _logger.LogDebug("=== START PROCESSING LINE ===");
                _logger.LogDebug("Raw input line: {line}", line);

                // Specific file operation handling
                var fileOperationRegex = new Regex(@"(?:(\w+):\s*)?(.+?):\s*(Deleted|Copied|Skipped)");
                var fileOperationMatch = fileOperationRegex.Match(line);
                if (fileOperationMatch.Success)
                {
                    string operation = fileOperationMatch.Groups[3].Value.ToLower();
                    string filename = fileOperationMatch.Groups[2].Value.Trim();

                    progressObj.CurrentOperation = operation switch
                    {
                        "deleted" => LocalizationManager.Instance["DeleteOperation"],
                        "copied" => LocalizationManager.Instance["CopyOperation"],
                        "skipped" => LocalizationManager.Instance["SkipOperation"],
                        _ => LocalizationManager.Instance["SyncOperation"]
                    };

                    progressObj.CurrentFile = $"{LocalizationManager.Instance["FileDeleted"]} {filename}";
                    _logger.LogDebug("File Operation: {operation}, File: {filename}", operation, filename);
                    reporter?.Report(progressObj);
                    return;
                }

                // Percentage extraction
                var percentRegex = new Regex(@"(?i)(\d+(?:\.\d+)?)\s*%");
                var percentMatch = percentRegex.Match(line);
                if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out double percent))
                {
                    // Use a more nuanced approach to progress calculation
                    progressObj.PercentComplete = Math.Min(100, Math.Max(0, percent));
                    _logger.LogDebug("Progress Update - Percentage: {percent}%", percent);
                    reporter?.Report(progressObj);
                }

                // Speed and ETA capture
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

                // Sync completion detection
                if (line.Contains("Transferred") && line.Contains("Checked") && line.Contains("Copied"))
                {
                    _logger.LogDebug("Sync operation completion detected");
                    progressObj.PercentComplete = 100;
                    progressObj.CurrentOperation = LocalizationManager.Instance["SyncComplete"];
                    progressObj.CurrentFile = LocalizationManager.Instance["SyncCompletedSuccess"];
                    progressObj.Speed = LocalizationManager.Instance["ZeroSpeed"];
                    progressObj.TimeRemaining = "-";
                    reporter?.Report(progressObj);
                    return;
                }

                // Nothing to transfer check
                if (line.IndexOf("There was nothing to transfer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _logger.LogDebug("Nothing to transfer detected - Updating progress to complete");
                    progressObj.PercentComplete = 100;
                    progressObj.Speed = LocalizationManager.Instance["ZeroSpeed"];
                    progressObj.TimeRemaining = "-";
                    progressObj.CurrentOperation = LocalizationManager.Instance["SyncComplete"];
                    progressObj.CurrentFile = LocalizationManager.Instance["NoFilesToTransfer"];
                    reporter?.Report(progressObj);
                }

                _logger.LogDebug("=== END PROCESSING LINE ===\n");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing progress output: {line}", line);
            }
        }
    }
    }
    
    
    

