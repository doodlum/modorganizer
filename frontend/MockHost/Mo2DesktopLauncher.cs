using System.Diagnostics;

namespace Mo2.Frontend;

internal static class Mo2DesktopLauncher
{
    // Some xdg-open handlers stay alive for the lifetime of their window.
    // Observe immediate launch failures, then let the desktop own that lifetime.
    internal static async Task StartAsync(ProcessStartInfo start)
    {
        using var process = Process.Start(start) ?? throw new IOException("Could not open the desktop file manager.");
        using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try { await process.WaitForExitAsync(startup.Token); }
        catch (OperationCanceledException) when (startup.IsCancellationRequested) { return; }
        if (process.ExitCode != 0) throw new IOException("The desktop file manager could not open the source folder.");
    }
}
