using Avalonia;
using Avalonia.Controls;
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

    internal static Grid Create(string scrollName, out Mo2ListScrollBar scroll)
    {
        scroll = new Mo2ListScrollBar {
            Name = scrollName, Width = 16, Orientation = Orientation.Vertical, SmallChange = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var rail = new Grid { Name = "ListRail", RowDefinitions = new RowDefinitions("*,24,28"), Margin = new Thickness(0, 16, 0, 8) };
        rail.Children.Add(scroll);
        var down = new UnifiedIcon { Value = IconValues.ArrowDownThick, Size = 20, Opacity = .3,
            HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetRow(down, 1); rail.Children.Add(down);
        var winner = new UnifiedIcon { Value = IconValues.TrophyOutline, Size = 20, Opacity = .5,
            HorizontalAlignment = HorizontalAlignment.Center };
        ToolTip.SetTip(winner, "Entries lower in the list win file conflicts.");
        Grid.SetRow(winner, 2); rail.Children.Add(winner);
        return rail;
    }

    // One way of following a table's scrolling, including hiding the table's own
    // scrollbar: the rail replaces it rather than sitting beside it.
    internal static void Connect(Mo2ListScrollBar scroll, TreeDataGrid table) =>
        Mo2ListScrollBar.Connect(scroll, table);
}
