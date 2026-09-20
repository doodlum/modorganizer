using Avalonia.Controls;

namespace Mo2.Frontend;

// Installing a real archive through the frontend, and saying what MO2 put up
// while it happened. MO2 owns the installer its plugins provide, so this reports
// any window it opened rather than assuming there was none.
internal static class Mo2InstallModCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window, string archive)
    {
        for (var attempt = 0; attempt < 600 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        if (!File.Exists(archive)) throw new Exception($"No archive at {archive}");
        live.ShowProfile();
        using var turn = await Mo2CheckTurn.Take();

        var before = live.Profile.Mods.Select(mod => mod.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Console.WriteLine($"  installing {Path.GetFileName(archive)} into {live.Profile.GameName} ({before.Count} mods before)");

        var install = live.Profile.InstallArchive(archive, live.Profile.CurrentTarget);
        // Watch for anything MO2 shows while its installer runs.
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (!install.IsCompleted) {
            foreach (var title in Mo2NativeWindows.Titles()) if (seen.Add(title)) Console.WriteLine($"  MO2 opened a window: \"{title}\"");
            await Task.Delay(500);
        }
        var result = await install;

        for (var attempt = 0; attempt < 60; attempt++) {
            await Task.Delay(500);
            if (live.Profile.Mods.Any(mod => !before.Contains(mod.Name))) break;
        }
        var added = live.Profile.Mods.Where(mod => !before.Contains(mod.Name)).Select(mod => mod.Name).ToArray();
        Console.WriteLine($"  result name={result?.ModName ?? "(none)"} path={result?.ModDirectory ?? "(none)"}");
        Console.WriteLine($"  new mods: {(added.Length == 0 ? "(none)" : string.Join(", ", added))}");
        if (seen.Count > 0) Console.WriteLine($"  NOTE MO2 showed {seen.Count} window(s) during the install");

        if (result is null && added.Length == 0) throw new Exception("The archive did not install");
        Console.WriteLine($"PASS install mod: {Path.GetFileName(archive)} installed as {string.Join(", ", added.DefaultIfEmpty(result?.ModName ?? "?"))}");
    }
}
