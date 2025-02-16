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

namespace DriveSync.Infrastructure.Services
{
    public interface IRcloneVersionService
    {
        Task<(bool IsUpdateAvailable, string LatestVersion, string CurrentVersion)> CheckRcloneVersion();
        Task<bool> DownloadLatestRclone(string downloadPath, IProgress<double> progress = null);
        event EventHandler<string> VersionCheckError;
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
        private const int API_CALL_DELAY_MS = 1000; // Minimum delay between API calls

        public event EventHandler<string> VersionCheckError;

        public RcloneVersionService(ILogger<RcloneVersionService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(GITHUB_API_URL),
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DriveSync");
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<(bool IsUpdateAvailable, string LatestVersion, string CurrentVersion)> CheckRcloneVersion()
        {
            try
            {
                await EnsureApiRateLimit();

                string currentVersion = GetCurrentRcloneVersion();
                _logger.LogInformation($"Current rclone version: {currentVersion}");

                var latestRelease = await FetchLatestRcloneReleaseWithRetry();
                if (latestRelease == null)
                {
                    _logger.LogWarning("Could not fetch latest rclone version");
                    OnVersionCheckError("Could not fetch latest version information. Please try again later.");
                    return (false, null, currentVersion);
                }

                string latestVersion = latestRelease.tag_name.TrimStart('v');
                bool isUpdateAvailable = IsNewVersionAvailable(currentVersion, latestVersion);

                _logger.LogInformation($"Latest version: {latestVersion}, Update available: {isUpdateAvailable}");
                return (isUpdateAvailable, latestVersion, currentVersion);
            }
            catch (HttpRequestException ex)
            {
                HandleHttpError(ex);
                return (false, null, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rclone version");
                OnVersionCheckError($"Error checking for updates: {ex.Message}");
                return (false, null, null);
            }
        }

        public async Task<bool> DownloadLatestRclone(string downloadPath, IProgress<double> progress = null)
        {
            try
            {
                await EnsureApiRateLimit();

                var latestRelease = await FetchLatestRcloneReleaseWithRetry();
                if (latestRelease == null)
                {
                    OnVersionCheckError("Could not fetch release information.");
                    return false;
                }

                var downloadAsset = Array.Find(latestRelease.assets,
                    a => a.name.Contains("windows-amd64.zip", StringComparison.OrdinalIgnoreCase));

                if (downloadAsset == null)
                {
                    OnVersionCheckError("Windows release package not found.");
                    return false;
                }

                return await DownloadFileWithProgress(downloadAsset.browser_download_url, downloadPath, progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading rclone");
                OnVersionCheckError($"Download failed: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> DownloadFileWithProgress(string url, string destinationPath, IProgress<double> progress)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var buffer = new byte[8192];
                var totalBytesRead = 0L;

                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var contentStream = await response.Content.ReadAsStreamAsync();

                while (true)
                {
                    var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;

                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    if (totalBytes > 0 && progress != null)
                    {
                        var progressPercentage = (double)totalBytesRead / totalBytes * 100;
                        progress.Report(progressPercentage);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading file");
                if (File.Exists(destinationPath))
                {
                    try { File.Delete(destinationPath); }
                    catch { /* Ignore cleanup errors */ }
                }
                throw;
            }
        }

        private string GetCurrentRcloneVersion()
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

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var versionMatch = System.Text.RegularExpressions.Regex
                    .Match(output, @"rclone\s+v(\d+\.\d+\.\d+)");

                return versionMatch.Success ? versionMatch.Groups[1].Value : "0.0.0";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting current rclone version");
                return "0.0.0";
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
                return false;
            }
        }

        private Version ParseVersion(string version)
        {
            // Remove any leading 'v' and trailing information
            version = version.TrimStart('v').Split('-')[0];

            // Ensure we have at least three version components
            var parts = version.Split('.');
            if (parts.Length < 3)
            {
                var paddedVersion = string.Join(".", parts);
                for (int i = parts.Length; i < 3; i++)
                {
                    paddedVersion += ".0";
                }
                version = paddedVersion;
            }

            return new Version(version);
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
                    throw; // Don't retry rate limit errors
                }
                catch (Exception ex)
                {
                    if (attempt == MAX_RETRIES)
                    {
                        _logger.LogError(ex, "Failed to fetch latest release after all retries");
                        throw;
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

        private void HandleHttpError(HttpRequestException ex)
        {
            string message = ex.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "GitHub API authentication failed.",
                HttpStatusCode.Forbidden => "Rate limit exceeded. Please try again later.",
                HttpStatusCode.NotFound => "Release information not found.",
                _ => "Error connecting to GitHub. Please check your internet connection."
            };

            _logger.LogError(ex, message);
            OnVersionCheckError(message);
        }

        protected virtual void OnVersionCheckError(string message)
        {
            VersionCheckError?.Invoke(this, message);
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