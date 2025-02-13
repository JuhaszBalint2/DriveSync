using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;

namespace DriveSync.Infrastructure.Services
{
    public interface IRcloneVersionService
    {
        Task<(bool IsUpdateAvailable, string LatestVersion, string CurrentVersion)> CheckRcloneVersion();
        Task<bool> DownloadLatestRclone(string downloadPath);
    }

    public class RcloneVersionService : IRcloneVersionService
    {
        private readonly ILogger<RcloneVersionService> _logger;
        private readonly HttpClient _httpClient;
        private const string GITHUB_RELEASE_URL = "https://api.github.com/repos/rclone/rclone/releases/latest";
        private const string DOWNLOAD_BASE_URL = "https://github.com/rclone/rclone/releases/download/";

        public RcloneVersionService(ILogger<RcloneVersionService> logger)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DriveSync");
        }

        public async Task<(bool IsUpdateAvailable, string LatestVersion, string CurrentVersion)> CheckRcloneVersion()
        {
            try
            {
                // Get current installed rclone version
                string currentVersion = GetCurrentRcloneVersion();

                // Fetch latest version from GitHub
                var latestRelease = await FetchLatestRcloneRelease();

                if (latestRelease == null)
                {
                    _logger.LogWarning("Could not fetch latest rclone version");
                    return (false, null, currentVersion);
                }

                string latestVersion = latestRelease.tag_name.TrimStart('v');
                bool isUpdateAvailable = IsNewVersionAvailable(currentVersion, latestVersion);

                return (isUpdateAvailable, latestVersion, currentVersion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking rclone version");
                return (false, null, null);
            }
        }

        public async Task<bool> DownloadLatestRclone(string downloadPath)
        {
            try
            {
                var latestRelease = await FetchLatestRcloneRelease();
                if (latestRelease == null) return false;

                // Determine appropriate download URL (Windows 64-bit)
                var downloadAsset = latestRelease.assets
                    .FirstOrDefault(a => a.name.Contains("windows-amd64.zip"));

                if (downloadAsset == null) return false;

                // Download the file
                var fileBytes = await _httpClient.GetByteArrayAsync(downloadAsset.browser_download_url);

                // Save to specified path
                await File.WriteAllBytesAsync(downloadPath, fileBytes);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading rclone");
                return false;
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
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Extract version from output (assumes standard rclone version output)
                var versionMatch = System.Text.RegularExpressions.Regex
                    .Match(output, @"rclone\s+v(\d+\.\d+\.\d+)");

                return versionMatch.Success ? versionMatch.Groups[1].Value : "0.0.0";
            }
            catch
            {
                return "0.0.0";
            }
        }

        private async Task<GitHubRelease> FetchLatestRcloneRelease()
        {
            try
            {
                var response = await _httpClient.GetStringAsync(GITHUB_RELEASE_URL);
                return JsonSerializer.Deserialize<GitHubRelease>(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching GitHub release");
                return null;
            }
        }

        private bool IsNewVersionAvailable(string currentVersion, string latestVersion)
        {
            var current = new Version(currentVersion);
            var latest = new Version(latestVersion);
            return latest > current;
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