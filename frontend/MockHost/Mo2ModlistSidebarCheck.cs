namespace Mo2.Frontend;

// A modlist stands beside the game it is built on, not inside it.
//
// The sidebar used to group instances by the game MO2 reports, which is right for
// two setups of one game and wrong for a modlist: Viva New Vegas and a person's own
// New Vegas both report New Vegas, so one button would have covered both and the
// tie-break would have decided which one a click reached. What separates them is
// the note Wabbajack installs leave behind, and that is what is checked here.
internal static class Mo2ModlistSidebarCheck
{
    public static void Run()
    {
        var work = Path.Combine(Path.GetTempPath(), "mo2-modlist-sidebar-" + Guid.NewGuid().ToString("N"));
        try {
            var plain = Path.Combine(work, "fallout-new-vegas");
            var first = Path.Combine(work, "moddinglinked-vivanewvegas");
            var second = Path.Combine(work, "other-list");
            foreach (var path in new[] { plain, first, second }) Directory.CreateDirectory(path);

            Mo2ModlistInstances.Mark(first, Modlist("Viva New Vegas", "ModdingLinked", "VivaNewVegas"));
            Mo2ModlistInstances.Mark(second, Modlist("Wild Card", "WildCardFNV", "WildCard"));

            var entries = new[] { Entry(plain), Entry(first), Entry(second) };
            var keys = entries.Select(Mo2SpineKey.For).ToArray();

            if (keys.Distinct().Count() != 3)
                throw new Exception($"Three instances produced {keys.Distinct().Count()} sidebar entries: {string.Join(", ", keys)}");
            if (keys[0] != "New Vegas")
                throw new Exception($"A plain instance should still be keyed by its game, was {keys[0]}");

            var names = entries.Select(Mo2SpineKey.Name).ToArray();
            if (names[0] != "New Vegas") throw new Exception($"A plain instance should be named for its game, was {names[0]}");
            if (names[1] != "Viva New Vegas") throw new Exception($"A modlist should be named for itself, was {names[1]}");
            if (names[2] != "Wild Card") throw new Exception($"A modlist should be named for itself, was {names[2]}");

            // The note has to survive being read back, because it is what everything
            // else about a modlist instance is read from.
            var note = Mo2ModlistInstances.Describe(first) ?? throw new Exception("A marked instance describes nothing");
            if (note.NamespacedName != "ModdingLinked/VivaNewVegas")
                throw new Exception($"The note records {note.NamespacedName}");
            if (Mo2ModlistInstances.Describe(plain) is not null)
                throw new Exception("A plain instance reported itself as a modlist");

            Console.WriteLine($"  sidebar entries: {string.Join(" | ", names)}");
            Console.WriteLine("PASS modlist sidebar: a modlist gets its own entry under its own name beside the plain game, and two modlists stay apart");
        } finally {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
        }
    }

    private static Mo2Modlist Modlist(string title, string repository, string machine) => new(
        Title: title, Description: "", Author: "", Repository: repository, MachineUrl: machine,
        WabbajackGame: "falloutnewvegas", Game: "Fallout: New Vegas", Version: "1.0", Tags: [],
        Official: false, Nsfw: false, Utility: false, DownloadSize: 0, TotalSize: 0,
        ImageUrl: null, ReadmeUrl: null, Updated: DateTime.UtcNow);

    // Only the two things the sidebar reads: where the instance is, and what MO2
    // says its game is.
    private static Mo2CatalogEntry Entry(string directory) => new(
        new Mo2Registration(directory, Path.Combine(directory, "plugins", "data", "frontend-bridge")),
        new Mo2InstanceSnapshot(directory, "New Vegas", "Default", Path.Combine(directory, "mods"),
            Path.Combine(directory, "downloads"), [], []), null);
}
