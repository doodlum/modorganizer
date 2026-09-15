using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2ArchiveSelectionCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        Mo2Archive[] rows = [new("Shared.bsa", "First mod", true, false), new("Shared.bsa", "Second mod", true, false)];
        var calls = 0;
        var view = new Mo2ArchivesView(_ => { calls++; return Task.FromResult(rows); }) {
            ViewModel = new Mo2ArchivesPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile)
        };
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 600, Height = 400, IsHitTestVisible = false };
        root.Children.Add(host);
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("Archives selection fixture did not settle");
                await Task.Delay(25);
            }
        }
        try {
            host.Children.Add(view);
            await Wait(() => calls > 0 && view.GetVisualDescendants().OfType<TreeDataGrid>().Any(t => t.Rows?.Count == 2));
            var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            var search = view.GetVisualDescendants().OfType<TextBox>().Single();
            void Expect(string? mod) {
                if ((table.RowSelection?.SelectedItem as Mo2Archive)?.Mod != mod)
                    throw new Exception("Archives selected a different source mod or resurrected a cleared selection");
                var actions = view.GetVisualDescendants().OfType<Button>().Where(b => b.Name is "BrowseArchive" or "ExtractArchive").ToArray();
                if (actions.Length != 2 || actions.Any(b => b.IsEnabled != (mod is not null && shell.Profile.CanChangeOriginalUi)))
                    throw new Exception("Archive actions do not match the visible selection");
            }
            async Task Filter(string value, int count) {
                search.Text = value; await Wait(() => table.Rows?.Count == count);
            }
            table.RowSelection!.Select(new IndexPath(1)); Expect("Second mod");
            await Filter("First", 1); Expect(null);
            await Filter("", 2); Expect("Second mod");
            table.RowSelection!.Clear(); Expect(null);
            await Filter("First", 1); await Filter("", 2); Expect(null);
            table.RowSelection!.Select(new IndexPath(1));
            rows = [new("Shared.bsa", "Second mod", false, false), new("Shared.bsa", "First mod", true, false)];
            await view.Refresh(); Expect("Second mod");
            rows = [new("Shared.bsa", "First mod", true, false)];
            await view.Refresh(); Expect(null);
            rows = [new("Shared.bsa", "First mod", true, false), new("Shared.bsa", "Second mod", true, false)];
            await view.Refresh(); Expect(null);
            Console.WriteLine("PASS Archives selection: duplicate names, filtering, explicit clear, reordered refresh, removal/recreation and action availability");
        } finally { root.Children.Remove(host); }
    }
}
