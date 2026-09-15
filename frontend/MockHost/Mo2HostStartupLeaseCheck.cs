using System.Diagnostics;

namespace Mo2.Frontend;

internal static class Mo2HostStartupLeaseCheck
{
    internal static async Task Hold(string path)
    {
        using var lease = await Mo2HostStartupLease.Acquire(path, TimeSpan.FromSeconds(5));
        Console.WriteLine("READY");
        await Console.In.ReadLineAsync();
    }

    internal static async Task Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "mo2-startup-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Process? child = null;
        try {
            var path = Path.Combine(root, "instance.lock");
            var info = new ProcessStartInfo(Environment.ProcessPath!) {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true,
            };
            if (Path.GetFileNameWithoutExtension(Environment.ProcessPath) == "dotnet")
                info.ArgumentList.Add(typeof(Program).Assembly.Location);
            info.ArgumentList.Add("--hold-host-startup-lease"); info.ArgumentList.Add(path);
            child = Process.Start(info) ?? throw new Exception("Lease child did not start");
            if (await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10)) != "READY")
                throw new Exception("Lease child did not acquire the lock");
            try {
                using var unexpected = await Mo2HostStartupLease.Acquire(path, TimeSpan.FromMilliseconds(250));
                throw new Exception("Concurrent process acquired an occupied startup lock");
            } catch (TimeoutException) { }
            using (var independent = await Mo2HostStartupLease.Acquire(Path.Combine(root, "other.lock"), TimeSpan.FromSeconds(1))) { }
            child.Kill(); await child.WaitForExitAsync();
            using (var recovered = await Mo2HostStartupLease.Acquire(path, TimeSpan.FromSeconds(2))) { }
            using (var reopened = await Mo2HostStartupLease.Acquire(path, TimeSpan.FromSeconds(2))) { }
            var registration = new Mo2Registration(root, root + "/bridge");
            if (Mo2HostStartupLease.PathFor(registration) != Mo2HostStartupLease.PathFor(registration with { Directory = root + "/" }))
                throw new Exception("Trailing slash bypasses the startup lock");
            Console.WriteLine("PASS host startup lease: cross-process exclusion, bounded wait, independent instances, crash recovery, normal release and path normalization");
        } finally {
            if (child is not null) { if (!child.HasExited) { child.Kill(); await child.WaitForExitAsync(); } child.Dispose(); }
            Directory.Delete(root, true);
        }
    }
}
