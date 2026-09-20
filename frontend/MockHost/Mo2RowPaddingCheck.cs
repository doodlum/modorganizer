using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;

namespace Mo2.Frontend;

// The two tables have to line up with each other and with their own column
// headings. Mods used a 3px inset on its title and 4px on version, category and
// the update pill while Plugins used 3px throughout, so no two columns shared an
// edge. Both rows now build their cells through Mo2TableRow; this is what stops
// either of them drifting again.
internal static class Mo2RowPaddingCheck
{
    private static string ModsHeading => Mo2ModRow.Headers.First(x => x.Column == Mo2ModRow.Name).Name;
    private static string PluginsHeading => Mo2PluginRow.Headers.First(x => x.Column == Mo2PluginRow.Name).Name;

    internal static void Run()
    {
        var faults = new List<string>();

        // Grip and status now live inside the name cell. Comparing the outer
        // metadata columns accidentally compares unrelated fields (for example,
        // mod Flags against plugin Form Version).
        var mods = Mo2ModRow.Columns();
        var plugins = Mo2PluginRow.Columns();
        if (mods.ColumnDefinitions[Mo2ModRow.Name].Width !=
            plugins.ColumnDefinitions[Mo2PluginRow.Name].Width)
            faults.Add("Name columns do not share the same sizing");
        var grip = new Border();
        var status = new CheckBox();
        var title = Mo2TableRow.Cell("Example");
        var name = Mo2TableRow.NameCell(grip, status, title);
        if (name.ColumnDefinitions.Count != 3 ||
            name.ColumnDefinitions[0].Width != new GridLength(Mo2TableRow.GripWidth) ||
            name.ColumnDefinitions[1].Width != new GridLength(Mo2TableRow.StatusColumn) ||
            name.ColumnDefinitions[2].Width != new GridLength(1, GridUnitType.Star) ||
            Grid.GetColumn(grip) != 0 || Grid.GetColumn(status) != 1 || Grid.GetColumn(title) != 2)
            faults.Add("Name-cell grip, status and title layout is inconsistent");
        var heading = Mo2TableRow.NameHeading("Name");
        if (heading.Margin.Left != Mo2TableRow.GripWidth + Mo2TableRow.StatusColumn + title.Margin.Left)
            faults.Add("Name heading does not align with the title after grip and status");

        // And every cell either row draws carries the shared inset.
        void Inset(string what, Thickness actual)
        {
            if (actual != Mo2TableRow.CellMargin)
                faults.Add($"{what} is inset {actual}, not the shared {Mo2TableRow.CellMargin}");
        }
        Inset("a shared cell", Mo2TableRow.Cell("x").Margin);
        Inset("a shared heading", Mo2TableRow.Heading("x").Margin);

        if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
        Console.WriteLine($"PASS row padding: Mods and Plugins share name sizing and aligned grip, status and title cells, " +
            $"and every cell and heading in both is inset {Mo2TableRow.CellMargin.Left}px");
    }

    // Everything about a table that the eye compares between the two pages.
    private sealed record Table(double Left, double Right, double Status, double Name,
        double RowLeft, double RowRight, double RowHeight, double Actions,
        double HeadingTop, double HeadingHeight, double FirstRowTop);

    // The same thing again against the rendered tables, which is where it actually
    // shows. The column definitions and the cell insets can agree exactly while the
    // two tables still do not line up, because each sits in a different host with
    // its own padding around it — declarations are not what the eye compares.
    internal static async Task Live(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.Mods.Count > 0); attempt++)
            await Task.Delay(100);

        Control? Page<T>() where T : Control =>
            window.GetVisualDescendants().OfType<T>().FirstOrDefault(x => x.IsEffectivelyVisible);

        // Both edges of both tables, and the rows themselves. Measuring only where a
        // table starts passes two tables that begin together and end 22px apart: the
        // rail beside the rows is what ends them, and the eye compares the whole
        // block, not its left edge.
        Table? Measure(Control? page, string statusName, string headingText)
        {
            if (page is null) return null;
            var heading = page.GetVisualDescendants().OfType<TextBlock>()
                .FirstOrDefault(x => x.Text == headingText);
            var toggle = page.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(x => x.Name == statusName);
            var table = page.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
            if (heading is null || toggle is null || table is null) return null;
            // The topmost row on screen, not whichever one the visual tree happens to
            // list first: rows are recycled as a list scrolls, so after any scrolling
            // the first one in the tree can be several rows down — and this measures
            // how far the first row sits below the column headings.
            var row = table.GetVisualDescendants().OfType<TreeDataGridRow>()
                .Where(x => x.Bounds.Height > 0 && x.TranslatePoint(default, page) is not null)
                .OrderBy(x => x.TranslatePoint(default, page)!.Value.Y).FirstOrDefault();
            if (row is null) return null;
            // The last thing drawn in the row, so the right-hand end can be compared
            // without knowing which actions each page happens to offer.
            var actions = row.GetVisualDescendants().OfType<Button>()
                .Where(x => x.Bounds.Width > 0 && x.TranslatePoint(default, page) is not null)
                .OrderBy(x => x.TranslatePoint(default, page)!.Value.X).LastOrDefault();
            // The responsive header can be completely hidden. Its separator then
            // retains stale bounds, so use the visible action pill as our anchor.
            var toolbar = page.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(x => x.IsEffectivelyVisible &&
                    x.Name == (page is Mo2ModsView ? "ModsToolbarActions" : "PluginsToolbarActions"));
            var headingRow = heading;
            var toolbarBottom = toolbar?.TranslatePoint(new Point(0, toolbar.Bounds.Height), page)?.Y;
            var headingTop = heading.TranslatePoint(default, page)?.Y;
            var rowTop = row.TranslatePoint(default, page)?.Y;
            if (toolbarBottom is null || headingTop is null || rowTop is null) return null;
            if (headingTop < toolbarBottom - 1)
                throw new Exception($"{page.GetType().Name}: column headings overlap the toolbar");
            var headingAt = heading.TranslatePoint(default, page)?.X;
            var toggleAt = (row.GetVisualDescendants().OfType<Control>().FirstOrDefault(x => x.Name == statusName) ?? toggle)
                .TranslatePoint(default, page)?.X;
            var tableAt = table.TranslatePoint(default, page)?.X;
            var rowAt = row.TranslatePoint(default, page)?.X;
            var actionsAt = actions?.TranslatePoint(new Point(actions.Bounds.Width, 0), page)?.X;
            if (headingAt is null || toggleAt is null || tableAt is null || rowAt is null || actionsAt is null) return null;
            if (table.Bounds.Width <= 0 || row.Bounds.Width <= 0) return null;
            return new Table(
                Left: tableAt.Value,
                Right: page.Bounds.Width - (tableAt.Value + table.Bounds.Width),
                Status: toggleAt.Value - tableAt.Value,
                Name: headingAt.Value - tableAt.Value,
                RowLeft: rowAt.Value - tableAt.Value,
                RowRight: page.Bounds.Width - (rowAt.Value + row.Bounds.Width),
                RowHeight: row.Bounds.Height,
                Actions: page.Bounds.Width - actionsAt.Value,
                HeadingTop: headingTop.Value - toolbarBottom.Value,
                HeadingHeight: headingRow.Bounds.Height,
                FirstRowTop: rowTop.Value - (headingTop.Value + headingRow.Bounds.Height));
        }

        Table? mods = null, plugins = null;
        for (var attempt = 0; attempt < 200 && (mods is null || plugins is null); attempt++) {
            await Task.Delay(100);
            // The heading text each table actually draws, taken from the same
            // declarations the columns are built from: spelled out here, the two
            // strings stopped matching when the columns took MO2's own names, and
            // the check reported that it could not measure either table rather than
            // that it was looking for headings neither page has.
            mods ??= Measure(Page<Mo2ModsView>(), "ModActivationToggle", ModsHeading);
            plugins ??= Measure(Page<Mo2PluginsView>(), "PluginActivationToggle", PluginsHeading);
        }
        if (mods is null || plugins is null)
            throw new Exception($"Could not measure both tables (mods={mods is not null}, plugins={plugins is not null})");

        var faults = new List<string>();
        void Same(string what, double a, double b)
        {
            if (Math.Abs(a - b) > 1.5) faults.Add($"{what}: Mods {a:F0}px, Plugins {b:F0}px");
        }
        // Each measured against its own table, so the comparison is of the padding
        // inside the tables rather than of where the two panels happen to be.
        Same("status column starts", mods.Status, plugins.Status);
        Same("name column starts", mods.Name, plugins.Name);
        // And the tables themselves sit the same distance inside their pages, at both
        // ends: the rows run to the table's edge, and the rail beyond it is the same
        // width on both pages. The mod list is measured from where its own list area
        // starts rather than from the page edge — MO2 puts the Filters group beside
        // that list, so the pane holds the group and the list side by side.
        Same("table starts", mods.Left - Mo2ModRow.SideWidth, plugins.Left);
        Same("table ends", mods.Right, plugins.Right);
        Same("rows start", mods.RowLeft, plugins.RowLeft);
        Same("rows end", mods.RowRight, plugins.RowRight);
        // Not the row actions: MO2 puts a mod's actions on its right-click menu, so a
        // mod row draws no buttons and the rightmost one found in it is whatever the
        // row happens to hold.
        Same("row height", mods.RowHeight, plugins.RowHeight);
        Same("headings below the toolbar", mods.HeadingTop, plugins.HeadingTop);
        Same("heading row height", mods.HeadingHeight, plugins.HeadingHeight);
        Same("first row below the headings", mods.FirstRowTop, plugins.FirstRowTop);

        if (faults.Count > 0) {
            // Name what actually contributes the offset, rather than leaving the
            // difference to be hunted for by hand.
            // The vertical trail, from the heading row up to the page: what stands
            // between the header separator and the first heading is never the heading
            // itself, it is a row left behind by something that moved elsewhere.
            string Down(Control? page, string headingText)
            {
                if (page is null) return "-";
                var heading = page.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(x => x.Text == headingText);
                if (heading?.GetSelfAndVisualAncestors().OfType<Grid>().FirstOrDefault() is not { } row) return "-";
                var parts = new List<string>();
                for (Visual? node = row; node is not null && !ReferenceEquals(node, page); node = node.GetVisualParent()) {
                    if (node is not Control control) continue;
                    var pad = control is TemplatedControl templated ? templated.Padding : default;
                    parts.Add($"{control.GetType().Name}#{control.Name} y{control.TranslatePoint(default, page)?.Y ?? -1:F0} " +
                        $"h{control.Bounds.Height:F0} m{control.Margin.Top:F0}/{control.Margin.Bottom:F0} p{pad.Top:F0}/{pad.Bottom:F0}");
                }
                // And the siblings the header stack shares its row with, which is
                // where space that belongs to neither the header nor the table hides.
                // The row the table sits in, and every sibling row above it: space
                // between the header and the table belongs to one of them.
                // And from the first row up to the table: what sits between the
                // headings and the first row is on that side, not the heading's.
                if (page.GetVisualDescendants().OfType<TreeDataGridRow>().FirstOrDefault() is { } first) {
                    parts.Add("| from the first row up:");
                    for (Visual? node = first; node is not null && !ReferenceEquals(node, page); node = node.GetVisualParent()) {
                        if (node is not Control control) continue;
                        var pad = control is TemplatedControl templated ? templated.Padding : default;
                        parts.Add($"{control.GetType().Name}#{control.Name} y{control.TranslatePoint(default, page)?.Y ?? -1:F0} " +
                            $"h{control.Bounds.Height:F0} m{control.Margin.Top:F0}/{control.Margin.Bottom:F0} p{pad.Top:F0}/{pad.Bottom:F0}");
                        if (control is TreeDataGrid) break;
                    }
                }
                if (page.GetVisualDescendants().OfType<Grid>()
                        .FirstOrDefault(x => x.RowDefinitions.Count > 2) is { } root) {
                    parts.Add($"| root {root.Name} rows " + string.Join(",", root.RowDefinitions.Select(d => d.Height.ToString())) + ":");
                    foreach (var child in root.Children.OfType<Control>())
                        parts.Add($"r{Grid.GetRow(child)} {child.GetType().Name}#{child.Name} " +
                            $"y{child.TranslatePoint(default, page)?.Y ?? -1:F0} h{child.Bounds.Height:F0} " +
                            $"m{child.Margin.Top:F0}/{child.Margin.Bottom:F0} vis{child.IsVisible}");
                }
                if (page.GetVisualDescendants().OfType<Control>().FirstOrDefault(x => x.Name == "PanelHeaderStack")
                        is { } stack && stack.GetVisualParent() is Panel host) {
                    parts.Add($"| stack y{stack.TranslatePoint(default, page)?.Y ?? -1:F0} h{stack.Bounds.Height:F0} in " +
                        $"{host.GetType().Name} beside:");
                    foreach (var sibling in host.Children.OfType<Control>())
                        parts.Add($"{sibling.GetType().Name}#{sibling.Name} y{sibling.TranslatePoint(default, page)?.Y ?? -1:F0} " +
                            $"h{sibling.Bounds.Height:F0} m{sibling.Margin.Top:F0}/{sibling.Margin.Bottom:F0} vis{sibling.IsVisible}");
                }
                return parts.Count == 0 ? "nothing above" : string.Join(" < ", parts);
            }

            string Trail(Control? page, string statusName)
            {
                if (page?.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault() is not { } table) return "-";
                var parts = new List<string>();
                for (Visual? node = table; node is not null && !ReferenceEquals(node, page); node = node.GetVisualParent()) {
                    if (node is not Control control) continue;
                    var pad = control is TemplatedControl templated ? templated.Padding : default;
                    if (control.Margin.Left != 0 || control.Margin.Right != 0 || pad.Left != 0 || pad.Right != 0)
                        parts.Add($"{control.GetType().Name}#{control.Name} m{control.Margin.Left:F0}/{control.Margin.Right:F0} " +
                            $"p{pad.Left:F0}/{pad.Right:F0} w{control.Bounds.Width:F0}");
                }
                if (page.GetVisualDescendants().OfType<Control>().FirstOrDefault(x => x.Name == statusName) is { } toggle) {
                    parts.Add("| from the toggle up:");
                    for (Visual? node = toggle; node is not null && !ReferenceEquals(node, table); node = node.GetVisualParent()) {
                        if (node is not Control control) continue;
                        var pad = control is TemplatedControl templated ? templated.Padding : default;
                        if (control.Margin.Left != 0 || control.Margin.Right != 0 || pad.Left != 0 || pad.Right != 0)
                            parts.Add($"{control.GetType().Name}#{control.Name} m{control.Margin.Left:F0}/{control.Margin.Right:F0} " +
                                $"p{pad.Left:F0}/{pad.Right:F0} w{control.Bounds.Width:F0}");
                    }
                }
                return parts.Count == 0 ? "nothing inset" : string.Join(" < ", parts);
            }
            throw new Exception(string.Join("; ", faults) +
                $". Mods: {Trail(Page<Mo2ModsView>(), "ModActivationToggle")}" +
                $". Plugins: {Trail(Page<Mo2PluginsView>(), "PluginActivationToggle")}" +
                $". Down mods: {Down(Page<Mo2ModsView>(), ModsHeading)}" +
                $". Down plugins: {Down(Page<Mo2PluginsView>(), PluginsHeading)}");
        }
        var initialCategories = Page<Mo2ModsView>()!.GetLogicalDescendants().OfType<Mo2CategoryFilterList>().Where(x => x.Name == "ModCategories").Distinct().Single();
        if (initialCategories.Items.Count != 0)
            throw new Exception("Initially closed category pane constructed unused filter rows");
        Console.WriteLine("PASS lazy category rows: initially closed pane has no unused category controls");
        double? helpTop = null;
        foreach (var page in new[] { Page<Mo2ModsView>()!, Page<Mo2PluginsView>()! }) {
            var help = page.GetVisualDescendants().OfType<Button>().Single(x => x.Name?.EndsWith("RailScrollBarHelp") == true);
            var position = help.TranslatePoint(default, page)!.Value;
            if (!help.IsEffectivelyVisible || help.Bounds.Width < 20 || position.X < 0 || position.Y < 0 ||
                position.X + help.Bounds.Width > page.Bounds.Width || position.Y + help.Bounds.Height > page.Bounds.Height)
                throw new Exception("List help is hidden or clipped");
            if (helpTop is { } expectedTop && Math.Abs(position.Y - expectedTop) > 1)
                throw new Exception($"List help buttons are misaligned: {expectedTop:F0}px / {position.Y:F0}px");
            helpTop = position.Y;
            var flyout = (Flyout)help.Flyout!;
            try {
                typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(help, null);
                await Task.Delay(150);
                if (!flyout.IsOpen || flyout.Content is not StackPanel content ||
                    content.Children.OfType<TextBlock>().Last().Bounds.Height <= 0)
                    throw new Exception("List help click did not show its explanation");
            } finally { flyout.Hide(); }
        }
        Console.WriteLine("PASS list rail help: Mods and Plugins buttons fit and open their explanations");
        var pairedFlags = 0;
        foreach (var row in Page<Mo2ModsView>()!.GetVisualDescendants().OfType<Grid>().Where(x => x.Name == "ModRedesignRow")) {
            var icons = row.GetVisualDescendants().OfType<Control>().ToArray();
            var flag = icons.Single(x => x.Name == "ModFlagIcon");
            var endorsement = icons.Single(x => x.Name == "ModEndorsementIcon");
            if (!flag.IsEffectivelyVisible || flag.Opacity == 0 || endorsement.Opacity == 0) continue;
            var left = flag.TranslatePoint(default, row)!.Value.X;
            var right = endorsement.TranslatePoint(default, row)!.Value.X;
            if (right < left + flag.Bounds.Width + 3 || right + endorsement.Bounds.Width > row.Bounds.Width)
                throw new Exception("Mod flag and endorsement glyphs overlap or leave the row");
            pairedFlags++;
        }
        if (pairedFlags == 0) throw new Exception("Flag layout requires a rendered mod with both flag and endorsement glyphs");
        Console.WriteLine($"PASS mod flag layout: {pairedFlags} rows show separate flag and endorsement glyphs without overlap");
        Console.WriteLine($"PASS row padding (live): both tables run from {mods.Left:F0}px to {mods.Right:F0}px of their " +
            $"page, their status column {mods.Status:F0}px and name column {mods.Name:F0}px into the table, their rows " +
            $"{mods.RowHeight:F0}px tall ending {mods.RowRight:F0}px from the page with the last action {mods.Actions:F0}px in, " +
            $"and their headings {mods.HeadingTop:F0}px below the toolbar with the first row {mods.FirstRowTop:F0}px " +
            $"below a {mods.HeadingHeight:F0}px heading row");
    }
}
