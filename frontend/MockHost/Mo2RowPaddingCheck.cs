using Avalonia;
using Avalonia.Controls;
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

    // The same thing again against real rows, where a cell built by hand rather
    // than through the shared helper would still show up.
    internal static async Task Live(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 200 && !(live.Profile.IsConnected && live.Profile.Mods.Count > 0); attempt++)
            await Task.Delay(100);
        var faults = new List<string>();
        Thickness[] Insets(Control root, string name) => root.GetVisualDescendants().OfType<Control>()
            .Where(x => x.GetType() == typeof(TextBlock) && x.Margin.Left > 0 && x.Margin.Top == 0)
            .Select(x => x.Margin).Distinct().ToArray();

        foreach (var (name, table) in new (string, Control?)[] {
                     ("Mods", window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault(x => x.IsEffectivelyVisible)),
                     ("Plugins", window.GetVisualDescendants().OfType<Mo2PluginsView>().FirstOrDefault(x => x.IsEffectivelyVisible)) }) {
            if (table is null) { faults.Add(name + " is not on screen"); continue; }
            var insets = Insets(table, name).Where(x => x != Mo2TableRow.CellMargin).ToArray();
            if (insets.Length > 0)
                faults.Add($"{name} draws cells inset {string.Join(", ", insets.Select(x => x.Left))} as well as the shared {Mo2TableRow.CellMargin.Left}");
        }
        if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
        Console.WriteLine($"PASS row padding (live): every horizontally inset label in both tables uses the shared " +
            $"{Mo2TableRow.CellMargin.Left}px");
    }
}
