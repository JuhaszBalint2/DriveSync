using System.Threading;
using System.Threading.Tasks;

namespace DriveSync.Infrastructure.Services
{
    public enum SyncType
    {
        Mirror, // Uses rclone sync.
        Backup, // Uses rclone copy.
        Move    // Uses rclone move.
    }

    public interface IRcloneService
    {
        Task<bool> ValidateRcloneInstallation();
        Task<string[]> ListRemotes();
        Task<string[]> ListDirectories(string remote, string path = "");
        Task<string> SyncDirectories(
            string sourceRemote,
            string sourcePath,
            string targetRemote,
            string targetPath,
            SyncType syncType,
            IProgress<SyncProgress> progress,
            CancellationToken cancellationToken);
    }
}
