namespace Mo2.Frontend;

// The flags a mod's row shows.
//
// MO2 gathers these as one tooltip on its Flags column and this frontend draws a
// glyph whenever that text is not empty. One of MO2's flags is "Not endorsed yet",
// which is true of nearly every mod nearly all of the time — on a Wabbajack modlist
// of a hundred and fifty it flagged almost every row, which is the same as flagging
// none of them. A flag is for something worth noticing about a row, and not having
// got round to endorsing something is not that.
//
// Dropped here rather than at each place that draws it, so the glyph, the row
// tooltip and the mod's own details all agree on what this mod's flags are.
internal static class Mo2ModFlags
{
    // MO2's own wording, from its mod list (ModInfo::FLAG_NOTENDORSED).
    private static readonly string[] Hidden = ["Not endorsed yet"];

    internal static string Shown(string flags)
    {
        if (flags.Length == 0) return flags;
        var kept = flags.Split('\n')
            .Where(line => !Hidden.Contains(line.Trim(), StringComparer.OrdinalIgnoreCase))
            .ToArray();
        return kept.Length == 0 ? "" : string.Join('\n', kept);
    }
}
