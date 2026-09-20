using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Text.Json;
namespace Mo2.Frontend;

internal static class Mo2DataActivationCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, string specification)
    {
        using var spec = JsonDocument.Parse(await File.ReadAllTextAsync(specification));
        var folder = spec.RootElement.GetProperty("folder").GetString()!;
        var result = spec.RootElement.GetProperty("result").GetString()!;
        var gameProbe = spec.RootElement.GetProperty("gameProbe").GetString()!;
        if (!folder.StartsWith("mo2-activation-probe-") || File.Exists(result) || File.Exists(gameProbe))
            throw new Exception("Probe must be new and absent from the physical game Data folder");
        async Task Wait(Func<bool> ready, string message) {
            var until = DateTime.UtcNow.AddSeconds(60);
            while (!ready()) { if (DateTime.UtcNow > until) throw new Exception(message); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.IsConnected, "Host unavailable");
        using var turn = await Mo2CheckTurn.Take();
        var target = shell.Profile.CurrentTarget;
        var view = new Mo2DataView { ViewModel = new Mo2DataPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) { Directory = folder } };
        var window = new Window { Content = view, Width = 650, Height = 650, ShowInTaskbar = false };
        window.Show();
        try {
            TreeDataGrid Table() => view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            await Wait(() => view.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Rows?.Any(x => x.Model is Mo2DataEntry { Name: "run.cmd" }) == true,
                "Probe not drawn in native Data");
            void Select() {
                var table = Table();
                Mo2RowMenu.SelectRow(table, Enumerable.Range(0, table.Rows!.Count).Single(i => table.Rows[i].Model is Mo2DataEntry { Name: "run.cmd" }));
            }
            Select();
            var vfsOnly = Environment.GetEnvironmentVariable("MO2_VERIFY_VFS_ONLY") == "1";
            if (!vfsOnly) {
                await view.ActivateSelection();
                await Wait(() => File.Exists(result), "Activation did not execute the command file");
                if ((await File.ReadAllTextAsync(result)).Trim() != "plain") throw new Exception("Ordinary Qt activation unexpectedly exposed the virtual file");
            }
            await Wait(() => Table().Rows?.Any(x => x.Model is Mo2DataEntry { Name: "run.cmd" }) == true, "Data did not refresh after activation");
            Select();
            var menu = Table().ContextMenu!;
            menu.Open(Table());
            var execute = menu.Items.OfType<MenuItem>().Single(x => x.Header as string == "Execute with VFS");
            await Wait(() => execute.IsEnabled, "MO2 did not enable Execute with VFS");
            menu.Close();
            execute.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await Wait(() => File.Exists(result) && File.ReadAllText(result).Trim() == "hooked" && !shell.Profile.ManagingMod, "VFS execution did not expose the virtual-only file");
            if (File.Exists(gameProbe) || target != shell.Profile.CurrentTarget) throw new Exception("Probe changed physical game Data or active profile");
            Console.WriteLine(vfsOnly
                ? "PASS Data activation: frontend Execute with VFS exposed an Overwrite-only file absent from physical game Data; ordinary activation NOT verified"
                : "PASS Data activation: Qt activation ran the nested command unhooked; frontend Execute with VFS exposed an Overwrite-only file absent from physical game Data");
        } finally { window.Close(); }
    }
}
