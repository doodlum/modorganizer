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
            Console.WriteLine("PASS Saves selection: duplicate titles, filtering, reordered refresh, and removal without selection resurrection");
        } finally { root.Children.Remove(host); }
    }
}
