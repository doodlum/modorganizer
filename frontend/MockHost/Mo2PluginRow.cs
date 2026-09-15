using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal static class Mo2PluginRow
{
    // MO2's own plugin list columns, in the order its model declares them
    // (src/pluginlist.h, EColumn) and under the headers it gives them
    // (pluginlist.cpp). The enable box sits in the name column as it does in MO2,
    // and the row's actions are on its right-click menu rather than in a column.
    internal const int Name = 0, Flags = 1, Priority = 2, ModIndex = 3,
        FormVersion = 4, HeaderVersion = 5, Author = 6, Description = 7;

    internal static Grid Columns() => new() { ColumnDefinitions = new ColumnDefinitions(
        "*,30,56,72,86,96,104,*") };

    internal static readonly (int Column, string Name)[] Headers = [
        (Name, "Name"), (Flags, "Flags"), (Priority, "Priority"), (ModIndex, "Mod Index"),
        (FormVersion, "Form Version"), (HeaderVersion, "Header Version"),
        (Author, "Author"), (Description, "Description")];

    internal static readonly HashSet<int> HiddenColumns = [];
    internal static readonly (int Column, string Name)[] OptionalColumns =
        Headers.Where(x => x.Column != Name).ToArray();

    private static readonly (int Column, double Width, double Threshold)[] Optional = [
        (Flags, 30, 300), (Priority, 56, 380), (ModIndex, 72, 460),
        (FormVersion, 86, 900), (HeaderVersion, 96, 1060), (Author, 104, 700),
        (Description, 200, 1240)];

    internal static void Fit(Grid row, double width) =>
        Mo2TableRow.Fit(row, width, Optional, HiddenColumns.Contains);
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
        // Name column: enable box, the lock MO2 marks a fixed load order with, then the
        // plugin's name. Its actions are on the row's right-click menu, as MO2 has them.
        var toggle = Mo2ModRow.Activation("PluginActivationToggle", plugin.DisplayName, plugin.IsActive, plugin.CanToggle && profile.CanChangeOriginalUi,
            async () => { await Change(profile.Order.FindPlugin(plugin.Key)?.IsActive != true); return profile.Order.FindPlugin(plugin.Key)?.IsActive == true; },
            () => target == profile.CurrentTarget && profile.CanChangeOriginalUi && profile.Order.FindPlugin(plugin.Key)?.CanToggle == true);
        var name = Mo2TableRow.Cell(plugin.DisplayName, plugin.IsActive ? .85 : .5);
        name.Name = "PluginName";
        ToolTip.SetTip(name, plugin.DisplayName + "\nMod: " + plugin.ModName + "\n" + plugin.Diagnostics);
        var title = new DockPanel();
        var padlock = new UnifiedIcon { Name = "PluginLockIcon", Value = new ProjektankerIcon("mdi-lock-outline"), Size = 14, Margin = new Thickness(0,0,4,0), VerticalAlignment = VerticalAlignment.Center, IsVisible = plugin.IsLocked };
        ToolTip.SetTip(padlock, "Load order locked by MO2"); DockPanel.SetDock(padlock, Dock.Left); title.Children.Add(padlock);
        title.Children.Add(name);
        var nameCell = new Grid { ColumnDefinitions = new ColumnDefinitions($"{Mo2TableRow.StatusColumn},*") };
        Mo2TableRow.Add(nameCell, toggle, 0); Mo2TableRow.Add(nameCell, title, 1);
        Mo2TableRow.Add(row, nameCell, Name);
        row.ContextFlyout = Mo2EntryMenu.Flyout(Actions);

        // MO2's Flags column is a glyph summarising what its tooltip spells out; a
        // plugin MO2 has something to warn about is the one that shows a mark.
        var flags = new UnifiedIcon { Name = "PluginFlagIcon",
            Value = new ProjektankerIcon(plugin.HasWarning ? "mdi-alert-circle-outline" : "mdi-flag"), Size = 14,
            Opacity = plugin.HasWarning || plugin.PluginFlags.Length > 0 ? .85 : 0,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var flagDetail = plugin.PluginFlags.Length > 0 ? plugin.PluginFlags : plugin.Diagnostics;
        if (flagDetail.Length > 0) ToolTip.SetTip(flags, flagDetail);
        Mo2TableRow.Add(row, flags, Flags);

        var priority = Mo2TableRow.Cell(plugin.PriorityText, .6);
        var modIndex = Mo2TableRow.Cell(plugin.ModIndex, .6);
        var formVersion = Mo2TableRow.Cell(plugin.FormVersion, .6);
        var headerVersion = Mo2TableRow.Cell(plugin.HeaderVersion, .6);
        var author = Mo2TableRow.Cell(plugin.Author, .6);
        var description = Mo2TableRow.Cell(plugin.Description, .6);
        if (plugin.Description.Length > 0) ToolTip.SetTip(description, plugin.Description);
        Mo2TableRow.Add(row, priority, Priority); Mo2TableRow.Add(row, modIndex, ModIndex);
        Mo2TableRow.Add(row, formVersion, FormVersion); Mo2TableRow.Add(row, headerVersion, HeaderVersion);
        Mo2TableRow.Add(row, author, Author); Mo2TableRow.Add(row, description, Description);
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
