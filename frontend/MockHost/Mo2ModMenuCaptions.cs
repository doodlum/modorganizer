namespace Mo2.Frontend;

// What the mod menu holds, in order, for a given mod.
//
// Kept apart from the menu that draws it so there is one statement of which entries
// a mod gets, which the menu builds from and the audit against MO2 reads. The two
// used to be the same code, which meant the only way to know what the menu would
// hold for a backup or an overwrite was to draw one.
//
// The order and the conditions are MO2's (src/modlistcontextmenu.cpp). MO2 branches
// first on what kind of thing the row is — overwrite, backup, separator, foreign, or
// an ordinary mod — and only an ordinary mod gets the long list.
internal static class Mo2ModMenuCaptions
{
    // How an entry is carried out. Most are MO2's own action, found by the wording
    // MO2 gave it and triggered where MO2 built it; the rest have a route of their
    // own here, either because this frontend does it differently on purpose or
    // because MO2's version opens a window a hosted MO2 could never show.
    internal enum Kind { Mo2, Divider, AllMods, SendTo, Colour, Rename, Remove, Explorer, Information, RestoreBackup }

    internal sealed record Entry(string Caption, Kind Kind = Kind.Mo2, bool Enabled = true);

    internal static IReadOnlyList<Entry> For(Mo2LiveMod mod, Mo2NexusIntegration? integration = null, bool overwriteHasContent = true)
    {
        var nexus = mod.NexusId > 0;
        var on = integration ?? Mo2NexusIntegration.Default;
        var entries = new List<Entry> {
            new("All Mods", Kind.AllMods),
            new("", Kind.Divider),
        };
        void Add(string caption, bool when = true, Kind kind = Kind.Mo2, bool enabled = true)
        { if (when) entries.Add(new Entry(caption, kind, enabled)); }

        // MO2's overwrite folder gets four actions and nothing else, and only while
        // there is something in it: an empty overwrite has nothing to sync out, move
        // or clear, so MO2 builds none of them and leaves it Open in Explorer alone.
        var overwrite = mod.IsOverwrite && overwriteHasContent;
        Add("Sync to Mods...", overwrite);
        Add("Create Mod...", overwrite);
        Add("Move content to Mod...", overwrite);
        Add("Clear Overwrite...", overwrite);

        // A separator, renamed and removed under its own wording.
        Add("Rename Separator...", mod.IsSeparator, Kind.Rename, mod.CanManage);
        Add("Remove Separator...", mod.IsSeparator, Kind.Remove, mod.CanManage);

        // A backup, which MO2 offers to put back or throw away and nothing else. It is
        // not the mod it was taken from and has none of that mod's actions.
        // Both of MO2's backup actions ask before they act, in a window a hosted MO2
        // cannot show — the same modal that used to hang the whole host. Restoring is
        // asked here instead, and MO2 only asks when the mod being restored over still
        // exists, so clearing it first leaves MO2 nothing to ask about. Removing goes
        // through this application's own removal, which is what Remove Mod does.
        Add("Restore Backup", mod.IsBackup, Kind.RestoreBackup);
        Add("Remove Backup...", mod.IsBackup, Kind.Remove);

        Add("", !mod.IsSeparator && !mod.IsOverwrite && !mod.IsBackup, Kind.Divider);

        // The update actions, for a mod MO2 knows on Nexus.
        Add("Change versioning scheme", mod.IsRegular && mod.HasUpdate);
        Add("Force-check updates", mod.IsRegular && nexus);
        Add("Un-ignore update", mod.IsRegular && nexus && mod.IgnoredVersion.Length > 0);
        Add("Ignore update", mod.IsRegular && nexus && mod.IgnoredVersion.Length == 0 && mod.HasUpdate);

        Add("", mod.IsRegular, Kind.Divider);
        Add("Enable selected", mod.IsRegular, Kind.Mo2, mod.CanManage);
        Add("Disable selected", mod.IsRegular, Kind.Mo2, mod.CanManage);
        Add("", true, Kind.Divider);

        // MO2 shows Send to... while its list is in priority order, which is the only
        // order this list is in. A backup keeps its place in that order too.
        Add("Send to... ", !mod.IsOverwrite, Kind.SendTo);
        Add("", !mod.IsOverwrite, Kind.Divider);

        Add("Rename Mod...", mod.IsRegular, Kind.Rename, mod.CanManage);
        Add("Reinstall Mod", mod.IsRegular, Kind.Mo2, mod.CanManage);
        Add("Remove Mod...", mod.IsRegular, Kind.Remove, mod.CanManage);
        Add("Create Backup", mod.IsRegular, Kind.Mo2, mod.CanManage);
        Add("Restore hidden files", mod.IsRegular && mod.HasHiddenFiles, Kind.Mo2, mod.CanManage);
        Add("", true, Kind.Divider);

        // MO2's colour actions paint a separator's row; an ordinary mod's colour is on
        // its Notes cell, which is where MO2 offers it.
        Add("Select Color...", mod.IsSeparator, Kind.Colour);
        Add("", true, Kind.Divider);

        // Endorsement and tracking, as MO2 reports them for this mod.
        // Each of these three is also switched off for the whole instance in MO2's
        // settings, and an instance can have them off — Viva New Vegas ships with
        // category mappings off — so the instance has to agree before the entry is
        // offered, or it is an entry MO2 never built and clicking it does nothing.
        var endorsing = mod.IsRegular && nexus && on.Endorsement;
        Add("Un-Endorse", endorsing && mod.Endorsed == "ENDORSED_TRUE");
        Add("Endorse", endorsing && mod.Endorsed is "ENDORSED_FALSE" or "ENDORSED_NEVER");
        Add("Won't endorse", endorsing && mod.Endorsed == "ENDORSED_FALSE");
        Add("Endorsement state unknown", endorsing && mod.Endorsed == "ENDORSED_UNKNOWN", Kind.Mo2, enabled: false);
        Add("Remap Category (From Nexus)", mod.IsRegular && nexus && on.CategoryMappings);
        Add("Start tracking", mod.IsRegular && nexus && on.Tracked && mod.Tracked == "TRACKED_FALSE");
        Add("Stop tracking", mod.IsRegular && nexus && on.Tracked && mod.Tracked == "TRACKED_TRUE");
        // MO2 states the answer it has not got back from Nexus yet rather than leaving
        // the row silent about it, and builds the entry disabled. Carried the same way,
        // as the endorsement one already is.
        Add("Tracked state unknown", mod.IsRegular && nexus && on.Tracked && mod.Tracked == "TRACKED_UNKNOWN", Kind.Mo2, enabled: false);
        Add("", true, Kind.Divider);

        // The two MO2 offers for a mod it could not read, or one installed for another
        // game. A backup gets these as well, which is how MO2 has it.
        Add("Ignore missing data", (mod.IsRegular || mod.IsBackup) && mod.NoValidData);
        Add("Mark as converted/working", (mod.IsRegular || mod.IsBackup) && mod.ForAnotherGame);
        Add("", true, Kind.Divider);

        // The links, which a backup carries too when MO2 knows it on Nexus.
        var linked = mod.IsRegular || mod.IsBackup;
        Add("Visit on Nexus", linked && nexus);
        Add("Visit the uploader's profile", linked && mod.Uploader.Length > 0);
        if (linked && Mo2ModMenu.Host(mod.Url) is { } host) Add("Visit on " + host);

        // Opened on this desktop rather than inside MO2's prefix, which is the one
        // difference from MO2 here and why it does not go through it.
        Add("Open in Explorer", true, Kind.Explorer);
        Add("", true, Kind.Divider);

        // MO2 makes this its default action, which is what a double-click opens here.
        Add("Information...", !mod.IsForeign, Kind.Information);

        return entries;
    }

    // MO2's All Mods submenu: what to install or create beside this row, then the
    // actions over the whole list. The two collapse entries sit where MO2 puts them,
    // straight after the three create actions.
    internal static readonly string[] AllModsChildren = [
        "Install mod above... ", "Create empty mod above", "Create separator above",
        "Collapse all", "Collapse others", "Expand all",
        "Enable all", "Disable all", "Check for updates", "Auto assign categories",
        "Refresh", "Export to csv...",
    ];

    // MO2's Send to..., which moves a mod the length of the list without dragging it.
    // The last two are only there for a mod that has conflicts, as MO2 has them.
    internal static string[] SendToChildren(Mo2LiveMod mod) => [
        "Lowest priority", "Highest priority", "Priority...", "Separator...",
        .. mod.Overwrites ? new[] { "First conflict" } : [],
        .. mod.Overwritten ? new[] { "Last conflict" } : [],
    ];

    // The captions alone, without the dividers, with each submenu followed by what it
    // holds — which is what the audit compares against MO2, since MO2 reports its own
    // submenus the same way.
    internal static string[] Captions(Mo2LiveMod mod, Mo2NexusIntegration? integration = null, bool overwriteHasContent = true) =>
        For(mod, integration, overwriteHasContent).Where(x => x.Kind != Kind.Divider).SelectMany(entry => entry.Kind switch {
            Kind.AllMods => new[] { entry.Caption }.Concat(AllModsChildren),
            Kind.SendTo => new[] { entry.Caption }.Concat(SendToChildren(mod)),
            // The colour picker holds MO2's two colour entries as its choices: a colour
            // to paint with, and MO2's own Reset Color to take it off again.
            Kind.Colour => new[] { entry.Caption, "Reset Color" },
            _ => [entry.Caption],
        }).ToArray();
}
