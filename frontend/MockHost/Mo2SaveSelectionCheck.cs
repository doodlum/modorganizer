using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2SaveSelectionCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        Mo2Save[] rows = [new("Same title", "a.fos"), new("Same title", "b.fos")];
        var calls = 0;
        var view = new Mo2SavesView(_ => { calls++; return Task.FromResult(rows); }) {
            ViewModel = new Mo2SavesPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile)
        };
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 600, Height = 400, IsHitTestVisible = false };
        Grid.SetRowSpan(host, root.RowDefinitions.Count);
        Grid.SetColumnSpan(host, root.ColumnDefinitions.Count);
        root.Children.Add(host);
        try {
            host.Children.Add(view);
            var until = DateTime.UtcNow.AddSeconds(5);
            while (calls == 0 || !view.GetVisualDescendants().OfType<TreeDataGrid>().Any()) {
                if (DateTime.UtcNow > until) throw new Exception("Saves selection check did not activate");
                await Task.Delay(25);
            }
            var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            var search = view.GetVisualDescendants().OfType<TextBox>().Single();
            table.RowSelection!.Select(new IndexPath(0));
            table.RowSelection.Select(new IndexPath(1));
            void ExpectMany(params string[] files) {
                var actual = table.RowSelection!.SelectedItems.OfType<Mo2Save>().Select(save => save.File).Order().ToArray();
                if (!actual.SequenceEqual(files.Order()))
                    throw new Exception("Saves multiple selection mismatch: " + string.Join(",", actual));
            }
            ExpectMany("a.fos", "b.fos");
            table.ContextMenu!.Open(table);
            await Task.Delay(100);
            var menu = table.ContextMenu.Items.OfType<MenuItem>().ToArray();
            if (!menu.Any(item => item.Header as string == "Delete 2 save(s)" && item.IsEnabled) ||
                !menu.Any(item => item.Header as string == "Fix enabled mods..." && !item.IsEnabled))
                throw new Exception("Saves multi-selection menu does not match MO2");
            table.ContextMenu.Close();
            search.Text = "a.fos"; await WaitRows(1); ExpectMany("a.fos");
            search.Text = ""; await WaitRows(2); ExpectMany("a.fos", "b.fos");
            rows = [rows[1], rows[0]];
            await view.Refresh(); ExpectMany("a.fos", "b.fos");
            rows = [new("Same title", "a.fos")];
            await view.Refresh(); ExpectMany("a.fos");
            rows = [new("Same title", "a.fos"), new("Same title", "b.fos")];
            await view.Refresh(); ExpectMany("a.fos");
            table.RowSelection!.Clear();
            table.RowSelection!.Select(new IndexPath(1));
            void Expect(string? file) {
                if ((table.RowSelection?.SelectedItem as Mo2Save)?.File != file)
                    throw new Exception($"Saves selection mismatch: expected {file ?? "none"}, actual {(table.RowSelection?.SelectedItem as Mo2Save)?.File ?? "none"}");
            }
            async Task WaitRows(int count) {
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (table.Rows?.Count != count) {
                    if (DateTime.UtcNow > deadline) throw new Exception("Saves filter did not settle");
                    await Task.Delay(25);
                }
            }
            search.Text = "a.fos"; await WaitRows(1); Expect(null);
            search.Text = ""; await WaitRows(2); Expect("b.fos");
            rows = [new("Renamed title", "b.fos"), new("Same title", "a.fos")];
            await view.Refresh(); Expect("b.fos");
            rows = [new("Same title", "a.fos")];
            await view.Refresh(); Expect(null);
            rows = [new("Same title", "a.fos"), new("Recreated", "b.fos")];
            await view.Refresh(); Expect(null);
            rows = [.. rows, new("Third", "c.fos")];
            await view.Refresh();
            table.RowSelection!.Select(new IndexPath(0)); table.RowSelection.Select(new IndexPath(1));
            Mo2RowMenu.SelectRow(table, 2); ExpectMany("c.fos");
            Console.WriteLine("PASS Saves selection: multiple rows, native menu counts/single-save repair, duplicate titles, filtering, reordered refresh, and removal without selection resurrection");
        } finally { root.Children.Remove(host); }
    }
}
