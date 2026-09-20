using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Security.Cryptography;
using System.Text.Json;

namespace Mo2.Frontend;

// Only the named, hash-verified disposable copies can enter this check's selection.
// The native confirmation remains open for independent inspection and acceptance.
internal static class Mo2SaveDeleteOutcomeCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window, string specification)
    {
        using var spec = JsonDocument.Parse(await File.ReadAllTextAsync(specification));
        var data = spec.RootElement;
        var rootPath = data.GetProperty("root").GetString()!;
        var prefix = data.GetProperty("prefix").GetString()!;
        var selected = data.GetProperty("selected").EnumerateArray().Select(value => value.GetString()!).ToArray();
        var files = data.GetProperty("files").EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetString()!);
        var originals = data.GetProperty("originals").EnumerateObject().ToDictionary(item => item.Name, item => item.Value.GetString()!);
        var physical = Environment.GetEnvironmentVariable("MO2_VERIFY_SAVE_DELETE_KEYBOARD") == "1";
        var routedDelete = Environment.GetEnvironmentVariable("MO2_VERIFY_SAVE_DELETE_ROUTED") == "1";
        static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        if (!prefix.StartsWith("mo2-save-delete-probe-", StringComparison.Ordinal) || selected.Length != 2 || selected.Distinct().Count() != 2 ||
            files.Any(file => Path.GetFileName(file.Key) != file.Key || !file.Key.StartsWith(prefix + "-", StringComparison.Ordinal) ||
                !File.Exists(Path.Combine(rootPath, file.Key)) || Hash(Path.Combine(rootPath, file.Key)) != file.Value) ||
            selected.Any(name => !files.ContainsKey(name)) || files.Keys.Intersect(originals.Keys).Any())
            throw new Exception("Bulk-delete check requires two unchanged disposable save copies");
        async Task Wait(Func<bool> ready, string failure, int seconds = 45) {
            var deadline = DateTime.UtcNow.AddSeconds(seconds);
            while (!ready()) { if (DateTime.UtcNow > deadline) throw new Exception(failure); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.IsConnected && !shell.Profile.SelectingProfile, "Native profile unavailable");
        var profile = shell.Profile;
        var target = profile.CurrentTarget;
        if (data.GetProperty("profile").GetString() != target.ProfilePath) throw new Exception("Probe profile mismatch");
        var saves = await profile.ReadSaves(target);
        if (selected.Any(name => saves.Count(save => save.File == name) != 1)) throw new Exception("Native disposable saves missing or ambiguous");
        var view = new Mo2SavesView { ViewModel = new Mo2SavesPage(new FixtureWindows { ActiveWindow = shell }, profile) };
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 650, Height = 450, Background = window.Background };
        Grid.SetRowSpan(host, root.RowDefinitions.Count); Grid.SetColumnSpan(host, root.ColumnDefinitions.Count);
        root.Children.Add(host); host.Children.Add(view);
        try {
            TreeDataGrid? Table() => view.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
            await Wait(() => Table()?.Rows?.Count == saves.Length, "Saves table did not populate");
            var table = Table()!;
            table.RowSelection!.Clear();
            var previousSelection = "";
            bool SelectionMatches() {
                var current = table.RowSelection.SelectedItems.OfType<Mo2Save>().Select(save => save.File).ToArray();
                var diagnostic = $"{current.Length} selected, {current.Count(selected.Contains)} disposable";
                if (physical && diagnostic != previousSelection) {
                    Console.WriteLine("TRACE Saves pointer: " + diagnostic); previousSelection = diagnostic;
                }
                return current.Order().SequenceEqual(selected.Order());
            }
            if (physical) {
                table.AddHandler(InputElement.PointerPressedEvent, (_, args) => {
                    var control = args.Source as Control;
                    var row = control?.GetVisualAncestors().OfType<TreeDataGridRow>().FirstOrDefault();
                    Console.WriteLine($"TRACE Saves pointer event: {args.Source?.GetType().Name}, row={row?.RowIndex}, position={args.GetPosition(table)}, modifiers={args.KeyModifiers}, handled={args.Handled}");
                },
                    RoutingStrategies.Tunnel, handledEventsToo: true);
                await Wait(() => table.GetVisualDescendants().OfType<TreeDataGridRow>().Count(row => row.Bounds.Height > 0) >= saves.Length,
                    "Save rows not laid out for pointer input");
                PixelPoint[] Points() => selected.Select(name => {
                    var index = Enumerable.Range(0, table.Rows!.Count).Single(i => table.Rows[i].Model is Mo2Save save && save.File == name);
                    var row = table.GetVisualDescendants().OfType<TreeDataGridRow>().Single(row => row.RowIndex == index);
                    return row.PointToScreen(new Point(Math.Min(row.Bounds.Width / 2, 120), row.Bounds.Height / 2));
                }).ToArray();
                // Header compaction and the initial window layout can move rows
                // after they first have bounds. Publish only settled coordinates.
                await Task.Delay(600); window.UpdateLayout();
                var points = Points();
                var settled = false;
                for (var attempt = 0; attempt < 20 && !settled; attempt++) {
                    await Task.Delay(250); window.UpdateLayout();
                    var current = Points(); settled = points.SequenceEqual(current); points = current;
                }
                if (!settled) throw new Exception("Save-row pointer coordinates did not settle");
                await File.WriteAllTextAsync(specification + ".input.json", JsonSerializer.Serialize(new {
                    pid = Environment.ProcessId, points = points.Select(point => new { x = point.X, y = point.Y }) }));
                await Wait(SelectionMatches, "Physical pointer did not select only the disposable saves", 180);
                await Wait(() => table.IsKeyboardFocusWithin, "Selected save cell did not receive keyboard focus");
            } else {
                foreach (var name in selected) {
                    var index = Enumerable.Range(0, table.Rows!.Count).Single(i => table.Rows[i].Model is Mo2Save save && save.File == name);
                    table.RowSelection.Select(table.Rows.RowIndexToModelIndex(index));
                }
            }
            if (!SelectionMatches())
                throw new Exception("Selection includes something other than the disposable copies");
            var menu = table.ContextMenu!;
            menu.Open(table);
            var action = menu.Items.OfType<MenuItem>().Single(item => item.Header as string == "Delete 2 save(s)");
            if (!action.IsEnabled) throw new Exception("Bulk delete action is unavailable");
            menu.Close();
            if (physical) await Wait(() => table.IsKeyboardFocusWithin, "Save-cell keyboard focus did not return after closing the menu");
            Console.WriteLine("READY save delete outcome: only two hash-verified disposable copies selected");
            if (routedDelete) {
                if (!physical) {
                    var index = Enumerable.Range(0, table.Rows!.Count).Single(i => table.Rows[i].Model is Mo2Save save && save.File == selected[0]);
                    await Wait(() => table.GetVisualDescendants().OfType<TreeDataGridCell>().Any(cell => cell.RowIndex == index && cell.ColumnIndex == 0),
                        "Selected save cell was not realized");
                    var cell = table.GetVisualDescendants().OfType<TreeDataGridCell>().Single(cell => cell.RowIndex == index && cell.ColumnIndex == 0);
                    if (!cell.Focus() || !SelectionMatches()) throw new Exception("Selected save cell could not receive routed input");
                }
                if (window.FocusManager?.GetFocusedElement() is not InputElement focused)
                    throw new Exception("No focused save cell for the routed Delete event");
                var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Delete };
                focused.RaiseEvent(key);
                if (!key.Handled) throw new Exception("Saves did not handle the routed Delete event");
            } else if (!physical) action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await Wait(() => files.Keys.All(name => !File.Exists(Path.Combine(rootPath, name))) && !profile.ManagingMod,
                "Native deletion did not remove every disposable save and companion", 180);
            await Wait(() => Table()?.Rows?.All(row => row.Model is not Mo2Save save || !selected.Contains(save.File)) == true,
                "Frontend retained deleted save rows");
            if (table.RowSelection.SelectedItems.Count != 0) throw new Exception("Deleted save selection was not cleared");
            if (profile.CurrentTarget != target || originals.Any(file => !File.Exists(Path.Combine(rootPath, file.Key)) || Hash(Path.Combine(rootPath, file.Key)) != file.Value))
                throw new Exception("Original save bytes or profile changed");
            var after = await profile.ReadSaves(target);
            var expected = data.GetProperty("originalSaves").EnumerateArray().Select(value => value.GetString()!).Order();
            if (!after.Select(save => save.File).Order().SequenceEqual(expected)) throw new Exception("Native original save list not restored");
            var input = routedDelete ? "routed Delete event; " : physical ? "pointer selection and Delete event; " : "menu action; ";
            Console.WriteLine($"PASS save delete outcome: {input}native confirmation removed {files.Count} disposable save/companion files; frontend rows and selection refreshed; original saves unchanged");
        } finally { root.Children.Remove(host); }
    }
}
