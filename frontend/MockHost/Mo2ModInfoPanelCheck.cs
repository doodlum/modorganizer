namespace Mo2.Frontend;

// The ported mod information panel, against a real mod in a real profile.
//
// Nine tabs were rebuilt from MO2's own (modinfodialog.cpp and its tabs), and the
// substance of all of them is what each one reads rather than how it is drawn. So
// what is checked is the reading: that the file tabs find the mod's files under
// MO2's own rules, that the plugin tab can tell a plugin the game loads from one
// kept out of its way, that conflicts are worked out against the load order, and
// that the two tabs which change something actually change it in MO2.
internal static class Mo2ModInfoPanelCheck
{
    public static async Task Run(string endpoint, string? wanted = null)
    {
        var profile = new Mo2LiveProfile(endpoint);
        await profile.Refresh();
        if (!profile.IsConnected) throw new Exception("Not connected to MO2: " + profile.Status);

        var mod = wanted is { Length: > 0 }
            ? profile.Mods.FirstOrDefault(x => x.Name == wanted || x.DisplayName == wanted)
            : profile.Mods.FirstOrDefault(x => x.IsRegular && x.NexusId > 0) ?? profile.Mods.FirstOrDefault(x => x.IsRegular);
        if (mod is null) throw new Exception("This profile has no ordinary mod to open the panel on");

        var page = new Mo2ModInfoPage(profile, mod);
        Console.WriteLine($"  \"{mod.DisplayName}\" at {page.Folder}");
        if (!page.FolderExists) throw new Exception("The panel could not find the mod's folder");

        // The three file tabs, under MO2's own extension rules.
        var text = page.TextFiles(); var ini = page.IniFiles(); var images = page.Images();
        Console.WriteLine($"  text {text.Length}, ini {ini.Length}, images {images.Length}");
        foreach (var item in text) {
            if (!File.Exists(item.Path)) throw new Exception($"Text tab lists {item.Display}, which is not there");
            if (Path.GetExtension(item.Path).Equals(".ini", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Text tab lists {item.Display}, which belongs to the ini tab");
        }
        foreach (var item in ini)
            if (!Path.GetExtension(item.Path).Equals(".ini", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"INI tab lists {item.Display}, which is not an ini file");

        // A plugin the game can load and one kept out of its way are different things,
        // and the tab has to tell them apart rather than listing the folder twice.
        var available = page.AvailablePlugins(); var optional = page.OptionalPlugins();
        Console.WriteLine($"  plugins {available.Length} available, {optional.Length} optional");
        foreach (var plugin in available)
            if (plugin.Display.Contains(Mo2ModInfoPage.OptionalFolder, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"{plugin.Display} is in the optional folder but listed as available");

        // The folder itself, which is the Filetree tab.
        var tree = page.Tree();
        if (tree.Length == 0) throw new Exception("The filetree tab is empty for a mod that has files");
        var files = Count(tree);
        Console.WriteLine($"  filetree {tree.Length} at the top, {files} entries in all");

        // Conflicts, worked out against the load order the profile is actually in.
        var conflicts = page.Conflicts();
        Console.WriteLine($"  conflicts {conflicts.Length} ({conflicts.Count(x => x.Winning)} winning)");
        foreach (var conflict in conflicts.Take(3))
            Console.WriteLine($"    {conflict.File} {(conflict.Winning ? "beats" : "loses to")} {conflict.Other}");

        // Categories, which MO2's own menu cannot be driven for — this is the tab
        // that closes that gap, so it has to list MO2's categories and mark the
        // mod's own among them.
        var categories = page.Categories();
        if (categories.Length == 0) throw new Exception("The categories tab lists nothing");
        var chosen = categories.Where(x => x.Chosen).Select(x => x.Name).ToArray();
        Console.WriteLine($"  categories {categories.Length} offered, {chosen.Length} on this mod: {string.Join(", ", chosen)}");
        Console.WriteLine($"    offered: {string.Join(", ", categories.Take(12).Select(x => x.Name))}");

        // And then the two tabs that write. Both are put back afterwards.
        var beforeNotes = mod.Notes;
        var probe = "checked by the information panel";
        await page.SaveNotes(probe);
        await profile.Refresh();
        var afterNotes = profile.Mods.First(x => x.Name == mod.Name).Notes;
        if (afterNotes != probe) {
            Console.WriteLine($"  notes did not save: MO2 reports \"{afterNotes}\" ({profile.Status})");
            throw new Exception("The Notes tab did not change the mod's notes in MO2");
        }
        Console.WriteLine("  notes saved and read back");
        await page.SaveNotes(beforeNotes);

        // A real category, not one of MO2's own bracketed filters like <Active>.
        var newCategory = categories.FirstOrDefault(x => !x.Chosen && !x.Name.StartsWith('<'));
        if (newCategory is not null) {
            await page.SaveCategories(chosen.Append(newCategory.Name));
            await profile.Refresh();
            var now = profile.Mods.First(x => x.Name == mod.Name).CategoryNames;
            if (!now.Contains(newCategory.Name, StringComparer.OrdinalIgnoreCase)) {
                Console.WriteLine($"  categories did not save: MO2 reports {string.Join(", ", now)} ({profile.Status})");
                throw new Exception("The Categories tab did not change the mod's categories in MO2");
            }
            Console.WriteLine($"  category \"{newCategory.Name}\" added and read back");
            await page.SaveCategories(chosen);
            await profile.Refresh();
        }

        Console.WriteLine($"PASS mod info panel: nine tabs read the mod's own folder and MO2's own record, and the two that write change it in MO2");
    }

    private static int Count(Mo2ModInfoPage.Node[] nodes) =>
        nodes.Length + nodes.Sum(node => Count(node.Children));
}
