using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// External Files, Data and Overwrite all show the contents of a folder, and all
// three were written separately: Data typed an arrow in front of folder names and
// styled its table for mod lists, Overwrite wrote its own sizes, showed a row of
// labelled buttons no other page has and had no way to hide a column. They now
// build from Mo2FolderPage, and this is what stops one of them drifting again —
// it looks at the pages as built rather than at which helper they called.
internal static class Mo2FolderPagesCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        var until = DateTime.UtcNow.AddSeconds(15);
        while (!shell.Profile.IsConnected || shell.Profile.SelectingProfile) {
            if (DateTime.UtcNow > until) throw new Exception("Native profile did not connect");
            await Task.Delay(50);
        }
        var windows = new FixtureWindows { ActiveWindow = shell };
        var host = new ContentControl();
        var shellWindow = new Window { Width = 900, Height = 700, Content = host, ShowInTaskbar = false };
        shellWindow.Show();
        var faults = new List<string>();
        var described = new List<string>();
        var geometry = new List<(string Page, double RowHeight, double IconAt, double NameAt)>();
        try {
            // The same two rows on each page — a folder and a file — so their tables
            // can be compared with each other rather than with whatever the live
            // profile happens to hold.
            foreach (var (name, create) in new (string, Func<Control>)[] {
                ("External Files", () => new Mo2ExternalFilesView((_, _, _) => Task.FromResult(
                    new Mo2ExternalScan("/fixture", 1, 2, [
                        new("folder/inner.esp", 42_275_072, "File"), new("loose.txt", 2048, "File")]))) {
                    ViewModel = new Mo2ExternalFilesPage(windows, shell.Profile) }),
                ("Data", () => new Mo2DataView((_, _) => Task.FromResult<Mo2DataEntry[]>([
                        new("folder", true, [], ""), new("loose.txt", false, ["Fixture"], "")])) {
                    ViewModel = new Mo2DataPage(windows, shell.Profile) }),
                ("Overwrite", () => new Mo2OverwriteView(_ => Task.FromResult<Mo2OverwriteFile[]>([
                        new("folder/inner.esp", 42_275_072), new("loose.txt", 2048)])) {
                    ViewModel = new Mo2OverwritePage(windows, shell.Profile) }),
            }) {
                var page = create();
                host.Content = page;
                for (var pass = 0; pass < 24; pass++) { await Task.Delay(50); shellWindow.UpdateLayout(); }

                // One picture of each, taken at the same size in the same window, so
                // the three can be compared with each other rather than with whatever
                // the panel they usually live in happened to be showing.
                if (Environment.GetEnvironmentVariable("MO2_FOLDER_PAGE_DIRECTORY") is { Length: > 0 } directory) {
                    Directory.CreateDirectory(directory);
                    using var shot = new Avalonia.Media.Imaging.RenderTargetBitmap(
                        new Avalonia.PixelSize(Math.Max(1, (int)shellWindow.ClientSize.Width), Math.Max(1, (int)shellWindow.ClientSize.Height)));
                    shot.Render(shellWindow);
                    shot.Save(Path.Combine(directory, $"folder-{name.Replace(" ", "-").ToLowerInvariant()}.png"));
                }

                var table = page.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
                if (table is null) { faults.Add($"{name} has no table"); continue; }
                // The file-tree styling, not the one NMA reserves for mod lists.
                if (!table.Classes.Contains("Compact"))
                    faults.Add($"{name} styles its table {string.Join("/", table.Classes.DefaultIfEmpty("nothing"))}, not Compact");
                if (table.Classes.Contains("MainListsStyling"))
                    faults.Add($"{name} styles its file table like a mod list");
                if (!table.ShowColumnHeaders) faults.Add($"{name} draws no column headings");

                // Search, status and a column toggle: what every one of these pages
                // needs, and what Overwrite was missing two of.
                var search = page.GetVisualDescendants().OfType<TextBox>()
                    .FirstOrDefault(x => x.Watermark is { Length: > 0 } && x.Watermark.StartsWith("Search", StringComparison.Ordinal));
                if (search is null) faults.Add($"{name} has no search box, or its watermark does not read as one");
                else if (search.MinWidth != 60) faults.Add($"{name} sizes its search box to {search.MinWidth}, not the shared 60");
                if (page.GetVisualDescendants().OfType<TextBlock>().All(x => x.Name?.EndsWith("Status", StringComparison.Ordinal) != true))
                    faults.Add($"{name} has no status line");
                if (page.GetVisualDescendants().OfType<Button>().All(x => x.Name is not ("ColumnToggleButton" or "ColumnsButton")))
                    faults.Add($"{name} offers no way to hide a column");

                // Actions are icons on the header line. Overwrite drew five labelled
                // buttons in a row of its own in the body.
                var group = page.GetVisualDescendants().OfType<Panel>().FirstOrDefault(x => x.Name == "PanelHeaderActions");
                var actions = group?.Children.OfType<Button>().Where(x => x.IsVisible).ToArray() ?? [];
                if (actions.Length == 0) faults.Add($"{name} puts no actions on its header line");
                foreach (var action in actions) {
                    if (action.Content is not UnifiedIcon)
                        faults.Add($"{name} has a header action that is not an icon: {action.Content}");
                    if (Math.Abs(action.Width - Mo2TableRow.ActionSize) > .5)
                        faults.Add($"{name} has a {action.Width:F0}px header action, not the shared {Mo2TableRow.ActionSize}px");
                }
                // Checkboxes and other toggles carry a label by nature; what this is
                // looking for is a plain command button sitting in the page body,
                // which is the shape Overwrite used and no other page did.
                var labelled = page.GetVisualDescendants().OfType<Button>()
                    .Where(x => x is not ToggleButton && x.Content is string text && text.Length > 0 &&
                                x.GetVisualAncestors().OfType<Panel>().All(a => a.Name != "PanelHeaderActions")).ToArray();
                if (labelled.Length > 0)
                    faults.Add($"{name} still draws {labelled.Length} labelled buttons in its body: " +
                        string.Join(", ", labelled.Select(x => x.Content)));

                // And the rows themselves, which is what the shared cell is for: the
                // icon and the name have to sit at the same place in all three, and
                // the rows have to be the same height.
                var row = table.GetVisualDescendants().OfType<TreeDataGridRow>().FirstOrDefault(x => x.Bounds.Height > 0);
                if (row is null) faults.Add($"{name} drew no rows from its fixture folder");
                else {
                    // Measured inside the shared cell, not from the edge of the row:
                    // External Files is a tree and draws its expander before the cell,
                    // which is the one thing about these three rows that differs on
                    // purpose. Everything inside the cell has to match.
                    var nameCell = row.GetVisualDescendants().OfType<Grid>().FirstOrDefault(x => x.Name == "FolderNameCell");
                    var rowIcon = nameCell?.GetVisualDescendants().OfType<UnifiedIcon>().FirstOrDefault() as Control;
                    var rowName = nameCell?.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault() as Control;
                    if (nameCell is null) faults.Add($"{name} does not draw its names through the shared cell");
                    else if (rowIcon is null) faults.Add($"{name} draws a row with no file or folder icon");
                    else if (rowName is null) faults.Add($"{name} draws a row with no name");
                    else geometry.Add((name, row.Bounds.Height,
                        rowIcon.TranslatePoint(default, nameCell)?.X ?? -1, rowName.TranslatePoint(default, nameCell)?.X ?? -1));
                }

                // Nothing in a row may be clipped. A size column narrow enough to cut
                // "40.32 MB" down to "40.32 …" is worse than no size at all, and which
                // page it happens on depends only on whether that column is the last.
                foreach (var clipped in table.GetVisualDescendants().OfType<TextBlock>()
                             .Where(x => x.Text is { Length: > 0 } && x.Bounds.Width > 0 &&
                                         x.Bounds.Width + .5 < x.DesiredSize.Width))
                    faults.Add($"{name} clips \"{clipped.Text}\" to {clipped.Bounds.Width:F0}px of " +
                        $"{clipped.DesiredSize.Width:F0}px");

                described.Add($"{name} ({actions.Length} icon actions)");
            }

            // And the pieces themselves behave the same wherever they are used.
            // Read from its children rather than its visual tree: the cell is built
            // here and never attached to a window, so it has no visual descendants.
            var cell = Mo2FolderPage.NameCell("thing", folder: true, tip: "a/b/thing");
            if (cell is not Grid { ColumnDefinitions.Count: 2 } grid ||
                grid.Children.OfType<UnifiedIcon>().FirstOrDefault() is not { } glyph ||
                grid.Children.OfType<TextBlock>().FirstOrDefault()?.Text != "thing")
                faults.Add("The shared name cell is no longer an icon beside a label");
            else if (glyph.Value?.Value.Value is not ProjektankerIcon { Value: "mdi-folder-outline" })
                faults.Add("A folder's shared name cell does not draw a folder");
            foreach (var (bytes, expected) in new (long, string)[] {
                (512, "512 B"), (2048, "2 KB"), (42_275_072, "40.32 MB"), (3_221_225_472, "3 GB") })
                if (Mo2FolderPage.SizeText(bytes) != expected)
                    faults.Add($"{bytes} bytes reads as {Mo2FolderPage.SizeText(bytes)}, not {expected}");

            foreach (var other in geometry.Skip(1)) {
                var first = geometry[0];
                if (Math.Abs(other.RowHeight - first.RowHeight) > 1.5)
                    faults.Add($"{other.Page} rows are {other.RowHeight:F0}px tall, {first.Page} rows {first.RowHeight:F0}px");
                if (Math.Abs(other.IconAt - first.IconAt) > 1.5)
                    faults.Add($"{other.Page} draws its icon {other.IconAt:F0}px into its name cell, {first.Page} {first.IconAt:F0}px");
                if (Math.Abs(other.NameAt - first.NameAt) > 1.5)
                    faults.Add($"{other.Page} draws its name {other.NameAt:F0}px into its name cell, {first.Page} {first.NameAt:F0}px");
            }

            if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
            Console.WriteLine($"PASS folder pages: {string.Join(", ", described)} share one file table, one search box, one " +
                "status line, one column toggle, one name cell with a folder or file icon, and one way of writing a size; " +
                $"none of them draws a labelled button in its body, and all three draw {geometry[0].RowHeight:F0}px rows with " +
                $"the icon {geometry[0].IconAt:F0}px and the name {geometry[0].NameAt:F0}px into that cell");
        } finally { shellWindow.Close(); }
    }
}
