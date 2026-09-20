namespace Mo2.Frontend;

// The instance settings MO2's own mod menu branches on.
//
// Three of its entries are not decided by the mod at all but by whether the
// instance has that integration turned on, and an instance can have them off:
// Viva New Vegas ships with category_mappings=false, which is why MO2 built no
// Remap Category entry for a mod that otherwise qualified for one. Offering it
// anyway is an entry that cannot work, because the action it would trigger is one
// MO2 never created.
//
// Read from the instance's own ModOrganizer.ini, which is where MO2 keeps them
// (Settings::NexusSettings), under MO2's own defaults for an ini that omits them.
internal sealed record Mo2NexusIntegration(bool Endorsement, bool Tracked, bool CategoryMappings)
{
    internal static readonly Mo2NexusIntegration Default = new(true, true, true);
}

// Whether the overwrite folder has anything in it, which is the other condition
// MO2 reads off the instance rather than the mod: an empty overwrite gets only
// Open in Explorer, because there is nothing to sync, clear or move out of it.
internal static class Mo2InstanceSettings
{
    private static readonly Dictionary<string, (DateTime Read, Mo2NexusIntegration Value)> Cached = new(StringComparer.OrdinalIgnoreCase);
    // Long enough not to read the file for every row of a menu, short enough that
    // changing the setting in MO2 is reflected without a restart.
    private static readonly TimeSpan Life = TimeSpan.FromSeconds(10);

    internal static Mo2NexusIntegration Nexus(string instance)
    {
        if (instance.Length == 0) return Mo2NexusIntegration.Default;
        var key = Mo2InstanceCatalog.LocalPath(instance);
        lock (Cached)
            if (Cached.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.Read < Life)
                return cached.Value;

        var value = Read(key);
        lock (Cached) Cached[key] = (DateTime.UtcNow, value);
        return value;
    }

    private static Mo2NexusIntegration Read(string instance)
    {
        try {
            var path = Path.Combine(instance, "ModOrganizer.ini");
            if (!File.Exists(path)) return Mo2NexusIntegration.Default;
            var settings = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var section = "General";
            foreach (var raw in File.ReadLines(path)) {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(';')) continue;
                if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; continue; }
                if (!section.Equals("Settings", StringComparison.OrdinalIgnoreCase)) continue;
                var split = line.IndexOf('=');
                if (split <= 0) continue;
                // Trimmed both sides: MO2 writes no spaces around the "=", other
                // writers of the same format do.
                var value = line[(split + 1)..].Trim();
                if (bool.TryParse(value, out var flag)) settings[line[..split].Trim()] = flag;
            }
            bool Get(string name) => !settings.TryGetValue(name, out var value) || value;
            return new Mo2NexusIntegration(
                Get("endorsement_integration"), Get("tracked_integration"), Get("category_mappings"));
        } catch (Exception) { return Mo2NexusIntegration.Default; }
    }

    // The categories MO2 assigns from, which it keeps in the instance as
    // "id|name|parent". Not the tree the filter sidebar uses: that one is MO2's own
    // filters — <Active>, <Contains Meshes> — which describe a mod rather than being
    // given to one.
    internal static (int Id, string Name, int Parent)[] Categories(string instance)
    {
        try {
            var file = Path.Combine(Mo2InstanceCatalog.LocalPath(instance), "categories.dat");
            if (!File.Exists(file)) return [];
            var rows = new List<(int, string, int)>();
            foreach (var line in File.ReadAllLines(file)) {
                var parts = line.Split('|');
                if (parts.Length < 3 || !int.TryParse(parts[0], out var id) || !int.TryParse(parts[^1], out var parent)) continue;
                var name = string.Join('|', parts[1..^1]).Trim();
                if (name.Length > 0) rows.Add((id, name, parent));
            }
            return rows.ToArray();
        } catch (Exception) { return []; }
    }

    internal static bool OverwriteHasContent(string instance)
    {
        try {
            var path = Path.Combine(Mo2InstanceCatalog.LocalPath(instance), "overwrite");
            return Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any();
        } catch (Exception) { return false; }
    }

    // The instance a profile belongs to: MO2 keeps a profile at
    // <instance>/profiles/<name>, so the instance is two directories up.
    internal static string InstanceOf(string profilePath)
    {
        if (profilePath.Length == 0) return "";
        var profiles = Path.GetDirectoryName(Mo2InstanceCatalog.LocalPath(profilePath));
        return Path.GetDirectoryName(profiles) ?? "";
    }
}
