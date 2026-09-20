using System.Diagnostics;

namespace Mo2.Frontend;

// The bundled 7-Zip that NexusMods.App already ships for its own extraction,
// taken from the runtime assets rather than through its DI container. Mod
// archives and the MO2 release itself both come out of it.
internal static class Mo2ArchiveFiles
{
    internal static string? Extractor()
    {
        var root = AppContext.BaseDirectory;
        foreach (var candidate in OperatingSystem.IsWindows()
                     ? new[] { Path.Combine(root, "runtimes", "win-x64", "native", "7z.exe"), Path.Combine(root, "7z.exe") }
                     : [Path.Combine(root, "runtimes", "linux-x64", "native", "7zz"), Path.Combine(root, "7zz")])
            if (File.Exists(candidate)) return candidate;
        return null;
    }

    internal static void Extract(string archive, string destination, CancellationToken token = default)
    {
        if (Extractor() is not { } extractor)
            throw new InvalidOperationException("No bundled 7-Zip to extract with");
        Directory.CreateDirectory(destination);
        var start = new ProcessStartInfo(extractor) {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
        };
        foreach (var argument in new[] { "x", archive, "-o" + destination, "-y", "-bso0", "-bsp0" })
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the bundled 7-Zip");
        process.WaitForExit(token is { CanBeCanceled: true } ? 600_000 : 600_000);
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"Extracting {Path.GetFileName(archive)} failed: {process.StandardError.ReadToEnd()}");
    }
}
