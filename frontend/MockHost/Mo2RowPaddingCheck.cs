using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// The two tables have to line up with each other and with their own column
// headings. Mods used a 3px inset on its title and 4px on version, category and
// the update pill while Plugins used 3px throughout, so no two columns shared an
// edge. Both rows now build their cells through Mo2TableRow; this is what stops
// either of them drifting again.
internal static class Mo2RowPaddingCheck
{
    internal static void Run()
    {
        var faults = new List<string>();

        // The columns both tables have in common are the same width. The ones
        // between them differ on purpose: the pages describe different things.
        var mods = Mo2ModRow.Columns();
        var plugins = Mo2PluginRow.Columns();
        void Same(string what, int modColumn, int pluginColumn)
        {
            var a = mods.ColumnDefinitions[modColumn].Width;
            var b = plugins.ColumnDefinitions[pluginColumn].Width;
            if (a != b) faults.Add($"{what}: Mods {a}, Plugins {b}");
        }
        Same("grip column", 0, 0);
        Same("status column", 1, 1);
        Same("name column", 2, 2);
        Same("actions column", mods.ColumnDefinitions.Count - 1, plugins.ColumnDefinitions.Count - 1);

        // And every cell either row draws carries the shared inset.
        void Inset(string what, Thickness actual)
        {
            if (actual != Mo2TableRow.CellMargin)
                faults.Add($"{what} is inset {actual}, not the shared {Mo2TableRow.CellMargin}");
        }
        Inset("a shared cell", Mo2TableRow.Cell("x").Margin);
        Inset("a shared heading", Mo2TableRow.Heading("x").Margin);

        if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
        Console.WriteLine($"PASS row padding: Mods and Plugins share their grip, status, name and actions columns, " +
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
            // The row the toggle is in, which is what carries the actions at its end.
            var row = toggle.GetSelfAndVisualAncestors().OfType<TreeDataGridRow>().FirstOrDefault();
            if (row is null) return null;
            // The last thing drawn in the row, so the right-hand end can be compared
            // without knowing which actions each page happens to offer.
            var actions = row.GetVisualDescendants().OfType<Button>()
                .Where(x => x.Bounds.Width > 0 && x.TranslatePoint(default, page) is not null)
                .OrderBy(x => x.TranslatePoint(default, page)!.Value.X).LastOrDefault();
            // Vertically as well: the two headers are different heights because they
            // say different things, but everything below the separator that divides
            // header from table has to be spaced the same on both pages.
            var rule = page.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(x => x.Name == "PanelHeaderSeparator");
            var headingRow = heading;
            var ruleBottom = rule?.TranslatePoint(new Point(0, rule.Bounds.Height), page)?.Y;
            var headingTop = headingRow?.TranslatePoint(default, page)?.Y;
            var rowTop = row.TranslatePoint(default, page)?.Y;
            if (rule is null || ruleBottom is null || headingTop is null || rowTop is null) return null;
            var headingAt = heading.TranslatePoint(default, page)?.X;
            var toggleAt = toggle.TranslatePoint(default, page)?.X;
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
                HeadingTop: headingTop.Value - ruleBottom.Value,
                HeadingHeight: headingRow.Bounds.Height,
                FirstRowTop: rowTop.Value - (headingTop.Value + headingRow.Bounds.Height));
        }

        Table? mods = null, plugins = null;
        for (var attempt = 0; attempt < 200 && (mods is null || plugins is null); attempt++) {
            await Task.Delay(100);
            mods ??= Measure(Page<Mo2ModsView>(), "ModActivationToggle", "Mod name");
            plugins ??= Measure(Page<Mo2PluginsView>(), "PluginActivationToggle", "Plugin name");
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
        // width on both pages.
        Same("table starts", mods.Left, plugins.Left);
        Same("table ends", mods.Right, plugins.Right);
        Same("rows start", mods.RowLeft, plugins.RowLeft);
        Same("rows end", mods.RowRight, plugins.RowRight);
        Same("row actions end", mods.Actions, plugins.Actions);
        Same("row height", mods.RowHeight, plugins.RowHeight);
        Same("headings below the separator", mods.HeadingTop, plugins.HeadingTop);
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
                $". Down mods: {Down(Page<Mo2ModsView>(), "Mod name")}" +
                $". Down plugins: {Down(Page<Mo2PluginsView>(), "Plugin name")}");
        }
        Console.WriteLine($"PASS row padding (live): both tables run from {mods.Left:F0}px to {mods.Right:F0}px of their " +
            $"page, their status column {mods.Status:F0}px and name column {mods.Name:F0}px into the table, their rows " +
            $"{mods.RowHeight:F0}px tall ending {mods.RowRight:F0}px from the page with the last action {mods.Actions:F0}px in, " +
            $"and their headings {mods.HeadingTop:F0}px below the header separator with the first row {mods.FirstRowTop:F0}px " +
            $"below a {mods.HeadingHeight:F0}px heading row");
    }
}
