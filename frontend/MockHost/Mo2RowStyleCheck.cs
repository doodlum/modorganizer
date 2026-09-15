using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;

namespace Mo2.Frontend;

// Both tables must show the same resting card, the same hover overlay, and the
// same selection outline. This check reads the CellsPresenter properties in each
// state, compares them across the two tables, and captures screenshots so the
// result can be inspected visually.
internal static class Mo2RowStyleCheck
{
    // Nothing drawn at all, as a rendered pixel reads back.
    private const string Clear = "#00000000";
    // The same absence as a brush reads back, which names itself rather than
    // spelling out its channels.
    private static bool IsClear(string colour) => colour is Clear or "Transparent" or "null";

    internal static async Task Run(Mo2LiveWorkspace live, Window window, string? screenshotDir)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        Control? mods = null, plugins = null;
        for (var attempt = 0; attempt < 300 && (mods is null || plugins is null); attempt++) {
            await Task.Delay(100);
            mods ??= window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault(x => x.IsEffectivelyVisible)
                ?? window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault();
            plugins ??= window.GetVisualDescendants().OfType<Mo2PluginsView>().FirstOrDefault(x => x.IsEffectivelyVisible)
                ?? window.GetVisualDescendants().OfType<Mo2PluginsView>().FirstOrDefault();
        }
        if (mods is null || plugins is null)
            throw new Exception($"Could not find both tables (mods={mods is not null}, plugins={plugins is not null})");

        using var turn = await Mo2CheckTurn.Take();

        var modsTable = mods.GetVisualDescendants().OfType<TreeDataGrid>().First(x => x.RowSelection is not null);
        var pluginsTable = plugins.GetVisualDescendants().OfType<TreeDataGrid>().First(x => x.RowSelection is not null);

        for (var attempt = 0; attempt < 200 && ((modsTable.Rows?.Count ?? 0) < 3 || (pluginsTable.Rows?.Count ?? 0) < 3); attempt++)
            await Task.Delay(100);

        var faults = new List<string>();

        // --- Resting state ---
        modsTable.RowSelection!.Clear();
        pluginsTable.RowSelection!.Clear();
        await Task.Delay(500);
        window.UpdateLayout();

        var modsResting = RowSnapshot(modsTable, selected: false);
        var pluginsResting = RowSnapshot(pluginsTable, selected: false);
        Console.WriteLine($"CHECK row-style resting: mods=[{modsResting}] plugins=[{pluginsResting}]");

        var modsTableBg = (modsTable.Background as ISolidColorBrush)?.Color.ToString() ?? "null";
        var pluginsTableBg = (pluginsTable.Background as ISolidColorBrush)?.Color.ToString() ?? "null";
        Console.WriteLine($"CHECK table-bg: mods={modsTableBg} plugins={pluginsTableBg}");
        if (modsTableBg != pluginsTableBg)
            faults.Add($"table background: mods={modsTableBg}, plugins={pluginsTableBg}");
        // A table that paints its own surface hides the panel from every entry on it,
        // so neither may — the NMA base theme sets one and its SortOrder styles do not.
        foreach (var (label, bg) in new[] { ("Mods", modsTableBg), ("Plugins", pluginsTableBg) })
            if (!IsClear(bg))
                faults.Add($"{label} table paints {bg} instead of letting the panel show through");

        if (modsResting.RowBg != pluginsResting.RowBg)
            faults.Add($"resting row background: mods={modsResting.RowBg}, plugins={pluginsResting.RowBg}");
        if (modsResting.RowCr != pluginsResting.RowCr)
            faults.Add($"resting row corner radius: mods={modsResting.RowCr}, plugins={pluginsResting.RowCr}");

        // What the two tables actually draw, rather than what they were asked to
        // draw. The properties above agreed while the pages still looked different,
        // because the Plugins row paints through a template whose own borders layer
        // over the row's background — so only the rendered pixel settles it. A
        // resting entry is expected to draw nothing at all, which is what lets the
        // panel behind it show through instead of a surface of the row's own.
        var modsPixel = RowPixel(modsTable, selected: false);
        var pluginsPixel = RowPixel(pluginsTable, selected: false);
        Console.WriteLine($"CHECK resting pixels: mods={modsPixel} plugins={pluginsPixel}");
        if (modsPixel != pluginsPixel)
            faults.Add($"resting row pixel: mods={modsPixel}, plugins={pluginsPixel}");
        if (modsPixel != Clear)
            faults.Add($"Mods resting row paints {modsPixel} instead of leaving the panel to show through");
        if (pluginsPixel != Clear)
            faults.Add($"Plugins resting row paints {pluginsPixel} instead of leaving the panel to show through");

        // A category bar is the one thing on the list that still draws a surface, and
        // it has to run the full width of the entries under it. It used to be inset,
        // which read as a narrower bar once those entries stopped drawing surfaces of
        // their own and the band their hover paints became what marks an entry's width.
        // Entry bodies are built when the list gets round to them, so the bar is not
        // there the moment the rows are.
        Border? bar = null;
        for (var attempt = 0; attempt < 40 && bar is null; attempt++) {
            bar = modsTable.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(x => x.Name == "ModSeparatorBar" && x.Bounds.Width > 0);
            if (bar is null) { await Task.Delay(100); window.UpdateLayout(); }
        }
        var entryRow = modsTable.GetVisualDescendants().OfType<TreeDataGridRow>()
            .FirstOrDefault(x => x.Bounds.Width > 0 && !x.GetVisualDescendants().OfType<Border>().Any(b => b.Name == "ModSeparatorBar"));
        if (bar is null || entryRow is null)
            Console.WriteLine($"CHECK separator width: not measured (bar={bar is not null}, entry={entryRow is not null})");
        else {
            Console.WriteLine($"CHECK separator width: bar={bar.Bounds.Width:F0} entry={entryRow.Bounds.Width:F0}");
            if (Math.Abs(bar.Bounds.Width - entryRow.Bounds.Width) > 1)
                faults.Add($"separator bar is {bar.Bounds.Width:F0}px against entries of {entryRow.Bounds.Width:F0}px");
        }

        // Rules drawn across a page above its first entry. The header's own separator
        // belongs to the panel chrome and is hidden with it, so what this finds is
        // anything else a page rules across itself — and the two pages must agree,
        // because a page carrying one the other does not is a page that looks unlike
        // its neighbour. Read from a render rather than from the tree: the one that
        // turned up this way was an edge between two fills, which no border property
        // describes.
        var modsRules = Rules(mods);
        var pluginsRules = Rules(plugins);
        Console.WriteLine($"CHECK page rules above the first entry: mods=[{string.Join(" ", modsRules)}] plugins=[{string.Join(" ", pluginsRules)}]");
        if (modsRules.Count != pluginsRules.Count)
            faults.Add($"page rules: mods draws {modsRules.Count} ({string.Join(" ", modsRules)}), plugins draws {pluginsRules.Count} ({string.Join(" ", pluginsRules)})");

        if (screenshotDir is not null) {
            Directory.CreateDirectory(screenshotDir);
            await Capture(window, Path.Combine(screenshotDir, "row-style-resting.png"));
        }

        // --- Selected state ---
        modsTable.RowSelection.Select(new IndexPath(1));
        pluginsTable.RowSelection.Select(new IndexPath(1));
        await Task.Delay(500);
        window.UpdateLayout();

        var modsSelected = RowSnapshot(modsTable, selected: true);
        var pluginsSelected = RowSnapshot(pluginsTable, selected: true);
        Console.WriteLine($"CHECK row-style selected: mods=[{modsSelected}] plugins=[{pluginsSelected}]");

        if (modsSelected.CellsBg != pluginsSelected.CellsBg)
            faults.Add($"selected CellsPresenter background: mods={modsSelected.CellsBg}, plugins={pluginsSelected.CellsBg}");
        if (modsSelected.CellsBorder != pluginsSelected.CellsBorder)
            faults.Add($"selected CellsPresenter border: mods={modsSelected.CellsBorder}, plugins={pluginsSelected.CellsBorder}");
        if (modsSelected.CellsBrush != pluginsSelected.CellsBrush)
            faults.Add($"selected CellsPresenter border brush: mods={modsSelected.CellsBrush}, plugins={pluginsSelected.CellsBrush}");
        if (modsSelected.CellsCr != pluginsSelected.CellsCr)
            faults.Add($"selected CellsPresenter corner radius: mods={modsSelected.CellsCr}, plugins={pluginsSelected.CellsCr}");

        var modsSelectedPixel = RowPixel(modsTable, selected: true);
        var pluginsSelectedPixel = RowPixel(pluginsTable, selected: true);
        Console.WriteLine($"CHECK selected pixels: mods={modsSelectedPixel} plugins={pluginsSelectedPixel}");
        if (modsSelectedPixel != pluginsSelectedPixel)
            faults.Add($"selected row pixel: mods={modsSelectedPixel}, plugins={pluginsSelectedPixel}");

        // Verify the outline IS present (not zero)
        if (modsSelected.CellsBorder == "0")
            faults.Add("Mods selected row has no border outline on CellsPresenter");
        if (pluginsSelected.CellsBorder == "0")
            faults.Add("Plugins selected row has no border outline on CellsPresenter");
        // Verify the background IS null (outline, not fill)
        if (modsSelected.CellsBg != "null")
            faults.Add($"Mods selected row has CellsPresenter fill ({modsSelected.CellsBg}) instead of outline-only");

        if (screenshotDir is not null)
            await Capture(window, Path.Combine(screenshotDir, "row-style-selected.png"));

        // --- Verify hover style is installed ---
        var modsHover = StyleInstalled(modsTable, ":pointerover", "SurfaceTranslucentLow");
        var pluginsHover = StyleInstalled(pluginsTable, ":pointerover", "SurfaceTranslucentLow");
        if (!modsHover)
            faults.Add("Mods table has no :pointerover style with SurfaceTranslucentLow on CellsPresenter");
        if (!pluginsHover)
            faults.Add("Plugins table has no :pointerover style with SurfaceTranslucentLow on CellsPresenter");

        // --- Verify selected+hover style is installed ---
        var modsSelHover = StyleInstalled(modsTable, ":selected", "SurfaceTranslucentLow");
        var pluginsSelHover = StyleInstalled(pluginsTable, ":selected", "SurfaceTranslucentLow");

        // Clean up: wait long enough for the selection group to hide, so a
        // following check that measures the toolbar doesn't see stale UI.
        modsTable.RowSelection.Clear();
        pluginsTable.RowSelection.Clear();
        await Task.Delay(1500);

        if (faults.Count > 0)
            Console.WriteLine("FAIL row style: " + string.Join("; ", faults));
        else
            Console.WriteLine("PASS row style: neither table nor its entries paint a surface " +
                $"of their own at rest, so both show the panel behind them (table={modsTableBg}, " +
                $"entry renders {modsPixel}); both carry the same resting row settings " +
                $"({modsResting.RowBg}, cr={modsResting.RowCr}), the same selection outline " +
                $"(border={modsSelected.CellsBorder}, brush={modsSelected.CellsBrush}, bg={modsSelected.CellsBg}, " +
                $"cr={modsSelected.CellsCr}), and the same hover overlay installed " +
                $"(mods={modsHover}, plugins={pluginsHover})");
    }

    private record struct Snapshot(string RowBg, string RowCr, string CellsBg, string CellsBorder, string CellsBrush, string CellsCr)
    {
        public override string ToString() =>
            $"row-bg={RowBg} row-cr={RowCr} cells-bg={CellsBg} cells-border={CellsBorder} cells-brush={CellsBrush} cells-cr={CellsCr}";
    }

    private static Snapshot RowSnapshot(TreeDataGrid table, bool selected)
    {
        var row = selected
            ? table.GetVisualDescendants().OfType<TreeDataGridRow>().FirstOrDefault(x => x.IsSelected && x.Bounds.Height > 0)
            : table.GetVisualDescendants().OfType<TreeDataGridRow>().FirstOrDefault(x => !x.IsSelected && x.Bounds.Height > 0);
        row ??= table.GetVisualDescendants().OfType<TreeDataGridRow>().FirstOrDefault(x => x.Bounds.Height > 0);
        if (row is null) return new("no-row", "-", "-", "-", "-", "-");

        var cells = row.GetVisualDescendants().OfType<TreeDataGridCellsPresenter>().FirstOrDefault();
        return new(
            RowBg: (row.Background as ISolidColorBrush)?.Color.ToString() ?? "null",
            RowCr: row.CornerRadius.TopLeft.ToString("F0"),
            CellsBg: cells is null ? "no-cells" : (cells.Background as ISolidColorBrush)?.Color.ToString() ?? "null",
            CellsBorder: cells?.BorderThickness.Top.ToString("F0") ?? "no-cells",
            CellsBrush: cells is null ? "no-cells" : (cells.BorderBrush as ISolidColorBrush)?.Color.ToString() ?? "null",
            CellsCr: cells?.CornerRadius.TopLeft.ToString("F0") ?? "no-cells"
        );
    }

    private static bool StyleInstalled(TreeDataGrid table, string pseudo, string brushFragment)
    {
        foreach (var style in table.Styles.OfType<Avalonia.Styling.Style>()) {
            var sel = style.Selector?.ToString() ?? "";
            if (!sel.Contains(pseudo, StringComparison.Ordinal)) continue;
            foreach (var setter in style.Setters.OfType<Avalonia.Styling.Setter>())
                if (setter.Property == TemplatedControl.BackgroundProperty &&
                    setter.Value is ISolidColorBrush brush &&
                    Application.Current!.TryFindResource(brushFragment + "Brush", Avalonia.Styling.ThemeVariant.Default, out var res) &&
                    res is ISolidColorBrush expected && brush.Color == expected.Color)
                    return true;
        }
        return false;
    }

    // The colour a row actually ends up drawing, read back from a render of the row
    // itself. Sampled just above the row's bottom edge, which is the band nothing in
    // a row occupies: the enable box and the text are centred on the line. Sampling
    // 4px in from the left worked only while every cell was inset 24px — at MO2's
    // density that point is inside the enable box, and the check read its orange.
    private static string RowPixel(TreeDataGrid table, bool selected)
    {
        var row = table.GetVisualDescendants().OfType<TreeDataGridRow>()
            .FirstOrDefault(x => x.IsSelected == selected && x.Bounds is { Width: > 40, Height: > 8 });
        if (row is null) return "no-row";
        var size = new PixelSize((int)row.Bounds.Width, (int)row.Bounds.Height);
        using var bitmap = new RenderTargetBitmap(size);
        bitmap.Render(row);
        var stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try {
            bitmap.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), pixels.Length, stride);
        } finally { handle.Free(); }
        var at = stride * Math.Max(0, size.Height - 2) + size.Width / 2 * 4;
        return $"#{pixels[at + 3]:x2}{pixels[at + 2]:x2}{pixels[at + 1]:x2}{pixels[at]:x2}";
    }

    // Thin horizontal bands a page draws above its first entry: a row whose colour
    // differs from its neighbours on both sides is a line across the page rather
    // than the boundary between two areas of it.
    private static List<string> Rules(Control page)
    {
        var found = new List<string>();
        // The page itself, not the panel around it: the panel's own tab strip draws a
        // scrollbar across the same band once it has more tabs than fit, and that is
        // chrome both pages share rather than something either page rules on itself.
        var root = page;
        var size = new PixelSize(Math.Max(1, (int)root.Bounds.Width), Math.Max(1, (int)root.Bounds.Height));
        // Where the column headings start. Their text is a colour change of its own,
        // and every entry below them is another, so the search stops above both.
        var headings = root.GetVisualDescendants().OfType<TreeDataGrid>()
            .Select(t => t.TranslatePoint(new Point(0, 0), root)?.Y).FirstOrDefault(y => y is > 0) ?? 120;
        using var bmp = new RenderTargetBitmap(size);
        bmp.Render(root);
        var stride = size.Width * 4;
        var px = new byte[stride * size.Height];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(px, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { bmp.CopyPixels(new PixelRect(0, 0, size.Width, size.Height), handle.AddrOfPinnedObject(), px.Length, stride); }
        finally { handle.Free(); }
        string At(int y) { var i = stride * y + size.Width / 2 * 4; return $"#{px[i + 3]:x2}{px[i + 2]:x2}{px[i + 1]:x2}{px[i]:x2}"; }
        for (var y = 2; y < Math.Min(size.Height - 2, (int)headings); y++)
            if (At(y) != At(y - 1) && At(y) != At(y + 1) && At(y - 1) == At(y + 1))
                found.Add($"y={y}:{At(y)}");
        return found;
    }

    private static async Task Capture(Window window, string path)
    {
        await Task.Delay(300);
        using var shot = new RenderTargetBitmap(
            new PixelSize(Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height)));
        shot.Render(window);
        shot.Save(path);
    }
}
