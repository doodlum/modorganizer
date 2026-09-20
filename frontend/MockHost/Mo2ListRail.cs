using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Layout;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// The strip beside an installed list: the scrollbar, and under it the arrow and
// trophy that say which end of the list wins a conflict. My Mods built this by
// hand and Plugins used the pieces the original app's load-order view happens to
// contain, wiring its own copy of the scroll synchronisation inline — thirty
// lines that had drifted from the shared one, so the plugin list kept the native
// scrollbar as well as the rail and the two pages scrolled differently.
internal static class Mo2ListRail
{
    // Room for the thumb at its widest, which is what both lists reserve beside
    // their rows.
    internal const int Width = Mo2TableRow.RailWidth;

    internal static Grid Create(string scrollName, out Mo2ListScrollBar scroll, bool plugins = false)
    {
        scroll = new Mo2ListScrollBar {
            Name = scrollName, Width = 16, Orientation = Orientation.Vertical, SmallChange = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var rail = new Grid { Name = "ListRail", RowDefinitions = new RowDefinitions("24,*,24,28"), Margin = new Thickness(0, 16, 0, 8) };
        var title = plugins ? "Plugin load order" : "Mod priority";
        var explanation = plugins
            ? "Plugins load from top to bottom. Later plugins can override records from earlier plugins. Masters must load before plugins that depend on them. Drag a movable plugin by its grip to change its position."
            : "Mods lower in the list win conflicts when enabled mods provide the same loose file. Select a mod to highlight conflicts and its plugins. Drag a mod by its grip to change priority; separators keep related mods together.";
        var help = Mo2QtWidgets.Icon(scrollName + "Help", title, "mdi-help-circle-outline", () => { });
        help.HorizontalAlignment = HorizontalAlignment.Center;
        help.Flyout = new Flyout {
            Placement = PlacementMode.Left,
            Content = new StackPanel { Spacing = 8, MaxWidth = 280, Children = {
                new TextBlock { Text = title, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = explanation, TextWrapping = TextWrapping.Wrap },
            } },
        };
        rail.Children.Add(help);
        Grid.SetRow(scroll, 1); rail.Children.Add(scroll);
        var down = new UnifiedIcon { Value = IconValues.ArrowDownThick, Size = 20, Opacity = .3,
            HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetRow(down, 2); rail.Children.Add(down);
        var winner = new UnifiedIcon { Value = IconValues.TrophyOutline, Size = 20, Opacity = .5,
            HorizontalAlignment = HorizontalAlignment.Center };
        ToolTip.SetTip(winner, plugins ? "Later plugins can override records from earlier plugins." : "Lower enabled mods win conflicts for the same loose file.");
        Grid.SetRow(winner, 3); rail.Children.Add(winner);
        return rail;
    }

    // One way of following a table's scrolling, including hiding the table's own
    // scrollbar: the rail replaces it rather than sitting beside it.
    internal static void Connect(Mo2ListScrollBar scroll, TreeDataGrid table) =>
        Mo2ListScrollBar.Connect(scroll, table);
}
