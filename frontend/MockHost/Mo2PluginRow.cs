using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal static class Mo2PluginRow
{
    // Display metadata that helps identify a plugin. Its position in the list
    // conveys priority; numeric load-order details belong in the row tooltip and
    // diagnostics, where they remain available without crowding the name.
    //
    // MO2 has an Author column here as well, and shows it. This list does not draw
    // one: a plugin's author is a field almost nothing fills in, so the column was a
    // heading over empty cells, taking width from the name and the description it
    // sat between.
    // MO2 has four more of these — Author, Form Version, Header Version and
    // Description — and shows them. None is drawn: a plugin's author and description
    // are fields almost nothing fills in, and its form and header versions are the
    // file format's own numbers, which matter when something is wrong with a plugin
    // rather than while reading a load order. All four are on the row's tooltip, so
    // what is gone is the width they took from the name, not the information.
    internal const int Name = 0, Flags = 1, ModIndex = 2;

    internal static Grid Columns() => new() { ColumnDefinitions = new ColumnDefinitions(
        "*,24,56") };

    internal static readonly (int Column, string Name)[] Headers = [
        // MO2 spells this one out as "Mod Index". It is headed with the hash a number
        // is written after, which is what the column holds and what the heading has
        // room for — the words took more width than the two characters under them.
        (Name, "Name"), (Flags, "Flags"), (ModIndex, "#")];

    // What the hash stands for, for the heading's tooltip and for anything reading
    // the list out.
    internal const string ModIndexName = "Mod Index";

    internal static readonly HashSet<int> HiddenColumns = [];
    internal static readonly (int Column, string Name)[] OptionalColumns =
        Headers.Where(x => x.Column != Name).ToArray();

    private static readonly (int Column, double Width, double Threshold)[] Optional = [
        (Flags, 24, 250),
        // Kept early, and so surviving a narrow panel: the index is what the game
        // itself calls this plugin, and it is what a form id in a crash log or
        // another mod's notes has to be matched against.
        (ModIndex, 56, 380)];

    internal static void Fit(Grid row, double width) =>
        Mo2TableRow.Fit(row, width, Optional, HiddenColumns.Contains,
            chrome: Mo2TableRow.RailWidth + Mo2TableRow.GripWidth + Mo2TableRow.StatusColumn + 2 * Mo2PanelChrome.Padding);
    // What the row does not have room to draw. The four columns MO2 has and this
    // list does not are here, so dropping them cost the width they took rather than
    // the information itself.
    internal static string Details(ScenarioPlugin plugin) => string.Join("\n", new[] {
        plugin.DisplayName,
        plugin.ModName.Length > 0 ? "Mod: " + plugin.ModName : "",
        plugin.PriorityText.Length > 0 ? "Priority: " + plugin.PriorityText : "",
        plugin.ModIndex.Length > 0 ? ModIndexName + ": " + plugin.ModIndex : "",
        plugin.Author.Length > 0 ? "Author: " + plugin.Author : "",
        plugin.FormVersion.Length > 0 ? "Form version: " + plugin.FormVersion : "",
        plugin.HeaderVersion.Length > 0 ? "Header version: " + plugin.HeaderVersion : "",
        plugin.Description.Length > 0 ? plugin.Description : "",
        plugin.Diagnostics,
    }.Where(text => text.Length > 0));

    internal static Control Create(Mo2LiveProfile profile, ScenarioPlugin plugin)
    {
        Mo2UiLatencyProbe.Count("Plugin rows created");
        var target = profile.CurrentTarget;
        Task Run(Func<Task> action) => target == profile.CurrentTarget && profile.CanChangeOriginalUi ? action() : Task.CompletedTask;
        async Task Change(bool active) => await Run(() => profile.SetPluginsActive([plugin.DisplayName], active));
        var actionSets = new List<object[]>();
        // MO2's own plugin menu (src/pluginlistcontextmenu.cpp), in its order and under
        // its wording. Enabling, moving and locking have routes of their own that the
        // list's drag and the checks for it already use; the rest go through MO2's own
        // menu action, so MO2 asks its own questions before acting on every plugin.
        object[] Actions() {
            var ready = target == profile.CurrentTarget && profile.CanChangeOriginalUi;
            if (target == profile.CurrentTarget && profile.Order.FindPlugin(plugin.Key) is { } latest) plugin = latest;
            // MO2 offers the two origin entries only for a plugin that came from a mod
            // it manages — "this is to avoid showing the option on game files like
            // skyrim.esm" — and the information window only for a mod it installed
            // rather than one the game supplies (pluginlistcontextmenu.cpp).
            var originMod = plugin.ModName.Length > 0 ? profile.Mods.FirstOrDefault(x => x.Name == plugin.ModName) : null;
            var origin = originMod is { IsForeign: false };
            MenuItem Mo2(string caption, bool enabled = true) {
                var item = Mo2EntryMenu.Action(caption,
                    () => Run(() => profile.RunPluginMenu([plugin.DisplayName], [[caption]], target)), ready && enabled);
                item.Tag = enabled;
                return item;
            }
            // MO2's Send to..., which is how a plugin is moved the length of the order
            // without dragging it there. All three are MO2's own, since it is MO2 that
            // knows where a plugin's masters let it land.
            var send = new MenuItem { Header = "Send to... " };
            foreach (var caption in new[] { "Top", "Bottom", "Priority..." })
                send.Items.Add(Mo2EntryMenu.Action(caption,
                    () => Run(() => profile.RunPluginMenu([plugin.DisplayName], [["Send to... ", caption]], target)),
                    ready && plugin.CanMove));
            object?[] built = [
            // MO2's own two, which act on everything selected rather than this row.
            Mo2EntryMenu.Action("Enable selected", () => Change(true), ready && plugin.CanToggle),
            Mo2EntryMenu.Action("Disable selected", () => Change(false), ready && plugin.CanToggle),
            new Separator(),
            Mo2("Enable all"), Mo2("Disable all"),
            new Separator(),
            send,
            new Separator(),
            // MO2 has no one-step move on this menu — a plugin is dragged, or sent
            // somewhere above — so neither is offered here.
            Mo2EntryMenu.Action(plugin.IsLocked ? "Unlock load order" : "Lock load order",
                () => Run(() => profile.SetPluginLocked(plugin.DisplayName, !plugin.IsLocked, target)), ready && plugin.IsActive && (plugin.IsLocked || plugin.CanMove)),
            new Separator(),
            // MO2 offers the folder of the mod a plugin came from, and that mod's own
            // window. Neither is on the menu at all for a plugin the game itself
            // supplies, which is MO2's own rule rather than a greyed-out entry.
            origin ? Mo2EntryMenu.Action("Open Origin in Explorer", () => Run(() => profile.OpenModFolder(plugin.ModName)), ready) : null,
            origin ? Mo2EntryMenu.Action("Open Origin Info...",
                () => Run(() => originMod is { } mod ? profile.ShowModDetails(mod.Id) : Task.CompletedTask), ready) : null,
            ];
            var items = Mo2RowMenu.Tidy(built).ToArray();
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
        ToolTip.SetTip(name, Details(plugin));
        var title = new DockPanel();
        var padlock = new UnifiedIcon { Name = "PluginLockIcon", Value = new ProjektankerIcon("mdi-lock-outline"), Size = 14, Margin = new Thickness(0,0,4,0), VerticalAlignment = VerticalAlignment.Center, IsVisible = plugin.IsLocked };
        ToolTip.SetTip(padlock, "Load order locked by MO2"); DockPanel.SetDock(padlock, Dock.Left); title.Children.Add(padlock);
        title.Children.Add(name);
        var grip = Mo2EntryMenu.Create("Plugin", plugin.DisplayName, Actions, ownContextMenu: false);
        if (!plugin.CanMove) {
            ToolTip.SetTip(grip, "MO2 fixes this plugin’s load-order position");
            Avalonia.Automation.AutomationProperties.SetName(grip, plugin.DisplayName + ": fixed load-order position");
        }
        var nameCell = Mo2TableRow.NameCell(grip, toggle, title);
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

        // MO2 draws this one as the game reads it: a hexadecimal index, and FE:xxx
        // for a light plugin. It is a number to be matched rather than read, so it is
        // set in the same fixed-width face the rest of the application uses for one.
        var modIndex = Mo2TableRow.Cell(plugin.ModIndex, .6);
        modIndex.FontFamily = FontFamily.Parse("monospace");
        ToolTip.SetTip(modIndex, plugin.ModIndex.Length > 0
            ? "The index the game gives this plugin, which its form ids begin with"
            : "The game gives no index to a plugin it is not loading");
        Mo2TableRow.Add(row, modIndex, ModIndex);
        void Refresh() {
            var ready = target == profile.CurrentTarget && profile.CanChangeOriginalUi;
            if (target == profile.CurrentTarget && profile.Order.FindPlugin(plugin.Key) is { } latest) plugin = latest;
            padlock.IsVisible = plugin.IsLocked;
            name.Opacity = plugin.IsActive ? .85 : .5;
            ToolTip.SetTip(name, Details(plugin));
            toggle.IsChecked = plugin.IsActive;
            toggle.IsEnabled = ready && plugin.CanToggle;
            // The menu was built for the plugin as it then was. What changes while it
            // exists is whether MO2 will take anything at all, and whether this plugin
            // can still be switched, moved or locked.
            foreach (var entries in actionSets) {
                var items = entries.OfType<MenuItem>().ToArray();
                foreach (var item in items)
                    item.IsEnabled = ready && (item.Header as string) switch {
                        "Enable selected" or "Disable selected" => plugin.CanToggle,
                        "Move earlier" or "Move later" => plugin.CanMove,
                        "Unlock load order" or "Lock load order" => plugin.IsActive && (plugin.IsLocked || plugin.CanMove),
                        // The rest are MO2's own, under the condition they were built
                        // with — which is on the item, since the menu outlives the row
                        // state it was built from.
                        _ => item.Tag is not false,
                    };
                if (items.FirstOrDefault(item => (item.Header as string) is "Unlock load order" or "Lock load order") is { } lockItem)
                    lockItem.Header = plugin.IsLocked ? "Unlock load order" : "Lock load order";
            }
        }
        row.AttachedToVisualTree += (_,_) => { profile.Changed += Refresh; Refresh(); };
        row.DetachedFromVisualTree += (_,_) => profile.Changed -= Refresh;
        row.LayoutUpdated += (_, _) => Fit(row, row.GetVisualAncestors().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().FirstOrDefault()?.Bounds.Width ?? row.Bounds.Width);
        return row;
    }
}
