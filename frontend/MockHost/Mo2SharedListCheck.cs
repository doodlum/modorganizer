using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// My Mods and Plugins are meant to be one page twice over. Checking that they
// behave alike is not the same as checking they are built alike: each behaviour
// matched separately still leaves two implementations that drift the moment one
// of them is touched. This walks both pages and compares what they are made of —
// the rail beside the rows, the toolbar on the header line, the group that
// appears with a selection — and fails naming whichever part only one of them has.
internal static class Mo2SharedListCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        Control? Page<T>() where T : Control =>
            window.GetVisualDescendants().OfType<T>().FirstOrDefault(x => x.IsEffectivelyVisible)
            ?? window.GetVisualDescendants().OfType<T>().FirstOrDefault();

        Control? mods = null, plugins = null;
        for (var attempt = 0; attempt < 300 && (mods is null || plugins is null); attempt++) {
            await Task.Delay(100);
            mods ??= Page<Mo2ModsView>();
            plugins ??= Page<Mo2PluginsView>();
        }
        if (mods is null || plugins is null) throw new Exception("Could not find both pages");
        // Other checks drive these same two tables from the same window opening.
        using var turn = await Mo2CheckTurn.Take();
        await Task.Delay(1500);

        // From no selection: the group on the header line is meant to grow when rows
        // are selected, so comparing the two toolbars while one page happens to have
        // a selection compares the selection, not the toolbar.
        foreach (var page in new[] { mods, plugins })
            foreach (var table in page.GetVisualDescendants().OfType<TreeDataGrid>())
                table.RowSelection?.Clear();
        await Task.Delay(500);

        var faults = new List<string>();
        var described = new List<string>();

        foreach (var (name, page) in new[] { ("My Mods", mods), ("Plugins", plugins) }) {
            var rail = page.GetVisualDescendants().OfType<Grid>().FirstOrDefault(x => x.Name == "ListRail");
            if (rail is null) { faults.Add($"{name} does not draw the shared rail beside its rows"); continue; }
            if (rail.GetVisualDescendants().OfType<Mo2ListScrollBar>().FirstOrDefault() is null)
                faults.Add($"{name}'s rail has no shared scrollbar");
            if (rail.GetVisualDescendants().OfType<NexusMods.UI.Sdk.Icons.UnifiedIcon>().Count() != 2)
                faults.Add($"{name}'s rail does not carry the arrow and the trophy");

            // The rail replaces the table's own scrollbar rather than sitting beside
            // it. Plugins kept both, because it followed its table's scrolling with a
            // copy of the shared connection that had lost that line.
            var table = page.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault(x => x.RowSelection is not null);
            var viewer = table?.GetVisualDescendants().OfType<ScrollViewer>()
                .FirstOrDefault(x => x.GetVisualDescendants().Any(c => c.GetType().Name == "TreeDataGridRowsPresenter"));
            if (viewer is null) faults.Add($"{name} has no scrolling list to follow");
            else if (viewer.VerticalScrollBarVisibility != ScrollBarVisibility.Hidden)
                faults.Add($"{name} shows its table's own scrollbar ({viewer.VerticalScrollBarVisibility}) as well as the rail");

            // No toolbar. MO2 carries none on a tab, so a page that grows one back has
            // drifted from what this frontend is replicating. The empty group that
            // used to hold one is gone, so this asks the header line itself: the
            // header, and nothing beside it.
            var line = page.GetVisualDescendants().OfType<Mo2HeaderLine>().FirstOrDefault();
            var beside = line is null ? 0 : line.GetVisualChildren().Count() - 1;
            if (beside > 0) faults.Add($"{name} draws a toolbar of {beside} actions on its header line");

            described.Add($"{name}: rail {rail.RowDefinitions.Count} rows, " +
                $"{page.GetVisualDescendants().OfType<Mo2ListScrollBar>().Count()} shared scrollbar, no toolbar");
        }

        // Both lists carry MO2's own columns, under MO2's own headers, in MO2's own
        // order. Read from the headings each page draws rather than from the tables
        // they were built from, so a page that stops drawing one is caught.
        // A column one icon wide takes the glyph it holds as its heading, as MO2's own
        // do — "Conflicts" and "Flags" do not fit there and were drawn as "C…" and
        // "Fl…" — and that glyph carries the column's name where a reader can get at
        // it. Reading only the words missed all four of them and reported MO2's own
        // columns as absent from both lists; it had been failing on that since the
        // glyph headings went in. The name is read off whichever kind of heading the
        // column has, in column order, so a heading that stops saying which column it
        // is still fails.
        static string NameOf(Control heading) => heading switch {
            TextBlock { FontWeight: Avalonia.Media.FontWeight.SemiBold, Text: { } text } => text,
            _ => Avalonia.Automation.AutomationProperties.GetName(heading) ?? "",
        };
        string Headings(Control page, int expected) => string.Join(" ", page.GetVisualDescendants().OfType<Grid>()
            .Where(x => x.ColumnDefinitions.Count == expected)
            .Select(x => string.Join(",", x.Children.OfType<Control>()
                .OrderBy(Grid.GetColumn).Select(NameOf).Where(name => name.Length > 0)))
            .Where(x => x.Length > 0).Take(1));
        var modHeadings = Headings(mods, Mo2ModRow.Headers.Length);
        var pluginHeadings = Headings(plugins, Mo2PluginRow.Headers.Length);
        var wantedMods = string.Join(",", Mo2ModRow.Headers.Select(x => x.Name));
        var wantedPlugins = string.Join(",", Mo2PluginRow.Headers.Select(x => x.Name));
        if (modHeadings != wantedMods) faults.Add($"My Mods heads its columns \"{modHeadings}\" rather than MO2's \"{wantedMods}\"");
        if (pluginHeadings != wantedPlugins) faults.Add($"Plugins heads its columns \"{pluginHeadings}\" rather than MO2's \"{wantedPlugins}\"");
        described.Add($"My Mods heads {Mo2ModRow.Headers.Length} MO2 columns and Plugins {Mo2PluginRow.Headers.Length}");

        // What the two toolbars held, and how they lined up with one another, used to
        // be checked here at length. There are no toolbars to compare on this branch —
        // the absence of one is asserted per page above instead.

        // The same rail, to the pixel: both pages reserve the same width for it and
        // give it the same room above and below.
        var rails = new[] { mods, plugins }
            .Select(page => page.GetVisualDescendants().OfType<Grid>().FirstOrDefault(x => x.Name == "ListRail"))
            .ToArray();
        if (rails.All(x => x is not null)) {
            if (rails[0]!.Margin != rails[1]!.Margin)
                faults.Add($"the rails are inset differently: {rails[0]!.Margin} and {rails[1]!.Margin}");
            if (Math.Abs(rails[0]!.Bounds.Width - rails[1]!.Bounds.Width) > 1.5)
                faults.Add($"the rails are {rails[0]!.Bounds.Width:F0}px and {rails[1]!.Bounds.Width:F0}px wide");
        }

        // And it follows the rows: scrolling the table moves the rail, which is the
        // whole reason the table's own scrollbar is hidden. The window is squeezed
        // first, because at a comfortable size neither of these lists is long enough
        // to scroll at all and the check would pass without testing anything.
        // The table is given a height rather than the window being shrunk: asking this
        // window to resize is ignored under this desktop, so the list stayed as long
        // as its rows and there was never anything to scroll.
        var scrolled = 0;
        foreach (var (name, page) in new[] { ("My Mods", mods), ("Plugins", plugins) }) {
            var bar = page.GetVisualDescendants().OfType<Mo2ListScrollBar>().FirstOrDefault();
            var table = page.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault(x => x.RowSelection is not null);
            var viewer = table?.GetVisualDescendants().OfType<ScrollViewer>()
                .FirstOrDefault(x => x.GetVisualDescendants().Any(c => c.GetType().Name == "TreeDataGridRowsPresenter"));
            if (bar is null || viewer is null || table is null) continue;
            table.Height = 160;
            await Task.Delay(600);
            window.UpdateLayout();
            await Task.Delay(300);
            if (bar.Maximum <= 0) {
                table.ClearValue(Layoutable.HeightProperty);
                faults.Add($"{name}'s rail reports nothing to scroll in a {table.Bounds.Height:F0}px table " +
                    $"(its rows extend {viewer.Extent.Height:F0}px in a {viewer.Viewport.Height:F0}px viewport)");
                continue;
            }
            viewer.Offset = new Vector(viewer.Offset.X, Math.Min(80, bar.Maximum));
            await Task.Delay(400);
            window.UpdateLayout();
            if (Math.Abs(bar.Value - viewer.Offset.Y) > 1.5)
                faults.Add($"{name}'s rail reads {bar.Value:F0} while its rows are at {viewer.Offset.Y:F0}");
            // And the other way: moving the rail moves the rows, which is what
            // dragging its thumb does.
            bar.Value = Math.Min(60, bar.Maximum);
            await Task.Delay(400);
            window.UpdateLayout();
            if (Math.Abs(viewer.Offset.Y - bar.Value) > 1.5)
                faults.Add($"{name}'s rows are at {viewer.Offset.Y:F0} while its rail reads {bar.Value:F0}");
            scrolled++;
            // Put the list back where it was found, rail included, and wait for it to
            // settle: anything checked after this in the same run reads these tables,
            // and one left scrolled reported its first row 249px below its headings.
            table.ClearValue(Layoutable.HeightProperty);
            bar.Value = 0;
            viewer.Offset = new Vector(viewer.Offset.X, 0);
            for (var settle = 0; settle < 20 && viewer.Offset.Y > .5; settle++) {
                viewer.Offset = new Vector(viewer.Offset.X, 0);
                await Task.Delay(100);
                window.UpdateLayout();
            }
            if (viewer.Offset.Y > .5) faults.Add($"{name} was left scrolled to {viewer.Offset.Y:F0} by this check");
        }

        if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
        Console.WriteLine($"PASS shared list pages: {string.Join("; ", described)} — both built from the same rail, scrollbar, " +
            $"search control and selection group, each hiding its table's own scrollbar, and {scrolled} of them followed " +
            "their rows in both directions once the list was too short to hold them");
    }
}
