using NexusMods.UI.Sdk;

namespace Mo2.Frontend;

internal interface IMo2ModInfoPage : IViewModelInterface { }

// MO2's mod information panel (src/modinfodialog.cpp and its tabs), rebuilt.
//
// What stood here before was a list of the fields the snapshot happened to carry.
// MO2's own panel is nine tabs over the mod's folder — what is in it, what of it
// the game will load, what other mods it fights with, and the three things about
// it that are the user's to change. None of that was reachable.
//
// Everything a tab shows is read the way MO2 reads it: its own folder for the
// files, MO2's list for what it knows about the mod, and MO2's own categories.
// Nothing is invented — a tab with nothing behind it says so rather than filling
// itself in.
internal sealed class Mo2ModInfoPage : AViewModel<IMo2ModInfoPage>, IMo2ModInfoPage
{
    // MO2's own file rules (modinfodialogtextfiles.cpp).
    private static readonly string[] TextKinds = [".txt", ".json", ".cfg", ".log", ".toml"];
    private static readonly string[] IniKinds = [".ini"];
    private static readonly string[] ImageKinds = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".dds"];
    private static readonly string[] PluginKinds = [".esp", ".esm", ".esl"];
    // Where MO2 puts a plugin it has been told to keep out of the game's way.
    internal const string OptionalFolder = "optional";

    internal Mo2LiveProfile Profile { get; }
    internal Mo2LiveMod Mod { get; private set; }
    internal string Folder { get; }
    internal Mo2ProfileTarget Target { get; }

    internal Mo2ModInfoPage(Mo2LiveProfile profile, Mo2LiveMod mod)
    {
        Profile = profile; Mod = mod; Target = profile.CurrentTarget;
        var instance = Mo2InstanceSettings.InstanceOf(profile.ProfilePath);
        Folder = mod.IsOverwrite
            ? Path.Combine(instance, "overwrite")
            : Path.Combine(instance, "mods", mod.Name);
    }

    internal bool FolderExists => Directory.Exists(Folder);

    // One file inside the mod, as a tab lists it: what MO2 shows in its list and
    // where the file actually is.
    internal sealed record Item(string Display, string Path);

    private IEnumerable<Item> Files(string[] kinds) =>
        !FolderExists ? [] : Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories)
            .Where(file => kinds.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !InOptional(file))
            .OrderBy(file => file, StringComparer.CurrentCultureIgnoreCase)
            .Select(file => new Item(Path.GetRelativePath(Folder, file), file));

    private bool InOptional(string file) =>
        Path.GetRelativePath(Folder, file).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(part => part.Equals(OptionalFolder, StringComparison.OrdinalIgnoreCase));

    internal Item[] TextFiles() => Files(TextKinds).ToArray();
    internal Item[] IniFiles() => Files(IniKinds).ToArray();
    internal Item[] Images() => Files(ImageKinds).ToArray();

    // MO2 splits a mod's plugins in two: the ones the game can see, and the ones
    // pushed into an "optional" folder so it cannot. Moving one between the two is
    // what its Optional Plugins tab is for.
    internal Item[] AvailablePlugins() => Files(PluginKinds).ToArray();

    internal Item[] OptionalPlugins()
    {
        var optional = Path.Combine(Folder, OptionalFolder);
        if (!Directory.Exists(optional)) return [];
        return Directory.EnumerateFiles(optional, "*", SearchOption.AllDirectories)
            .Where(file => PluginKinds.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .OrderBy(file => file, StringComparer.CurrentCultureIgnoreCase)
            .Select(file => new Item(Path.GetRelativePath(optional, file), file)).ToArray();
    }

    // MO2 moves the file itself rather than recording a preference, so this does
    // too: a plugin in the optional folder is one the game cannot load.
    internal string? MovePlugin(Item plugin, bool toOptional)
    {
        try {
            var optional = Path.Combine(Folder, OptionalFolder);
            var destination = toOptional
                ? Path.Combine(optional, Path.GetFileName(plugin.Path))
                : Path.Combine(Folder, Path.GetFileName(plugin.Path));
            if (File.Exists(destination)) return $"{Path.GetFileName(destination)} is already there";
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Move(plugin.Path, destination);
            return null;
        } catch (Exception error) { return error.Message; }
    }

    // What this mod wins and loses against the others, which MO2 works out from the
    // load order: a file both mods provide belongs to whichever sits later.
    internal sealed record Conflict(string File, string Other, bool Winning);

    internal Conflict[] Conflicts()
    {
        if (!FolderExists) return [];
        var instance = Mo2InstanceSettings.InstanceOf(Profile.ProfilePath);
        var root = Path.Combine(instance, "mods");
        var mine = Owned(Folder);
        if (mine.Count == 0) return [];

        var results = new List<Conflict>();
        foreach (var other in Profile.Mods.Where(x => x.IsRegular && x.Name != Mod.Name && (x.State & 6) != 0)) {
            var folder = Path.Combine(root, other.Name);
            if (!Directory.Exists(folder)) continue;
            var theirs = Owned(folder);
            foreach (var file in mine.Intersect(theirs, StringComparer.OrdinalIgnoreCase))
                results.Add(new Conflict(file, other.DisplayName, Mod.Priority > other.Priority));
            if (results.Count > 500) break;
        }
        return results.OrderBy(x => x.File, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static HashSet<string> Owned(string folder)
    {
        try {
            return Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(folder, file))
                .Where(file => !file.StartsWith("meta.ini", StringComparison.OrdinalIgnoreCase))
                .Take(4000).ToHashSet(StringComparer.OrdinalIgnoreCase);
        } catch (Exception) { return []; }
    }

    // The mod's folder as a tree, which is MO2's Filetree tab.
    internal sealed record Node(string Name, string Path, bool IsFolder, Node[] Children);

    internal Node[] Tree() => FolderExists ? Children(Folder, 0) : [];

    private static Node[] Children(string folder, int depth)
    {
        if (depth > 6) return [];
        try {
            var folders = Directory.GetDirectories(folder).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new Node(Path.GetFileName(x), x, true, Children(x, depth + 1)));
            var files = Directory.GetFiles(folder).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => new Node(Path.GetFileName(x), x, false, []));
            return folders.Concat(files).ToArray();
        } catch (Exception) { return []; }
    }

    // MO2's categories, as a flat list of what this mod has and what it could have.
    internal sealed record Category(string Name, bool Chosen, int Depth);

    // Read from the instance's own categories.dat rather than from the tree the
    // filter sidebar uses. That tree is MO2's filters — <Active>, <Conflicted>,
    // <Contains Meshes> — which describe a mod rather than being given to one;
    // categories.dat is the list MO2 actually assigns from, as "id|name|parent".
    internal Category[] Categories()
    {
        var owned = Mod.CategoryNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var file = Path.Combine(Mo2InstanceSettings.InstanceOf(Profile.ProfilePath), "categories.dat");
        var rows = new List<(int Id, string Name, int Parent)>();
        try {
            if (File.Exists(file))
                foreach (var line in File.ReadAllLines(file)) {
                    var parts = line.Split('|');
                    if (parts.Length < 3 || !int.TryParse(parts[0], out var id) || !int.TryParse(parts[^1], out var parent)) continue;
                    var name = string.Join('|', parts[1..^1]).Trim();
                    if (name.Length > 0) rows.Add((id, name, parent));
                }
        } catch (Exception) { }
        if (rows.Count == 0) return [];

        var result = new List<Category>();
        void Walk(int parent, int depth)
        {
            foreach (var row in rows.Where(x => x.Parent == parent).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)) {
                result.Add(new Category(row.Name, owned.Contains(row.Name), depth));
                if (depth < 4) Walk(row.Id, depth + 1);
            }
        }
        Walk(0, 0);
        return result.ToArray();
    }

    internal Task SaveCategories(IEnumerable<string> chosen) =>
        Profile.SetModCategories(Mod.Name, chosen.ToArray(), Target);

    internal Task SaveNotes(string notes) => Profile.SetModNotes(Mod.Name, notes, Target);

    internal void Reread()
    {
        if (Profile.Mods.FirstOrDefault(x => x.Name == Mod.Name) is { } fresh) Mod = fresh;
    }
}
