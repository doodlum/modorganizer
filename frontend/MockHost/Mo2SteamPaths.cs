using System.Text.RegularExpressions;

namespace Mo2.Frontend;

// Steam lays its libraries out the same way on both platforms; only the root
// differs. The Linux roots are the ones this host already used, kept first so
// Linux keeps resolving exactly as before.
internal static partial class Mo2SteamPaths
{
    internal static IEnumerable<string> Roots()
    {
        if (OperatingSystem.IsWindows()) {
            foreach (var variable in new[] { "ProgramFiles(x86)", "ProgramFiles" }) {
                var program = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrEmpty(program)) yield return Path.Combine(program, "Steam");
            }
            yield break;
        }
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(home, ".local", "share");
        yield return Path.Combine(data, "Steam");
        yield return Path.Combine(home, ".steam", "steam");
    }

    // Every steamapps directory Steam knows about, including libraries on other
    // drives, which libraryfolders.vdf lists by path.
    private static IEnumerable<string> LibraryFolders()
    {
        foreach (var root in Roots()) {
            var steamapps = Path.Combine(root, "steamapps");
            if (!Directory.Exists(steamapps)) continue;
            yield return steamapps;
            var index = Path.Combine(steamapps, "libraryfolders.vdf");
            if (!File.Exists(index)) continue;
            string text;
            try { text = File.ReadAllText(index); } catch (IOException) { continue; }
            foreach (Match match in LibraryPath().Matches(text)) {
                var other = Path.Combine(match.Groups[1].Value.Replace("\\\\", "\\"), "steamapps");
                if (Directory.Exists(other)) yield return other;
            }
        }
    }

    // The installed directory for a Steam app id, read from its own manifest
    // rather than guessed from the game name.
    internal static string? InstallDirectory(string appId)
    {
        foreach (var steamapps in LibraryFolders()) {
            var manifest = Path.Combine(steamapps, $"appmanifest_{appId}.acf");
            if (!File.Exists(manifest)) continue;
            string text;
            try { text = File.ReadAllText(manifest); } catch (IOException) { continue; }
            var match = InstallDir().Match(text);
            if (!match.Success) continue;
            var path = Path.Combine(steamapps, "common", match.Groups[1].Value);
            if (Directory.Exists(path)) return path;
        }
        return null;
    }

    // The best square-ish artwork Steam caches for a game, or null when it has none.
    internal static string? LibraryArt(string appId)
    {
        foreach (var root in Roots()) {
            var cache = Path.Combine(root, "appcache", "librarycache");
            foreach (var name in new[] { "library_600x900.jpg", "header.jpg" }) {
                var path = Path.Combine(cache, appId, name);
                if (File.Exists(path)) return path;
            }
            // Older Steam builds cached these flat, named by app id.
            var flat = Path.Combine(cache, appId + "_library_600x900.jpg");
            if (File.Exists(flat)) return flat;
        }
        return null;
    }

    [GeneratedRegex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPath();

    [GeneratedRegex("\"installdir\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDir();
}
