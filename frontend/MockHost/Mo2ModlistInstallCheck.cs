using System.Diagnostics;

namespace Mo2.Frontend;

// Installing a real modlist, end to end, with no Wabbajack window anywhere.
//
// This is the long one: it downloads what the list asks for from Nexus with the
// credential this application already holds, and ends with an MO2 instance. It is
// a check rather than a test because there is nothing here to stub — what is being
// verified is that the real thing works, which only the real thing can show.
internal static class Mo2ModlistInstallCheck
{
    public static async Task Run(string? wanted)
    {
        var name = wanted is { Length: > 0 } ? wanted : "Viva New Vegas";
        var lists = await Mo2ModlistCatalog.Read();
        var list = lists.FirstOrDefault(x => x.Title.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception($"The gallery does not offer {name}");
        if (!list.Supported) throw new Exception($"{name} is for {list.WabbajackGame}, which Mod Organizer does not manage");

        var key = Mo2NexusCredential.Read()
            ?? throw new Exception("No Nexus credential: sign in through the application first");

        Console.WriteLine($"  {list.Title} {list.Version} — {list.NamespacedName}, {list.Game}");
        Console.WriteLine($"  {list.DownloadSize / 1024d / 1024:0} MB list, {list.TotalSize / 1024d / 1024 / 1024:0.0} GB total");
        Console.WriteLine($"  into {Mo2ModlistInstances.DirectoryFor(list)}");

        // Nothing of Wabbajack's should ever be on screen. Watched throughout rather
        // than sampled at the end, because a window that opened and closed is still
        // a window the person saw.
        var seen = new HashSet<string>();
        using var watching = new CancellationTokenSource();
        var watcher = Task.Run(async () => {
            while (!watching.IsCancellationRequested) {
                foreach (var title in Mo2NativeWindows.Titles())
                    if (title.Contains("Wabbajack", StringComparison.OrdinalIgnoreCase)) seen.Add(title);
                try { await Task.Delay(1000, watching.Token); } catch (OperationCanceledException) { return; }
            }
        });

        var clock = Stopwatch.StartNew();
        var last = "";
        var result = await Mo2WabbajackInstall.Run(list, key, line => {
            if (line is null || line == last) return;
            last = line;
            Console.WriteLine($"  [{clock.Elapsed:hh\\:mm\\:ss}] {line}");
        });
        watching.Cancel();
        await watcher;

        if (seen.Count != 0) throw new Exception("Wabbajack showed a window: " + string.Join("; ", seen));

        var instance = result.Instance;
        foreach (var required in new[] { "ModOrganizer.ini", "portable.txt" })
            if (!File.Exists(Path.Combine(instance, required)))
                throw new Exception($"The install left no {required}");

        var snapshot = Mo2ProfileFiles.Read(instance);
        Console.WriteLine($"  instance game {snapshot.Game}, profiles {string.Join(", ", snapshot.Profiles.Select(x => x.Name))}");
        if (Mo2SupportedGames.Find(snapshot.Game)?.Name != list.Game)
            throw new Exception($"The instance reports {snapshot.Game}, expected {list.Game}");
        if (snapshot.Profiles.Length == 0) throw new Exception("The instance has no profile");

        var note = Mo2ModlistInstances.Describe(instance)
            ?? throw new Exception("The instance carries no record of the modlist it came from");
        if (note.NamespacedName != list.NamespacedName)
            throw new Exception($"The instance records {note.NamespacedName}, expected {list.NamespacedName}");

        // The bridge is what makes it an instance this application can drive, rather
        // than a folder Wabbajack happened to leave behind.
        var bridge = Path.Combine(instance, "plugins", "nexus_frontend_bridge");
        if (!Directory.Exists(bridge)) throw new Exception("The instance has no frontend bridge");

        var mods = Path.Combine(instance, "mods");
        var count = Directory.Exists(mods) ? Directory.GetDirectories(mods).Length : 0;
        Console.WriteLine($"  {count} mods, took {clock.Elapsed:hh\\:mm\\:ss}");
        if (count == 0) throw new Exception("The instance has no mods");

        Console.WriteLine($"PASS modlist install: {list.Title} installed as its own MO2 instance with {count} mods, no Wabbajack window shown");
    }
}
