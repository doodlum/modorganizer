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
            Same("a group appears with a selection", x => x.GroupShown);
            Same("that group is hidden with nothing selected", x => x.GroupHiddenWhenEmpty);
            Same("the page says how many rows are selected", x => x.CountText.Length > 0);
            Same("the count reads", x => Digits(x.CountText));
            Same("a deselect action empties the selection", x => x.DeselectClears);
            Same("a selected row is painted", x => x.SelectedRow);
            Same("a row under the pointer is painted", x => x.HoveredRow);
        }

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

        if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
        Console.WriteLine($"PASS selection parity: both tables take {(first.MultiSelect ? "many rows" : "one row")} at a time, " +
            $"report \"{first.CountText}\" when three are picked, hide that group again with nothing selected, clear from the " +
            $"page's own deselect action, and paint a selected row {first.SelectedRow} with {first.HoveredRow} under the " +
            $"pointer; selecting a mod marks {pluginsMarkedByMods} of its plugins and selecting a plugin marks " +
            $"{modsMarkedByPlugins} of the mods that install it");
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
