using System.Diagnostics;
using System.Text.Json;

namespace Mo2.Frontend;

internal static class Mo2HostStartup
{
    private static readonly SemaphoreSlim Gate = new(1);
    private static readonly Dictionary<string, Process> Started = new();

    // Process evidence is separate from mailbox responsiveness. A slow or modal
    // host is never considered stopped just because a snapshot timed out.
    internal static bool IsRunning(Mo2Registration registration)
    {
        if (Started.TryGetValue(registration.Directory, out var process)) {
            if (!process.HasExited) return true;
            process.Dispose(); Started.Remove(registration.Directory);
        }
        var executable = Path.Combine(registration.Directory, "ModOrganizer.exe");
        if (OperatingSystem.IsLinux()) {
            foreach (var directory in Directory.EnumerateDirectories("/proc")) {
                if (!int.TryParse(Path.GetFileName(directory), out _)) continue;
                try {
                    var arguments = File.ReadAllText(Path.Combine(directory, "cmdline")).Split('\0', StringSplitOptions.RemoveEmptyEntries);
                    if (arguments.Length == 0) continue;
                    if (Mo2InstanceCatalog.LocalPath(arguments[0]) == executable) return true;
                    if (registration.Launcher is { } launcher && arguments.Take(2).Contains(launcher)) return true;
                } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
            }
        } else {
            foreach (var candidate in Process.GetProcessesByName("ModOrganizer")) {
                using (candidate) {
                    try { if (string.Equals(candidate.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase)) return true; }
                    catch (System.ComponentModel.Win32Exception) { throw new InvalidOperationException("Unable to inspect an existing MO2 process; connect to it before starting another host"); }
                }
            }
        }
        return false;
    }

    public static async Task<JsonElement> Connect(Mo2Registration registration, Action<string> report)
    {
        await Gate.WaitAsync();
        try {
            // Browser protocol handlers are separate processes. Hold this lease
            // through connection, then recheck process state before any launch.
            using var lease = await Mo2HostStartupLease.Acquire(Mo2HostStartupLease.PathFor(registration), TimeSpan.FromSeconds(90));
            var running = IsRunning(registration);
            if (!running && registration.Launcher is { } launcher) {
                if (!File.Exists(launcher)) throw new FileNotFoundException("The configured MO2 launcher no longer exists");
                report("Starting MO2; complete any setup dialogs in its window");
                var info = new ProcessStartInfo(launcher) { WorkingDirectory = registration.Directory, UseShellExecute = OperatingSystem.IsWindows() };
                if (!info.UseShellExecute) { info.RedirectStandardOutput = true; info.RedirectStandardError = true; }
                var started = Process.Start(info) ?? throw new InvalidOperationException("MO2 launcher did not start");
                Started[registration.Directory] = started;
                // Consume output without exposing possible credentials from custom launchers.
                if (!info.UseShellExecute) { started.BeginOutputReadLine(); started.BeginErrorReadLine(); }
            } else if (!running && !File.Exists(Path.Combine(registration.Endpoint, "endpoint.json"))) {
                throw new InvalidOperationException("Choose a launcher for this instance, or start MO2 with the frontend bridge enabled");
            }
            var client = new Mo2BridgeClient(registration.Endpoint);
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (true) {
                if (File.Exists(Path.Combine(registration.Endpoint, "endpoint.json"))) {
                    // A stale endpoint may belong to the previous host session. Only
                    // read-only snapshots are retried; launching is not retried here.
                    try { return await client.SendAsync("snapshot", timeout: TimeSpan.FromSeconds(5)); }
                    catch (TimeoutException) { }
                    catch (InvalidOperationException error) when (error.Message.Contains("Bridge session changed", StringComparison.Ordinal)) { }
                }
                if (DateTime.UtcNow >= deadline)
                    throw new TimeoutException("MO2 has not connected yet. Complete its setup or close any modal dialog, then select the profile again. No second host was started.");
                if (Started.TryGetValue(registration.Directory, out var started) && started.HasExited && !IsRunning(registration))
                    throw new InvalidOperationException("The configured launcher exited before MO2 connected. Check its launch command and bridge installation.");
                report("Waiting for MO2; complete any setup dialogs in its window");
                await Task.Delay(500);
            }
        } finally { Gate.Release(); }
    }
}
