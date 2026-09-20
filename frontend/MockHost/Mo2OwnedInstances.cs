using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Mo2.Frontend;

// MO2 and the instances built on it, owned by the frontend and kept inside its
// own data directory.
//
// The frontend used to connect to whatever MO2 the user had already set up,
// discovered under the home directory or registered by hand. It now installs its
// own copy of MO2 and builds an instance per game, so a machine with no MO2 at
// all can be taken from nothing to a managed game.
//
// Each instance is one of MO2's own portable instances: its binaries, its
// ModOrganizer.ini and its mods, profiles, downloads and overwrite all in one
// directory. The alternative, a global instance, lives under MO2's own
// AppLocalDataLocation and is selected by a name in per-user settings shared by
// every MO2 on the machine. Portable avoids that entirely — portable.txt beside
// the binaries makes MO2 refuse to change instance at all, so the host always
// opens this game and never shows the instance picker.
//
// The published MO2 release is used as-is. This repository forks MO2 for a single
// patch to processrunner.cpp — unhooked launches keeping their arguments and
// skipping VFS preparation — which the release does not carry; everything the
// frontend needs of MO2 comes through the bridge, an ordinary Python plugin.
internal static class Mo2OwnedInstances
{
    // The extracted release, copied into each instance rather than run directly.
    internal static string TemplateRoot => Mo2ConfigPaths.DataCombine("mo2");
    internal static string InstancesRoot => Mo2ConfigPaths.DataCombine("instances");
    private static string VersionFile => Path.Combine(TemplateRoot, ".installed-version");

    internal static bool Installed => File.Exists(Path.Combine(TemplateRoot, "ModOrganizer.exe"));
    internal static string? InstalledVersion => File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : null;

    internal static string InstanceDirectory(string game) =>
        Path.Combine(InstancesRoot, Mo2SupportedGames.Find(game)?.IconKey ?? game);

    internal static string InstanceExecutable(string game) =>
        Path.Combine(InstanceDirectory(game), "ModOrganizer.exe");

    internal static bool HasInstance(string game) =>
        File.Exists(Path.Combine(InstanceDirectory(game), "ModOrganizer.ini")) && File.Exists(InstanceExecutable(game));

    // The bundled 7-Zip that NexusMods.App already ships for its own extraction,
    // taken straight from the runtime assets rather than through its DI container.
    private static string? Extractor()
    {
        var root = AppContext.BaseDirectory;
        foreach (var candidate in OperatingSystem.IsWindows()
                     ? new[] { Path.Combine(root, "runtimes", "win-x64", "native", "7z.exe"), Path.Combine(root, "7z.exe") }
                     : [Path.Combine(root, "runtimes", "linux-x64", "native", "7zz"), Path.Combine(root, "7zz")])
            if (File.Exists(candidate)) return candidate;
        return null;
    }

    internal sealed record Release(string Version, string Url, long Size);

    // The newest published MO2 release, and the plain archive from it: the same
    // tag also carries source, debug symbols and a uibase archive.
    internal static async Task<Release> LatestRelease(HttpClient client, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/ModOrganizer2/modorganizer/releases/latest");
        request.Headers.UserAgent.ParseAdd("mo2-nexus-frontend");
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var version = document.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v')
            ?? throw new InvalidOperationException("MO2 release has no tag");
        foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray()) {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (!name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase)) continue;
            if (name.Contains("-pdbs", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("-src", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("-uibase", StringComparison.OrdinalIgnoreCase)) continue;
            return new Release(version, asset.GetProperty("browser_download_url").GetString()!, asset.GetProperty("size").GetInt64());
        }
        throw new InvalidOperationException("MO2 release has no application archive");
    }

    // Downloads and extracts MO2 if it is not already there. Extraction goes to a
    // staging directory and is moved into place once complete, so an interrupted
    // install never leaves a half-extracted MO2 that looks installed.
    internal static async Task Install(Action<string?> report, CancellationToken token = default)
    {
        if (Installed) return;
        if (Extractor() is not { } extractor)
            throw new InvalidOperationException("No bundled 7-Zip to extract MO2 with");

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        report("Looking up the latest Mod Organizer release…");
        var release = await LatestRelease(client, token);

        Directory.CreateDirectory(Mo2ConfigPaths.DataCombine());
        var archive = Path.Combine(Mo2ConfigPaths.DataCombine(), $"Mod.Organizer-{release.Version}.7z");
        if (!File.Exists(archive) || new FileInfo(archive).Length != release.Size) {
            report($"Downloading Mod Organizer {release.Version}…");
            using var response = await client.GetAsync(release.Url, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            var partial = archive + ".part";
            await using (var source = await response.Content.ReadAsStreamAsync(token))
            await using (var destination = File.Create(partial))
                await Copy(source, destination, release.Size, report, token);
            File.Move(partial, archive, overwrite: true);
        }

        var staging = TemplateRoot + ".staging";
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        Directory.CreateDirectory(staging);
        report($"Extracting Mod Organizer {release.Version}…");
        var start = new ProcessStartInfo(extractor) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { "x", archive, "-o" + staging, "-y", "-bso0", "-bsp0" }) start.ArgumentList.Add(argument);
        using (var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the bundled 7-Zip")) {
            await process.WaitForExitAsync(token);
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Extracting MO2 failed: {await process.StandardError.ReadToEndAsync(token)}");
        }
        // Some archives wrap everything in a single directory; take that as the root.
        var root = staging;
        if (!File.Exists(Path.Combine(root, "ModOrganizer.exe")) &&
            Directory.GetFiles(root).Length == 0 && Directory.GetDirectories(root) is [var only])
            root = only;
        if (!File.Exists(Path.Combine(root, "ModOrganizer.exe")))
            throw new InvalidOperationException("The MO2 archive contained no ModOrganizer.exe");

        if (Directory.Exists(TemplateRoot)) Directory.Delete(TemplateRoot, recursive: true);
        Directory.Move(root, TemplateRoot);
        if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        await File.WriteAllTextAsync(VersionFile, release.Version, token);
        report($"Mod Organizer {release.Version} installed.");
    }

    private static async Task Copy(Stream source, Stream destination, long size, Action<string?> report, CancellationToken token)
    {
        var buffer = new byte[1 << 20];
        long done = 0;
        var announced = -1;
        int read;
        while ((read = await source.ReadAsync(buffer, token)) > 0) {
            await destination.WriteAsync(buffer.AsMemory(0, read), token);
            done += read;
            if (size <= 0) continue;
            var percent = (int)(done * 100 / size);
            if (percent == announced || percent % 5 != 0) continue;
            announced = percent;
            report($"Downloading Mod Organizer… {percent}%");
        }
    }

    // Creates the portable instance MO2 would have created itself, with its own
    // copy of the binaries, and installs the bridge into it.
    internal static string CreateInstance(string game, Action<string?>? report = null)
    {
        var entry = Mo2SupportedGames.Find(game) ?? throw new InvalidOperationException($"{game} is not a game MO2 supports");
        var executable = Mo2SupportedGames.ExecutablePath(game)
            ?? throw new InvalidOperationException($"{game} is not installed, so there is no game directory to manage");
        if (!Installed) throw new InvalidOperationException("Mod Organizer is not installed yet");
        var gameDirectory = Path.GetDirectoryName(executable)!;
        var instance = InstanceDirectory(game);

        if (!File.Exists(Path.Combine(instance, "ModOrganizer.exe"))) {
            report?.Invoke($"Setting up Mod Organizer for {game}…");
            Mirror(TemplateRoot, instance);
        }
        foreach (var folder in new[] { "mods", "downloads", "overwrite", Path.Combine("profiles", "Default") })
            Directory.CreateDirectory(Path.Combine(instance, folder));

        var ini = Path.Combine(instance, "ModOrganizer.ini");
        if (!File.Exists(ini)) {
            // MO2 reads this with QSettings: gamePath is stored as a byte array and
            // INI escaping doubles the separators on Windows.
            var escaped = gameDirectory.Replace("\\", "\\\\");
            File.WriteAllText(ini, string.Join('\n', [
                "[General]",
                $"gameName={entry.Mo2GameName}",
                $"gamePath=@ByteArray({escaped})",
                // Games whose plugin offers more than one variant — New Vegas has
                // Steam, GOG and Epic — are incomplete without one, and MO2 answers
                // that by opening its Setting up an instance dialog. Detection here
                // goes through Steam's own manifests, so Steam is the variant.
                "game_edition=Steam",
                "selected_profile=@ByteArray(Default)",
                // MO2 must never put a window in front of this frontend. On a first
                // start it asks whether to show its tutorial and then runs it, both
                // from MainWindow's first showEvent and both before the bridge can
                // hide anything. Declaring the instance already started skips that
                // branch entirely rather than racing it.
                "first_start=false",
                // Without a version MO2 reads this as an instance predating 2.5 and
                // asks how to migrate its categories. The instance is new, so it is
                // already on the current category system and there is nothing to
                // migrate; recording the version says so and keeps that system.
                $"version={InstalledVersion ?? "2.5.2"}",
                "",
            ]));
        }
        // Makes MO2 refuse to change instance, so this host always opens this game
        // and never consults the current-instance name shared by every MO2 on the
        // machine, nor shows the instance picker.
        var portable = Path.Combine(instance, "portable.txt");
        if (!File.Exists(portable)) File.WriteAllText(portable, "");

        // An empty profile still needs its lists, or MO2 treats it as unreadable.
        var profile = Path.Combine(instance, "profiles", "Default");
        foreach (var file in new[] { "modlist.txt", "plugins.txt", "loadorder.txt", "archives.txt" }) {
            var path = Path.Combine(profile, file);
            if (!File.Exists(path)) File.WriteAllText(path, "");
        }
        InstallBridge(instance);
        SuppressPrompts();
        return instance;
    }

    // MO2's own windows must never reach the user, and a first run has several
    // questions that are asked before the bridge can hide anything: its tutorial
    // offer, its create-instance introduction, and two about categories. Each is
    // gated by a flag in MO2's global settings — QSettings("Mod Organizer Team",
    // "Mod Organizer"), which is the registry on Windows and an ini elsewhere —
    // so setting all four answers them in advance rather than racing them.
    //
    // This is deliberately belt and braces: the instance is also written complete
    // enough that MO2 has no reason to ask in the first place.
    private static readonly string[] PromptFlags = [
        "HideCreateInstanceIntro", "HideTutorialQuestion",
        "HideCategoryReminder", "HideAssignCategoriesQuestion",
    ];

    internal static void SuppressPrompts()
    {
        try {
            if (OperatingSystem.IsWindows()) {
                foreach (var flag in PromptFlags) {
                    var start = new ProcessStartInfo("reg") { UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardOutput = true, RedirectStandardError = true };
                    foreach (var argument in new[] { "add", @"HKCU\Software\Mod Organizer Team\Mod Organizer",
                                 "/v", flag, "/t", "REG_DWORD", "/d", "1", "/f" }) start.ArgumentList.Add(argument);
                    using var process = Process.Start(start);
                    process?.WaitForExit(10000);
                }
                return;
            }
            // QSettings writes unsectioned keys under [General] in its ini format.
            var config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            var path = Path.Combine(config, "Mod Organizer Team", "Mod Organizer.conf");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : ["[General]"];
            foreach (var flag in PromptFlags) {
                if (lines.Any(line => line.StartsWith(flag + "=", StringComparison.Ordinal))) continue;
                var general = lines.FindIndex(line => line.Trim() == "[General]");
                if (general < 0) { lines.Insert(0, "[General]"); general = 0; }
                lines.Insert(general + 1, flag + "=true");
            }
            File.WriteAllLines(path, lines);
        } catch (Exception) {
            // Never block adding a game on being unable to write a preference; the
            // instance itself is already written so that nothing needs asking.
        }
    }

    // Hard links where the filesystem allows it, so a second instance costs its
    // own configuration rather than another 400MB of identical binaries. Falls
    // back to copying, which is always correct if slower.
    private static void Mirror(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source)) {
            var target = Path.Combine(destination, Path.GetFileName(file));
            if (File.Exists(target)) continue;
            if (!TryLink(file, target)) File.Copy(file, target);
        }
        foreach (var child in Directory.EnumerateDirectories(source))
            Mirror(child, Path.Combine(destination, Path.GetFileName(child)));
    }

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateHardLinkW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string newFile, string existingFile, IntPtr attributes);

    [DllImport("libc", SetLastError = true, EntryPoint = "link")]
    private static extern int LinkUnix(string existing, string newFile);

    private static bool TryLink(string existing, string target)
    {
        try {
            return OperatingSystem.IsWindows()
                ? CreateHardLink(target, existing, IntPtr.Zero)
                : LinkUnix(existing, target) == 0;
        } catch (DllNotFoundException) { return false; } catch (EntryPointNotFoundException) { return false; }
    }

    // The bridge is an ordinary MO2 Python tool extension, and a portable instance
    // carries its own plugins directory.
    internal static void InstallBridge(string instance)
    {
        var source = BridgeSource();
        if (source is null) return;
        CopyTree(source, Path.Combine(instance, "plugins", Path.GetFileName(source)));
    }

    private static string? BridgeSource()
    {
        // Beside the built application when published, and up the tree in a checkout.
        var candidates = new List<string> { Path.Combine(AppContext.BaseDirectory, "mo2-plugin", "nexus_frontend_bridge") };
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (var depth = 0; depth < 8 && directory is not null; depth++, directory = directory.Parent)
            candidates.Add(Path.Combine(directory.FullName, "mo2-plugin", "nexus_frontend_bridge"));
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static void CopyTree(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var child in Directory.EnumerateDirectories(source)) {
            if (Path.GetFileName(child) is "__pycache__") continue;
            CopyTree(child, Path.Combine(destination, Path.GetFileName(child)));
        }
    }

    // The same silence, written into an instance rather than into the machine.
    //
    // The flags above go to the shared Mod Organizer Team settings, which a portable
    // instance does not read: it keeps its settings in its own ModOrganizer.ini. The
    // instances this application writes are quiet because that ini says so — and an
    // instance written by something else does not. A Wabbajack install came up on
    // "Show tutorial?", a modal nobody could answer because a hosted MO2 draws
    // nothing on screen, with every bridge request queued behind it for good.
    //
    // Only missing keys are added, and only in [General]. Everything the other
    // program wrote is left exactly as it wrote it.
    internal static void SuppressInstancePrompts(string instance)
    {
        var path = Path.Combine(instance, "ModOrganizer.ini");
        if (!File.Exists(path)) return;
        try {
            var wanted = new Dictionary<string, string> {
                // What makes MO2 ask about the tutorial at all.
                ["first_start"] = "false",
                // Without a version it treats the instance as pre-dating the current
                // category system and offers to migrate it.
                ["version"] = InstalledVersion ?? "2.5.2",
            };
            var lines = File.ReadAllLines(path).ToList();
            var section = "General";
            var general = -1;
            for (var i = 0; i < lines.Count; i++) {
                var line = lines[i].Trim();
                if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; if (section == "General") general = i; continue; }
                var split = line.IndexOf('=');
                if (split <= 0 || section != "General") continue;
                wanted.Remove(line[..split].Trim());
            }
            if (wanted.Count == 0) return;
            if (general < 0) { lines.Insert(0, "[General]"); general = 0; }
            foreach (var (key, value) in wanted) lines.Insert(general + 1, $"{key}={value}");
            File.WriteAllLines(path, lines);
        } catch (Exception) {
            // An instance that cannot be quietened is still an instance; the failure
            // shows up as MO2 asking something, not as a broken install.
        }
    }
}
