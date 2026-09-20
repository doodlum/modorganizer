using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2DataSearchCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell)
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
        var reads = new List<string>();
        var stale = new TaskCompletionSource<Mo2DataEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expanding = new TaskCompletionSource<Mo2DataEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var hidden = new TaskCompletionSource<Mo2DataEntry[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var model = new Mo2DataPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile);
        var view = new Mo2DataView((_, path) => {
            reads.Add(path);
            if (path == "Outer" && reads.Count(x => x == path) == 1) return stale.Task;
            if (path == "Outer" && reads.Count(x => x == path) == 3) return expanding.Task;
            return path switch {
                "" => Task.FromResult<Mo2DataEntry[]>([new("Outer", true, [], ""), new("Other", true, [], ""),
                    new("Hidden.mohidden", true, [], ""), new("root.txt", false, ["Game"], "")]),
                "Outer" => Task.FromResult<Mo2DataEntry[]>([new("Inner", true, [], ""), new("ArchiveOnly", true, [], ""), new("plain.txt", false, ["A"], "")]),
                "Outer/Inner" => Task.FromResult<Mo2DataEntry[]>([new("needle.esp", false, ["A", "B"], "")]),
                "Outer/ArchiveOnly" => Task.FromResult<Mo2DataEntry[]>([new("texture.dds", false, ["A"], "assets.bsa")]),
                "Other" => Task.FromResult<Mo2DataEntry[]>([]),
                "Hidden.mohidden" => hidden.Task,
                _ => throw new Exception("Unexpected traversal: " + path),
            };
        }) { ViewModel = model };
        var host = new ContentControl { Content = view };
        var window = new Window { Width = 600, Height = 650, Content = host, ShowInTaskbar = false };
        window.Show();
        try {
            TreeDataGrid Table() => view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            await Wait(() => view.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Rows?.Count == 3, "Initial root unavailable");
            var filter = view.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "DataQtFilter");
            async Task Filter(string text) {
                var edited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<TextChangedEventArgs> changed = (_, _) => edited.TrySetResult();
                filter.TextChanged += changed;
                try { filter.Text = text; await edited.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
                finally { filter.TextChanged -= changed; }
            }
            var status = view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "DataStatus");
            bool Settled() => status.Text?.EndsWith(" visible entries") == true;
            await Filter("old");
            await Wait(() => reads.Contains("Outer"), "Search did not read unopened branch");
            await Filter("");
            stale.SetResult([new("NeverRead", true, [], "")]);
            await Task.Delay(250);
            if (Table().Rows?.Count != 3 || reads.Any(x => x.Contains("NeverRead")) || !Settled())
                throw new Exception("Clearing search retained or continued stale traversal");
            await Filter("needle");
            await Wait(() => Settled() && Table().Rows?.Count == 3 &&
                Table().Rows[2].Model is Mo2DataEntry { Name: "needle.esp" }, "Unopened descendant or its ancestors missing");
            if (!reads.Contains("Outer/Inner") || reads.Contains("Hidden.mohidden"))
                throw new Exception("Search traversal ignored descendant/hidden-folder rules");
            var count = reads.Count;
            await Filter("texture");
            await Wait(() => Settled() && Table().Rows?.Count == 3 &&
                Table().Rows[2].Model is Mo2DataEntry { Name: "texture.dds" }, "Second search failed");
            if (reads.Count != count) throw new Exception("Changing query re-read indexed folders");
            await Filter("");
            await Wait(() => Table().Rows?.Count == 3, "Clearing query did not restore collapsed roots");
            if (model.ExpandedDirectories.Count != 0) throw new Exception("Search changed saved expansion state");
            // Drop the cache so Expand All must read unopened nested folders.
            var rootsBefore = reads.Count(x => x == "");
            view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DataRefreshButton")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Wait(() => reads.Count(x => x == "") > rootsBefore && Settled(), "Refresh did not reload roots");
            var pendingExpansion = view.SetAllExpanded(true);
            await Wait(() => reads.Count(x => x == "Outer") == 3, "Expand All did not start reading an unopened folder");
            await view.SetAllExpanded(false);
            expanding.SetResult([new("NeverRead", true, [], "")]);
            await pendingExpansion;
            if (Table().Rows?.Count != 3 || reads.Any(x => x.Contains("NeverRead")))
                throw new Exception("Collapse All retained or continued a pending expansion");
            var nestedBefore = reads.Count(x => x == "Outer/Inner");
            await view.SetAllExpanded(true);
            if (Table().Rows?.Count != 8 || reads.Count(x => x == "Outer/Inner") != nestedBefore + 1)
                throw new Exception("Expand All did not load and display unopened descendants");
            await view.SetAllExpanded(false);
            if (Table().Rows?.Count != 3 || model.ExpandedDirectories.Count != 0)
                throw new Exception("Collapse All did not restore collapsed roots");
            await Filter("needle");
            await Wait(() => Settled() && Table().Rows?.Count == 3, "Search after Collapse All failed");
            await view.SetAllExpanded(false);
            if (Table().Rows?.Count != 1) throw new Exception("Collapse All was overridden by active search: rows=" + Table().Rows?.Count + "; saved=" + string.Join(",", model.ExpandedDirectories));
            await view.SetAllExpanded(true);
            if (Table().Rows?.Count != 3) throw new Exception("Expand All lost the filtered descendant");
            await view.SetAllExpanded(false);
            await Filter("");
            await Wait(() => Settled() && Table().Rows?.Count == 3, "Clearing filter after tree actions failed");
            Console.WriteLine("PASS Data tree menu actions: Expand All reads unopened descendants; Collapse All clears expansion, including while searching");
            var conflicts = view.GetVisualDescendants().OfType<CheckBox>().Single(x => x.Name == "DataConflictsOnly");
            conflicts.IsChecked = true;
            await Wait(() => Settled() && Table().Rows?.Count == 1, "Conflicts filter did not prune empty branches");
            var outer = Table().Source!.Items.OfType<Mo2DataEntry>().Single();
            if (outer.Name != "Outer" || outer.Children.Count != 1 || outer.Children[0].Name != "Inner")
                throw new Exception("Nonconflicting subtree survived pruning");
            conflicts.IsChecked = false;
            var archives = view.GetVisualDescendants().OfType<CheckBox>().Single(x => x.Name == "DataFromArchives");
            archives.IsChecked = false;
            await Wait(() => Settled() && Table().Rows?.Count == 2, "Archive-only/empty folders were not pruned");
            outer = Table().Source!.Items.OfType<Mo2DataEntry>().Single(x => x.Name == "Outer");
            if (outer.Children.Any(x => x.Name == "ArchiveOnly")) throw new Exception("Archive-only folder survived pruning");
            archives.IsChecked = true;
            await Filter("needle");
            view.GetVisualDescendants().OfType<CheckBox>().Single(x => x.Name == "DataHiddenFiles").IsChecked = true;
            await Wait(() => reads.Contains("Hidden.mohidden"), "Hidden folder was never searched when enabled");
            var table = Table(); host.Content = null;
            hidden.SetResult([new("needle-hidden.esp", false, ["Hidden"], "")]);
            await Task.Delay(250);
            if (table.Rows!.Any(x => x.Model is Mo2DataEntry { Name: "needle-hidden.esp" }))
                throw new Exception("Detached search published a late result");
            Console.WriteLine("PASS Data search: unopened descendants/ancestors, query cancellation, index reuse, expansion restoration, conflict/archive pruning, hidden folders and detached late results");
        } finally { stale.TrySetResult([]); expanding.TrySetResult([]); hidden.TrySetResult([]); window.Close(); }
    }
    internal static async Task RunNative(Mo2LiveWorkspace shell)
    {
        // The optional dispatcher diagnostic starts sampling after ten seconds.
        // Let it begin before measuring search when both checks are requested.
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MO2_UI_LATENCY_REPORT")))
            await Task.Delay(TimeSpan.FromSeconds(12));
        var target = shell.Profile.CurrentTarget;
        var root = await shell.Profile.ReadDataDirectory(target, "");
        var folders = new Queue<string>(root.Where(x => x.Directory && !x.Name.EndsWith(".mohidden", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Name.Equals("NVSE", StringComparison.OrdinalIgnoreCase) || x.Name.Equals("SKSE", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Name));
        Mo2DataEntry? file = null;
        string? expected = null;
        for (var checkedFolders = 0; checkedFolders < 64 && folders.TryDequeue(out var folder); checkedFolders++) {
            var inside = await shell.Profile.ReadDataDirectory(target, folder);
            file = inside.FirstOrDefault(x => !x.Directory && !x.Name.EndsWith(".mohidden", StringComparison.OrdinalIgnoreCase));
            if (file is not null) { expected = folder + "/" + file.Name; break; }
            foreach (var child in inside.Where(x => x.Directory && !x.Name.EndsWith(".mohidden", StringComparison.OrdinalIgnoreCase)))
                folders.Enqueue(folder + "/" + child.Name);
        }
        if (file is null || expected is null) throw new Exception("No visible nested native file found for the search check");
        var reads = 0;
        var renders = new List<double>();
        var view = new Mo2DataView(async (current, path) => {
            reads++;
            return await shell.Profile.ReadDataDirectory(current, path);
        }) { ViewModel = new Mo2DataPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) };
        view.RenderMeasured = elapsed => renders.Add(elapsed);
        var window = new Window { Width = 650, Height = 650, Content = view, ShowInTaskbar = false };
        window.Show();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        double last = 0, maxGap = 0;
        timer.Tick += (_, _) => { var now = watch.Elapsed.TotalMilliseconds; maxGap = Math.Max(maxGap, now - last); last = now; };
        try {
            var deadline = DateTime.UtcNow.AddSeconds(120);
            while (view.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Rows?.Count is not > 0) {
                if (DateTime.UtcNow > deadline) throw new Exception("Native search root unavailable");
                await Task.Delay(25);
            }
            var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            var status = view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "DataStatus");
            renders.Clear(); watch.Restart(); timer.Start();
            var field = view.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "DataQtFilter");
            var edited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            field.TextChanged += (_, _) => edited.TrySetResult();
            field.Text = file.Name;
            await edited.Task.WaitAsync(TimeSpan.FromSeconds(5));
            while (status.Text?.EndsWith(" visible entries") != true) {
                if (DateTime.UtcNow > deadline) throw new Exception("Native whole-tree search exceeded 120 seconds");
                if (status.Text?.StartsWith("Search incomplete:") == true) throw new Exception("Native search reported an incomplete scan");
                await Task.Delay(25);
            }
            timer.Stop();
            var coldRenderCount = renders.Count;
            var coldRenderTotal = renders.Sum();
            var coldRenderMax = renders.DefaultIfEmpty().Max();
            if (!table.Rows!.Any(x => x.Model is Mo2DataEntry entry && entry.RelativePath == expected))
                throw new Exception($"Native search missed the known unopened file after {reads} reads, {watch.Elapsed.TotalSeconds:F2}s, status={status.Text}");
            if (shell.Profile.CurrentTarget != target) throw new Exception("Native search crossed profile targets");
            var coldMilliseconds = watch.Elapsed.TotalMilliseconds;
            var indexedReads = reads;
            async Task<double> ChangeFilter(string query) {
                var changed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                EventHandler<TextChangedEventArgs> handler = (_, _) => changed.TrySetResult();
                field.TextChanged += handler;
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                try {
                    field.Text = query;
                    await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    var until = DateTime.UtcNow.AddSeconds(30);
                    while (status.Text?.EndsWith(" visible entries") != true) {
                        if (DateTime.UtcNow > until) throw new Exception("Indexed query did not settle");
                        await Task.Delay(25);
                    }
                    return elapsed.Elapsed.TotalMilliseconds;
                } finally { field.TextChanged -= handler; }
            }
            var clearedMilliseconds = await ChangeFilter("");
            var visibleRoots = root.Count(x => x.MatchesVisibility(false, true, false));
            if (table.Rows!.Count != visibleRoots) throw new Exception("Clearing native search did not restore collapsed roots");
            var warmMilliseconds = await ChangeFilter(file.Name.Length > 1 ? file.Name[..^1] : file.Name);
            if (!table.Rows!.Any(x => x.Model is Mo2DataEntry entry && entry.RelativePath == expected))
                throw new Exception("Indexed query lost the known native file");
            if (reads != indexedReads) throw new Exception("Indexed query reread native folders");
            var report = new { game = shell.Profile.GameName, directoryReads = reads, elapsedMilliseconds = coldMilliseconds,
                coldRenderCount, coldRenderTotal, coldRenderMax,
                maximumUiTimerGapMilliseconds = maxGap, clearMilliseconds = clearedMilliseconds,
                indexedQueryMilliseconds = warmMilliseconds, indexedQueryAdditionalReads = reads - indexedReads, expectedPathFound = true };
            var path = Environment.GetEnvironmentVariable("MO2_DATA_SEARCH_REPORT") ?? Path.Combine(AppContext.BaseDirectory, "../../../../artifacts/data-search-native.json");
            File.WriteAllText(Path.GetFullPath(path), System.Text.Json.JsonSerializer.Serialize(report));
            Console.WriteLine($"DATA render timing: {coldRenderCount} source rebuilds, {coldRenderTotal:F1}ms total, {coldRenderMax:F1}ms maximum synchronous work");
            Console.WriteLine($"PASS Data search native: known unopened file found; {reads} directory reads in {coldMilliseconds / 1000:F2}s; maximum 50ms UI timer gap {maxGap:F1}ms; clear {clearedMilliseconds:F1}ms; indexed query {warmMilliseconds:F1}ms with no additional reads");
        } finally { timer.Stop(); window.Close(); }
    }

}
