namespace Mo2.Frontend;

// What a separator's closed state follows.
//
// Three cases, and the first is the one a freshly installed modlist lands in: MO2
// has recorded nothing, and the separators start closed rather than unrolling a
// hundred and fifty mods at once. The other two are deference — MO2's own record is
// honoured when it has one, and the person's own choice wins over both.
internal static class Mo2SeparatorStateCheck
{
    public static void Run()
    {
        var work = Path.Combine(Path.GetTempPath(), "mo2-separators-" + Guid.NewGuid().ToString("N"));
        var profile = Path.Combine(work, "profiles", "Viva New Vegas Extended");
        Directory.CreateDirectory(profile);
        string[] separators = ["Bug Fixes_separator", "Content_separator", "LOD_separator"];
        try {
            var fresh = Mo2SeparatorState.Initial(profile, separators);
            if (!separators.All(fresh.Contains) || fresh.Count != separators.Length)
                throw new Exception($"A profile MO2 has said nothing about opened with {fresh.Count} of {separators.Length} closed");
            Console.WriteLine($"  nothing recorded: all {fresh.Count} separators closed");

            // MO2's own record, in the format Qt writes a string list in.
            File.WriteAllLines(Path.Combine(profile, "settings.ini"), [
                "[General]", "LocalSaves=true",
                "[UserInterface]", "collapsed_separators=Content_separator, \"LOD_separator\"",
            ]);
            var recorded = Mo2SeparatorState.Initial(profile, separators);
            if (recorded.Count != 2 || !recorded.Contains("Content_separator") || !recorded.Contains("LOD_separator"))
                throw new Exception($"MO2's record was not honoured: {string.Join(", ", recorded)}");
            if (recorded.Contains("Bug Fixes_separator"))
                throw new Exception("A separator MO2 leaves open was closed anyway");
            Console.WriteLine($"  MO2 recorded two: {string.Join(", ", recorded.Order())}");

            // An empty record is a statement, not an absence: MO2 saying nothing is
            // collapsed must not be read as MO2 saying nothing at all.
            File.WriteAllLines(Path.Combine(profile, "settings.ini"), [
                "[UserInterface]", "collapsed_separators=@Invalid()",
            ]);
            var none = Mo2SeparatorState.Initial(profile, separators);
            if (none.Count != 0) throw new Exception($"An empty MO2 record closed {none.Count} separators");
            Console.WriteLine("  MO2 recorded an empty list: all open");

            // The person's own choice, which outlives a restart and outranks both.
            Mo2SeparatorState.Save(profile, ["Bug Fixes_separator"]);
            var chosen = Mo2SeparatorState.Initial(profile, separators);
            if (chosen.Count != 1 || !chosen.Contains("Bug Fixes_separator"))
                throw new Exception($"The saved choice was not used: {string.Join(", ", chosen)}");
            Console.WriteLine("  own choice kept: Bug Fixes_separator");

            // Two profiles of the same name in different instances are different
            // profiles, so their states must not be the same file.
            var other = Path.Combine(work, "second", "profiles", "Viva New Vegas Extended");
            Directory.CreateDirectory(other);
            var separate = Mo2SeparatorState.Initial(other, separators);
            if (separate.Count != separators.Length)
                throw new Exception("A same-named profile in another instance reused the first one's state");
            Console.WriteLine("  same-named profile elsewhere keeps its own state");

            Console.WriteLine("PASS separator state: closed by default, MO2's own record honoured, the person's choice kept per profile");
        } finally {
            try { Directory.Delete(work, recursive: true); } catch (IOException) { }
            try {
                foreach (var stale in new[] { profile, Path.Combine(work, "second", "profiles", "Viva New Vegas Extended") })
                    Mo2SeparatorState.Forget(stale);
            } catch (Exception) { }
        }
    }
}
