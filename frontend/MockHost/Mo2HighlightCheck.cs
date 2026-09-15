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
                if (!seenPlugins.SetEquals(linked) || !seenConflicts.SetEquals(winning.Concat(losing)))
                    throw new Exception("Not every native linked/conflict row was observed");
                mods.RowSelection.Clear();
                await Scan(plugins, rails[1], true, clear: true);
                await Scan(mods, rails[0], false, clear: true);
                Console.WriteLine($"PASS native selection highlights: {linked.Count} linked plugins, {winning.Count} red and {losing.Count} green conflict rows; cleared across recycled rows");
            } finally {
                mods.RowSelection?.Clear();
                foreach (var rail in rails) rail.Value = 0;
            }
            return winning.Concat(losing).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        if (target != profile.CurrentTarget) throw new Exception("Profile changed during highlight check");
    }
}
