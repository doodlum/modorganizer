using System.Text.Json;

namespace Mo2.Frontend;

internal sealed record Mo2ModEntry(string Name, bool Enabled, bool Unmanaged, int FileOrder, bool DirectoryExists);
internal sealed record Mo2PluginEntry(string Name, int Order, bool? AsteriskMarker);
internal sealed record Mo2ProfileSnapshot(string Name, string Directory, Mo2ModEntry[] ModEntries, Mo2PluginEntry[] Plugins);
internal sealed record Mo2DownloadSnapshot(string Name, long Bytes, bool Partial, bool HasMetadata);
internal sealed record Mo2InstanceSnapshot(string Directory, string Game, string SelectedProfile, string ModsDirectory,
    string DownloadsDirectory, Mo2ProfileSnapshot[] Profiles, Mo2DownloadSnapshot[] Downloads);

// Read-only bootstrap/discovery. Runtime game-plugin decisions and all mutations
// belong to the MO2 host; this reader never rewrites its files.
internal static class Mo2ProfileFiles
{
    public static string ReadGameDirectory(string root)
    {
        // Matched on the key rather than on "gamePath=", because an ini written with
        // spaces around the "=" is the same format and Wabbajack writes one.
        var value = File.ReadLines(Path.Combine(root, "ModOrganizer.ini"))
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.IndexOf('=') > 0 &&
                line[..line.IndexOf('=')].Trim().Equals("gamePath", StringComparison.OrdinalIgnoreCase));
        if (value is null) throw new InvalidOperationException("MO2 has no configured game folder.");
        return Resolve(value[(value.IndexOf('=') + 1)..].Trim(), root);
    }
    public static Mo2InstanceSnapshot Read(string root)
    {
        root = Path.GetFullPath(root);
        var ini = ReadIni(Path.Combine(root, "ModOrganizer.ini"));
        var basePath = Resolve(ini.GetValueOrDefault("Settings/base_directory", root), root);
        var mods = Resolve(ini.GetValueOrDefault("Settings/mod_directory", "%BASE_DIR%/mods"), basePath);
        var profiles = Resolve(ini.GetValueOrDefault("Settings/profiles_directory", "%BASE_DIR%/profiles"), basePath);
        var downloads = Resolve(ini.GetValueOrDefault("Settings/download_directory", "%BASE_DIR%/downloads"), basePath);
        return new(root, Decode(ini.GetValueOrDefault("General/gameName", "Unknown game")),
            Decode(ini.GetValueOrDefault("General/selected_profile", "")), mods, downloads,
            Directory.Exists(profiles) ? Directory.GetDirectories(profiles).Order(StringComparer.OrdinalIgnoreCase).Select(path => ReadProfile(path, mods)).ToArray() : [],
            Directory.Exists(downloads) ? Directory.GetFiles(downloads).Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase).Select(path => new Mo2DownloadSnapshot(Path.GetFileName(path), new FileInfo(path).Length,
                    path.EndsWith(".unfinished", StringComparison.OrdinalIgnoreCase), File.Exists(path + ".meta"))).ToArray() : []);
    }

    public static Mo2ProfileSnapshot ReadProfile(string path, string modsDirectory)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mods = new List<Mo2ModEntry>();
        foreach (var line in Lines(Path.Combine(path, "modlist.txt"))) {
            var marker = line[0];
            var name = (marker is '+' or '-' or '*' ? line[1..] : line).Trim();
            if (name.Length == 0 || !names.Add(name)) continue;
            mods.Add(new(name, marker != '-', marker == '*', mods.Count,
                IsChildName(name) && Directory.Exists(Path.Combine(modsDirectory, name))));
        }
        var enabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in Lines(Path.Combine(path, "plugins.txt"))) {
            var name = line.TrimStart('*').Trim();
            if (name.Length > 0) enabled.TryAdd(name, line.StartsWith('*'));
        }
        var order = Lines(Path.Combine(path, "loadorder.txt")).Concat(enabled.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var plugins = order.Select((name, i) => new Mo2PluginEntry(name, i, enabled.TryGetValue(name, out var active) ? active : null)).ToArray();
        return new(Path.GetFileName(path), path, mods.ToArray(), plugins);
    }

    private static bool IsChildName(string name) => name is not "." and not ".." && name.IndexOfAny(['/', '\\']) < 0;
    private static IEnumerable<string> Lines(string path) => File.Exists(path)
        ? File.ReadLines(path).Select(line => line.Trim().TrimStart('\uFEFF')).Where(line => line.Length > 0 && !line.StartsWith('#')) : [];
    private static Dictionary<string, string> ReadIni(string path)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = "General";
        foreach (var line in File.ReadLines(path).Select(line => line.Trim())) {
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#')) continue;
            if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; continue; }
            var split = line.IndexOf('=');
            // The value is trimmed as well as the key. MO2 writes "key=value" with no
            // spaces, but an ini written by anything else may put them around the "="
            // and both are the same file format. Wabbajack does: its instances came
            // back with a game of " New Vegas", which matched nothing, and a selected
            // profile still wrapped in @ByteArray because the wrapper no longer
            // started the string.
            if (split > 0) result[section + "/" + line[..split].Trim()] = line[(split + 1)..].Trim();
        }
        return result;
    }
    private static string Decode(string value) => value.StartsWith("@ByteArray(") && value.EndsWith(')') ? value[11..^1] : value.Trim('"');
    private static string Resolve(string value, string basePath)
    {
        value = Decode(value).Replace("%BASE_DIR%", basePath, StringComparison.OrdinalIgnoreCase).Replace("\\\\", "\\").Replace('\\', '/');
        if (!OperatingSystem.IsWindows() && value.StartsWith("Z:/", StringComparison.OrdinalIgnoreCase)) value = value[2..];
        if (!OperatingSystem.IsWindows() && value.Length > 1 && value[1] == ':')
            throw new NotSupportedException($"Windows path requires the instance's Wine drive mapping: {value}");
        return Path.GetFullPath(value, basePath);
    }

    public static void Inspect(string[] roots)
    {
        Console.WriteLine(JsonSerializer.Serialize(new {
            schemaVersion = 1,
            limitations = "Read-only disk snapshot. Mod entries retain file order (MO2 reverses managed priorities). Missing mods, unmanaged content, forced plugin activation and running downloads require the MO2 host/game plugins. AsteriskMarker records the literal plugins.txt marker, not resolved plugin activation.",
            instances = roots.Select(Read).ToArray(),
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
