using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal static class Mo2PluginRow
{
    internal static Grid Columns() => new() { ColumnDefinitions = new ColumnDefinitions(
        $"{Mo2TableRow.GripColumn},{Mo2TableRow.StatusColumn},*,70,150,{Mo2TableRow.ActionsColumn}") };
    internal static readonly HashSet<int> HiddenColumns = [];
    internal static readonly (int Column, string Name)[] OptionalColumns = [(3, "Type"), (4, "Masters")];

    internal static void Fit(Grid row, double width) =>
        Mo2TableRow.Fit(row, width, [(3, 70, 610), (4, 150, 800)], HiddenColumns.Contains);
    internal static Control Create(Mo2LiveProfile profile, ScenarioPlugin plugin)
    {
        Mo2UiLatencyProbe.Count("Plugin rows created");
        var target = profile.CurrentTarget;
        Task Run(Func<Task> action) => target == profile.CurrentTarget && profile.CanChangeOriginalUi ? action() : Task.CompletedTask;
        async Task Change(bool active) => await Run(() => profile.SetPluginsActive([plugin.DisplayName], active));
        var actionSets = new List<MenuItem[]>();
        MenuItem[] Actions() {
            var ready = target == profile.CurrentTarget && profile.CanChangeOriginalUi;
            if (target == profile.CurrentTarget && profile.Order.FindPlugin(plugin.Key) is { } latest) plugin = latest;
            MenuItem[] items = [
            Mo2EntryMenu.Action("Enable", () => Change(true), ready && plugin.CanToggle),
            Mo2EntryMenu.Action("Disable", () => Change(false), ready && plugin.CanToggle),
            Mo2EntryMenu.Action("Move earlier", () => Run(() => profile.Order.MoveItemDelta(default, plugin.Key, -1)), ready && plugin.CanMove),
            Mo2EntryMenu.Action("Move later", () => Run(() => profile.Order.MoveItemDelta(default, plugin.Key, 1)), ready && plugin.CanMove),
            Mo2EntryMenu.Action(plugin.IsLocked ? "Unlock load order" : "Lock load order",
                () => Run(() => profile.SetPluginLocked(plugin.DisplayName, !plugin.IsLocked, target)), ready && plugin.IsActive && (plugin.IsLocked || plugin.CanMove))
            ];
            actionSets.Add(items); return items;
        }
        var row = Columns(); row.Name = "PluginRedesignRow";
        var grip = Mo2EntryMenu.Create("Plugin", plugin.DisplayName, Actions); grip.Width = Mo2TableRow.GripWidth; Mo2TableRow.Add(row, grip, 0);
        var toggle = Mo2ModRow.Activation("PluginActivationToggle", plugin.DisplayName, plugin.IsActive, plugin.CanToggle && profile.CanChangeOriginalUi,
            async () => { await Change(profile.Order.FindPlugin(plugin.Key)?.IsActive != true); return profile.Order.FindPlugin(plugin.Key)?.IsActive == true; },
            () => target == profile.CurrentTarget && profile.CanChangeOriginalUi && profile.Order.FindPlugin(plugin.Key)?.CanToggle == true);
        Mo2TableRow.Add(row, toggle, 1);
        var name = Mo2TableRow.Cell(plugin.DisplayName, plugin.IsActive ? .85 : .5);
        name.Name = "PluginName";
        ToolTip.SetTip(name, plugin.DisplayName + "\nMod: " + plugin.ModName + "\n" + plugin.Diagnostics);
        var title = new DockPanel();
        var padlock = new UnifiedIcon { Name = "PluginLockIcon", Value = new ProjektankerIcon("mdi-lock-outline"), Size = 14, Margin = new Thickness(0,0,4,0), VerticalAlignment = VerticalAlignment.Center, IsVisible = plugin.IsLocked };
        ToolTip.SetTip(padlock, "Load order locked by MO2"); DockPanel.SetDock(padlock, Dock.Left); title.Children.Add(padlock);
        title.Children.Add(name); Mo2TableRow.Add(row, title, 2);
        Mo2TableRow.Add(row, Mo2TableRow.Cell(Path.GetExtension(plugin.DisplayName).TrimStart('.').ToUpperInvariant(), .6), 3);
        var masters = Mo2TableRow.Cell(string.Join(", ", plugin.Masters), .6);
        ToolTip.SetTip(masters, masters.Text); Mo2TableRow.Add(row, masters, 4);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var info = Mo2ModRow.IconButton(plugin.HasWarning ? "mdi-alert-circle-outline" : "mdi-information-outline", "Plugin details", () => {
            if (target != profile.CurrentTarget) return;
            var table = row.GetVisualAncestors().OfType<TreeDataGrid>().FirstOrDefault();
            if (table?.RowSelection is not { } selection || table.Rows is not { } rows) return;
            var index = Enumerable.Range(0, rows.Count).FirstOrDefault(i => rows[i].Model is NexusMods.App.UI.Controls.CompositeItemModel<NexusMods.Abstractions.Games.ISortItemKey> m && m.Key.Equals(plugin.Key), -1);
            if (index >= 0) { selection.Clear(); selection.Select(rows.RowIndexToModelIndex(index)); }
            if (row.GetVisualAncestors().OfType<Mo2PluginsView>().FirstOrDefault()?.ViewModel is { } page)
                page.ShowPluginDetails = true;
        });
        actions.Children.Add(info);
        var menu = Mo2EntryMenu.Flyout(Actions);
        var more = Mo2ModRow.IconButton("mdi-menu-down", "Plugin actions", () => { }); more.Flyout = menu; actions.Children.Add(more);
        Mo2TableRow.Add(row, actions, 5);
        void Refresh() {
            var ready = target == profile.CurrentTarget && profile.CanChangeOriginalUi;
            if (target == profile.CurrentTarget && profile.Order.FindPlugin(plugin.Key) is { } latest) plugin = latest;
            padlock.IsVisible = plugin.IsLocked;
            name.Opacity = plugin.IsActive ? .85 : .5;
            ToolTip.SetTip(name, plugin.DisplayName + "\nMod: " + plugin.ModName + "\n" + plugin.Diagnostics);
            toggle.IsChecked = plugin.IsActive;
            toggle.IsEnabled = ready && plugin.CanToggle;
            foreach (var items in actionSets) {
                items[0].IsEnabled = items[1].IsEnabled = ready && plugin.CanToggle;
                items[2].IsEnabled = items[3].IsEnabled = ready && plugin.CanMove;
                items[4].Header = plugin.IsLocked ? "Unlock load order" : "Lock load order";
                items[4].IsEnabled = ready && plugin.IsActive && (plugin.IsLocked || plugin.CanMove);
            }
        }
        row.AttachedToVisualTree += (_,_) => { profile.Changed += Refresh; Refresh(); };
        row.DetachedFromVisualTree += (_,_) => profile.Changed -= Refresh;
        row.LayoutUpdated += (_, _) => Fit(row, row.GetVisualAncestors().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().FirstOrDefault()?.Bounds.Width ?? row.Bounds.Width);
        return row;
    }
}
