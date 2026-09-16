using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using NexusMods.Abstractions.Games;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Search;
using NexusMods.MnemonicDB.Abstractions;

namespace Mo2.Frontend;

internal static class Mo2FilteredRowsCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT")))
            throw new Exception("Use an isolated paired layout for the row check");
        // The detail is asked for when the wait runs out, not when it starts: what
        // the lists look like after twenty seconds is the part worth reporting, and
        // building the sentence up front described the state before the wait.
        async Task Wait(Func<bool> ready, string phase = "filtered rows", Func<string?>? detail = null) {
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!ready()) {
                if (DateTime.UtcNow > until)
                    throw new Exception(phase + " did not settle" + (detail?.Invoke() is { } why ? " — " + why : ""));
                await Task.Delay(25);
            }
        }
        await Wait(() => shell.Profile.IsConnected && !shell.Profile.SelectingProfile &&
            window.GetVisualDescendants().OfType<Mo2ModsView>().Any(v => v.IsEffectivelyVisible && v.GetVisualDescendants().OfType<TreeDataGrid>().Any(t => t.Rows?.Count > 0)) &&
            window.GetVisualDescendants().OfType<Mo2PluginsView>().Any(v => v.IsEffectivelyVisible && v.GetVisualDescendants().OfType<TreeDataGrid>().Any(t => t.Rows?.Count > 0)), "initial native tables");
        var profile = shell.Profile;
        var target = profile.CurrentTarget;
        var mods = window.GetVisualDescendants().OfType<Mo2ModsView>().Single(v => v.IsEffectivelyVisible);
        var plugins = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single(v => v.IsEffectivelyVisible);
        var modTable = mods.NativeView.FindControl<TreeDataGrid>("TreeDataGrid")!;
        var pluginTable = plugins.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        var modBox = mods.NativeView.FindControl<SearchControl>("SearchControl")!.FindControl<TextBox>("SearchTextBox")!;
        var pluginBox = plugins.GetVisualDescendants().OfType<SearchControl>().Single().FindControl<TextBox>("SearchTextBox")!;
        var modText = modBox.Text; var pluginText = pluginBox.Text;
        var originalHeight = window.Height;
        var originalState = window.WindowState;
        var modState = profile.Mods.Select(m => (m.Name, m.Priority, Active: m.State & 6)).ToArray();
        var pluginState = profile.Order.Plugins.Select(p => (p.DisplayName, p.SortIndex, p.IsActive)).ToArray();
        // Why a list is not ready, or null when it is. This answered yes or no, and
        // a run that waited twenty seconds for it could say only that something had
        // not settled — not which list, which row, or which of the four things a row
        // has to agree about.
        string? NotReady(TreeDataGrid table, bool isPlugin)
        {
            var which = isPlugin ? "the plugin list" : "the mod list";
            var rows = table.GetVisualDescendants().OfType<TreeDataGridRow>()
                .Where(r => r.RowIndex >= 0 && r.RowIndex < table.Rows!.Count).ToArray();
            if (rows.Length == 0) return $"{which} has none of its {table.Rows?.Count} row(s) realised";
            foreach (var row in rows) {
                var model = table.Rows![row.RowIndex].Model;
                var name = model switch {
                    CompositeItemModel<ISortItemKey> plugin when isPlugin => profile.Order.FindPlugin(plugin.Key)?.DisplayName,
                    CompositeItemModel<EntityId> mod when !isPlugin => profile.FindMod(mod.Key)?.Name,
                    _ => null
                };
                if (name is null) return $"{which} row {row.RowIndex} carries a {model?.GetType().Name ?? "null"} MO2 does not know";
                // What a row carries now. This used to insist on a drag handle in
                // every row, which is the shape the lists had before MO2's own menu
                // went onto the row's ContextFlyout: since that change only a
                // separator keeps a grip, so the check had been failing on the first
                // row of every list — a feature the frontend deliberately no longer
                // has, not a row that disagrees with MO2.
                //
                // The menu is what has to be on the row, and it is what carries
                // MO2's own entries. A grip where there is still one has to name the
                // same entry as the row it is in.
                // The menu on the row itself, not any menu anywhere under it: asking
                // whether some descendant had one passed with the row's own menu
                // deleted, because a mod's Notes cell carries a colour menu of its
                // own and answered for it.
                //
                // A separator is the exception: its menu goes on the TreeDataGridRow
                // itself, which is where its grip puts it.
                var separator = !isPlugin && row.GetVisualDescendants().OfType<Control>().Any(x => x.Name == "ModSeparatorBar");
                var entry = separator ? row : row.GetVisualDescendants().OfType<Control>()
                    .FirstOrDefault(x => x.Name == (isPlugin ? "PluginRedesignRow" : "ModRedesignRow"));
                if (entry is null) return $"{which} row {row.RowIndex} (\"{name}\") drew no entry of its own";
                if (entry.ContextFlyout is null && entry.ContextMenu is null)
                    return $"{which} row {row.RowIndex} (\"{name}\") carries no menu";
                var grip = row.GetVisualDescendants().OfType<Border>()
                    .FirstOrDefault(b => b.Name == (isPlugin ? "PluginDragHandle" : "ModDragHandle"));
                if (grip is not null && !Equals(grip.Tag, name))
                    return $"{which} row {row.RowIndex} draws \"{name}\" over a handle for \"{grip.Tag}\"";
                var toggle = row.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault();
                if (toggle is not null && !Equals(toggle.Tag, name))
                    return $"{which} row {row.RowIndex} draws \"{name}\" over a switch for \"{toggle.Tag}\"";
            }
            return null;
        }
        bool Ready(TreeDataGrid table, bool isPlugin) => NotReady(table, isPlugin) is null;
        try {
            // Each pause crosses the real search control's 100ms debounce, but
            // does not wait for row construction before issuing the next query.
            foreach (var query in Enumerable.Range(0, 4).SelectMany(_ => new[] { "is:enabled", "___missing_row_check___", "" })) {
                modBox.Text = pluginBox.Text = query;
                await Task.Delay(120);
            }
            modBox.Text = pluginBox.Text = "___missing_row_check___";
            // Each wait says which one it is. All three used to report the same
            // "filtered rows did not settle", so a failing run said that the check
            // had failed and nothing about where — emptying the lists, filling them
            // again, or the scroll at the end.
            await Wait(() => modTable.Rows!.Count == 0 && pluginTable.Rows!.Count == 0,
                $"both lists emptied by a query nothing matches (mods {modTable.Rows?.Count}, plugins {pluginTable.Rows?.Count})");
            modBox.Text = pluginBox.Text = "";
            await Wait(() => Ready(modTable, false) && Ready(pluginTable, true),
                "both lists rebuilt against MO2's models when the query is cleared",
                () => NotReady(modTable, false) ?? NotReady(pluginTable, true));
            window.WindowState = WindowState.Normal;
            await Task.Delay(200);
            window.Height = 520;
            await Wait(() => Math.Abs(window.ClientSize.Height - 520) <= 1, "actual 520px viewport");
            var rails = new[] {
                mods.GetVisualDescendants().OfType<ScrollBar>().Single(s => s.Name == "ModsRailScrollBar"),
                plugins.GetVisualDescendants().OfType<ScrollBar>().Single(s => s.Name == "PluginRailScrollBar") };
            await Wait(() => rails.All(rail => rail.Maximum > 0), "scrollable lists");
            foreach (var bottom in new[] { true, false, true, false }) {
                foreach (var rail in rails)
                    rail.Value = bottom ? rail.Maximum : 0;
                await Task.Delay(120);
            }
            await Wait(() => Ready(modTable, false) && Ready(pluginTable, true),
                "both lists still matching MO2's models after scrolling to each end twice",
                () => NotReady(modTable, false) ?? NotReady(pluginTable, true));
            await Mo2HighlightCheck.Run(shell, modTable, pluginTable, rails);
            if (target != profile.CurrentTarget || !modState.SequenceEqual(profile.Mods.Select(m => (m.Name, m.Priority, Active: m.State & 6))) ||
                !pluginState.SequenceEqual(profile.Order.Plugins.Select(p => (p.DisplayName, p.SortIndex, p.IsActive))))
                throw new Exception("Filtering/scrolling changed native mod or plugin state");
            Console.WriteLine("PASS rapid debounced filtering, empty/clear, scrolling both lists: row handles/toggles match native models; native states unchanged");
        } finally { modBox.Text = modText; pluginBox.Text = pluginText; window.Height = originalHeight; window.WindowState = originalState; }
    }
}
