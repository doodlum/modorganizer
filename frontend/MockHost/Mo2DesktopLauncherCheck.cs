using System.Diagnostics;
using System.Text.Json;

namespace Mo2.Frontend;

internal static class Mo2DesktopLauncherCheck
{
    internal static async Task Run()
    {
        if (!OperatingSystem.IsLinux()) throw new NotSupportedException("This check uses the Linux test environment.");
        var root = Path.Combine(Path.GetTempPath(), "mo2-desktop-launch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var script = Path.Combine(root, "handler.py");
        var receipt = Path.Combine(root, "receipt.json");
        int? child = null;
        try {
            await File.WriteAllTextAsync(script, "import json,os,sys,time\nwith open(sys.argv[1], 'w') as f: json.dump({'pid':os.getpid(),'args':sys.argv[2:]},f)\ntime.sleep(20)\n");
            var folder = Path.Combine(root, "folder with spaces $(literal)");
            var start = new ProcessStartInfo("python3") { UseShellExecute = false };
            start.ArgumentList.Add(script); start.ArgumentList.Add(receipt); start.ArgumentList.Add(folder);
            var clock = Stopwatch.StartNew();
            await Mo2DesktopLauncher.StartAsync(start);
            using var result = JsonDocument.Parse(await File.ReadAllTextAsync(receipt));
            child = result.RootElement.GetProperty("pid").GetInt32();
            if (result.RootElement.GetProperty("args")[0].GetString() != folder) throw new Exception("Folder arguments changed");
            using var process = Process.GetProcessById(child.Value);
            if (process.HasExited || clock.Elapsed > TimeSpan.FromSeconds(4)) throw new Exception("Desktop handler blocked startup or was terminated");
            Console.WriteLine($"PASS: long-lived desktop handler returned in {clock.Elapsed.TotalMilliseconds:F0} ms and remained alive; folder argument preserved");
            foreach (var code in new[] { 0, 7 }) {
                var exit = new ProcessStartInfo("python3") { UseShellExecute = false };
                exit.ArgumentList.Add("-c"); exit.ArgumentList.Add($"raise SystemExit({code})");
                try {
                    await Mo2DesktopLauncher.StartAsync(exit);
                    if (code != 0) throw new Exception("Immediate desktop failure was ignored");
                } catch (IOException) when (code != 0) { }
            }
            Console.WriteLine("PASS: immediate success and failure distinguished");
        } finally {
            if (child is { } pid) {
                try { using var process = Process.GetProcessById(pid); if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(); } }
                catch (ArgumentException) { }
            }
            Directory.Delete(root, recursive: true);
        }
    }
}
