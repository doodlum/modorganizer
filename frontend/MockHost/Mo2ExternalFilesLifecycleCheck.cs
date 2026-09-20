using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2ExternalFilesLifecycleCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        async Task Wait(Func<bool> ready, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(ready))] string condition = "") {
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("External Files lifecycle did not settle: " + condition);
                await Task.Delay(25);
            }
        }
        await Wait(() => shell.Profile.IsConnected && !shell.Profile.SelectingProfile);
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 600, Height = 400, IsHitTestVisible = false, Background = window.Background };
        Grid.SetColumnSpan(host, root.ColumnDefinitions.Count);
        Grid.SetRowSpan(host, root.RowDefinitions.Count);
        root.Children.Add(host);
        Mo2ExternalScan Scan(string path) => new("/fixture", 1, 2, [new(path, 1, "External file")]);
        try {
            foreach (var failure in new[] { false, true })
            foreach (var earlyReopen in new[] { false, true }) {
                var pending = new TaskCompletionSource<Mo2ExternalScan>(TaskCreationOptions.RunContinuationsAsynchronously);
                var reads = 0;
                var view = new Mo2ExternalFilesView((_, _, _) => ++reads == 1 ? pending.Task : Task.FromResult(Scan("fresh.esp"))) {
                    ViewModel = new Mo2ExternalFilesPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile)
                };
                host.Children.Add(view); await Wait(() => reads == 1);
                var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
                var reveal = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "RevealExternalFiles");
                host.Children.Remove(view);
                if (earlyReopen) host.Children.Add(view);
                if (failure) pending.SetException(new IOException("Stale scan failure"));
                else pending.SetResult(Scan("stale.esp"));
                if (!earlyReopen) {
                    await Wait(() => !view.IsReading);
                    if (reads != 1 || table.Rows?.Count > 0 || reveal.IsEnabled)
                        throw new Exception("Detached scan published rows or enabled reveal");
                    host.Children.Add(view);
                }
                await Wait(() => reads == 2 && !view.IsReading && table.Rows?.Count == 1);
                if (((Mo2ExternalFileNode)table.Rows![0].Model!).Path != "fresh.esp" || !reveal.IsEnabled ||
                    view.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Stale scan failure"))
                    throw new Exception("Reopened scan retained stale result or error");
                host.Children.Remove(view);
                if (table.Rows?.Count > 0 || reveal.IsEnabled)
                    throw new Exception("Closed External Files retained actionable scan results");
            }
            var keepRoot = true;
            var treeView = new Mo2ExternalFilesView((_, _, _) => Task.FromResult(new Mo2ExternalScan("/fixture", 1, 2,
                new Mo2ExternalFile[] { new("Data/NVSE/plugin.dll", 10, "External file"), new("root.dll", 20, "External file") }
                    .Where(file => keepRoot || file.Path != "root.dll").ToArray()))) {
                ViewModel = new Mo2ExternalFilesPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile)
            };
            host.Children.Add(treeView);
            await Wait(() => !treeView.IsReading && treeView.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Rows?.Count == 3);
            var treeTable = treeView.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            var search = treeView.GetVisualDescendants().OfType<TextBox>().Single(box => box.Name == "ExternalFilesSearch");
            var source = (HierarchicalTreeDataGridSource<Mo2ExternalFileNode>)treeTable.Source!;
            source.Collapse(new IndexPath(0));
            await Wait(() => treeTable.Rows!.Count == 2, "collapse top-level folder");
            search.Text = "plugin.dll";
            await Task.Delay(100);
            await Wait(() => treeTable.Rows!.Count == 3,
                "search ancestor rows (observed " + treeTable.Rows!.Count + ": " + string.Join(", ", treeTable.Rows.Select(row => ((Mo2ExternalFileNode)row.Model!).Path)) + ")");
            if (((Mo2ExternalFileNode)treeTable.Rows![2].Model!).Path != "Data/NVSE/plugin.dll")
                throw new Exception("Search did not expose matching nested file");
            search.Text = "";
            await Wait(() => treeTable.Rows!.Count == 2, "restore collapsed folder after clearing search");
            treeTable.RowSelection!.Select(new IndexPath(1));
            await treeView.Refresh();
            if ((treeTable.RowSelection?.SelectedItem as Mo2ExternalFileNode)?.Path != "root.dll" || treeTable.Rows!.Count != 2)
                throw new Exception("Rescan lost selected file or collapsed folder");
            keepRoot = false;
            await treeView.Refresh();
            if (treeTable.RowSelection?.SelectedItem is not null)
                throw new Exception("Removed file selection moved to an unrelated entry");
            ((HierarchicalTreeDataGridSource<Mo2ExternalFileNode>)treeTable.Source!).ExpandAll();
            foreach (var width in new[] { 550.0, 340.0, 250.0, 550.0 }) {
                host.Width = width;
                await Wait(() => Math.Abs(treeTable.Bounds.Width - (width - 2 * Mo2PanelChrome.PaddingFor(treeView.Bounds.Height).Left)) < 2,
                    "Table did not reach requested panel width");
                await Wait(() => treeTable.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Any(x =>
                    x.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == Mo2ExternalFilesView.ColumnHeaders[0]) &&
                    Math.Abs(x.Bounds.Width - ((HierarchicalTreeDataGridSource<Mo2ExternalFileNode>)treeTable.Source!).Columns[0].ActualWidth) < 2),
                    "Name header layout did not catch up to column sizing");
                var name = treeTable.GetVisualDescendants().OfType<TreeDataGridColumnHeader>()
                    .Single(x => x.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == Mo2ExternalFilesView.ColumnHeaders[0]));
                if (name.Bounds.Width < 120)
                    throw new Exception($"External Files name column is only {name.Bounds.Width:F0}px at panel width {width}, table {treeTable.Bounds.Width}; " +
                        string.Join("; ", treeTable.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Select(x =>
                            $"{string.Join('/', x.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text))}: x={x.TranslatePoint(default, treeTable)?.X}, width={x.Bounds.Width}")));
                if (width == 550) {
                    foreach (var title in Mo2ExternalFilesView.ColumnHeaders.Skip(1))
                        await Wait(() => treeTable.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Any(x => x.Bounds.Width >= 100 &&
                            x.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == title)),
                            "Metadata did not return when the panel widened: " + title);
                }
                if (Environment.GetEnvironmentVariable("MO2_EXTERNAL_FIT_SCREENSHOTS") is { } directory) {
                    Directory.CreateDirectory(directory);
                    using var shot = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
                    shot.Render(window); shot.Save(Path.Combine(directory, $"external-{width:F0}.png"));
                }
            }
            // Column preferences remove columns from the source. Fitting must
            // follow their identities, even after both optional columns vanish.
            var current = (HierarchicalTreeDataGridSource<Mo2ExternalFileNode>)treeTable.Source!;
            current.Columns.SetColumnWidth(1, new GridLength(150));
            await Task.Delay(250); window.UpdateLayout();
            var resized = treeTable.GetVisualDescendants().OfType<TreeDataGridColumnHeader>().Single(x =>
                x.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == Mo2ExternalFilesView.ColumnHeaders[1]));
            if (Math.Abs(resized.Bounds.Width - 150) > 2)
                throw new Exception("Routine layout undid manual metadata column sizing");
            var protectedName = new Mo2ColumnToggle("fixture-tree-name-" + Guid.NewGuid().ToString("N"), () => { },
                hiddenByDefault: [Mo2FolderPage.NameHeader], pinned: [Mo2FolderPage.NameHeader]);
            protectedName.Apply(current.Columns);
            if (protectedName.Hidden.Contains(Mo2FolderPage.NameHeader) || protectedName.CanHide(Mo2FolderPage.NameHeader))
                throw new Exception("Tree Name column is not protected from hidden-column preferences");
            while (current.Columns.Count > 1) current.Columns.RemoveAt(1);
            host.Width = 340;
            await Task.Delay(250); window.UpdateLayout();
            if (current.Columns.Count != 1 || treeTable.Bounds.Width <= 0)
                throw new Exception("Hidden metadata columns reappeared during resize");
            host.Width = 550;
            host.IsHitTestVisible = true;
            await Mo2ToolbarSearchCheck.Run(window, treeView);
            host.Children.Remove(treeView);
            Console.WriteLine("PASS External Files fit: readable names at 550/340/250px; metadata returns when widened; manual sizing retained and removed columns stay removed");
            Console.WriteLine("PASS External Files rescan: selection and collapsed folders retained, missing selection cleared");
            Console.WriteLine("PASS External Files tree UI: folder collapse, search expands ancestors, clearing search restores collapse");
            Console.WriteLine("PASS External Files lifecycle: late success/failure discarded, reopen reads fresh, closing clears rows and disables reveal");
        } finally { root.Children.Remove(host); }
    }
}
