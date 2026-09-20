namespace Mo2.Frontend;

// Reading the Wabbajack gallery, against the live gallery rather than a fixture.
//
// A fixture would only prove this parses what it was written against. What has to
// hold is that it parses what Wabbajack publishes today: over a hundred separate
// repositories, each kept by whoever authored the lists in it, in a shape nobody
// here controls.
internal static class Mo2ModlistCatalogCheck
{
    public static async Task Run(string? wanted = null)
    {
        var lists = await Mo2ModlistCatalog.Read(line => { if (line is { Length: > 0 }) Console.WriteLine("  " + line); }, refresh: true);
        if (lists.Count < 100) throw new Exception($"The gallery returned only {lists.Count} modlists");

        var supported = lists.Where(x => x.Supported).ToArray();
        if (supported.Length == 0) throw new Exception("No modlist mapped onto a game MO2 manages");

        foreach (var list in supported)
            if (Mo2SupportedGames.Find(list.Game!) is null)
                throw new Exception($"{list.Title} maps to {list.Game}, which is not a game in the catalogue");

        var games = supported.GroupBy(x => x.Game!).OrderByDescending(x => x.Count()).ToArray();
        Console.WriteLine($"  {lists.Count} modlists, {supported.Length} for games MO2 manages");
        foreach (var game in games) Console.WriteLine($"    {game.Key}: {game.Count()}");

        var skipped = lists.Where(x => !x.Supported).Select(x => x.WabbajackGame).Distinct().OrderBy(x => x).ToArray();
        Console.WriteLine($"  not offered: {string.Join(", ", skipped)}");

        // Every list has to carry what it takes to install it and what it takes to
        // choose it. A row with no size or no version is a row nobody can judge.
        foreach (var list in supported) {
            if (string.IsNullOrWhiteSpace(list.MachineUrl) || string.IsNullOrWhiteSpace(list.Repository))
                throw new Exception($"{list.Title} has no name Wabbajack could be given");
            if (list.NamespacedName.Count(c => c == '/') != 1)
                throw new Exception($"{list.Title} has a malformed name: {list.NamespacedName}");
        }
        var sized = supported.Count(x => x.TotalSize > 0);
        var illustrated = supported.Count(x => x.ImageUrl is { Length: > 0 });
        Console.WriteLine($"  {sized}/{supported.Length} give a size, {illustrated}/{supported.Length} give artwork");

        var name = wanted ?? "Viva New Vegas";
        var found = lists.FirstOrDefault(x => x.Title.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new Exception($"The gallery does not offer {name}");
        Console.WriteLine($"  {found.Title} — {found.NamespacedName}, {found.Game}, v{found.Version}, " +
            $"{found.TotalSize / 1024d / 1024 / 1024:0.0}GB total, instance key {found.InstanceKey}");
        if (!found.Supported) throw new Exception($"{name} is for {found.WabbajackGame}, which did not map to a game MO2 manages");

        Console.WriteLine($"PASS modlist catalogue: {lists.Count} modlists read from the live gallery, {supported.Length} installable, {name} among them");
    }
}
