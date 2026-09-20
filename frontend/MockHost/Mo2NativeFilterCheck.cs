namespace Mo2.Frontend;

internal static class Mo2NativeFilterCheck
{
    internal static async Task Run(Mo2LiveWorkspace live)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!live.Profile.CanChangeOriginalUi && DateTime.UtcNow < deadline) await Task.Delay(100);
        var profile = live.Profile;
        var target = profile.CurrentTarget;
        var criteria = profile.CategoryTree.SelectMany(x => x.Walk()).Where(x => x.Type is 0 or 2).ToArray();
        if (criteria.Length == 0) throw new Exception("Native special/content filter tree is absent");
        var names = profile.Mods.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var matches = await profile.ReadModFilterMatches(target, criteria);
        if (matches.Count != criteria.Length || matches.Values.SelectMany(x => x).Any(x => !names.Contains(x)))
            throw new Exception("Native filter matches do not belong to the current mod list");
        var stale = target with { ProfilePath = target.ProfilePath + "-stale" };
        try { await profile.ReadModFilterMatches(stale, criteria); throw new Exception("Stale filter target accepted"); }
        catch (InvalidOperationException) { }
        Console.WriteLine($"PASS native filter adapter: {matches.Count} criteria read through the frontend, exact criterion identities, known mod names and stale target rejection");
    }
}
