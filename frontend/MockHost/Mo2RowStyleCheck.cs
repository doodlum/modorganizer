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

        if (modsResting.RowBg != pluginsResting.RowBg)
            faults.Add($"resting row background: mods={modsResting.RowBg}, plugins={pluginsResting.RowBg}");
        if (modsResting.RowCr != pluginsResting.RowCr)
            faults.Add($"resting row corner radius: mods={modsResting.RowCr}, plugins={pluginsResting.RowCr}");

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
            Console.WriteLine("PASS row style: both tables show the same resting background " +
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

    private static async Task Capture(Window window, string path)
    {
        await Task.Delay(300);
        using var shot = new RenderTargetBitmap(
            new PixelSize(Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height)));
        shot.Render(window);
        shot.Save(path);
    }
}
