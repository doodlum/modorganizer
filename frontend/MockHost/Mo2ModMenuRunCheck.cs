using System.Diagnostics;

namespace Mo2.Frontend;

// Every entry on the mod menu is pressed, not just listed.
//
// Comparing captions says the menu has the right entries; it says nothing about
// whether pressing one does anything. Half of MO2's mod actions ask something
// before they act, in a window a hosted MO2 draws nowhere — an unanswered one
// holds MO2's event loop with every later request queued behind it until MO2 is
// killed, which is what picking "Send to... → Separator..." did.
//
// So each entry is run against a live MO2 and watched: what MO2 asked on the way,
// whether the request came back at all, and whether MO2 is still answering
// afterwards. An entry that wedges the host fails here rather than in use.
internal static class Mo2ModMenuRunCheck
{
    // What each entry needs before MO2 will do it, and what the frontend supplies.
    // An entry with no answer here must not raise a question at all.
    private static Mo2LiveProfile.Mo2MenuAnswer? AnswerFor(string caption, Mo2LiveProfile profile) => caption switch {
        "Priority..." => new("int", "5"),
        "Separator..." => new("choice", FirstSeparator(profile) ?? ""),
        _ => null,
    };

    private static string? FirstSeparator(Mo2LiveProfile profile) =>
        profile.Mods.Where(x => x.IsSeparator).OrderBy(x => x.Priority)
            .Select(x => x.Name.EndsWith("_separator", StringComparison.Ordinal) ? x.Name[..^10] : x.Name)
            .FirstOrDefault();

    // Entries that change or destroy something the profile would not get back, or
    // that reach the network. They are checked as far as MO2 building them, which is
    // what the caption audit already does; pressing them here would be vandalism.
    private static readonly (string Caption, string Why)[] NotPressed = [
        ("Remove Mod...", "deletes the mod"),
        ("Remove Separator...", "deletes the separator"),
        ("Remove Backup...", "deletes the backup"),
        ("Restore Backup", "replaces a mod with an older copy"),
        ("Reinstall Mod", "re-runs the installer over the mod"),
        ("Clear Overwrite...", "empties the overwrite folder"),
        ("Sync to Mods...", "moves the overwrite's files into mods"),
        ("Create Mod...", "makes a mod out of the overwrite"),
        ("Move content to Mod...", "moves the overwrite's files into another mod"),
        ("Install mod above... ", "opens a file picker for an archive to install"),
        ("Endorse", "endorses on the account, which is not this machine's to spend"),
        ("Un-Endorse", "the same in reverse"),
        ("Won't endorse", "records a choice against the account"),
        ("Start tracking", "changes what the account follows"),
        ("Stop tracking", "the same in reverse"),
        ("Check for updates", "queries Nexus for every mod in the profile"),
        ("Force-check updates", "queries Nexus"),
        ("Visit on Nexus", "opens a browser"),
        ("Visit the uploader's profile", "opens a browser"),
        ("Open in Explorer", "opens a file manager window on this desktop"),
        ("Export to csv...", "opens a file picker for where to write"),
    ];

    public static async Task Run(string endpoint, string? wanted = null)
    {
        var profile = new Mo2LiveProfile(endpoint);
        await profile.Refresh();
        if (!profile.IsConnected) throw new Exception("Not connected to MO2: " + profile.Status);
        var target = profile.CurrentTarget;

        var mod = wanted is { Length: > 0 }
            ? profile.Mods.FirstOrDefault(x => x.Name == wanted || x.DisplayName == wanted)
            : profile.Mods.FirstOrDefault(x => x.IsRegular && x.NexusId > 0) ?? profile.Mods.FirstOrDefault(x => x.IsRegular);
        if (mod is null) throw new Exception("This profile has no ordinary mod to open a menu on");
        Console.WriteLine($"  pressing on \"{mod.DisplayName}\"");

        var instance = Mo2InstanceSettings.InstanceOf(profile.ProfilePath);
        var entries = Mo2ModMenuCaptions.Captions(mod, Mo2InstanceSettings.Nexus(instance),
            Mo2InstanceSettings.OverwriteHasContent(instance));

        // The profile is put back exactly as it was. Pressing these is not a dry run —
        // a priority really moves, a mod really switches off — and a check that leaves
        // a curated load order rearranged has broken the thing it was checking.
        var order = Path.Combine(Mo2InstanceCatalog.LocalPath(profile.ProfilePath), "modlist.txt");
        var before = File.Exists(order) ? File.ReadAllText(order) : null;
        var modsBefore = Directory.Exists(Path.Combine(instance, "mods"))
            ? Directory.GetDirectories(Path.Combine(instance, "mods")).Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

        var pressed = 0;
        var worked = new List<string>();
        var skipped = new List<string>();
        var faults = new List<string>();
        try {
        foreach (var caption in entries) {
            if (NotPressed.FirstOrDefault(x => x.Caption == caption) is { Caption: not null } excused) {
                skipped.Add($"{caption} ({excused.Why})"); continue;
            }
            // Drawn here rather than triggered in MO2, so there is no MO2 action to
            // press; the list checks cover them.
            if (caption is "All Mods" or "Send to... " or "Collapse all" or "Collapse others" or "Expand all"
                or "Information..." or "Select Color..." or "Reset Color" or "Rename Mod..." or "Rename Separator...") {
                skipped.Add($"{caption} (drawn by the frontend)"); continue;
            }

            // Where MO2 keeps this entry: inside All Mods, inside Send to..., or at
            // the top of the menu.
            string[][] path =
                Mo2ModMenuCaptions.AllModsChildren.Contains(caption) ? [["All Mods", caption]] :
                Mo2ModMenuCaptions.SendToChildren(mod).Contains(caption) ? [["Send to... ", caption]] :
                [[caption]];

            // What the entry should have changed, read off the mod before it is run.
            var was = profile.Mods.FirstOrDefault(x => x.Name == mod.Name);
            var clock = Stopwatch.StartNew();
            try {
                await profile.RunModMenu([mod.Name], path, target, AnswerFor(caption, profile));
            } catch (Exception error) { faults.Add($"{caption}: {error.Message}"); }
            if (clock.Elapsed > TimeSpan.FromSeconds(45)) faults.Add($"{caption} took {clock.Elapsed.TotalSeconds:0}s");

            // MO2 still answering is the thing that matters: a wedged host answers
            // nothing after the entry that wedged it.
            var alive = await Answering(profile);
            if (!alive) { faults.Add($"{caption} left MO2 unable to answer"); break; }
            pressed++;

            // And then whether it did anything. Not hanging is not the same as working,
            // and an entry that quietly does nothing is the failure this is looking for.
            var now = profile.Mods.FirstOrDefault(x => x.Name == mod.Name);
            var effect = Effect(caption, was, now, profile, instance, mod);
            if (effect is { Length: > 0 }) worked.Add($"{caption}: {effect}");
            else if (Observable.Contains(caption)) faults.Add($"{caption} changed nothing");
            Console.WriteLine($"    {caption}: {(effect is { Length: > 0 } ? effect : "ran")} ({clock.Elapsed.TotalSeconds:0.0}s)");
        }
        } finally {
            // Put back what was moved, whatever happened above.
            if (before is not null) {
                try {
                    await Task.Delay(500);
                    File.WriteAllText(order, before);
                    // And then told to read it. MO2 holds the profile in memory and
                    // writes it back out later, so a file put right underneath a
                    // running MO2 is one it has not seen and will overwrite — the
                    // first run of this looked restored and was not.
                    await profile.RunModMenu([mod.Name], [["All Mods", "Refresh"]], target);
                    await profile.Refresh();
                } catch (Exception) { }
            }
            foreach (var folder in Directory.Exists(Path.Combine(instance, "mods"))
                         ? Directory.GetDirectories(Path.Combine(instance, "mods")) : []) {
                if (modsBefore.Contains(Path.GetFileName(folder))) continue;
                try { Directory.Delete(folder, recursive: true); Console.WriteLine($"  removed {Path.GetFileName(folder)}, which this check created"); }
                catch (IOException) { }
            }
        }

        Console.WriteLine($"  pressed {pressed}, of which {worked.Count} changed something, not pressed {skipped.Count}");
        foreach (var skip in skipped) Console.WriteLine($"    skipped {skip}");
        if (faults.Count != 0) throw new Exception("Entries that did not behave:\n    " + string.Join("\n    ", faults));
        Console.WriteLine($"PASS mod menu run: {pressed} entries pressed against a live MO2, none left it unable to answer");
    }

    // The entries whose effect can be seen from here. One of these changing nothing
    // is a failure; the rest are run to prove they do not wedge the host, which is
    // all that can be said about an action whose work is invisible from outside.
    private static readonly string[] Observable = [
        "Priority...", "Separator...", "Lowest priority", "Highest priority",
        "Enable selected", "Disable selected", "Create Backup",
    ];

    private static string? Effect(string caption, Mo2LiveMod? was, Mo2LiveMod? now,
        Mo2LiveProfile profile, string instance, Mo2LiveMod mod)
    {
        if (was is null || now is null) return null;
        bool Enabled(Mo2LiveMod m) => (m.State & 6) != 0;
        switch (caption) {
            case "Priority..." or "Lowest priority" or "Highest priority":
                return was.Priority != now.Priority ? $"priority {was.Priority} to {now.Priority}" : null;
            case "Separator...": {
                // Which separator the mod now sits under, which is what sending it to
                // one means.
                string? Under(Mo2LiveMod target) => profile.Mods.OrderBy(x => x.Priority)
                    .TakeWhile(x => x.Id != target.Id).LastOrDefault(x => x.IsSeparator)?.DisplayName;
                var from = Under(was); var to = Under(now);
                return from != to || was.Priority != now.Priority ? $"now under {to ?? "no separator"}" : null;
            }
            // Judged by where the mod ended up rather than by whether it moved: these
            // two set a state rather than toggle one, and a mod that was already
            // enabled is still enabled afterwards because the entry worked.
            case "Enable selected":
                return Enabled(now) ? "enabled" : null;
            case "Disable selected":
                return Enabled(now) ? null : "disabled";
            case "Create Backup": {
                var mods = Path.Combine(instance, "mods");
                var made = Directory.Exists(mods)
                    ? Directory.GetDirectories(mods).Select(Path.GetFileName)
                        .Where(x => x is not null && x.StartsWith(mod.Name + "_backup", StringComparison.OrdinalIgnoreCase)).ToArray()
                    : [];
                return made.Length > 0 ? $"wrote {made[0]}" : null;
            }
            default: return null;
        }
    }

    private static async Task<bool> Answering(Mo2LiveProfile profile)
    {
        try {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await profile.Refresh();
            return profile.IsConnected;
        } catch (Exception) { return false; }
    }
}
