namespace Mo2.Frontend;

// The window polls MO2 every two seconds to notice changes made in MO2 itself.
// Every one of those polls used to be a full snapshot, which MO2 builds on its own
// UI thread. This checks that the poll now asks only when something it watches has
// actually moved, and that it still asks regularly regardless so nothing it does
// not watch can leave the frontend stale.
internal static class Mo2RefreshCadenceCheck
{
    internal static async Task Run(Mo2LiveProfile profile)
    {
        if (!profile.IsConnected || profile.ProfilePath.Length == 0)
            throw new Exception("The refresh cadence check needs a connected profile");
        await profile.Refresh();
        var start = profile.SnapshotReads;

        // Nothing has changed, so the next few polls should cost MO2 nothing.
        for (var tick = 0; tick < 5; tick++) await profile.RefreshIfChanged();
        var quiet = profile.SnapshotReads - start;
        if (quiet != 0) throw new Exception($"{quiet} snapshots were read while nothing had changed");

        // A change in the profile folder is what MO2's own edits look like from here.
        var directory = Mo2InstanceCatalog.LocalPath(profile.ProfilePath);
        var stamps = profile.WatchedStamps();
        Directory.SetLastWriteTimeUtc(directory, DateTime.UtcNow.AddSeconds(1));
        if (profile.WatchedStamps() == stamps) throw new Exception("Touching the profile folder did not change what the poll watches");
        // Retried rather than sampled once: a read is skipped outright while another
        // command is in flight, and under a full suite run there often is one.
        for (var attempt = 0; attempt < 30 && profile.SnapshotReads == start; attempt++) {
            await profile.RefreshIfChanged();
            if (profile.SnapshotReads == start) await Task.Delay(100);
        }
        if (profile.SnapshotReads != start + 1) throw new Exception(
            $"A changed profile folder produced {profile.SnapshotReads - start} reads, not one");

        // And a full read still happens on its own after enough quiet polls.
        var afterChange = profile.SnapshotReads;
        for (var tick = 0; tick < 12; tick++) await profile.RefreshIfChanged();
        var eventual = profile.SnapshotReads - afterChange;
        if (eventual is < 1 or > 2) throw new Exception($"{eventual} unprompted reads over 12 quiet polls");
        Console.WriteLine($"PASS refresh cadence: five quiet polls read nothing from MO2, a changed profile folder read once, " +
            $"and {eventual} full read(s) still happened over 12 quiet polls");
    }
}
