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
                _logger.LogDebug("Processing line: {line}", line);
                // Simplified percentage extraction.
                var percentRegex = new Regex(@"(?i)(\d+(?:\.\d+)?)\s*%");
                var percentMatch = percentRegex.Match(line);
                if (percentMatch.Success && double.TryParse(percentMatch.Groups[1].Value, out double percent))
                {
                    progressObj.PercentComplete = Math.Min(100, Math.Max(0, percent));
                    _logger.LogDebug("Parsed percent: {percent}%", percent);
                    reporter?.Report(progressObj);
                }
                // Capture speed and ETA.
                var statsRegex = new Regex(@"(?:Transferred:.*?\s+)?\(*\s*([\d\.]+\s*\w+/s)\s*\)?(?:.*ETA[:\s]*([\dhms]+))?", RegexOptions.IgnoreCase);
                var statsMatch = statsRegex.Match(line);
                if (statsMatch.Success)
                {
                    bool statsUpdated = false;
                    if (statsMatch.Groups[1].Success && !string.IsNullOrWhiteSpace(statsMatch.Groups[1].Value))
                    {
                        progressObj.Speed = statsMatch.Groups[1].Value.Trim();
                        statsUpdated = true;
                    }
                    if (statsMatch.Groups[2].Success && !string.IsNullOrWhiteSpace(statsMatch.Groups[2].Value))
                    {
                        progressObj.TimeRemaining = statsMatch.Groups[2].Value.Trim();
                        statsUpdated = true;
                    }
                    if (statsUpdated)
                    {
                        _logger.LogDebug("Parsed speed: {speed}, ETA: {eta}", progressObj.Speed, progressObj.TimeRemaining);
                        reporter?.Report(progressObj);
                    }
                }
                // Fallback for file-level operations.
                var keywordRegex = new Regex(@"(?i)(CHECKING|CHECK|COPYING|COPY|DELETING|DELETE|SKIPPING|SKIP)");
                var keywordMatch = keywordRegex.Match(line);
                if (keywordMatch.Success)
                {
                    string opRaw = keywordMatch.Groups[1].Value.ToUpper().Trim();
                    _logger.LogDebug($"Raw Operation Token: {opRaw}"); // Added debug logging
                    string op = opRaw switch
                    {
                        "CHECKING" or "CHECK" => OP_CHECK,
                        "COPYING" or "COPY" => OP_COPY,
                        "DELETING" or "DELETE" => OP_DELETE,
                        "SKIPPING" or "SKIP" => OP_SKIP,
                        _ => LocalizationManager.Instance["SyncOperation"]
                    };
                    _logger.LogDebug($"Mapped Operation Code: {op}"); // Added debug logging

                    if (op == OP_CHECK && line.IndexOf("Finish", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        progressObj.CurrentOperation = LocalizationManager.Instance["FileVerificationCheck"];
                        progressObj.CurrentFile = string.Empty;
                    }
                    else
                    {
                        // Map operation types to localized strings
                        progressObj.CurrentOperation = op switch
                        {
                            OP_CHECK => LocalizationManager.Instance["CheckOperation"],
                            OP_COPY => LocalizationManager.Instance["CopyOperation"],
                            OP_DELETE => LocalizationManager.Instance["DeleteOperation"],
                            OP_SKIP => LocalizationManager.Instance["skipping"], // Changed to 'Skipping' with capital S
                            _ => LocalizationManager.Instance["SyncOperation"]
                        };

                        _logger.LogDebug($"Final Operation Text: {progressObj.CurrentOperation}"); // Added debug logging

                        var tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length > 0)
                        {
                            progressObj.CurrentFile = tokens[tokens.Length - 1];
                        }
                    }
                    _logger.LogDebug("Parsed file-level op: {op} on file {filePart}", progressObj.CurrentOperation, progressObj.CurrentFile);
                    reporter?.Report(progressObj);
                }
                if (line.IndexOf("There was nothing to transfer", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    progressObj.PercentComplete = 100;
                    progressObj.Speed = LocalizationManager.Instance["ZeroSpeed"];
                    progressObj.TimeRemaining = "-";
                    progressObj.CurrentOperation = LocalizationManager.Instance["SyncComplete"];
                    progressObj.CurrentFile = LocalizationManager.Instance["NoFilesToTransfer"];
                    reporter?.Report(progressObj);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing progress output: {line}", line);
            }
        }
    }
    }

