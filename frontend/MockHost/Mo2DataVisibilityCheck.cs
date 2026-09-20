using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Security.Cryptography;
using System.Text.Json;

namespace Mo2.Frontend;

internal static class Mo2DataVisibilityCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, string specification)
    {
        using var spec = JsonDocument.Parse(await File.ReadAllTextAsync(specification));
        var folder = spec.RootElement.GetProperty("folder").GetString()!;
        var root = spec.RootElement.GetProperty("root").GetString()!;
        const string name = "onlyvirtual.txt";
        var path = Path.Combine(root, name);
        var hiddenPath = path + ".mohidden";
        var hash = spec.RootElement.GetProperty("hashes").GetProperty(name).GetString()!;
        string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
        if (!folder.StartsWith("mo2-activation-probe-", StringComparison.Ordinal) || Path.GetFileName(root) != folder ||
            !File.Exists(path) || File.Exists(hiddenPath) || Hash(path) != hash)
            throw new Exception("Visibility check requires an unchanged temporary probe");
        async Task Wait(Func<bool> ready, string failure) {
            var deadline = DateTime.UtcNow.AddSeconds(40);
            while (!ready()) { if (DateTime.UtcNow > deadline) throw new Exception(failure); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.IsConnected, "Native host unavailable");
        using var turn = await Mo2CheckTurn.Take();
        var target = shell.Profile.CurrentTarget;
        if (spec.RootElement.GetProperty("profile").GetString() != target.ProfilePath)
            throw new Exception("Probe belongs to a different profile");
        var view = new Mo2DataView { ViewModel = new Mo2DataPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) { Directory = folder } };
        var window = new Window { Content = view, Width = 650, Height = 650, ShowInTaskbar = false };
        window.Show();
        TreeDataGrid? Table() => view.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
        bool Has(string file) => Table()?.Rows?.Any(x => x.Model is Mo2DataEntry e && e.Name == file) == true;
        async Task Invoke(string file, string caption) {
            await Wait(() => Has(file) && !shell.Profile.ManagingMod, "Data file not ready: " + file);
            var table = Table()!;
            Mo2RowMenu.SelectRow(table, Enumerable.Range(0, table.Rows!.Count).Single(i => table.Rows[i].Model is Mo2DataEntry e && e.Name == file));
            var menu = table.ContextMenu!;
            menu.Open(table);
            var action = menu.Items.OfType<MenuItem>().Single(x => x.Header as string == caption);
            await Wait(() => action.IsEnabled, "MO2 did not enable " + caption);
            menu.Close();
            action.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        try {
            await Wait(() => Has(name), "Temporary file not drawn");
            await Invoke(name, "Hide");
            await Wait(() => File.Exists(hiddenPath) && !File.Exists(path) && !shell.Profile.ManagingMod && !Has(name), "Hide did not rename and remove the visible row");
            if (Hash(hiddenPath) != hash || Has(name + ".mohidden")) throw new Exception("Hide changed bytes or bypassed hidden-file filtering");
            var native = await shell.Profile.ReadDataDirectory(target, folder);
            if (native.Any(x => x.Name == name) || native.Count(x => x.Name == name + ".mohidden") != 1)
                throw new Exception("Native merged Data did not reflect Hide");
            var hidden = view.GetVisualDescendants().OfType<CheckBox>().Single(x => x.Name == "DataHiddenFiles");
            hidden.IsChecked = true;
            await Wait(() => Has(name + ".mohidden"), "Hidden Files did not reveal the renamed row");
            await Invoke(name + ".mohidden", "Un-Hide");
            await Wait(() => File.Exists(path) && !File.Exists(hiddenPath) && !shell.Profile.ManagingMod && Has(name), "Un-Hide did not restore file and row");
            hidden.IsChecked = false;
            await Wait(() => Has(name) && !Has(name + ".mohidden"), "Restored file disappeared with Hidden Files off");
            native = await shell.Profile.ReadDataDirectory(target, folder);
            if (native.Count(x => x.Name == name) != 1 || native.Any(x => x.Name == name + ".mohidden") || Hash(path) != hash)
                throw new Exception("Native file or bytes not restored");
            if (shell.Profile.CurrentTarget != target) throw new Exception("Visibility action changed profile");
            Console.WriteLine("PASS Data visibility: frontend Hide/Un-Hide changed native merged rows, Hidden Files revealed the hidden entry, exact bytes and profile restored");
        } finally {
            try {
                // Recovery is confined to this unchanged probe and still uses MO2.
                if (File.Exists(hiddenPath) && !File.Exists(path) && Hash(hiddenPath) == hash && shell.Profile.CurrentTarget == target) {
                    await Wait(() => !shell.Profile.ManagingMod, "Pending native action prevents probe restoration");
                    var rows = await shell.Profile.ReadDataDirectory(target, folder);
                    var entry = rows.Single(x => x.Name == name + ".mohidden");
                    var error = await shell.Profile.PreviewDataFile(folder, entry, target, "unhide");
                    if (error is not null || !File.Exists(path) || File.Exists(hiddenPath) || Hash(path) != hash)
                        throw new Exception("Temporary probe restoration failed: " + error);
                }
            } finally { window.Close(); }
        }
    }
}
