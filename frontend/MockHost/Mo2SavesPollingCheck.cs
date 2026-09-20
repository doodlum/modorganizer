using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using System.Text.Json;

namespace Mo2.Frontend;

internal static class Mo2SavesPollingCheck
{
    private static async Task Wait(Func<bool> ready, string failure, int seconds = 15)
    {
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (!ready()) { if (DateTime.UtcNow > end) throw new Exception(failure); await Task.Delay(25); }
    }

    internal static async Task Run(Mo2LiveWorkspace shell, Window window, string? specification)
    {
        await Wait(() => shell.Profile.CanChangeOriginalUi, "Native host not ready");
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 600, Height = 400, Background = window.Background };
        Grid.SetRowSpan(host, root.RowDefinitions.Count); Grid.SetColumnSpan(host, root.ColumnDefinitions.Count);
        root.Children.Add(host);
        try {
            Mo2Save[] rows = Enumerable.Range(0, 250).Select(i => new Mo2Save($"Save {i}", $"save-{i:D3}.fos")).ToArray();
            var calls = 0; var activeReads = 0; var maxReads = 0; var fail = false;
            TaskCompletionSource<Mo2Save[]>? next = null;
            var view = new Mo2SavesView(async _ => {
                calls++; activeReads++; maxReads = Math.Max(maxReads, activeReads);
                try {
                    if (next is { } pending) { next = null; return await pending.Task; }
                    if (fail) throw new IOException("Simulated save read failure");
                    return rows.ToArray();
                } finally { activeReads--; }
            }, TimeSpan.FromMilliseconds(150)) {
                ViewModel = new Mo2SavesPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile)
            };
            host.Children.Add(view);
            TreeDataGrid? Table() => view.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
            await Wait(() => Table()?.Rows?.Count == 250, "Initial fixture saves missing");
            var table = Table()!;
            var search = view.GetVisualDescendants().OfType<TextBox>().Single();
            var status = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SavesStatus");
            table.RowSelection!.Select(new IndexPath(80));
            var initialSource = table.Source;
            await Wait(() => table.GetVisualDescendants().OfType<ScrollViewer>().Any(scroll => scroll.Extent.Height > 1000), "Tall Saves list did not lay out");
            var scroll = table.GetVisualDescendants().OfType<ScrollViewer>().MaxBy(scroll => scroll.Extent.Height)!;
            scroll.Offset = new Vector(0, 500);
            await Task.Delay(200);
            var offset = scroll.Offset;
            if (offset.Y < 300) throw new Exception("Saves scrolling fixture did not move");
            var count = calls;
            await Wait(() => calls >= count + 2, "No automatic save polls");
            if (!ReferenceEquals(table.Source, initialSource) || scroll.Offset != offset) throw new Exception("Unchanged polling rebuilt or scrolled the save table");
            var gate = next = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await Wait(() => activeReads == 1, "Delayed background read did not begin");
            count = calls; await Task.Delay(400);
            if (calls != count || maxReads != 1 || table.Rows?.Count != 250 || !ReferenceEquals(table.Source, initialSource))
                throw new Exception("Background reads overlap or blank/rebuild the visible list");
            table.ContextMenu!.Open(table);
            if (!table.ContextMenu.Items.OfType<MenuItem>().Any(item => item.Header as string == "Delete 1 save(s)" && item.IsEnabled))
                throw new Exception("Background polling disabled an otherwise available save action");
            table.ContextMenu.Close();
            search.Text = "save-080";
            await Wait(() => table.Rows?.Count == 1, "Search did not work during a background read");
            search.Text = "";
            await Wait(() => table.Rows?.Count == 250, "Cleared search did not restore visible saves");
            var filteredSource = table.Source;
            gate.SetResult(rows.ToArray());
            await Wait(() => activeReads == 0 && status.Text == "250 of 250 saves", "Background completion left a loading status");
            if (!ReferenceEquals(table.Source, filteredSource) || (table.RowSelection.SelectedItem as Mo2Save)?.File != "save-080.fos")
                throw new Exception("Background completion lost the filtered selection or rebuilt identical rows");
            rows = [new("New save", "new.fos"), ..rows];
            await Wait(() => table.Rows?.Count == 251, "New save did not appear automatically");
            if ((table.RowSelection.SelectedItem as Mo2Save)?.File != "save-080.fos") throw new Exception("New save shifted selected identity");
            var beforeError = table.Source;
            fail = true;
            await Wait(() => status.Text == "Simulated save read failure", "Background read failure missing");
            if (!ReferenceEquals(table.Source, beforeError)) throw new Exception("Read failure rebuilt the last known table");
            fail = false;
            await Wait(() => status.Text == "251 of 251 saves", "Background error did not recover");
            host.IsVisible = false; count = calls;
            await Task.Delay(400);
            if (calls != count) throw new Exception("Hidden retained Saves view kept polling");
            host.IsVisible = true;
            await Wait(() => calls > count, "Visible Saves view did not resume polling");
            gate = next = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await Wait(() => activeReads == 1, "Queued-refresh probe did not begin");
            host.IsVisible = false; count = calls;
            await view.Refresh();
            var oldRows = rows;
            rows = [.. rows, new("Queued refresh", "queued.fos")];
            gate.SetResult(oldRows);
            await Wait(() => calls == count + 1 && table.Rows?.Count == 252, "Explicit refresh was lost behind a background read");
            host.IsVisible = true;
            rows = rows.Where(save => save.File != "save-080.fos").ToArray();
            await Wait(() => table.Rows?.Count == 251 && table.RowSelection.SelectedItem is null, "Removed selected save remained selected");
            gate = next = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await Wait(() => activeReads == 1, "Detach probe did not begin");
            host.Children.Remove(view); count = calls;
            await Task.Delay(400);
            if (calls != count) throw new Exception("Detached save view kept polling");
            gate.SetResult([new("Stale", "stale.fos")]);
            await Wait(() => activeReads == 0, "Detached read did not complete");
            host.Children.Add(view);
            await Wait(() => Table()?.Rows?.Count == 251, "Reopened view did not read fresh saves");
            if (Table()!.Rows!.Any(row => row.Model is Mo2Save { File: "stale.fos" })) throw new Exception("Detached result leaked into the reopened view");
            rows = [];
            await Wait(() => Table()?.Rows?.Count == 0, "Saves did not become empty after external removal");
            var emptySource = Table()!.Source; count = calls;
            await Wait(() => calls >= count + 2, "Empty Saves view stopped polling");
            if (!ReferenceEquals(Table()!.Source, emptySource)) throw new Exception("Empty polls rebuilt the table");
            rows = [new("First save", "first.fos")];
            await Wait(() => Table()?.Rows?.Count == 1, "First save did not appear in an empty view");
            host.Children.Remove(view);
            Console.WriteLine("PASS Saves polling: automatic additions/removals, unchanged source/scroll retention, coalescing, search and action availability during reads, error recovery, hidden pause, detach/reopen");

            if (string.IsNullOrEmpty(specification)) return;
            if (File.Exists(specification) || File.Exists(specification + ".ready")) throw new Exception("Choose a new native polling specification");
            var nativeCalls = 0; var nativeReading = false; double longestRead = 0;
            var native = new Mo2SavesView(async target => {
                nativeCalls++; nativeReading = true;
                var timer = System.Diagnostics.Stopwatch.StartNew();
                try { return await shell.Profile.ReadSaves(target); }
                finally { nativeReading = false; longestRead = Math.Max(longestRead, timer.Elapsed.TotalMilliseconds); }
            }) {
                ViewModel = new Mo2SavesPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile)
            };
            host.Children.Add(native);
            TreeDataGrid? NativeTable() => native.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
            await Wait(() => NativeTable()?.Rows?.Count > 0, "Native saves did not load");
            var nativeTable = NativeTable()!;
            var originals = nativeTable.Rows!.Select(row => ((Mo2Save)row.Model!).File).ToArray();
            nativeTable.RowSelection!.Select(new IndexPath(0));
            var selected = ((Mo2Save)nativeTable.RowSelection.SelectedItem!).File;
            var source = nativeTable.Source;
            count = nativeCalls;
            await Wait(() => nativeCalls >= count + 2 && !nativeReading, "Native periodic reads did not run");
            if (!ReferenceEquals(source, nativeTable.Source)) throw new Exception("Identical native reads rebuilt Saves");
            await File.WriteAllTextAsync(specification + ".ready", "ready");
            Console.WriteLine("READY native Saves polling: create the disposable probe");
            await Wait(() => File.Exists(specification + ".prepared"), "No native save probe was created", 90);
            using var spec = JsonDocument.Parse(await File.ReadAllTextAsync(specification));
            var copies = spec.RootElement.GetProperty("selected").EnumerateArray().Select(value => value.GetString()!).ToArray();
            if (spec.RootElement.GetProperty("profile").GetString() != shell.Profile.CurrentTarget.ProfilePath)
                throw new Exception("Native polling probe belongs to another profile");
            await Wait(() => nativeTable.Rows!.Count == originals.Length + copies.Length && copies.All(name => nativeTable.Rows.Any(row => row.Model is Mo2Save save && save.File == name)),
                "Native additions did not appear without manual frontend refresh");
            if ((nativeTable.RowSelection.SelectedItem as Mo2Save)?.File != selected) throw new Exception("Native additions lost selected original save");
            await File.WriteAllTextAsync(specification + ".added", "added");
            Console.WriteLine("READY native Saves polling: remove only the disposable probe");
            await Wait(() => nativeTable.Rows!.Count == originals.Length && nativeTable.Rows.Select(row => ((Mo2Save)row.Model!).File).Order().SequenceEqual(originals.Order()),
                "Native removals did not appear without manual frontend refresh", 90);
            if ((nativeTable.RowSelection.SelectedItem as Mo2Save)?.File != selected) throw new Exception("Native removals lost selected original save");
            Console.WriteLine($"PASS native Saves polling: real file additions/removals appeared automatically, unchanged polls retained the table, original selection preserved; {nativeCalls} reads, slowest {longestRead:F0}ms");
        } finally { root.Children.Remove(host); }
    }
}
