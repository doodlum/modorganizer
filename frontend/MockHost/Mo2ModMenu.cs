using Avalonia.Controls;

namespace Mo2.Frontend;

// MO2's own mod menu (src/modlistcontextmenu.cpp), which is where its mod list is
// actually worked from — the buttons above the list are a handful of the same
// actions, and everything else a mod can be done to is here.
//
// The shape is MO2's: the same entries, in the same order, under the same wording,
// appearing under the same conditions. Those conditions are read off the mod as MO2
// reports it, so a mod MO2 does not know on Nexus has no Nexus entries here either.
//
// The work is MO2's too. A few entries have a route of their own that predates this
// and is checked elsewhere — enabling, moving, renaming, removing, colouring and the
// details window — and the rest go through `RunModMenu`, which finds MO2's own action
// by the wording MO2 gave it and triggers it where MO2 built it. Nothing in this file
// decides what an action does.
internal static class Mo2ModMenu
{
    // The entry's own condition, kept on the item so a later refresh can re-enable
    // the ones the mod still supports without rebuilding the menu.
    private static MenuItem Entry(string caption, Func<Task> action, bool enabled = true)
    {
        var item = Mo2EntryMenu.Action(caption, action, enabled);
        item.Tag = enabled;
        return item;
    }

    // MO2's "All Mods" submenu, which is what its menu opens with: what to install or
    // create beside this row, and the actions over the whole list.
    private static MenuItem AllMods(Mo2LiveProfile profile, Mo2ModsAdapter adapter, Func<Mo2LiveMod> current,
        Mo2ProfileTarget target, Func<Func<Task>, Task> run)
    {
        var all = new MenuItem { Header = "All Mods" };
        foreach (var caption in new[] { "Install mod above... ", "Create empty mod above", "Create separator above",
                                        "Enable all", "Disable all", "Check for updates", "Auto assign categories",
                                        "Refresh", "Export to csv..." }) {
            var wanted = caption;
            // MO2 names the first three after where the new mod lands, which depends on
            // the row the menu was opened on and the order the list is sorted in, so
            // both spellings are offered to MO2 and the one it built is taken.
            string[][] paths = wanted switch {
                "Install mod above... " => [["All Mods", "Install mod above... "], ["All Mods", "Install mod inside... "], ["All Mods", "Install mod below... "], ["All Mods", "Install mod..."]],
                "Create empty mod above" => [["All Mods", "Create empty mod above"], ["All Mods", "Create empty mod inside"], ["All Mods", "Create empty mod below"], ["All Mods", "Create empty mod"]],
                "Create separator above" => [["All Mods", "Create separator above"], ["All Mods", "Create separator"]],
                // MO2 renames these two while a filter is on, because they then act on
                // what the filter left.
                "Enable all" => [["All Mods", "Enable all"], ["All Mods", "Enable all matching mods"]],
                "Disable all" => [["All Mods", "Disable all"], ["All Mods", "Disable all matching mods"]],
                _ => [["All Mods", wanted]],
            };
            all.Items.Add(Entry(wanted, () => run(() => profile.RunModMenu([current().Name], paths, target))));
            // MO2 puts these two straight after the three create actions, since its
            // list has separators that collapse. They fold this list's own rows rather
            // than going through MO2, whose collapsed state is its window's, not the
            // profile's.
            if (wanted == "Create separator above") {
                all.Items.Add(Entry("Collapse all", () => { adapter.SetAllSeparators(true); return Task.CompletedTask; }));
                all.Items.Add(Entry("Expand all", () => { adapter.SetAllSeparators(false); return Task.CompletedTask; }));
            }
        }
        all.Tag = true;
        return all;
    }

    // MO2's Send to..., which is how a mod is moved the length of the list without
    // dragging it there. Its two conflict entries are only there for a mod that has
    // conflicts, which is what MO2 does with them.
    private static MenuItem SendTo(Mo2LiveProfile profile, Mo2ModsAdapter adapter, Func<Mo2LiveMod> current,
        Mo2ProfileTarget target, Func<Func<Task>, Task> run)
    {
        var mod = current();
        var send = new MenuItem { Header = "Send to... ", Tag = true };
        // The two ends have a route of their own, which is the one the list's own drag
        // and the check for it already use.
        send.Items.Add(Entry("Lowest priority", () => run(() => adapter.Move(mod.Id, 0, absolute: true)), mod.CanManage));
        send.Items.Add(Entry("Highest priority", () => run(() => adapter.Move(mod.Id, int.MaxValue, absolute: true)), mod.CanManage));
        foreach (var caption in new[] { "Priority...", "Separator..." })
            send.Items.Add(Entry(caption, () => run(() => profile.RunModMenu([mod.Name], [["Send to... ", caption]], target)), mod.CanManage));
        if (mod.Overwrites)
            send.Items.Add(Entry("First conflict", () => run(() => profile.RunModMenu([mod.Name], [["Send to... ", "First conflict"]], target)), mod.CanManage));
        if (mod.Overwritten)
            send.Items.Add(Entry("Last conflict", () => run(() => profile.RunModMenu([mod.Name], [["Send to... ", "Last conflict"]], target)), mod.CanManage));
        return send;
    }

    internal static object[] Build(Mo2LiveProfile profile, Mo2ModsAdapter adapter, Func<Mo2LiveMod> current,
        Mo2ProfileTarget target, Func<Func<Task>, Task> run, Func<Task> rename)
    {
        var mod = current();
        // MO2's own action, by the wording MO2 gives it.
        MenuItem Mo2(string caption, bool enabled = true) =>
            Entry(caption, () => run(() => profile.RunModMenu([current().Name], [[caption]], target)), enabled);
        var nexus = mod.NexusId > 0;
        object?[] items = [
            AllMods(profile, adapter, current, target, run),
            new Separator(),
            // MO2 offers its overwrite folder four actions of its own and nothing else.
            mod.IsOverwrite ? Mo2("Sync to Mods...") : null,
            mod.IsOverwrite ? Mo2("Create Mod...") : null,
            mod.IsOverwrite ? Mo2("Move content to Mod...") : null,
            mod.IsOverwrite ? Mo2("Clear Overwrite...") : null,
            // A separator is renamed and removed under its own wording.
            mod.IsSeparator ? Entry("Rename Separator...", rename, mod.CanManage) : null,
            mod.IsSeparator ? Entry("Remove Separator...", () => run(() => Remove(profile, current())), mod.CanManage) : null,
            mod.IsSeparator || mod.IsOverwrite ? null : new Separator(),
            // The update actions, which MO2 only offers for a mod it knows on Nexus.
            mod.IsRegular && mod.HasUpdate ? Mo2("Change versioning scheme") : null,
            mod.IsRegular && nexus ? Mo2("Force-check updates") : null,
            mod.IsRegular && nexus && mod.IgnoredVersion.Length > 0 ? Mo2("Un-ignore update") : null,
            mod.IsRegular && nexus && mod.IgnoredVersion.Length == 0 && mod.HasUpdate ? Mo2("Ignore update") : null,
            mod.IsRegular ? new Separator() : null,
            mod.IsRegular ? Mo2("Enable selected", mod.CanManage) : null,
            mod.IsRegular ? Mo2("Disable selected", mod.CanManage) : null,
            new Separator(),
            // MO2 shows Send to... while its list is in priority order, which is the
            // only order this frontend's list is in.
            mod.IsOverwrite ? null : SendTo(profile, adapter, current, target, run),
            mod.IsOverwrite ? null : new Separator(),
            mod.IsRegular ? Entry("Rename Mod...", rename, mod.CanManage) : null,
            mod.IsRegular ? Mo2("Reinstall Mod", mod.CanManage) : null,
            mod.IsRegular ? Entry("Remove Mod...", () => run(() => Remove(profile, current())), mod.CanManage) : null,
            mod.IsRegular ? Mo2("Create Backup", mod.CanManage) : null,
            mod.IsRegular && mod.HasHiddenFiles ? Mo2("Restore hidden files", mod.CanManage) : null,
            new Separator(),
            // MO2's colour actions, which paint a separator's row. An ordinary mod's
            // colour is on its Notes cell's own menu, which is where MO2 offers it.
            // MO2's colour dialog cannot be drawn from here, so the colours it starts
            // from are offered and MO2's own action writes whichever is picked.
            mod.IsSeparator ? Colour(profile, current, target, run) : null,
            new Separator(),
            // Endorsement and tracking, as MO2 reports them for this mod.
            mod.IsRegular && nexus && mod.Endorsed == "ENDORSED_TRUE" ? Mo2("Un-Endorse") : null,
            mod.IsRegular && nexus && mod.Endorsed is "ENDORSED_FALSE" or "ENDORSED_NEVER" ? Mo2("Endorse") : null,
            mod.IsRegular && nexus && mod.Endorsed == "ENDORSED_FALSE" ? Mo2("Won't endorse") : null,
            mod.IsRegular && nexus && mod.Endorsed == "ENDORSED_UNKNOWN" ? Entry("Endorsement state unknown", () => Task.CompletedTask, false) : null,
            mod.IsRegular && nexus ? Mo2("Remap Category (From Nexus)") : null,
            mod.IsRegular && nexus && mod.Tracked == "TRACKED_FALSE" ? Mo2("Start tracking") : null,
            mod.IsRegular && nexus && mod.Tracked == "TRACKED_TRUE" ? Mo2("Stop tracking") : null,
            new Separator(),
            // The two MO2 offers for a mod it could not read, or one installed for
            // another game.
            mod.IsRegular && mod.NoValidData ? Mo2("Ignore missing data") : null,
            mod.IsRegular && mod.ForAnotherGame ? Mo2("Mark as converted/working") : null,
            new Separator(),
            mod.IsRegular && nexus ? Mo2("Visit on Nexus") : null,
            mod.IsRegular && mod.Uploader.Length > 0 ? Mo2("Visit the uploader's profile") : null,
            mod.IsRegular && Host(mod.Url) is { } host ? Mo2("Visit on " + host) : null,
            // Opened on this desktop rather than in MO2's prefix, which is the one
            // difference from MO2 here and is why this one does not go through it.
            Entry("Open in Explorer", () => run(() => profile.OpenModFolder(current().Name))),
            new Separator(),
            // MO2 makes this the default action of the menu, which is also what this
            // frontend's rows open on a double-click.
            mod.IsForeign ? null : Entry("Information...", () => run(() => profile.ShowModDetails(current().Id))),
        ];
        return Mo2RowMenu.Tidy(items).ToArray();
    }

    private static MenuItem Colour(Mo2LiveProfile profile, Func<Mo2LiveMod> current, Mo2ProfileTarget target, Func<Func<Task>, Task> run)
    {
        var mod = current();
        var item = Mo2EntryMenu.ColorMenu("Select Color...", mod.Color.Length > 0 || mod.NotesColor.Length > 0,
            color => run(() => profile.SetModColor(current().Name, color, target)));
        item.Tag = true;
        return item;
    }

    private static Task Remove(Mo2LiveProfile profile, Mo2LiveMod mod)
    {
        profile.Remove([NexusMods.Abstractions.Loadouts.LoadoutItemId.From(mod.Id)]);
        return Task.CompletedTask;
    }

    // The site a mod's own URL points at, which is how MO2 names that entry.
    internal static string? Host(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Host.Length > 0 ? parsed.Host : null;
}
