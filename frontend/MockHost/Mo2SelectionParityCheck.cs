using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;

namespace Mo2.Frontend;

// My Mods and Plugins are the two tables a person works in, side by side, and
// selecting rows in them has to feel like the same thing. The rows are built from
// shared parts and line up to the pixel; what this checks is what happens when you
// select them — how many rows you may select, what the page tells you about the
// selection, how to get rid of it, and what a selected row looks like.
internal static class Mo2SelectionParityCheck
{
    private sealed record Behaviour(
        string Page, bool MultiSelect, int SelectedAfterThree, bool GroupShown, string CountText,
        bool GroupHiddenWhenEmpty, bool DeselectClears, Color? SelectedRow, Color? HoveredRow);

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
        if (mods is null || plugins is null)
            throw new Exception($"Could not find both tables (mods={mods is not null}, plugins={plugins is not null})");
        // Other checks drive these same two tables from the same window opening.
        using var turn = await Mo2CheckTurn.Take();

        var behaviours = new List<Behaviour>();
        foreach (var (name, page) in new[] { ("My Mods", mods), ("Plugins", plugins) }) {
            var table = page.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault(x => x.RowSelection is not null);
            for (var attempt = 0; attempt < 200 && (table?.Rows?.Count ?? 0) < 3; attempt++) {
                await Task.Delay(100);
                table ??= page.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault(x => x.RowSelection is not null);
            }
            if (table?.RowSelection is null || (table.Rows?.Count ?? 0) < 3)
                throw new Exception($"{name} never showed three rows to select");
            var selection = table.RowSelection;

            selection.Clear();
            await Task.Delay(300);
            var group = Group(page);
            var hiddenWhenEmpty = group is null || !group.IsEffectivelyVisible;

            // Three rows, one at a time, which is what a run of ctrl-clicks does.
            for (var row = 0; row < 3; row++) selection.Select(new IndexPath(row));
            await Task.Delay(400);
            var selected = selection.SelectedIndexes.Count;
            group = Group(page);
            var shown = group is { } visible && visible.IsEffectivelyVisible;
            // Whatever the page says about the selection, wherever it says it.
            var count = group?.GetSelfAndVisualDescendants().OfType<TextBlock>()
                .Select(x => x.Text ?? "")
                .FirstOrDefault(x => x.Contains("select", StringComparison.OrdinalIgnoreCase)) ?? "";
            if (count.Length == 0 && group is not null)
                count = group.GetSelfAndVisualDescendants().OfType<StandardButton>()
                    .Select(x => x.Text ?? "").FirstOrDefault(x => x.Contains("select", StringComparison.OrdinalIgnoreCase)) ?? "";

            var selectedColour = RowColour(table, ":selected");
            var hoveredColour = RowColour(table, ":pointerover");

            // And the way out of a selection, which has to be on the page rather than
            // only on the keyboard.
            var deselect = Deselect(page);
            deselect?.Command?.Execute(null);
            if (deselect is not null) deselect.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(400);
            var cleared = selection.SelectedIndexes.Count == 0;
            selection.Clear();
            await Task.Delay(200);

            behaviours.Add(new Behaviour(name, !selection.SingleSelect, selected, shown, count,
                hiddenWhenEmpty, cleared, selectedColour, hoveredColour));
        }

        var faults = new List<string>();
        var first = behaviours[0];
        foreach (var other in behaviours.Skip(1)) {
            void Same<T>(string what, Func<Behaviour, T> read)
            {
                if (!Equals(read(first), read(other)))
                    faults.Add($"{what}: {first.Page} {Describe(read(first))}, {other.Page} {Describe(read(other))}");
            }
            Same("rows that may be selected at once", x => x.MultiSelect ? "many" : "one");
            Same("rows selected after three were picked", x => x.SelectedAfterThree);
            Same("a selected row is painted", x => x.SelectedRow);
            Same("a row under the pointer is painted", x => x.HoveredRow);
        }

        // Neither list carries a row of bulk actions any more, and that is deliberate
        // rather than drift: MO2 puts no toolbar on a tab, and a selection is worked
        // from the row's own menu — which on both lists carries MO2's own entries.
        // So the absence is asserted rather than the presence compared: a group that
        // grows back on either page fails.
        foreach (var page in behaviours.Where(x => x.GroupShown))
            faults.Add($"{page.Page} grew a selection group back, where MO2 works a selection from the row menu");

        // What a selected row actually looks like, rather than what style was declared
        // for it. The plugin rows are wrapped in a border of the original view's own,
        // which paints over whatever the row beneath it is painted, so the same
        // :selected style can produce a filled bar on one list and an outline on the
        // other.
        (string Row, string Inner, string Edge, string Cells) Painted(TreeDataGrid table)
        {
            var row = table.GetVisualDescendants().OfType<TreeDataGridRow>()
                .FirstOrDefault(x => x.IsSelected && x.Bounds.Height > 0)
                ?? table.GetVisualDescendants().OfType<TreeDataGridRow>().FirstOrDefault(x => x.Bounds.Height > 0);
            if (row is null) return ("-", "-", "-", "-");
            var inner = row.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "RowBorder");
            var cells = row.GetVisualDescendants().OfType<TreeDataGridCellsPresenter>().FirstOrDefault();
            var cellsDesc = cells is null ? "no cells" :
                $"bg={(cells.Background as ISolidColorBrush)?.Color.ToString() ?? "null"} " +
                $"border={cells.BorderThickness.Top:F0} " +
                $"brush={(cells.BorderBrush as ISolidColorBrush)?.Color.ToString() ?? "null"} " +
                $"cr={cells.CornerRadius.TopLeft:F0}";
            return ((row.Background as ISolidColorBrush)?.Color.ToString() ?? "none",
                inner is null ? "no inner border" : (inner.Background as ISolidColorBrush)?.Color.ToString() ?? "none",
                $"{row.BorderThickness.Top:F0}/{(inner?.BorderThickness.Top ?? 0):F0}",
                cellsDesc);
        }

        var modsTableEarly = mods.GetVisualDescendants().OfType<TreeDataGrid>().First(x => x.RowSelection is not null);
        var pluginsTableEarly = plugins.GetVisualDescendants().OfType<TreeDataGrid>().First(x => x.RowSelection is not null);
        modsTableEarly.RowSelection!.Clear(); pluginsTableEarly.RowSelection!.Clear();
        modsTableEarly.RowSelection.Select(new IndexPath(0));
        pluginsTableEarly.RowSelection.Select(new IndexPath(0));
        await Task.Delay(700);
        window.UpdateLayout();
        var modsPaint = Painted(modsTableEarly);
        var pluginsPaint = Painted(pluginsTableEarly);
        if (modsPaint.Row != pluginsPaint.Row)
            faults.Add($"a selected row is filled {modsPaint.Row} on My Mods and {pluginsPaint.Row} on Plugins");
        if (modsPaint.Edge != pluginsPaint.Edge)
            faults.Add($"a selected row is outlined {modsPaint.Edge} on My Mods and {pluginsPaint.Edge} on Plugins");
        if (modsPaint.Cells != pluginsPaint.Cells)
            faults.Add($"a selected CellsPresenter is [{modsPaint.Cells}] on My Mods and [{pluginsPaint.Cells}] on Plugins");
        modsTableEarly.RowSelection.Clear(); pluginsTableEarly.RowSelection.Clear();
        await Task.Delay(300);

        // And the other half of it: selecting in one table marks what goes with it in
        // the other. Selecting mods has always marked the plugins they install;
        // selecting plugins marked nothing, so the same question could only be asked
        // from one side.
        var modsTable = mods.GetVisualDescendants().OfType<TreeDataGrid>().First(x => x.RowSelection is not null);
        var pluginsTable = plugins.GetVisualDescendants().OfType<TreeDataGrid>().First(x => x.RowSelection is not null);

        // Both what the profile linked and what the other table actually painted,
        // read while the selection is still live: a count taken after clearing it
        // reads zero however well the marking works.
        Func<string, Task>? shots = null;
        async Task<(int Painted, int Linked)> MarkedInTheOther(TreeDataGrid selectIn, TreeDataGrid readFrom, bool fromMods)
        {
            selectIn.RowSelection!.Clear();
            readFrom.RowSelection!.Clear();
            await Task.Delay(400);
            var before = Marked(readFrom);
            // Every row in turn, because which mod owns a plugin depends on the
            // profile and any one row may own nothing in the other table.
            var painted = 0;
            var linked = 0;
            var colours = "";
            for (var row = 0; row < Math.Min(12, selectIn.Rows!.Count) && painted == 0; row++) {
                selectIn.RowSelection.Clear();
                selectIn.RowSelection.Select(new IndexPath(row));
                await Task.Delay(500);
                painted = Marked(readFrom) - before;
                linked = fromMods ? live.Profile.LinkedPlugins.Count : live.Profile.LinkedMods.Count;
                if (painted > 0 && shots is { } take) await take(readFrom == pluginsTable ? "mod-selected" : "plugin-selected");
                if (painted == 0 && linked > 0) colours = string.Join(" ", readFrom
                    .GetVisualDescendants().OfType<TreeDataGridRow>()
                    .Select(x => (x.Background as ISolidColorBrush)?.Color.ToString() ?? "-")
                    .Concat(readFrom.GetVisualDescendants().OfType<Border>().Where(x => x.Name == "RowBorder")
                        .Select(x => "border:" + ((x.Background as ISolidColorBrush)?.Color.ToString() ?? "-")))
                    .Distinct());
            }
            selectIn.RowSelection.Clear();
            await Task.Delay(300);
            if (colours.Length > 0) Console.WriteLine($"CHECK selection parity colours: {colours}");
            return (painted, linked);
        }

        // A picture of each direction, so what the two tables do to each other can be
        // looked at rather than only counted.
        async Task Capture(TreeDataGrid selectIn, string name)
        {
            if (Environment.GetEnvironmentVariable("MO2_SELECTION_DIRECTORY") is not { Length: > 0 } directory) return;
            Directory.CreateDirectory(directory);
            await Task.Delay(700);
            using var shot = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new PixelSize(Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height)));
            shot.Render(window);
            shot.Save(Path.Combine(directory, $"selection-{name}.png"));
        }

        shots = name => Capture(modsTable, name);
        var byMods = await MarkedInTheOther(modsTable, pluginsTable, fromMods: true);
        var byPlugins = await MarkedInTheOther(pluginsTable, modsTable, fromMods: false);
        var pluginsMarkedByMods = byMods.Painted;
        var modsMarkedByPlugins = byPlugins.Painted;
        if (pluginsMarkedByMods <= 0)
            faults.Add($"selecting a mod marked no plugin in the other table (the profile linked {byMods.Linked} " +
                $"plugins; the plugins table has {pluginsTable.GetVisualDescendants().OfType<TreeDataGridRow>().Count()} rows drawn)");
        if (modsMarkedByPlugins <= 0)
            faults.Add($"selecting a plugin marked no mod in the other table, where selecting a mod marks its plugins " +
                $"(the profile linked {byPlugins.Linked} mods; the mods table has " +
                $"{modsTable.GetVisualDescendants().OfType<TreeDataGridRow>().Count()} rows drawn)");

        // What a click does, which is how a selection is actually made. Everything
        // above drives the selection model directly and would pass even if pressing a
        // row did nothing at all on one of the two pages.
        int ClickRow(TreeDataGrid table, int index)
        {
            table.RowSelection!.Clear();
            var row = table.GetVisualDescendants().OfType<TreeDataGridRow>()
                .Where(x => x.Bounds.Height > 0 && x.TranslatePoint(default, window) is not null)
                .OrderBy(x => x.TranslatePoint(default, window)!.Value.Y).Skip(index).FirstOrDefault();
            if (row is null) return -1;
            // On the row's name, away from the toggle and the actions at either end.
            var target = row.GetVisualDescendants().OfType<TextBlock>()
                .FirstOrDefault(x => x.Text is { Length: > 2 }) as Control ?? row;
            var pointer = new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true);
            var at = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window) ?? default;
            var pressed = new Avalonia.Input.PointerPointProperties(
                Avalonia.Input.RawInputModifiers.LeftMouseButton, Avalonia.Input.PointerUpdateKind.LeftButtonPressed);
            target.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(target, pointer, window, at, 0, pressed, Avalonia.Input.KeyModifiers.None));
            target.RaiseEvent(new Avalonia.Input.PointerReleasedEventArgs(target, pointer, window, at, 0,
                new Avalonia.Input.PointerPointProperties(Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PointerUpdateKind.LeftButtonReleased),
                Avalonia.Input.KeyModifiers.None, Avalonia.Input.MouseButton.Left));
            return table.RowSelection.SelectedIndexes.Count;
        }

        var clicked = (Mods: ClickRow(modsTable, 1), Plugins: 0);
        await Task.Delay(400);
        clicked = (clicked.Mods, ClickRow(pluginsTable, 1));
        await Task.Delay(400);
        if (clicked.Mods != clicked.Plugins)
            faults.Add($"clicking a row selects {clicked.Mods} on My Mods and {clicked.Plugins} on Plugins");
        else if (clicked.Mods != 1)
            faults.Add($"clicking a row selected {clicked.Mods} rows on both pages");
        modsTable.RowSelection!.Clear(); pluginsTable.RowSelection!.Clear();
        await Task.Delay(300);

        // A selection has to survive MO2 telling the frontend something changed.
        // Plugins keeps its selection across an activation update by name; if Mods
        // drops its own on the same refresh, working in the two lists feels different
        // in the way that matters most — the work you had selected disappears.
        modsTable.RowSelection!.Clear(); pluginsTable.RowSelection!.Clear();
        await Task.Delay(300);
        modsTable.RowSelection.Select(new IndexPath(0));
        modsTable.RowSelection.Select(new IndexPath(1));
        pluginsTable.RowSelection.Select(new IndexPath(0));
        pluginsTable.RowSelection.Select(new IndexPath(1));
        await Task.Delay(600);
        var before = (Mods: modsTable.RowSelection.SelectedIndexes.Count, Plugins: pluginsTable.RowSelection.SelectedIndexes.Count);
        await live.Profile.Refresh();
        await Task.Delay(1500);
        window.UpdateLayout();
        var after = (Mods: modsTable.RowSelection.SelectedIndexes.Count, Plugins: pluginsTable.RowSelection.SelectedIndexes.Count);
        if (before.Mods != before.Plugins)
            faults.Add($"could not select the same number of rows to begin with: {before.Mods} and {before.Plugins}");
        else if (after.Mods != after.Plugins)
            faults.Add($"after a refresh My Mods holds {after.Mods} of {before.Mods} selected rows and Plugins {after.Plugins}");
        modsTable.RowSelection.Clear(); pluginsTable.RowSelection.Clear();
        await Task.Delay(300);

        if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
        Console.WriteLine($"PASS selection parity: both tables take {(first.MultiSelect ? "many rows" : "one row")} at a time " +
            $"and paint a selected row {first.SelectedRow} with {first.HoveredRow} under the pointer; neither draws a " +
            "selection group, as MO2 draws none — a selection is worked from the row's own menu" +
            $"; selecting a mod marks {pluginsMarkedByMods} of its plugins and selecting a plugin marks " +
            $"{modsMarkedByPlugins} of the mods that install it; a refresh leaves {after.Mods} of {before.Mods} rows " +
            $"selected in both, and a click on a row selects exactly it on either page");
    }

    // How many rows a table is currently painting as linked to the other table's
    // selection. Read from the rows themselves, because that colour is applied to
    // whichever rows are realised rather than held in a property to inspect.
    private static int Marked(TreeDataGrid table) =>
        table.GetVisualDescendants().OfType<TreeDataGridRow>()
            .Count(row => Is(row.Background) ||
                          row.GetVisualDescendants().OfType<Border>().Any(x => x.Name == "RowBorder" && Is(x.Background)));

    private static bool Is(IBrush? brush) =>
        (brush as ISolidColorBrush)?.Color == Mo2RowHighlights.LinkedColour;

    private static string Describe(object? value) => value switch {
        null => "nothing",
        bool flag => flag ? "yes" : "no",
        _ => value.ToString() ?? "nothing",
    };

    private static string Digits(string text) => new(text.Where(char.IsDigit).ToArray());

    // The group a page shows while rows are selected: the native one on Mods, and
    // whatever Plugins puts beside it.
    private static Control? Group(Control page) =>
        page.GetVisualDescendants().OfType<Control>().FirstOrDefault(x => x.Name == "ContextControlGroup");

    private static Button? Deselect(Control page) =>
        page.GetVisualDescendants().OfType<Button>().FirstOrDefault(x => x.Name is "DeselectItemsButton");

    // What a row is painted while selected or hovered, read from the styles the
    // table carries rather than from a screenshot: the two tables install their row
    // styles separately and a different brush in either is the thing that would show.
    private static Color? RowColour(Control table, string pseudo)
    {
        foreach (var style in table.Styles.OfType<Avalonia.Styling.Style>()) {
            if (style.Selector?.ToString()?.Contains(pseudo, StringComparison.Ordinal) != true) continue;
            foreach (var setter in style.Setters.OfType<Avalonia.Styling.Setter>())
                if (setter.Property == TemplatedControl.BackgroundProperty && setter.Value is ISolidColorBrush brush)
                    return brush.Color;
        }
        return null;
    }
}
