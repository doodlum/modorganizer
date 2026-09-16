using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.Abstractions.Games;
using NexusMods.App.UI.Controls;
using NexusMods.MnemonicDB.Abstractions;

namespace Mo2.Frontend;

internal static class Mo2HighlightCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, TreeDataGrid mods, TreeDataGrid plugins, ScrollBar[] rails)
    {
        var profile = shell.Profile;
        var target = profile.CurrentTarget;
        var candidate = profile.Mods.Where(m => !m.IsSeparator && !m.IsOverwrite &&
            profile.Order.Plugins.Any(p => string.Equals(p.ModName, m.Name, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(m => m.Conflicts.Length > 0).ThenByDescending(m => m.CanManage).FirstOrDefault()
            ?? throw new Exception("No native mod with linked plugins available for highlight check");
        var neighbors = await Check(candidate, requireLinked: true);
        var other = profile.Mods.FirstOrDefault(m => neighbors.Contains(m.Name));
        if (other is not null) await Check(other, requireLinked: false);
        async Task<HashSet<string>> Check(Mo2LiveMod candidate, bool requireLinked)
        {
            var response = await new Mo2BridgeClient(target.Endpoint).SendAsync("selectionLinks", new() {
                ["profilePath"] = target.ProfilePath, ["names"] = new[] { candidate.Name }
            });
            HashSet<string> Read(string key) => response.GetProperty(key).EnumerateArray()
                .Select(v => v.GetString()!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var linked = Read("plugins"); var winning = Read("winningMods"); var losing = Read("losingMods");
            if (requireLinked && linked.Count == 0) throw new Exception("Native readback has no linked plugins");
            var index = Enumerable.Range(0, mods.Rows!.Count).Single(i => mods.Rows[i].Model is CompositeItemModel<EntityId> m && m.Key == candidate.Id);
            try {
                mods.RowSelection!.Clear(); mods.RowSelection.Select(mods.Rows.RowIndexToModelIndex(index));
                var until = DateTime.UtcNow.AddSeconds(15);
                while (!profile.LinkedPlugins.SetEquals(linked) || !profile.WinningMods.SetEquals(winning) || !profile.LosingMods.SetEquals(losing)) {
                    if (DateTime.UtcNow > until) throw new Exception("Selected mod links did not match native MO2 readback");
                    await Task.Delay(50);
                }
                var seenPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var seenConflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                async Task Scan(TreeDataGrid table, ScrollBar rail, bool isPlugin, bool clear = false) {
                    for (double offset = 0; ; offset += Math.Max(40, rail.ViewportSize * .75)) {
                        rail.Value = Math.Min(offset, rail.Maximum);
                        await Task.Delay(120);
                        foreach (var row in table.GetVisualDescendants().OfType<TreeDataGridRow>().Where(r => r.RowIndex >= 0 && r.RowIndex < table.Rows!.Count)) {
                            var model = table.Rows![row.RowIndex].Model;
                            var name = isPlugin ? profile.Order.FindPlugin(((CompositeItemModel<ISortItemKey>)model!).Key)?.DisplayName
                                : profile.FindMod(((CompositeItemModel<EntityId>)model!).Key)?.Name;
                            if (name is null) throw new Exception("Highlight row lost its native identity");
                            string? expected = clear ? null : isPlugin ? linked.Contains(name) ? "#66514385" : null
                                : winning.Contains(name) ? "#66893D43" : losing.Contains(name) ? "#664F7D43" : null;
                            if (expected is not null) {
                                if (row.Background is not ISolidColorBrush brush || brush.Color != Color.Parse(expected))
                                    throw new Exception("Native linked/conflict row has the wrong highlight");
                                (isPlugin ? seenPlugins : seenConflicts).Add(name);
                            } else if (clear && row.Background is ISolidColorBrush brush &&
                                new[] { "#66514385", "#66893D43", "#664F7D43" }.Any(c => brush.Color == Color.Parse(c)))
                                throw new Exception("Cleared selection left a stale row highlight");
                        }
                        if (rail.Value >= rail.Maximum - 1) break;
                    }
                }
                await Scan(plugins, rails[1], true);
                await Scan(mods, rails[0], false);
                // Which rows, not merely that some were missed: scrolling both lists
                // end to end and reporting a set mismatch without naming it leaves a
                // failing run with nothing to act on.
                // Only the conflicts that are rows in the list. MO2 answers with its
                // origins, and one of them is "data" — the game's own Data folder,
                // which MO2 does not draw in its mod list either. Demanding a row for
                // it failed a run that had agreed with MO2 about every row there was.
                // What is left out is named in the pass line rather than dropped
                // silently.
                var origins = winning.Concat(losing).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var listed = profile.Mods.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var conflicts = origins.Where(listed.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var unlisted = origins.Except(conflicts, StringComparer.OrdinalIgnoreCase).ToArray();
                if (!seenPlugins.SetEquals(linked) || !seenConflicts.SetEquals(conflicts))
                    throw new Exception("Not every native linked/conflict row was observed — " + string.Join("; ", new[] {
                        Missing("plugin", linked, seenPlugins), Missing("conflict", conflicts, seenConflicts),
                    }.Where(x => x.Length > 0)));
                mods.RowSelection.Clear();
                await Scan(plugins, rails[1], true, clear: true);
                await Scan(mods, rails[0], false, clear: true);
                Console.WriteLine($"PASS native selection highlights: {linked.Count} linked plugins, {winning.Count} red and {losing.Count} green conflict rows; cleared across recycled rows" +
                    (unlisted.Length > 0 ? $"; MO2 also named {string.Join(", ", unlisted)}, which it draws no row for and neither does this list" : ""));
            } finally {
                mods.RowSelection?.Clear();
                foreach (var rail in rails) rail.Value = 0;
            }
            return winning.Concat(losing).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        if (target != profile.CurrentTarget) throw new Exception("Profile changed during highlight check");

        // What MO2 named that no row was seen for, and what was seen that MO2 did
        // not name — both directions, because either is a disagreement.
        static string Missing(string kind, HashSet<string> wanted, HashSet<string> seen)
        {
            var unseen = wanted.Except(seen, StringComparer.OrdinalIgnoreCase).ToArray();
            var extra = seen.Except(wanted, StringComparer.OrdinalIgnoreCase).ToArray();
            var parts = new List<string>();
            if (unseen.Length > 0) parts.Add($"MO2 names {unseen.Length} {kind} row(s) no row was seen for: {string.Join(", ", unseen)}");
            if (extra.Length > 0) parts.Add($"{extra.Length} {kind} row(s) were highlighted that MO2 did not name: {string.Join(", ", extra)}");
            return string.Join("; ", parts);
        }
    }
}
