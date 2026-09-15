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
        async Task Wait(Func<bool> ready, string phase = "filtered rows") {
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!ready()) { if (DateTime.UtcNow > until) throw new Exception(phase + " did not settle"); await Task.Delay(25); }
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
        bool Ready(TreeDataGrid table, bool isPlugin)
        {
            var rows = table.GetVisualDescendants().OfType<TreeDataGridRow>()
                .Where(r => r.RowIndex >= 0 && r.RowIndex < table.Rows!.Count).ToArray();
            if (rows.Length == 0) return false;
            foreach (var row in rows) {
                var model = table.Rows![row.RowIndex].Model;
                var name = model switch {
                    CompositeItemModel<ISortItemKey> plugin when isPlugin => profile.Order.FindPlugin(plugin.Key)?.DisplayName,
                    CompositeItemModel<EntityId> mod when !isPlugin => profile.FindMod(mod.Key)?.Name,
                    _ => null
                };
                if (name is null) return false;
                var grip = row.GetVisualDescendants().OfType<Border>().SingleOrDefault(b => b.Name == (isPlugin ? "PluginDragHandle" : "ModDragHandle"));
                if (grip is null || !Equals(grip.Tag, name)) return false;
                var toggle = row.GetVisualDescendants().OfType<ToggleButton>().FirstOrDefault();
                if (toggle is not null && !Equals(toggle.Tag, name)) return false;
            }
            return true;
        }
        try {
            // Each pause crosses the real search control's 100ms debounce, but
            // does not wait for row construction before issuing the next query.
            foreach (var query in Enumerable.Range(0, 4).SelectMany(_ => new[] { "is:enabled", "___missing_row_check___", "" })) {
                modBox.Text = pluginBox.Text = query;
                await Task.Delay(120);
            }
            modBox.Text = pluginBox.Text = "___missing_row_check___";
            await Wait(() => modTable.Rows!.Count == 0 && pluginTable.Rows!.Count == 0);
            modBox.Text = pluginBox.Text = "";
            await Wait(() => Ready(modTable, false) && Ready(pluginTable, true));
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
            await Wait(() => Ready(modTable, false) && Ready(pluginTable, true));
            await Mo2HighlightCheck.Run(shell, modTable, pluginTable, rails);
            if (target != profile.CurrentTarget || !modState.SequenceEqual(profile.Mods.Select(m => (m.Name, m.Priority, Active: m.State & 6))) ||
                !pluginState.SequenceEqual(profile.Order.Plugins.Select(p => (p.DisplayName, p.SortIndex, p.IsActive))))
                throw new Exception("Filtering/scrolling changed native mod or plugin state");
            Console.WriteLine("PASS rapid debounced filtering, empty/clear, scrolling both lists: row handles/toggles match native models; native states unchanged");
        } finally { modBox.Text = modText; pluginBox.Text = pluginText; window.Height = originalHeight; window.WindowState = originalState; }
    }
}
