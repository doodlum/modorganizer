using Avalonia;
using Avalonia.Input;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2DataTreeCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        async Task Wait(Func<bool> ready, string message) {
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception(message);
                await Task.Delay(25);
            }
        }
        await Wait(() => shell.Profile.IsConnected && !shell.Profile.SelectingProfile, "Profile unavailable");
        using var turn = await Mo2CheckTurn.Take();
        var host = new Grid();
        var preview = new Window { Width = 600, Height = 650, Content = host, ShowInTaskbar = false };
        preview.Show();
        var reads = new List<string>();
        var pending = new TaskCompletionSource<Mo2DataEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new Mo2DataPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile);
        var view = new Mo2DataView((_, path) => {
            reads.Add(path);
            return path switch {
                "" => Task.FromResult<Mo2DataEntry[]>([new("Tools", true, [], ""), new("Textures", true, [], ""), new("tool.exe", false, ["Root"], "")]),
                "Tools" => Task.FromResult<Mo2DataEntry[]>([new("tool.exe", false, ["Nested"], "")]),
                "Textures" => pending.Task,
                _ => throw new Exception("Unexpected read " + path),
            };
        }) { ViewModel = model };
        try {
            host.Children.Add(view);
            TreeDataGrid Table() => view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            await Wait(() => view.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Rows?.Count == 3, "Root did not load");
            if (reads.Count != 1) throw new Exception("Collapsed branches were eagerly loaded");
            ((HierarchicalTreeDataGridSource<Mo2DataEntry>)Table().Source!).Expand(new IndexPath(0));
            try { await Wait(() => Table().Rows?.Count == 4 && Table().Rows[1].Model is Mo2DataEntry { IsLoading: false }, "Expansion did not retain root siblings and insert child"); }
            catch (Exception error) { throw new Exception(error.Message + "; reads=" + string.Join(",", reads) +
                "; rows=" + Table().Rows?.Count + "; expanded=" + string.Join(",", model.ExpandedDirectories)); }
            var nested = (Mo2DataEntry)Table().Rows![1].Model!;
            if (nested.RelativePath != "Tools/tool.exe" || nested.ParentPath != "Tools")
                throw new Exception("Nested action path lost its parent");
            // Right-clicking visible row 1 must select Tools/tool.exe, not root 1.
            await Wait(() => Table().GetVisualDescendants().OfType<TreeDataGridRow>().Any(x => x.RowIndex == 1), "Nested visual row not drawn");
            var row = Table().GetVisualDescendants().OfType<TreeDataGridRow>().First(x => x.RowIndex == 1);
            var pointer = new Pointer(0, PointerType.Mouse, true);
            var position = row.TranslatePoint(new Point(40, row.Bounds.Height / 2), preview) ?? default;
            row.RaiseEvent(new PointerPressedEventArgs(row, pointer, preview, position, 0,
                new PointerPointProperties(RawInputModifiers.RightMouseButton, PointerUpdateKind.RightButtonPressed), KeyModifiers.None));
            row.RaiseEvent(new PointerReleasedEventArgs(row, pointer, preview, position, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.RightButtonReleased), KeyModifiers.None, MouseButton.Right));
            Table().ContextMenu?.Close();
            if ((Table().RowSelection?.SelectedItem as Mo2DataEntry)?.RelativePath != "Tools/tool.exe")
                throw new Exception("Row menu selection targeted a root sibling instead of the nested file");
            // Hide metadata the same way column preferences do, without writing
            // preferences. Layout must not keep using the old column indices.
            var columns = ((HierarchicalTreeDataGridSource<Mo2DataEntry>)Table().Source!).Columns;
            while (columns.Count > 1) columns.RemoveAt(1);
            preview.Width = 520;
            await Task.Delay(250); preview.UpdateLayout();
            if (columns.Count != 1) throw new Exception("Data resize resurrected hidden metadata columns");
            preview.Width = 600;
            var refresh = view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DataRefreshButton");
            refresh.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            try { await Wait(() => reads.Count(x => x == "Tools") == 2 && Table().Rows?.Count == 4, "Refresh did not restore expanded branch"); }
            catch (Exception error) { throw new Exception(error.Message + "; reads=" + string.Join(",", reads) +
                "; rows=" + Table().Rows?.Count + "; expanded=" + string.Join(",", model.ExpandedDirectories)); }
            if ((Table().RowSelection?.SelectedItem as Mo2DataEntry)?.RelativePath != "Tools/tool.exe")
                throw new Exception("Refresh lost nested selection");
            var filter = view.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "DataQtFilter");
            filter.Text = "tool.exe";
            await Wait(() => Table().Rows?.Count == 3 && Table().Rows[0].Model is Mo2DataEntry { Name: "Tools" },
                "Filtering hid the ancestor of a matching loaded file");
            filter.Text = "";
            await Wait(() => Table().Rows?.Count == 4, "Clearing filter did not restore siblings");
            ((HierarchicalTreeDataGridSource<Mo2DataEntry>)Table().Source!).Collapse(new IndexPath(0));
            await Wait(() => Table().Rows?.Count == 3, "Collapse left children visible");
            ((HierarchicalTreeDataGridSource<Mo2DataEntry>)Table().Source!).Expand(new IndexPath(0));
            await Wait(() => Table().Rows?.Count == 4, "Cached branch did not reopen");
            if (reads.Count(x => x == "Tools") != 2) throw new Exception("Reopening cached branch repeated read");
            ((HierarchicalTreeDataGridSource<Mo2DataEntry>)Table().Source!).Expand(new IndexPath(1));
            await Wait(() => reads.Contains("Textures"), "Second branch read never started");
            host.Children.Remove(view);
            pending.SetResult([new("stale.dds", false, ["Stale"], "")]);
            await Task.Delay(150);
            if (Table().Rows!.Any(x => x.Model is Mo2DataEntry { Name: "stale.dds" }))
                throw new Exception("Detached child read published into tree");
            Console.WriteLine("PASS Data tree: lazy expansion keeps siblings, nested action paths, refresh restores expansion/selection, cached reopen, detached child result discarded");
            var rootRows = 100;
            var longView = new Mo2DataView(async (_, path) => {
                await Task.Delay(60);
                return path == "" ? Enumerable.Range(0, rootRows).Select(i =>
                    new Mo2DataEntry(i == 20 ? "Branch" : $"file-{i:000}.txt", i == 20, ["Fixture"], "")).ToArray()
                    : [new("child.txt", false, ["Fixture"], "")];
            }) { ViewModel = new Mo2DataPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) };
            host.Children.Add(longView);
            await Wait(() => longView.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Rows?.Count == 100, "Long tree root did not load");
            var longTable = longView.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            await Task.Delay(150); preview.UpdateLayout();
            var scroll = longTable.GetVisualDescendants().OfType<ScrollViewer>().First(x => x.Extent.Height > x.Viewport.Height);
            scroll.Offset = new Avalonia.Vector(0, 350);
            await Task.Delay(100); preview.UpdateLayout();
            var offset = scroll.Offset.Y;
            if (offset < 300) throw new Exception("Long tree fixture did not scroll");
            ((HierarchicalTreeDataGridSource<Mo2DataEntry>)longTable.Source!).Expand(new IndexPath(20));
            await Wait(() => longTable.Rows?.Count == 101 && longTable.Rows[21].Model is Mo2DataEntry { Name: "child.txt" }, "Scrolled branch did not load");
            await Task.Delay(250); preview.UpdateLayout();
            if (Math.Abs(scroll.Offset.Y - offset) > 2)
                throw new Exception($"Expanding a scrolled branch jumped from {offset} to {scroll.Offset.Y}");
            longView.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DataRefreshButton")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Wait(() => longTable.Rows?.Count == 101, "Long tree refresh did not finish");
            await Task.Delay(250); preview.UpdateLayout();
            if (Math.Abs(scroll.Offset.Y - offset) > 2)
                throw new Exception($"Refreshing a scrolled tree jumped from {offset} to {scroll.Offset.Y}");
            rootRows = 5;
            longView.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DataRefreshButton")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Wait(() => longTable.Rows?.Count == 5, "Shortened tree did not refresh");
            await Task.Delay(250); preview.UpdateLayout();
            if (scroll.Offset.Y > 1) throw new Exception("Refresh retained an invalid offset beyond the shorter tree");
            host.Children.Remove(longView);
            Console.WriteLine("PASS Data tree scroll: expansion/refresh retain viewport in a 100-row tree; shorter results clamp the offset");
            var native = new Mo2DataView { ViewModel = new Mo2DataPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) };
            host.Children.Add(native);
            await Wait(() => native.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Rows?.Count > 0, "Native Data root unavailable");
            var nativeTable = native.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            var roots = nativeTable.Source!.Items.OfType<Mo2DataEntry>().ToArray();
            var folderIndex = Array.FindIndex(roots, x => x.Directory && x.Name.Equals("nvse", StringComparison.OrdinalIgnoreCase));
            if (folderIndex < 0) folderIndex = Array.FindIndex(roots, x => x.Directory);
            if (folderIndex < 0) throw new Exception("No native Data folder to expand");
            var folder = roots[folderIndex];
            var expected = await shell.Profile.ReadDataDirectory(shell.Profile.CurrentTarget, folder.Name);
            ((HierarchicalTreeDataGridSource<Mo2DataEntry>)nativeTable.Source).Expand(new IndexPath(folderIndex));
            await Wait(() => nativeTable.Rows!.All(x => x.Model is not Mo2DataEntry { IsLoading: true }) &&
                nativeTable.Rows.Count == roots.Length + expected.Length, "Native children differ from bridge listing");
            var actual = nativeTable.Rows!.Select(x => x.Model).OfType<Mo2DataEntry>().Where(x => x.ParentPath == folder.Name).ToArray();
            if (!actual.Select(x => (x.Name, x.Directory, x.Source, x.Archive)).SequenceEqual(expected.Select(x => (x.Name, x.Directory, x.Source, x.Archive))))
                throw new Exception("Native expanded files differ from MO2 directory readback");
            await Task.Delay(150); preview.UpdateLayout();
            if (Environment.GetEnvironmentVariable("MO2_DATA_TREE_SCREENSHOT") is { Length: > 0 } capture) {
                using var shot = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize((int)preview.ClientSize.Width, (int)preview.ClientSize.Height));
                shot.Render(preview); shot.Save(capture);
            }
            Console.WriteLine("PASS Data tree native: expanded folder matches MO2 readback and root siblings remain visible; no file actions triggered");

        } finally { host.Children.Clear(); preview.Close(); }
    }
}
