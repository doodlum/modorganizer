using SteamKit2;

namespace Mo2.Frontend;

internal sealed record Mo2ExternalFile(string Path, long Bytes, string Kind);
internal sealed record Mo2ExternalScan(string GameDirectory, int Depots, int SteamFiles, Mo2ExternalFile[] Files);

// Classify against the exact installed depot versions, never a guessed vanilla list.
// This service is read-only. A missing/invalid manifest prevents classification.
internal static class Mo2ExternalFiles
{
    internal static Mo2ExternalScan Scan(string gameDirectory, string appId, CancellationToken token = default,
        IEnumerable<string>? extraCaches = null)
    {
        gameDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory));
        var steamApps = Directory.GetParent(gameDirectory)?.Parent?.FullName
            ?? throw new InvalidOperationException("Game folder is not in a Steam library.");
        var appPath = Path.Combine(steamApps, $"appmanifest_{appId}.acf");
        var appBytes = File.ReadAllBytes(appPath);
        var app = KeyValue.LoadFromString(System.Text.Encoding.UTF8.GetString(appBytes))
            ?? throw new InvalidDataException("Steam's installation record could not be read.");
        if (app["appid"].Value != appId || !string.Equals(Path.GetFileName(gameDirectory), app["installdir"].Value, StringComparison.Ordinal))
            throw new InvalidDataException("Steam's installation record does not match this game folder.");
        var depots = app["InstalledDepots"].Children;
        if (depots.Count == 0) throw new InvalidDataException("Steam has no installed depot manifests for this game.");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var caches = new[] { Path.Combine(steamApps, "depotcache"), Path.Combine(steamApps, "..", "depotcache"),
            Path.Combine(home, ".local/share/Steam/depotcache"), Path.Combine(home, ".steam/steam/depotcache") }
            .Concat(extraCaches ?? []).Select(Path.GetFullPath).Distinct().ToArray();
        var owned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var depot in depots) {
            token.ThrowIfCancellationRequested();
            if (!uint.TryParse(depot.Name, out var depotId) || !ulong.TryParse(depot["manifest"].Value, out var manifestId))
                throw new InvalidDataException("Steam's installed depot record is incomplete.");
            var name = $"{depotId}_{manifestId}.manifest";
            var file = caches.Select(cache => Path.Combine(cache, name)).FirstOrDefault(File.Exists)
                ?? throw new FileNotFoundException($"Steam manifest {depotId} is not cached. Open Steam and verify this game's installation, then refresh. No files have been classified.");
            var manifest = DepotManifest.LoadFromFile(file);
            if (manifest is null || manifest.FilenamesEncrypted || manifest.DepotID != depotId || manifest.ManifestGID != manifestId || manifest.Files is null)
                throw new InvalidDataException($"Steam manifest {depotId} is unavailable or does not match the installed version.");
            foreach (var entry in manifest.Files) {
                if ((entry.Flags & EDepotFileFlag.Directory) != 0) continue;
                owned.Add(Normalize(entry.FileName));
            }
        }
        if (owned.Count == 0) throw new InvalidDataException("Steam's manifests contain no game files.");
        var files = FindExtras(gameDirectory, owned, token);
        if (!File.ReadAllBytes(appPath).SequenceEqual(appBytes))
            throw new IOException("Steam changed the installed version during the scan. Refresh after the update finishes.");
        return new(gameDirectory, depots.Count, owned.Count, files);
    }

    internal static string Normalize(string path)
    {
        path = path.Replace('\\', '/');
        if (path.StartsWith('/') || path.Contains(':') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Steam manifest contains an invalid relative file path.");
        return path;
    }

    internal static Mo2ExternalFile[] FindExtras(string root, HashSet<string> owned, CancellationToken token)
    {
        var result = new List<Mo2ExternalFile>();
        var pending = new Stack<string>(); pending.Push(root);
        while (pending.TryPop(out var folder)) {
            token.ThrowIfCancellationRequested();
            foreach (var entry in new DirectoryInfo(folder).EnumerateFileSystemInfos()) {
                token.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(root, entry.FullName).Replace(Path.DirectorySeparatorChar, '/');
                var link = (entry.Attributes & FileAttributes.ReparsePoint) != 0;
                if (!link && (entry.Attributes & FileAttributes.Directory) != 0) { pending.Push(entry.FullName); continue; }
                // Report links themselves, never traverse outside the game folder.
                if (!owned.Contains(relative)) result.Add(new(relative, link ? 0 : ((FileInfo)entry).Length, link ? "External link" : "External file"));
            }
        }
        return result.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // Physical scan paths use '/' between components. Unlike Steam manifest
    // paths, Unix filenames may contain literal backslashes or colons.
    internal static string[] PhysicalParts(string path)
    {
        var parts = path.Split('/');
        if (Path.IsPathRooted(path) || parts.Any(part => part is "" or "." or ".."))
            throw new InvalidDataException("External file has an invalid relative path.");
        return parts;
    }
}
