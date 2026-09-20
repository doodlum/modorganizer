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
        // The bridge records the host's own process id, which settles this without
        // comparing paths at all. Path comparison is not always enough: a process
        // reports the real location of its image, and a redirected file system —
        // a packaged app's local-data redirection, for one — gives that a different
        // spelling from the path the instance is registered under.
        try {
            var endpoint = Path.Combine(registration.Endpoint, "endpoint.json");
            if (File.Exists(endpoint)) {
                using var document = JsonDocument.Parse(File.ReadAllText(endpoint));
                if (document.RootElement.TryGetProperty("pid", out var recorded) && recorded.TryGetInt32(out var id)) {
                    try { using var host = Process.GetProcessById(id); if (!host.HasExited) return true; }
                    catch (ArgumentException) { /* the recorded host is gone */ }
                }
            }
        } catch (IOException) { } catch (JsonException) { }

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
                report("Starting MO2");
                // MO2_FRONTEND_HOST tells the bridge to keep MO2's own window, splash
                // and toasts off screen. The Proton launcher script sets it; a
                // frontend-owned instance is started directly, so it is set here.
                //
                // Set on this process rather than on the child, because a child that
                // carries its own environment block cannot be started through the
                // shell, and MO2 started without the shell fails to resolve its
                // side-by-side manifest ("the application has failed to start because
                // its side-by-side configuration is incorrect"). The shell-started
                // child inherits this, and nothing here reads the variable itself.
                Environment.SetEnvironmentVariable("MO2_FRONTEND_HOST", "1");
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
