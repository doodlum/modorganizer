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
        // The same list the audit reads, so the menu drawn and the menu compared are
        // one list. Collapse all and Expand all are in it and handled below.
        foreach (var caption in Mo2ModMenuCaptions.AllModsChildren) {
            if (caption is "Collapse all" or "Collapse others" or "Expand all") continue;
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
                // MO2 offers this beside the other two: everything folded except the one
                // the menu was opened on, which is how a long list is read one section at
                // a time. Folded here rather than in MO2 for the same reason the other
                // two are — the collapsed state belongs to this list, not to MO2's window.
                all.Items.Add(Entry("Collapse others", () => { adapter.CollapseOthers(current()); return Task.CompletedTask; }));
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
        // MO2 asks for the priority and for the separator in windows of its own, which
        // a hosted MO2 cannot show — picking Separator... used to hang it outright.
        // The question is asked here instead and the answer handed to MO2's action.
        send.Items.Add(Entry("Priority...", () => run(() => SendToPriority(profile, adapter, mod, target)), mod.CanManage));
        send.Items.Add(Entry("Separator...", () => run(() => SendToSeparator(profile, adapter, mod, target)), mod.CanManage));
        foreach (var caption in Mo2ModMenuCaptions.SendToChildren(mod).Where(x => x is "First conflict" or "Last conflict"))
            send.Items.Add(Entry(caption, () => run(() => profile.RunModMenu([mod.Name], [["Send to... ", caption]], target)), mod.CanManage));
        return send;
    }

    // Built by walking the one statement of what this mod's menu holds, so what is
    // drawn and what the audit against MO2 reads can never be two different lists.
    internal static object[] Build(Mo2LiveProfile profile, Mo2ModsAdapter adapter, Func<Mo2LiveMod> current,
        Mo2ProfileTarget target, Func<Func<Task>, Task> run, Func<Task> rename)
    {
        var mod = current();
        // MO2's own action, by the wording MO2 gives it.
        MenuItem Mo2(string caption, bool enabled = true) =>
            Entry(caption, () => run(() => profile.RunModMenu([current().Name], [[caption]], target)), enabled);

        // The instance decides three of these as much as the mod does — whether it has
        // Nexus endorsement, tracking and category mapping turned on — and whether its
        // overwrite folder holds anything.
        var instance = Mo2InstanceSettings.InstanceOf(profile.ProfilePath);
        var items = Mo2ModMenuCaptions.For(mod, Mo2InstanceSettings.Nexus(instance),
            Mo2InstanceSettings.OverwriteHasContent(instance)).Select(entry => (object?)(entry.Kind switch {
            Mo2ModMenuCaptions.Kind.Divider => new Separator(),
            Mo2ModMenuCaptions.Kind.AllMods => AllMods(profile, adapter, current, target, run),
            Mo2ModMenuCaptions.Kind.SendTo => SendTo(profile, adapter, current, target, run),
            Mo2ModMenuCaptions.Kind.Colour => Colour(profile, current, target, run),
            Mo2ModMenuCaptions.Kind.Rename => Entry(entry.Caption, rename, entry.Enabled),
            Mo2ModMenuCaptions.Kind.Remove => Entry(entry.Caption, () => run(() => Remove(profile, current())), entry.Enabled),
            Mo2ModMenuCaptions.Kind.Explorer => Entry(entry.Caption, () => run(() => profile.OpenModFolder(current().Name))),
            Mo2ModMenuCaptions.Kind.RestoreBackup => Entry(entry.Caption, () => run(() => RestoreBackup(profile, current(), target))),
            Mo2ModMenuCaptions.Kind.Information => Entry(entry.Caption,
                () => run(() => profile.ShowModInformation is { } show ? show(current().Id) : profile.ShowModDetails(current().Id))),
            _ => Mo2(entry.Caption, entry.Enabled),
        })).ToArray();
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

    // MO2's Send to... → Priority, which asks for a number. Asked here, in a window
    // that can actually be seen, and MO2's own action is then given the answer.
    private static async Task SendToPriority(Mo2LiveProfile profile, Mo2ModsAdapter adapter, Mo2LiveMod mod, Mo2ProfileTarget target)
    {
        if (adapter.AskPriority is not { } ask) return;
        if (await ask(mod) is not { } priority) return;
        await profile.RunModMenu([mod.Name], [["Send to... ", "Priority..."]], target,
            new Mo2LiveProfile.Mo2MenuAnswer("int", priority.ToString()));
    }

    // The same for Send to... → Separator, which MO2 asks with a list of the
    // separators in the profile. MO2 lists them without the "_separator" its folders
    // carry, so the answer is spelled the way MO2 spells its own choices.
    private static async Task SendToSeparator(Mo2LiveProfile profile, Mo2ModsAdapter adapter, Mo2LiveMod mod, Mo2ProfileTarget target)
    {
        if (adapter.AskSeparator is not { } ask) return;
        var separators = profile.Mods.Where(x => x.IsSeparator).OrderBy(x => x.Priority)
            .Select(x => x.Name.EndsWith("_separator", StringComparison.Ordinal) ? x.Name[..^10] : x.Name)
            .ToArray();
        if (separators.Length == 0) return;
        if (await ask(separators) is not { } chosen) return;
        await profile.RunModMenu([mod.Name], [["Send to... ", "Separator..."]], target,
            new Mo2LiveProfile.Mo2MenuAnswer("choice", chosen));
    }

    // MO2 names a backup after the mod it was taken from, with _backup and an
    // optional number after it.
    internal static string? BackedUp(string name)
    {
        var match = System.Text.RegularExpressions.Regex.Match(name, @"^(.*)_backup[0-9]*$");
        return match.Success && match.Groups[1].Value.Length > 0 ? match.Groups[1].Value : null;
    }

    // Putting a backup back.
    //
    // MO2 does this itself and asks first — but only when the mod being restored over
    // is still there, and that question is a modal a hosted MO2 can never show. So the
    // question is asked here, and what it agrees to is done here too: the mod in the
    // way is removed through this application's own removal, which leaves MO2 nothing
    // to ask about, and MO2's own Restore Backup then does the actual work.
    private static async Task RestoreBackup(Mo2LiveProfile profile, Mo2LiveMod backup, Mo2ProfileTarget target)
    {
        if (BackedUp(backup.Name) is { } original &&
            profile.Mods.FirstOrDefault(x => x.Name == original) is { } inTheWay) {
            if (profile.ConfirmRemoval is not { } confirm) return;
            if (!await confirm([inTheWay])) return;
            profile.Remove([NexusMods.Abstractions.Loadouts.LoadoutItemId.From(inTheWay.Id)]);
            // MO2 has to have finished removing it before it is asked to restore over
            // it, or it will find the mod still there and put its question up.
            for (var attempt = 0; attempt < 40 && profile.Mods.Any(x => x.Name == original); attempt++)
                await Task.Delay(250);
            if (profile.Mods.Any(x => x.Name == original)) return;
        }
        await profile.RunModMenu([backup.Name], [["Restore Backup"]], target);
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
