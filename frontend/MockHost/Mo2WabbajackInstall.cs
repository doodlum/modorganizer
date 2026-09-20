using System.Diagnostics;

namespace Mo2.Frontend;

// Installing a Wabbajack modlist, as work this application is doing.
//
// Wabbajack's own window is never opened. Its command line is run instead, and
// what it says on the way is turned into the same one-line progress everything
// else here reports, so a modlist install reads like the rest of the application
// rather than like another program running inside it.
//
// The result is an MO2 instance: Wabbajack writes ModOrganizer.ini and portable.txt
// into the output folder itself. The bridge goes in beside them, which is what
// makes the instance one this frontend can then drive, and a note of the list it
// came from, which is what keeps it separate from the base game in the sidebar.
internal static class Mo2WabbajackInstall
{
    // Shared by every modlist rather than kept per instance. Wabbajack hashes what
    // it has already fetched and skips it, so two lists that want the same mod
    // download it once — and these are tens of gigabytes.
    internal static string DownloadsRoot => Mo2ConfigPaths.DataCombine("modlist-downloads");
    internal static string ArchiveRoot => Mo2ConfigPaths.DataCombine("modlists");

    internal static string ArchiveFor(Mo2Modlist list) =>
        Path.Combine(ArchiveRoot, list.InstanceKey + ".wabbajack");

    internal sealed record Result(string Instance, string Downloads, int Files);

    internal static async Task<Result> Run(
        Mo2Modlist list, string? apiKey, Action<string?> report, CancellationToken token = default)
    {
        if (!list.Supported)
            throw new InvalidOperationException($"{list.Title} is for {list.WabbajackGame}, which Mod Organizer does not manage");

        await Mo2WabbajackTool.Install(report, token);

        var instance = Mo2ModlistInstances.DirectoryFor(list);
        Directory.CreateDirectory(instance);
        Directory.CreateDirectory(DownloadsRoot);
        Directory.CreateDirectory(ArchiveRoot);

        report($"Installing {list.Title}…");
        var start = Mo2WabbajackTool.Command(apiKey,
            "install",
            "-m", list.NamespacedName,
            "-w", ArchiveFor(list),
            "-o", instance,
            "-d", DownloadsRoot);

        using var process = Process.Start(start)
            ?? throw new InvalidOperationException("Wabbajack's command line did not start");

        // Both streams are drained while it runs. Wabbajack logs through the console
        // and a full one blocks the process it belongs to, so a modlist install that
        // nobody was reading would simply stop part way.
        var tail = new Queue<string>();
        void Line(string? line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            var text = Summarise(line);
            lock (tail) {
                tail.Enqueue(line);
                while (tail.Count > 40) tail.Dequeue();
            }
            if (text is not null) report(text);
        }
        var output = Read(process.StandardOutput, Line, token);
        var errors = Read(process.StandardError, Line, token);

        try {
            await process.WaitForExitAsync(token);
        } catch (OperationCanceledException) {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw;
        }
        await Task.WhenAll(output, errors);

        if (process.ExitCode != 0) {
            string recent;
            lock (tail) recent = string.Join("\n", tail);
            throw new InvalidOperationException(
                $"Wabbajack could not install {list.Title} (exit code {process.ExitCode}).\n{recent}");
        }

        var ini = Path.Combine(instance, "ModOrganizer.ini");
        if (!File.Exists(ini))
            throw new InvalidOperationException($"Wabbajack finished but left no Mod Organizer instance in {instance}");

        // The same bridge every instance here gets: without it MO2 starts but says
        // nothing, and the frontend has no modlist to show.
        Mo2OwnedInstances.InstallBridge(instance);
        Mo2ModlistInstances.Mark(instance, list);
        await SaveArtwork(list, instance, token);
        Mo2OwnedInstances.SuppressPrompts();
        Mo2OwnedInstances.SuppressInstancePrompts(instance);

        var files = Directory.Exists(Path.Combine(instance, "mods"))
            ? Directory.GetDirectories(Path.Combine(instance, "mods")).Length : 0;
        report($"{list.Title} installed.");
        return new Result(instance, DownloadsRoot, files);
    }

    private static async Task Read(StreamReader reader, Action<string?> line, CancellationToken token)
    {
        while (await reader.ReadLineAsync(token) is { } text) line(text);
    }

    // Wabbajack's console carries its own log format, with a timestamp and level in
    // front of every line. What is wanted on screen is the sentence, not the log.
    private static string? Summarise(string line)
    {
        var text = line.Trim();
        if (text.Length == 0) return null;
        // "[12:34:56 INF] Downloading foo" and similar.
        if (text.StartsWith('[') && text.IndexOf(']') is var close and > 0) {
            var level = text[1..close];
            if (level.Contains("DBG", StringComparison.OrdinalIgnoreCase) ||
                level.Contains("TRC", StringComparison.OrdinalIgnoreCase)) return null;
            text = text[(close + 1)..].Trim();
        }
        return text.Length == 0 ? null : text;
    }

    // Kept with the instance rather than fetched when the sidebar draws: the
    // sidebar is drawn constantly and the gallery is someone else's server. A
    // picture that cannot be fetched is not a failed install — the sidebar falls
    // back to the game's own icon.
    private static async Task SaveArtwork(Mo2Modlist list, string instance, CancellationToken token)
    {
        if (list.ImageUrl is not { Length: > 0 } url) return;
        try {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("mo2-nexus-frontend");
            var bytes = await client.GetByteArrayAsync(url, token);
            await File.WriteAllBytesAsync(Mo2ModlistInstances.ArtworkPath(instance), bytes, token);
        } catch (Exception) { }
    }
}
