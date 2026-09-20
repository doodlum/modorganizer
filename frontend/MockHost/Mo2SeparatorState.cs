namespace Mo2.Frontend;

// Which separators are closed.
//
// MO2 keeps this per profile, as a UserInterface/collapsed_separators list in the
// profile's own settings.ini, and expands everything it does not name. The frontend
// used to keep nothing: every separator was open on every visit, which for a
// Wabbajack modlist means a hundred and fifty mods under a dozen headings with all
// of them unrolled at once.
//
// So MO2's list is read and honoured. Where MO2 has recorded nothing — a profile
// nobody has collapsed anything in yet, which is every freshly installed modlist —
// the separators start closed, because a list of headings is what a separator is
// for and scrolling past everything to find them is not.
//
// Changes are kept beside the frontend's other preferences rather than written back
// into MO2's file: MO2 holds that file open, rewrites it wholesale when the profile
// changes, and would drop anything written underneath it.
internal static class Mo2SeparatorState
{
    private const string Section = "UserInterface";
    private const string Key = "collapsed_separators";

    private static string OwnPath(string profilePath) =>
        Mo2ConfigPaths.Combine("separators", Fingerprint(profilePath) + ".txt");

    // Profile paths are long and full of characters a file name cannot hold, and two
    // instances can both have a profile called Default.
    private static string Fingerprint(string profilePath)
    {
        var normalised = Mo2InstanceCatalog.LocalPath(profilePath).ToLowerInvariant();
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalised));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    // What to start with, given the separators the profile actually has.
    internal static HashSet<string> Initial(string profilePath, IEnumerable<string> separators)
    {
        var all = separators.ToArray();
        if (Own(profilePath) is { } chosen) return chosen;
        if (FromMo2(profilePath) is { } recorded) return recorded;
        return new HashSet<string>(all, StringComparer.Ordinal);
    }

    private static HashSet<string>? Own(string profilePath)
    {
        try {
            var path = OwnPath(profilePath);
            if (!File.Exists(path)) return null;
            return new HashSet<string>(
                File.ReadAllLines(path).Select(x => x.Trim()).Where(x => x.Length > 0), StringComparer.Ordinal);
        } catch (Exception) { return null; }
    }

    internal static void Save(string profilePath, IEnumerable<string> collapsed)
    {
        try {
            var path = OwnPath(profilePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, collapsed);
        } catch (IOException) { }
    }

    // MO2's own record, read from the profile it belongs to. Absent means MO2 has
    // nothing to say rather than "nothing is collapsed", which is the distinction
    // that decides what a first visit looks like.
    private static HashSet<string>? FromMo2(string profilePath)
    {
        try {
            var path = Path.Combine(Mo2InstanceCatalog.LocalPath(profilePath), "settings.ini");
            if (!File.Exists(path)) return null;
            var section = "General";
            foreach (var raw in File.ReadLines(path)) {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith(';')) continue;
                if (line.StartsWith('[') && line.EndsWith(']')) { section = line[1..^1]; continue; }
                var split = line.IndexOf('=');
                if (split <= 0 || !section.Equals(Section, StringComparison.OrdinalIgnoreCase)) continue;
                if (!line[..split].Trim().Equals(Key, StringComparison.OrdinalIgnoreCase)) continue;
                return Parse(line[(split + 1)..].Trim());
            }
            return null;
        } catch (Exception) { return null; }
    }

    // Qt writes a string list as comma-separated values, quoting any that need it and
    // writing @Invalid() for a list it stored as empty.
    private static HashSet<string> Parse(string value)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        if (value.Length == 0 || value.StartsWith("@Invalid", StringComparison.OrdinalIgnoreCase)) return result;
        foreach (var part in value.Split(',')) {
            var item = part.Trim();
            if (item.Length >= 2 && item[0] == '"' && item[^1] == '"') item = item[1..^1];
            item = item.Replace("\\,", ",");
            if (item.Length > 0) result.Add(item);
        }
        return result;
    }

    // Used by the check to leave nothing behind in the real preferences directory.
    internal static void Forget(string profilePath)
    {
        try { var path = OwnPath(profilePath); if (File.Exists(path)) File.Delete(path); } catch (IOException) { }
    }
}
