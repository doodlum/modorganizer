using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Text.Json;

namespace Mo2.Frontend;

internal static class Mo2NativeToolDialogCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, string specification)
    {
        using var spec = JsonDocument.Parse(await File.ReadAllTextAsync(specification));
        var request = spec.RootElement.GetProperty("request").GetString()!;
        var report = spec.RootElement.GetProperty("report").GetString()!;
        if (File.Exists(request) || File.Exists(report)) throw new Exception("Tool check requires fresh observer paths");
        async Task Wait(Func<bool> ready, string message) {
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (!ready()) { if (DateTime.UtcNow > deadline) throw new Exception(message); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.CanChangeOriginalUi, "Native host not ready");
        using var turn = await Mo2CheckTurn.Take();
        var target = shell.Profile.CurrentTarget;
        if (shell.Profile.OriginalUiVisible != false) throw new Exception("Check requires the native main window hidden");
        var view = new Mo2ToolsView { ViewModel = new Mo2ToolsPage(new FixtureWindows { ActiveWindow = shell }, shell.Profile) };
        var window = new Window { Content = view, Width = 600, Height = 500, ShowInTaskbar = false };
        window.Show();
        try {
            Button? Launch() => view.GetVisualDescendants().OfType<Button>()
                .SingleOrDefault(x => x.Name == "LaunchToolButton" && Equals(ToolTip.GetTip(x), "Open INI Editor"));
            await Wait(() => Launch()?.IsEnabled == true && !view.IsReading, "INI Editor action is not available");
            await File.WriteAllTextAsync(request, "INI Editor");
            Launch()!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => shell.Profile.ManagingMod, "INI Editor click did not start a native action");
            if (Launch()?.IsEnabled != false) throw new Exception("Tool launch stayed enabled during the native dialog");
            await Wait(() => File.Exists(report), "Observer did not confirm native INI Editor visibility and closure");
            using var evidence = JsonDocument.Parse(await File.ReadAllTextAsync(report));
            if (!evidence.RootElement.GetProperty("visible").GetBoolean() || !evidence.RootElement.GetProperty("closed").GetBoolean() ||
                evidence.RootElement.GetProperty("title").GetString() != "INI Files" ||
                !evidence.RootElement.GetProperty("mainWindowHidden").GetBoolean()) throw new Exception("Unexpected native tool observer result");
            await Wait(() => !shell.Profile.ManagingMod && Launch()?.IsEnabled == true, "Tools did not recover after the native dialog closed");
            if (shell.Profile.CurrentTarget != target || !shell.Profile.IsConnected || shell.Profile.OriginalUiVisible != false)
                throw new Exception("Native tool changed profile, disconnected, or exposed the MO2 main window");
            Console.WriteLine("PASS native tool dialog: Tools entry opened the real INI Editor; independent observer saw and closed it; busy actions disabled and recovered; profile retained and native main window hidden");
        } finally { window.Close(); }
    }
}
