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

        Mo2HeaderLine? Chrome(Control page) =>
            page.GetVisualDescendants().OfType<Mo2HeaderLine>().FirstOrDefault();

        // Where a table's first two columns actually land, measured from the page's
        // own left edge so the panels' positions do not come into it.
        (double Status, double Name, double Table)? Offsets(Control? page, string statusName, string headingText)
        {
            if (page is null) return null;
            var heading = page.GetVisualDescendants().OfType<TextBlock>()
                .FirstOrDefault(x => x.Text == headingText);
            var toggle = page.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(x => x.Name == statusName);
            var table = page.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
            if (heading is null || toggle is null || table is null) return null;
            var headingAt = heading.TranslatePoint(default, page)?.X;
            var toggleAt = toggle.TranslatePoint(default, page)?.X;
            var tableAt = table.TranslatePoint(default, page)?.X;
            if (headingAt is null || toggleAt is null || tableAt is null) return null;
            return (toggleAt.Value, headingAt.Value, tableAt.Value);
        }

        (double Status, double Name, double Table)? mods = null, plugins = null;
        for (var attempt = 0; attempt < 200 && (mods is null || plugins is null); attempt++) {
            await Task.Delay(100);
            mods ??= Offsets(Page<Mo2ModsView>(), "ModActivationToggle", "Mod name");
            plugins ??= Offsets(Page<Mo2PluginsView>(), "PluginActivationToggle", "Plugin name");
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
        Same("status column starts", mods.Value.Status - mods.Value.Table, plugins.Value.Status - plugins.Value.Table);
        Same("name column starts", mods.Value.Name - mods.Value.Table, plugins.Value.Name - plugins.Value.Table);
        // And the tables themselves sit the same distance inside their pages.
        Same("table starts", mods.Value.Table, plugins.Value.Table);

        if (faults.Count > 0) {
            // Name what actually contributes the offset, rather than leaving the
            // difference to be hunted for by hand.
            string Trail(Control? page, string statusName)
            {
                if (page?.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault() is not { } table) return "-";
                var parts = new List<string>();
                for (Visual? node = table; node is not null && !ReferenceEquals(node, page); node = node.GetVisualParent()) {
                    if (node is not Control control) continue;
                    var pad = control is TemplatedControl templated ? templated.Padding : default;
                    if (control.Margin.Left != 0 || pad.Left != 0)
                        parts.Add($"{control.GetType().Name}#{control.Name} m{control.Margin.Left:F0} p{pad.Left:F0}");
                }
                if (page.GetVisualDescendants().OfType<Control>().FirstOrDefault(x => x.Name == statusName) is { } toggle) {
                    parts.Add("| from the toggle up:");
                    for (Visual? node = toggle; node is not null && !ReferenceEquals(node, table); node = node.GetVisualParent()) {
                        if (node is not Control control) continue;
                        var pad = control is TemplatedControl templated ? templated.Padding : default;
                        if (control.Margin.Left != 0 || pad.Left != 0)
                            parts.Add($"{control.GetType().Name}#{control.Name} m{control.Margin.Left:F0} p{pad.Left:F0}");
                    }
                }
                return parts.Count == 0 ? "nothing inset" : string.Join(" < ", parts);
            }
            throw new Exception(string.Join("; ", faults) +
                $". Mods: {Trail(Page<Mo2ModsView>(), "ModActivationToggle")}" +
                $". Plugins: {Trail(Page<Mo2PluginsView>(), "PluginActivationToggle")}");
        }
        Console.WriteLine($"PASS row padding (live): both tables start {mods.Value.Table:F0}px into their page, " +
            $"their status column {mods.Value.Status - mods.Value.Table:F0}px in and their name column " +
            $"{mods.Value.Name - mods.Value.Table:F0}px in");
    }
}
