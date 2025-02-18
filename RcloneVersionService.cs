using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Headers;
using System.Threading;
using System.Net;
using System.Linq;
using System.Security.Cryptography;

namespace DriveSync.Infrastructure.Services
{
    public class RcloneVersionException : Exception
    {
        public RcloneErrorType ErrorType { get; }

        public RcloneVersionException(string message, RcloneErrorType errorType, Exception innerException = null)
            : base(message, innerException)
        {
            ErrorType = errorType;
        }
    }

    public enum RcloneErrorType
    {
        NetworkError,
        DownloadError,
        ValidationError,
        ExtractionError,
        VersionCheckError,
        UnknownError
    }

    public interface IRcloneVersionService
    {
        Task<(bool IsUpdateAvailable, string LatestVersion, string CurrentVersion)> CheckRcloneVersion();
        Task<bool> DownloadLatestRclone(string downloadPath, IProgress<double> progress = null);
        Task<bool> ValidateRcloneFile(string filePath);
        Task<bool> RollbackToVersion(string version);
        event EventHandler<string> VersionCheckError;
        event EventHandler<(string Message, RcloneErrorType ErrorType)> ErrorOccurred;
    }

    public class RcloneVersionService : IRcloneVersionService
    {
        private readonly ILogger<RcloneVersionService> _logger;
        private readonly HttpClient _httpClient;
        private const string GITHUB_API_URL = "https://api.github.com";
        private const string GITHUB_RELEASE_URL = "/repos/rclone/rclone/releases/latest";
        private const int MAX_RETRIES = 3;
        private const int RETRY_DELAY_MS = 1000;
        private DateTime _lastApiCall = DateTime.MinValue;
        private const int API_CALL_DELAY_MS = 1000;
        private const int CHUNK_SIZE = 8192;
        private const string VERSION_HISTORY_FILE = "version_history.json";

        public event EventHandler<string> VersionCheckError;
        public event EventHandler<(string Message, RcloneErrorType ErrorType)> ErrorOccurred;

        private class VersionHistory
        {
            public string Version { get; set; }
            public string Path { get; set; }
            public DateTime InstallDate { get; set; }
            public string Checksum { get; set; }
        }

        public RcloneVersionService(ILogger<RcloneVersionService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(GITHUB_API_URL),
                Timeout = TimeSpan.FromMinutes(5) // Increased timeout for large downloads
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DriveSync");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<(bool IsUpdateAvailable, string LatestVersion, string CurrentVersion)> CheckRcloneVersion()
        {
            int retryCount = 0;
            while (retryCount < MAX_RETRIES)
            {
                try
                {
                    await EnsureApiRateLimit();

                    string currentVersion = await GetCurrentRcloneVersion();
                    _logger.LogInformation($"Current rclone version: {currentVersion}");

                    var latestRelease = await FetchLatestRcloneReleaseWithRetry();
                    if (latestRelease == null)
                    {
                        OnErrorOccurred("Could not fetch latest version information.", RcloneErrorType.VersionCheckError);
                        return (false, null, currentVersion);
                    }

                    string latestVersion = latestRelease.tag_name.TrimStart('v');
                    bool isUpdateAvailable = IsNewVersionAvailable(currentVersion, latestVersion);

                    _logger.LogInformation($"Latest version: {latestVersion}, Update available: {isUpdateAvailable}");
                    return (isUpdateAvailable, latestVersion, currentVersion);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError(ex, $"HTTP error checking version (attempt {retryCount + 1}/{MAX_RETRIES})");
                    if (retryCount == MAX_RETRIES - 1)
                    {
                        OnErrorOccurred($"Network error: {ex.Message}", RcloneErrorType.NetworkError);
                        throw new RcloneVersionException("Failed to check version after multiple attempts", RcloneErrorType.NetworkError, ex);
                    }
                    await Task.Delay((retryCount + 1) * RETRY_DELAY_MS);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking rclone version");
                    OnErrorOccurred($"Unexpected error: {ex.Message}", RcloneErrorType.UnknownError);
                    throw new RcloneVersionException("Failed to check version", RcloneErrorType.UnknownError, ex);
                }
                retryCount++;
            }
            throw new RcloneVersionException("Maximum retry attempts exceeded", RcloneErrorType.NetworkError);
        }

        public async Task<bool> DownloadLatestRclone(string downloadPath, IProgress<double> progress = null)
        {
            int retryCount = 0;
            long? resumePosition = 0;

            while (retryCount < MAX_RETRIES)
            {
                try
                {
                    var latestRelease = await FetchLatestRcloneReleaseWithRetry();
                    if (latestRelease == null)
                    {
                        OnErrorOccurred("Could not fetch release information", RcloneErrorType.DownloadError);
                        return false;
                    }

                    var downloadAsset = Array.Find(latestRelease.assets,
                        a => a.name.Contains("windows-amd64.zip", StringComparison.OrdinalIgnoreCase));

                    if (downloadAsset == null)
                    {
                        OnErrorOccurred("Windows release package not found", RcloneErrorType.DownloadError);
                        return false;
                    }

                    bool downloadSuccess = await DownloadFileWithProgress(
                        downloadAsset.browser_download_url,
                        downloadPath,
                        progress,
                        resumePosition);

                    if (downloadSuccess)
                    {
                        if (await ValidateRcloneFile(downloadPath))
                        {
                            await SaveVersionHistory(latestRelease.tag_name, downloadPath);
                            return true;
                        }
                        else
                        {
                            File.Delete(downloadPath);
                            OnErrorOccurred("Downloaded file validation failed", RcloneErrorType.ValidationError);
                            return false;
                        }
                    }

                    resumePosition = new FileInfo(downloadPath).Length;
                    retryCount++;
                    await Task.Delay(RETRY_DELAY_MS * retryCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error downloading rclone (attempt {retryCount + 1}/{MAX_RETRIES})");
                    if (retryCount == MAX_RETRIES - 1)
                    {
                        OnErrorOccurred($"Download failed: {ex.Message}", RcloneErrorType.DownloadError);
                        return false;
                    }
                    retryCount++;
                    await Task.Delay(RETRY_DELAY_MS * retryCount);
                }
            }
            return false;
        }

        public async Task<bool> ValidateRcloneFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    OnErrorOccurred("File not found", RcloneErrorType.ValidationError);
                    return false;
                }

                // Calculate file hash
                string fileHash;
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = await Task.Run(() => sha256.ComputeHash(stream));
                    fileHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }

                // Basic file validation
                using (var archive = System.IO.Compression.ZipFile.OpenRead(filePath))
                {
                    var rcloneEntry = archive.Entries.FirstOrDefault(e =>
                        e.FullName.EndsWith("rclone.exe", StringComparison.OrdinalIgnoreCase));

                    if (rcloneEntry == null)
                    {
                        OnErrorOccurred("rclone.exe not found in archive", RcloneErrorType.ValidationError);
                        return false;
                    }

                    if (rcloneEntry.Length < 1024 * 1024) // Minimum expected size
                    {
                        OnErrorOccurred("rclone.exe file size is suspiciously small", RcloneErrorType.ValidationError);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating rclone file");
                OnErrorOccurred($"Validation error: {ex.Message}", RcloneErrorType.ValidationError);
                return false;
            }
        }

        public async Task<bool> RollbackToVersion(string version)
        {
            try
            {
                var historyPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    VERSION_HISTORY_FILE
                );

                if (!File.Exists(historyPath))
                {
                    OnErrorOccurred("No version history found", RcloneErrorType.ValidationError);
                    return false;
                }

                var historyJson = await File.ReadAllTextAsync(historyPath);
                var history = JsonSerializer.Deserialize<VersionHistory[]>(historyJson);

                var targetVersion = history.FirstOrDefault(v => v.Version == version);
                if (targetVersion == null)
                {
                    OnErrorOccurred($"Version {version} not found in history", RcloneErrorType.ValidationError);
                    return false;
                }

                if (!File.Exists(targetVersion.Path))
                {
                    OnErrorOccurred($"Version {version} files not found", RcloneErrorType.ValidationError);
                    return false;
                }

                // Validate the rollback version
                if (!await ValidateRcloneFile(targetVersion.Path))
                {
                    OnErrorOccurred($"Version {version} validation failed", RcloneErrorType.ValidationError);
                    return false;
                }

                // Calculate current checksum
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(targetVersion.Path))
                {
                    var hash = await Task.Run(() => sha256.ComputeHash(stream));
                    var currentHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                    if (currentHash != targetVersion.Checksum)
                    {
                        OnErrorOccurred($"Version {version} checksum mismatch", RcloneErrorType.ValidationError);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error rolling back to version {version}");
                OnErrorOccurred($"Rollback error: {ex.Message}", RcloneErrorType.UnknownError);
                return false;
            }
        }

        private async Task<bool> DownloadFileWithProgress(string url, string destinationPath, IProgress<double> progress, long? resumePosition = null)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (resumePosition > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(resumePosition, null);
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var totalBytesRead = resumePosition ?? 0L;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(
                    destinationPath,
                    resumePosition > 0 ? FileMode.Append : FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    CHUNK_SIZE,
                    true
                );

                var buffer = new byte[CHUNK_SIZE];
                var lastProgressReport = DateTime.Now;
                var isMoreToRead = true;

                while (isMoreToRead)
                {
                    var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    var now = DateTime.Now;
                    if ((now - lastProgressReport).TotalMilliseconds >= 100) // Throttle progress updates
                    {
                        if (totalBytes > 0)
                        {
                            var progressPercentage = (double)totalBytesRead / totalBytes * 100;
                            progress?.Report(Math.Round(progressPercentage, 2));
                        }
                        lastProgressReport = now;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file");
                throw;
            }
        }

        private async Task SaveVersionHistory(string version, string filePath)
        {
            try
            {
                var historyPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    VERSION_HISTORY_FILE
                );

                var versionHistory = new List<VersionHistory>();
                if (File.Exists(historyPath))
                {
                    var existingHistoryJson = await File.ReadAllTextAsync(historyPath);
                    versionHistory = JsonSerializer.Deserialize<List<VersionHistory>>(existingHistoryJson);
                }

                // Calculate checksum
                string checksum;
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = await Task.Run(() => sha256.ComputeHash(stream));
                    checksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }

                versionHistory.Add(new VersionHistory
                {
                    Version = version,
                    Path = filePath,
                    InstallDate = DateTime.UtcNow,
                    Checksum = checksum
                });

                // Keep only the last 5 versions
                if (versionHistory.Count > 5)
                {
                    versionHistory = versionHistory.OrderByDescending(h => h.InstallDate).Take(5).ToList();
                }

                var updatedHistoryJson = JsonSerializer.Serialize(versionHistory, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(historyPath, updatedHistoryJson);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving version history");
            }
        }

        private async Task<string> GetCurrentRcloneVersion()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "rclone",
                        Arguments = "version",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8
                    }
                };

                var output = new System.Text.StringBuilder();
                var tcs = new TaskCompletionSource<bool>();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data == null)
                        tcs.TrySetResult(true);
                    else
                        output.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();

                // Add timeout
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                cts.Token.Register(() => tcs.TrySetCanceled());

                try
                {
                    await process.WaitForExitAsync(cts.Token);
                    await tcs.Task;
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(); } catch { }
                    throw new RcloneVersionException("Timeout getting current version", RcloneErrorType.VersionCheckError);
                }

                if (process.ExitCode != 0)
                {
                    throw new RcloneVersionException(
                        $"rclone version command failed with exit code {process.ExitCode}",
                        RcloneErrorType.VersionCheckError);
                }

                var versionMatch = System.Text.RegularExpressions.Regex
                    .Match(output.ToString(), @"rclone\s+v(\d+\.\d+\.\d+)");

                return versionMatch.Success ? versionMatch.Groups[1].Value : "0.0.0";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting current rclone version");
                throw new RcloneVersionException(
                    "Failed to get current rclone version",
                    RcloneErrorType.VersionCheckError,
                    ex);
            }
        }

        private bool IsNewVersionAvailable(string currentVersion, string latestVersion)
        {
            try
            {
                if (string.IsNullOrEmpty(currentVersion) || string.IsNullOrEmpty(latestVersion))
                    return false;

                var current = ParseVersion(currentVersion);
                var latest = ParseVersion(latestVersion);

                return latest > current;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing versions");
                throw new RcloneVersionException(
                    "Failed to compare versions",
                    RcloneErrorType.VersionCheckError,
                    ex);
            }
        }

        private Version ParseVersion(string version)
        {
            version = version.TrimStart('v').Split('-')[0];
            var parts = version.Split('.');
            var paddedVersion = string.Join(".", parts.Concat(Enumerable.Repeat("0", Math.Max(0, 3 - parts.Length))));
            return new Version(paddedVersion);
        }

        private async Task<GitHubRelease> FetchLatestRcloneReleaseWithRetry()
        {
            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                try
                {
                    var response = await _httpClient.GetAsync(GITHUB_RELEASE_URL);

                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        var resetTime = response.Headers.Contains("X-RateLimit-Reset") ?
                            DateTimeOffset.FromUnixTimeSeconds(long.Parse(response.Headers.GetValues("X-RateLimit-Reset").First())).DateTime :
                            DateTime.UtcNow.AddMinutes(5);

                        _logger.LogWarning($"Rate limit exceeded. Reset at: {resetTime}");
                        throw new RateLimitExceededException(resetTime);
                    }

                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<GitHubRelease>(content);
                }
                catch (RateLimitExceededException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    if (attempt == MAX_RETRIES)
                    {
                        _logger.LogError(ex, "Failed to fetch latest release after all retries");
                        throw new RcloneVersionException(
                            "Failed to fetch latest release",
                            RcloneErrorType.NetworkError,
                            ex);
                    }

                    _logger.LogWarning(ex, $"Attempt {attempt} failed, retrying...");
                    await Task.Delay(RETRY_DELAY_MS * attempt);
                }
            }

            return null;
        }

        private async Task EnsureApiRateLimit()
        {
            var timeSinceLastCall = DateTime.UtcNow - _lastApiCall;
            if (timeSinceLastCall.TotalMilliseconds < API_CALL_DELAY_MS)
            {
                await Task.Delay(API_CALL_DELAY_MS - (int)timeSinceLastCall.TotalMilliseconds);
            }
            _lastApiCall = DateTime.UtcNow;
        }

        private void OnErrorOccurred(string message, RcloneErrorType errorType)
        {
            _logger.LogError($"Rclone error occurred: {message} (Type: {errorType})");
            ErrorOccurred?.Invoke(this, (message, errorType));
        }
    }

    public class RateLimitExceededException : Exception
    {
        public DateTime ResetTime { get; }

        public RateLimitExceededException(DateTime resetTime)
            : base($"GitHub API rate limit exceeded. Resets at {resetTime}")
        {
            ResetTime = resetTime;
        }
    }

    public class GitHubRelease
    {
        public string tag_name { get; set; }
        public GitHubReleaseAsset[] assets { get; set; }
    }

    public class GitHubReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
    }
}