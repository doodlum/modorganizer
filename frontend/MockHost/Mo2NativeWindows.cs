using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Mo2.Frontend;

// What MO2 currently has on screen.
//
// A frontend-hosted MO2 is meant to show nothing at all, so this exists to say
// when it does. Its hidden windows carry no native handle, which is exactly why
// a dialog it puts up cannot be answered and holds its event loop: this reports
// the ones that are real windows, which is what a user would actually see.
internal static class Mo2NativeWindows
{
    private delegate bool EnumProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);

    internal static IReadOnlyList<string> Titles()
    {
        if (!OperatingSystem.IsWindows()) return [];
        var hosts = new HashSet<uint>();
        foreach (var process in Process.GetProcessesByName("ModOrganizer")) using (process) hosts.Add((uint)process.Id);
        if (hosts.Count == 0) return [];
        var titles = new List<string>();
        try {
            EnumWindows((window, _) => {
                GetWindowThreadProcessId(window, out var owner);
                if (!hosts.Contains(owner) || !IsWindowVisible(window)) return true;
                var text = new StringBuilder(512);
                if (GetWindowTextW(window, text, text.Capacity) > 0) titles.Add(text.ToString());
                return true;
            }, IntPtr.Zero);
        } catch (DllNotFoundException) { return []; } catch (EntryPointNotFoundException) { return []; }
        return titles;
    }
}
