using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2DataExportCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, string destination)
    {
        if (File.Exists(destination)) throw new Exception("Export check requires a new destination");
        async Task Wait(Func<bool> ready, string message) {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (!ready()) {
                if (DateTime.UtcNow > deadline) throw new Exception(message);
                await Task.Delay(50);
            }
        }
        await Wait(() => shell.Profile.IsConnected, "Host unavailable");
        using var turn = await Mo2CheckTurn.Take();
        var target = shell.Profile.CurrentTarget;
        var view = new Mo2DataView { ViewModel = new Mo2DataPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) };
        var window = new Window { Content = view, Width = 650, Height = 650, ShowInTaskbar = false };
        window.Show();
        try {
            await Wait(() => view.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Rows?.Count is > 0, "Data did not load");
            var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            var files = table.Source!.Items.OfType<Mo2DataEntry>().Where(x => !x.Directory).Select(x => x.Name).ToArray();
            if (files.Length == 0) throw new Exception("No native root files available to verify export");
            Mo2RowMenu.SelectRow(table, 0);
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_EXPORT_EMPTY") == "1") table.RowSelection!.Clear();
            var menu = table.ContextMenu!;
            menu.Open(table);
            var export = menu.Items.OfType<MenuItem>().Single(x => x.Header as string == "Save Tree to Text File...");
            await Wait(() => export.IsEnabled, "Native tree export remained disabled");
            menu.Close();
            export.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Console.WriteLine("CHECK Data export: frontend command invoked; waiting for native save dialog");
            await Wait(() => File.Exists(destination) && !shell.Profile.ManagingMod, "Native export did not finish");
            var text = await File.ReadAllTextAsync(destination);
            if (files.Any(x => !text.Contains(x, StringComparison.OrdinalIgnoreCase)))
                throw new Exception("Export is missing native root files");
            if (target != shell.Profile.CurrentTarget) throw new Exception("Export changed the active profile");
            Console.WriteLine("PASS Data export: frontend menu invoked native save dialog and MO2 wrote the merged tree; root files verified; empty selection=" +
                (Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_EXPORT_EMPTY") == "1"));
        } finally { window.Close(); }
    }
}
