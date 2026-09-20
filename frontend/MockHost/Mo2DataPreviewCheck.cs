using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using System.Text.Json;

namespace Mo2.Frontend;

// Opt-in native acceptance. A separately scoped observer confirms and closes
// the real Qt Preview window; a successful bridge reply alone is insufficient.
internal static class Mo2DataPreviewCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, string specification)
    {
        using var spec = JsonDocument.Parse(await File.ReadAllTextAsync(specification));
        var supported = spec.RootElement.GetProperty("supported").GetString()!;
        var unsupported = spec.RootElement.GetProperty("unsupported").GetString()!;
        var report = spec.RootElement.GetProperty("report").GetString()!;
        var request = spec.RootElement.GetProperty("request").GetString()!;
        var enter = Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_PREVIEW_ENTER") == "1";
        if (File.Exists(report) || File.Exists(request)) throw new Exception("Preview check requires fresh observer paths");
        async Task Wait(Func<bool> ready, string failure) {
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (!ready()) { if (DateTime.UtcNow > deadline) throw new Exception(failure); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.IsConnected, "Host unavailable");
        using var turn = await Mo2CheckTurn.Take();
        var target = shell.Profile.CurrentTarget;
        async Task Check(string path, bool enabled) {
            var cut = path.LastIndexOf('/');
            var directory = cut < 0 ? "" : path[..cut];
            var name = path[(cut + 1)..];
            var native = await shell.Profile.ReadListMenu("readFileMenu", [path], target, "data");
            if (native.Single(x => x.Text == "Preview").Enabled != enabled)
                throw new Exception("Native preview support does not match the supplied case");
            // FileTree orders Preview first when double-click/Enter should use
            // installed preview extensions. Do not change the user's preference.
            if (enter && enabled && native.First().Text != "Preview")
                throw new Exception("Keyboard preview check requires MO2's existing preview-on-activation preference");
            var view = new Mo2DataView { ViewModel = new Mo2DataPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) { Directory = directory } };
            var window = new Window { Content = view, Width = 650, Height = 650, ShowInTaskbar = false };
            window.Show();
            try {
                TreeDataGrid? Table() => view.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
                await Wait(() => Table()?.Rows?.Any(x => x.Model is Mo2DataEntry e && e.Name == name) == true, "Preview file not drawn");
                var table = Table()!;
                Mo2RowMenu.SelectRow(table, Enumerable.Range(0, table.Rows!.Count).Single(i => table.Rows[i].Model is Mo2DataEntry e && e.Name == name));
                var menu = table.ContextMenu!;
                menu.Open(table);
                var items = menu.Items.OfType<MenuItem>().ToArray();
                // Common Refresh becoming enabled proves the asynchronous native
                // availability read finished, including the disabled-file case.
                await Wait(() => items.Single(x => x.Header as string == "Refresh").IsEnabled, "Native availability did not finish");
                var preview = items.Single(x => x.Header as string == "Preview");
                if (preview.IsEnabled != enabled) throw new Exception("Frontend preview availability differs from MO2");
                menu.Close();
                if (!enabled) return;
                await File.WriteAllTextAsync(request, "preview");
                if (enter) {
                    table.Focus();
                    var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter };
                    table.RaiseEvent(key);
                    if (!key.Handled) throw new Exception("Data table did not handle Enter activation");
                } else preview.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                await Wait(() => File.Exists(report), "Native observer did not see the Preview dialog");
                using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(report));
                if (!evidence.RootElement.GetProperty("visible").GetBoolean() || !evidence.RootElement.GetProperty("closed").GetBoolean())
                    throw new Exception("Native Preview dialog was not observed and closed");
                await Wait(() => !shell.Profile.ManagingMod && Table()?.Rows?.Any(x => x.Model is Mo2DataEntry e && e.Name == name) == true,
                    "Frontend did not recover after closing Preview");
                if (view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text?.Contains("No enabled MO2 preview extension") == true))
                    throw new Exception("Preview returned an extension error");
            } finally { window.Close(); }
        }
        await Check(unsupported, false);
        await Check(supported, true);
        if (target != shell.Profile.CurrentTarget || !shell.Profile.IsConnected)
            throw new Exception("Preview changed profile or disconnected the frontend");
        Console.WriteLine($"PASS Data preview: unsupported file disabled; {(enter ? "Enter on" : "Preview menu for")} supported nested file opened a visible native Qt Preview dialog and frontend recovered after closure");
    }
}
