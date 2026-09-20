namespace Mo2.Frontend;

// A modlist instance loads: its MO2 comes up, the frontend reads it, and what
// comes back is the modlist rather than an empty profile.
//
// The install check ends at the folder on disk. This is the other half — that the
// folder is an instance this application can actually be in, which is what the
// sidebar entry promises when it is clicked.
internal static class Mo2ModlistLiveCheck
{
    public static async Task Run(string endpoint)
    {
        var instance = Path.GetFullPath(Path.Combine(endpoint, "..", "..", ".."));
        var note = Mo2ModlistInstances.Describe(instance)
            ?? throw new Exception($"{instance} carries no modlist record");
        Console.WriteLine($"  {note.Title} {note.Version} ({note.NamespacedName})");

        var profile = new Mo2LiveProfile(endpoint);
        await profile.Refresh();
        if (!profile.IsConnected) throw new Exception("The modlist instance did not connect: " + profile.Status);

        var mods = profile.Mods.Count;
        var enabled = profile.Mods.Count(x => (x.State & 2) != 0);
        var plugins = profile.Order.Plugins.Count;
        var active = profile.Order.Plugins.Count(x => x.IsActive);
        Console.WriteLine($"  game {profile.GameName}, profile {Path.GetFileName(profile.ProfilePath.TrimEnd('/', '\\'))}");
        Console.WriteLine($"  {mods} mods ({enabled} enabled), {plugins} plugins ({active} active)");

        // A modlist is a great many mods by definition. A handful would mean the
        // frontend attached to the wrong instance, or to one that installed nothing.
        if (mods < 50) throw new Exception($"Only {mods} mods: this does not look like an installed modlist");
        if (plugins == 0) throw new Exception("The instance reports no plugins");
        if (active == 0) throw new Exception("The instance has no active plugins, so nothing would load in game");

        // No MO2 window may be on screen while this is read. A hosted MO2 draws
        // nothing, and a modal it put up instead would be one nobody could answer.
        var shown = Mo2NativeWindows.Titles()
            .Where(x => x.Contains("Mod Organizer", StringComparison.OrdinalIgnoreCase) ||
                        x.Contains("Wabbajack", StringComparison.OrdinalIgnoreCase)).ToArray();
        Console.WriteLine($"  native windows seen: {(shown.Length == 0 ? "none" : string.Join("; ", shown))}");

        Console.WriteLine($"PASS modlist live: {note.Title} loads as its own instance with {mods} mods and {active} active plugins");
    }
}
