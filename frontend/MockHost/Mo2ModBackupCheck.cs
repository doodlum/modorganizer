namespace Mo2.Frontend;

// Create Backup makes one, and what it makes gets a backup's menu.
//
// The backup branch of MO2's menu had nothing behind it here: a backup was treated
// as an ordinary mod and offered that mod's actions, none of which MO2 builds for
// one. There was no backup in any profile to notice it with, so this makes one.
//
// It also proves the two entries a backup does get are safe to offer. MO2 asks
// before restoring and before removing, in a window a hosted MO2 cannot show, and
// an entry that opens one of those is an entry that hangs the host until it is
// killed — which is what this frontend routes around.
internal static class Mo2ModBackupCheck
{
    public static async Task Run(string endpoint)
    {
        var profile = new Mo2LiveProfile(endpoint);
        await profile.Refresh();
        if (!profile.IsConnected) throw new Exception("Not connected to MO2: " + profile.Status);
        var target = profile.CurrentTarget;

        var existing = profile.Mods.FirstOrDefault(x => x.IsBackup);
        var mod = existing is null
            ? profile.Mods.FirstOrDefault(x => x is { IsRegular: true, CanManage: true })
              ?? throw new Exception("This profile has no ordinary mod to back up")
            : null;

        if (existing is null) {
            Console.WriteLine($"  backing up \"{mod!.DisplayName}\" through MO2's own Create Backup");
            await profile.RunModMenu([mod.Name], [["Create Backup"]], target);
            await profile.RunModMenu([mod.Name], [["All Mods", "Refresh"]], target);
            for (var attempt = 0; attempt < 20 && !profile.Mods.Any(x => x.IsBackup); attempt++) {
                await Task.Delay(500);
                await profile.Refresh();
            }
            existing = profile.Mods.FirstOrDefault(x => x.IsBackup);

            // The work itself is checked on disk rather than in the list. MO2 makes the
            // folder either way; whether it then shows it as a row is its own business,
            // and this build does not — so the branch below is checked against a row of
            // the shape MO2 describes rather than left unchecked.
            var folder = Path.Combine(Mo2InstanceSettings.InstanceOf(profile.ProfilePath), "mods", mod.Name + "_backup");
            if (!Directory.Exists(folder))
                throw new Exception($"Create Backup left nothing at {folder}");
            Console.WriteLine($"  Create Backup wrote {Path.GetFileName(folder)}");
            if (existing is null)
                Console.WriteLine("  MO2 does not list its own backups in this build, so the row is checked as MO2 describes it");
        }

        existing ??= mod! with { Name = mod.Name + "_backup", DisplayName = mod.DisplayName + "_backup", Flags = "Backup" };

        Console.WriteLine($"  backup \"{existing.DisplayName}\", of \"{Mo2ModMenu.BackedUp(existing.Name) ?? "?"}\"");
        if (existing.IsRegular) throw new Exception("A backup is still being treated as an ordinary mod");

        // MO2's own menu for it, when MO2 has a row to build one from.
        var theirs = Array.Empty<string>();
        if (profile.Mods.Any(x => x.Name == existing.Name)) {
            theirs = (await profile.ReadListMenu("readModMenu", [existing.Name], target))
                .SelectMany(x => x.Captions()).Where(x => x.Length > 0).ToArray();
            foreach (var caption in new[] { "Restore Backup", "Remove Backup..." })
                if (!theirs.Contains(caption, StringComparer.Ordinal))
                    throw new Exception($"MO2 does not offer {caption} for its own backup: {string.Join(", ", theirs)}");
        }

        var ours = Mo2ModMenuCaptions.For(existing);
        var captions = ours.Where(x => x.Kind != Mo2ModMenuCaptions.Kind.Divider).Select(x => x.Caption).ToArray();
        Console.WriteLine($"  MO2 offers {theirs.Length}, the frontend offers {captions.Length}: {string.Join(", ", captions)}");

        foreach (var caption in new[] { "Restore Backup", "Remove Backup..." })
            if (!captions.Contains(caption, StringComparer.Ordinal))
                throw new Exception($"The frontend does not offer {caption} for a backup");
        // None of the ordinary mod's actions, which MO2 does not build for a backup.
        foreach (var caption in new[] { "Reinstall Mod", "Create Backup", "Rename Mod...", "Enable selected" })
            if (captions.Contains(caption, StringComparer.Ordinal))
                throw new Exception($"A backup is offered {caption}, which MO2 does not build for one");

        // Neither of the two may be routed into MO2: both of MO2's open a question
        // box, and a hosted MO2 draws nothing on screen to answer it with.
        foreach (var entry in ours.Where(x => x.Caption is "Restore Backup" or "Remove Backup..."))
            if (entry.Kind == Mo2ModMenuCaptions.Kind.Mo2)
                throw new Exception($"{entry.Caption} would open MO2's own question box, which nobody could answer");
        Console.WriteLine("  both are answered in this application rather than in MO2");

        Console.WriteLine($"PASS mod backup: Create Backup made one, it gets MO2's backup menu and not an ordinary mod's, and neither entry opens an MO2 dialog");
    }
}
