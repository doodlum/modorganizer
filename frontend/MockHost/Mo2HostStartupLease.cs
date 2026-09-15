using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Mo2.Frontend;

internal static class Mo2HostStartupLease
{
    internal static string PathFor(Mo2Registration registration)
    {
        var directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(registration.Directory));
        if (OperatingSystem.IsWindows()) directory = directory.ToUpperInvariant();
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(directory)));
        var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(config, "mo2-nexus-frontend", "host-startup", id + ".lock");
    }

    internal static async Task<FileStream> Acquire(string path, TimeSpan timeout)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var elapsed = Stopwatch.StartNew();
        while (true) {
            try {
                // Keep the file after release: unlinking it can let a new process
                // lock a different inode while another process holds the old one.
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            } catch (IOException) {
                if (elapsed.Elapsed >= timeout)
                    throw new TimeoutException("Another frontend process is connecting to this MO2 instance. Wait for it to finish, then retry. No second host was started.");
                await Task.Delay(100);
            }
        }
    }
}
