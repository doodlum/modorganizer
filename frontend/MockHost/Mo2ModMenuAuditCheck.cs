using System.Linq;
namespace Mo2.Frontend;

// Every entry MO2 puts on a mod is accounted for.
//
// MO2's mod list is worked from this menu, so an entry missing here is behaviour
// that cannot be reached at all. What this asks MO2 for is the menu it would build
// for a real mod in a real profile, and then insists that each entry is either one
// the frontend carries or one it has a stated reason not to.
//
// The point is the last part. A menu can be compared once by hand and drift the
// next time MO2 changes; this fails on any entry nobody has classified, so the
// reasons below have to stay true rather than merely having been true.
internal static class Mo2ModMenuAuditCheck
{
    // Entries the frontend deliberately does not carry, and why. Every one of these
    // is an entry MO2 shows; leaving one out is a decision, not an oversight.
    private static readonly (string Caption, string Why)[] Excluded = [
        ("Change Categories",
            "A gap, not a decision. MO2 hangs this on the menu as a submenu widget whose " +
            "ticks are applied when the submenu closes, and the bridge works by triggering " +
            "one of MO2's actions by name — MO2 reports this one with no entries inside it " +
            "to trigger. Nothing else here assigns a category to a mod, so that cannot be " +
            "done from the frontend at all."),
        ("Primary Category",
            "The same submenu in a second form, unreachable for the same reason and leaving " +
            "the same gap."),
    ];

    public static async Task Run(string endpoint, string? wanted = null)
    {
        var profile = new Mo2LiveProfile(endpoint);
        await profile.Refresh();
        if (!profile.IsConnected) throw new Exception("Not connected to MO2: " + profile.Status);
        var target = profile.CurrentTarget;
        var instance = Mo2InstanceSettings.InstanceOf(profile.ProfilePath);
        var nexus = Mo2InstanceSettings.Nexus(instance);
        Console.WriteLine($"  instance {Path.GetFileName(instance)}: endorsement {nexus.Endorsement}, " +
            $"tracking {nexus.Tracked}, category mappings {nexus.CategoryMappings}, " +
            $"overwrite {(Mo2InstanceSettings.OverwriteHasContent(instance) ? "has content" : "empty")}");

        var kinds = new List<(string Kind, Mo2LiveMod? Mod)> {
            ("regular mod", profile.Mods.FirstOrDefault(x => x.IsRegular && x.NexusId > 0)
                ?? profile.Mods.FirstOrDefault(x => x.IsRegular)),
            ("separator", profile.Mods.FirstOrDefault(x => x.IsSeparator)),
            ("overwrite", profile.Mods.FirstOrDefault(x => x.IsOverwrite)),
            ("backup", profile.Mods.FirstOrDefault(x => x.IsBackup)),
        };
        if (wanted is { Length: > 0 })
            kinds.Insert(0, ("chosen", profile.Mods.FirstOrDefault(x => x.Name == wanted || x.DisplayName == wanted)));

        Console.WriteLine($"  profile has {profile.Mods.Count} mods: {profile.Mods.Count(x => x.IsRegular)} regular, " +
            $"{profile.Mods.Count(x => x.IsSeparator)} separators, {profile.Mods.Count(x => x.IsOverwrite)} overwrite, " +
            $"{profile.Mods.Count(x => x.IsBackup)} backup, {profile.Mods.Count(x => x.IsForeign)} foreign");
        var unclassified = new List<string>();
        var missing = new List<string>();
        foreach (var (kind, mod) in kinds) {
            if (mod is null) { Console.WriteLine($"  {kind}: none in this profile"); continue; }
            string[] theirs;
            try { theirs = Flatten(await profile.ReadListMenu("readModMenu", [mod.Name], target)); }
            catch (Exception error) { Console.WriteLine($"  {kind}: MO2 would not report its menu ({error.Message})"); continue; }

            var ours = Mo2ModMenuCaptions.Captions(mod, Mo2InstanceSettings.Nexus(instance),
                Mo2InstanceSettings.OverwriteHasContent(instance));
            Console.WriteLine($"  {kind} \"{mod.DisplayName}\": MO2 offers {theirs.Length}, the frontend offers {ours.Length}");
            if (Environment.GetEnvironmentVariable("MO2_MENU_DUMP") == "1") {
                Console.WriteLine("    MO2: " + string.Join(" | ", theirs));
                Console.WriteLine("    us : " + string.Join(" | ", ours));
            }

            foreach (var caption in theirs) {
                if (ours.Any(x => Canonical(x) == Canonical(caption))) continue;
                if (Excluded.Any(x => Matches(caption, x.Caption))) continue;
                unclassified.Add($"{kind}: {caption}");
            }
            // The other direction: an entry offered here that MO2 would not build is an
            // entry that will not work, because the work is MO2's.
            foreach (var caption in ours) {
                if (theirs.Any(x => Canonical(x) == Canonical(caption))) continue;
                if (Local.Any(x => Matches(caption, x))) continue;
                missing.Add($"{kind}: {caption}");
            }
        }

        foreach (var (caption, why) in Excluded) Console.WriteLine($"  not carried — {caption}: {why}");

        if (unclassified.Count != 0)
            throw new Exception("MO2 offers entries the frontend neither carries nor explains:\n    " + string.Join("\n    ", unclassified));
        if (missing.Count != 0)
            throw new Exception("The frontend offers entries MO2 would not build, so they cannot work:\n    " + string.Join("\n    ", missing));

        Console.WriteLine("PASS mod menu: every entry MO2 offers is carried or has a stated reason, and nothing is offered that MO2 would not build");
    }

    // Entries the frontend draws itself rather than triggering in MO2, so MO2's own
    // menu is not expected to contain them.
    private static readonly string[] Local = [
        "All Mods", "Collapse all", "Collapse others", "Expand all", "Send to... ", "Lowest priority", "Highest priority",
        "Reset Color",
        "Open in Explorer", "Information...", "Rename Mod...", "Rename Separator...",
        "Remove Mod...", "Remove Separator...", "Select Color...",
    ];

    // One action, spelled several ways. MO2 renames these by where the menu was
    // opened — "inside" on a separator, "below" at the end of the list — and by
    // whether a filter is on, which changes what "all" means. The frontend offers one
    // entry for each and hands MO2 every spelling, so the audit compares them as one.
    private static readonly string[][] Aliases = [
        ["Install mod above", "Install mod inside", "Install mod below", "Install mod"],
        ["Create empty mod above", "Create empty mod inside", "Create empty mod below", "Create empty mod"],
        ["Create separator above", "Create separator"],
        ["Enable all", "Enable all matching mods"],
        ["Disable all", "Disable all matching mods"],
    ];

    private static string Canonical(string caption)
    {
        var trimmed = caption.TrimEnd(' ', '.', '…');
        foreach (var group in Aliases)
            if (group.Any(name => trimmed.Equals(name, StringComparison.OrdinalIgnoreCase)))
                return group[0];
        return trimmed;
    }

    // MO2 renames several entries by where the menu was opened; a caption matches if
    // either is a prefix of the other once trailing punctuation and spaces are gone.
    private static bool Matches(string caption, string other)
    {
        var left = Canonical(caption); var right = Canonical(other);
        return left.Equals(right, StringComparison.OrdinalIgnoreCase) ||
               left.StartsWith(right, StringComparison.OrdinalIgnoreCase) ||
               right.StartsWith(left, StringComparison.OrdinalIgnoreCase);
    }

    private static string[] Flatten(Mo2MenuEntry[] entries) =>
        entries.SelectMany(entry => entry.Captions()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
}
