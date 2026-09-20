namespace Mo2.Frontend;

// An installed modlist becomes an instance the sidebar offers, and one the
// frontend can start on its own.
//
// The install itself only produces a folder. What makes it something a person can
// go to is registration: the catalogue records it, its own MO2 is set as its
// launcher, and the sidebar keys it apart from the plain game. This runs the same
// steps the Install button runs after Wabbajack finishes, so what is checked is
// that path rather than a description of it.
internal static class Mo2ModlistRegisterCheck
{
    public static async Task Run(string? wanted)
    {
        var name = wanted is { Length: > 0 } ? wanted : "Viva New Vegas";
        var lists = await Mo2ModlistCatalog.Read();
        var list = lists.FirstOrDefault(x => x.Title.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception($"The gallery does not offer {name}");

        var instance = Mo2ModlistInstances.DirectoryFor(list);
        if (!Mo2ModlistInstances.Installed(list))
            throw new Exception($"{name} is not installed at {instance}");

        var catalog = new Mo2InstanceCatalog("");
        if (!catalog.Registrations.Any(x => Mo2InstanceCatalog.LocalPath(x.Directory) == Mo2InstanceCatalog.LocalPath(instance))) {
            catalog.Add(instance);
            var added = catalog.Read().Single(x =>
                Mo2InstanceCatalog.LocalPath(x.Registration.Directory) == Mo2InstanceCatalog.LocalPath(instance)).Registration;
            catalog.SetLauncher(added, Path.Combine(instance, "ModOrganizer.exe"));
            Console.WriteLine("  registered and given its own MO2 as launcher");
        } else Console.WriteLine("  already registered");

        var entries = catalog.Read();
        var entry = entries.FirstOrDefault(x =>
            Mo2InstanceCatalog.LocalPath(x.Registration.Directory) == Mo2InstanceCatalog.LocalPath(instance))
            ?? throw new Exception("The instance did not appear in the catalogue");
        if (entry.Error is { } error) throw new Exception($"The catalogue could not read the instance: {error}");
        if (entry.Instance is not { } snapshot) throw new Exception("The catalogue read no instance");
        if (entry.Registration.Launcher is not { Length: > 0 } launcher || !File.Exists(launcher))
            throw new Exception("The instance has no launcher of its own");

        Console.WriteLine($"  game {snapshot.Game}, profiles {string.Join(", ", snapshot.Profiles.Select(x => x.Name))}, selected {snapshot.SelectedProfile}");
        if (snapshot.Profiles.Length == 0) throw new Exception("The instance has no profile to open");

        // The point of the whole thing: it is its own row, not folded into the game.
        var readable = entries.Where(x => x.Instance is not null).ToArray();
        var rows = readable.GroupBy(Mo2SpineKey.For)
            .Select(g => Mo2SpineKey.Name(g.First())).ToArray();
        Console.WriteLine($"  sidebar: {string.Join(" | ", rows)}");
        if (!rows.Contains(list.Title))
            throw new Exception($"The sidebar does not offer {list.Title}: {string.Join(", ", rows)}");

        var plain = readable.Where(x => x.Instance!.Game == snapshot.Game &&
            Mo2ModlistInstances.Describe(x.Registration.Directory) is null).ToArray();
        if (plain.Length > 0 && rows.Count(x => x == snapshot.Game) == 0)
            throw new Exception($"The plain {snapshot.Game} instance lost its own sidebar entry");
        Console.WriteLine(plain.Length > 0
            ? $"  stands beside the plain {snapshot.Game} instance"
            : $"  no plain {snapshot.Game} instance registered to stand beside");

        Console.WriteLine($"  bridge endpoint {entry.Registration.Endpoint}");
        Console.WriteLine($"PASS modlist register: {list.Title} is its own sidebar entry with its own MO2, ready to open");
    }
}
