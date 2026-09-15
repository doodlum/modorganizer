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

            // One search control, of the same kind, on both.
            var search = page.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Search.SearchControl>().FirstOrDefault();
            if (search is null) faults.Add($"{name} does not use the shared search control");

            // One selection group, of the same kind, on both.
            var group = page.GetVisualDescendants().OfType<Control>().FirstOrDefault(x => x.Name == "ContextControlGroup");
            if (group is null) faults.Add($"{name} has no selection group");
            var deselect = page.GetVisualDescendants().OfType<Control>().FirstOrDefault(x => x.Name == "DeselectItemsButton");
            if (deselect is null) faults.Add($"{name} has no deselect action in its selection group");

            // And the actions live on the header line, in the panel chrome's own group.
            var actions = page.GetVisualDescendants().OfType<Panel>().FirstOrDefault(x => x.Name == "PanelHeaderActions");
            if (actions is null) faults.Add($"{name} puts nothing on its header line");

            described.Add($"{name}: rail {rail.RowDefinitions.Count} rows, " +
                $"{page.GetVisualDescendants().OfType<Mo2ListScrollBar>().Count()} shared scrollbar, " +
                $"{(search is null ? 0 : 1)} search control, {(group is null ? 0 : 1)} selection group");
        }

        // The toolbar holds the same things in the same order on both pages. Each
        // page assembled its own, so the pill, the search and the selection group
        // could sit in any order relative to one another without anything noticing.
        string Shape(Control page) => string.Join(" ", page.GetVisualDescendants().OfType<Toolbar>()
            .SelectMany(toolbar => toolbar.Items.OfType<Control>())
            .Select(item => item switch {
                Border { Name: { } pill } when pill.EndsWith("PrimaryActions", StringComparison.Ordinal) => "pill",
                NexusMods.App.UI.Controls.Search.SearchControl => "search",
                Panel { Name: "ContextControlGroup" } => "selection",
                ItemsControl inner when inner.Items.OfType<NexusMods.App.UI.Controls.Search.SearchControl>().Any() => "search",
                _ => item.GetType().Name.ToLowerInvariant(),
            }));
        var shapes = new[] { ("My Mods", Shape(mods)), ("Plugins", Shape(plugins)) };
        if (shapes[0].Item2 != shapes[1].Item2)
            faults.Add($"the toolbars hold different things: My Mods \"{shapes[0].Item2}\", Plugins \"{shapes[1].Item2}\"");
        described.Add($"both toolbars read \"{shapes[0].Item2}\"");

        // The controls inside the toolbar, to the pixel and the glyph. Both pages
        // reach for "an icon button" and got different ones: the overflow dots were
        // drawn from different icons at different sizes, and the search control's own
        // button came out a different size again.
        (double W, double H, double Icon, string Glyph) Shape2(Control? button)
        {
            if (button is null) return (-1, -1, -1, "-");
            var glyph = button.GetSelfAndVisualDescendants().OfType<NexusMods.UI.Sdk.Icons.UnifiedIcon>().FirstOrDefault();
            return (button.Bounds.Width, button.Bounds.Height, glyph?.Size ?? -1,
                glyph?.Value?.Value.Value is ProjektankerIcon { Value: { } icon } ? icon : glyph?.Value?.ToString() ?? "-");
        }
        Control? Overflow(Control page) => page.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(x => x.Name is "ModsOverflowButton" or "PluginsOverflowButton");
        Control? SearchToggle(Control page) => page.GetVisualDescendants()
            .OfType<NexusMods.App.UI.Controls.Search.SearchControl>().FirstOrDefault()
            ?.GetVisualDescendants().OfType<Button>().FirstOrDefault();

        var overflows = new[] { ("My Mods", Shape2(Overflow(mods))), ("Plugins", Shape2(Overflow(plugins))) };
        if (overflows[0].Item2 != overflows[1].Item2)
            faults.Add($"the overflow buttons differ: My Mods {overflows[0].Item2}, Plugins {overflows[1].Item2}");
        var searches = new[] { ("My Mods", Shape2(SearchToggle(mods))), ("Plugins", Shape2(SearchToggle(plugins))) };
        if (searches[0].Item2 != searches[1].Item2)
            faults.Add($"the search buttons differ: My Mods {searches[0].Item2}, Plugins {searches[1].Item2}");
        described.Add($"overflow {overflows[0].Item2}, search {searches[0].Item2}");

        // Sizes, not just names: the same control can be put on the line in a box only
        // one page has. My Mods' search sat inside the original markup's padded items
        // control, so the same search button was 32px tall there and 24 on Plugins.
        string Sizes(Control page) => string.Join(" ", page.GetVisualDescendants().OfType<Toolbar>()
            .SelectMany(toolbar => toolbar.Items.OfType<Control>())
            .Select(item => $"{item.Bounds.Width:F0}x{item.Bounds.Height:F0}"));
        var sizes = new[] { ("My Mods", Sizes(mods)), ("Plugins", Sizes(plugins)) };
        if (sizes[0].Item2 != sizes[1].Item2)
            faults.Add($"the toolbars lay out differently: My Mods {sizes[0].Item2}, Plugins {sizes[1].Item2}");
        var heights = new[] { mods, plugins }.Select(page => page.GetVisualDescendants().OfType<Toolbar>()
            .FirstOrDefault()?.Bounds.Height ?? -1).ToArray();
        if (Math.Abs(heights[0] - heights[1]) > 1.5)
            faults.Add($"the toolbars are {heights[0]:F0}px and {heights[1]:F0}px tall");
        described.Add($"both toolbars {heights[0]:F0}px tall holding {sizes[0].Item2}");

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
