using NexusMods.App.UI.WorkspaceSystem;
using Avalonia.VisualTree;
using System.Reactive.Linq;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Navigation;
using Avalonia.Controls;
using NexusMods.App.UI.Controls.Alerts;

namespace Mo2.Frontend;

internal static class Mo2AlertLifecycleCheck
{
    internal static async Task Run(Window owner)
    {
        var settings = new AlertSettingsWrapper();
        var alert = new Alert { AlertSettings = settings, Title = "Help lifecycle check", Body = "Temporary diagnostic" };
        var root = new Border { Child = alert };
        var window = new Window { Content = root, Width = 360, Height = 220, Title = "Help lifecycle verification" };
        void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        async Task Settle() { window.UpdateLayout(); await Task.Delay(100); }
        async Task WaitLoaded(bool loaded) {
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (alert.IsLoaded != loaded && DateTime.UtcNow < deadline) await Task.Delay(50);
            Require(alert.IsLoaded == loaded, "Help loaded state did not settle");
        }
        try {
            window.Show(owner); await Settle();
            await WaitLoaded(true);
            Require(alert.IsLoaded && alert.IsVisible, $"Initial help did not load (loaded={alert.IsLoaded}, visible={alert.IsVisible}, dismissed={alert.IsDismissed})");
            settings.DismissAlert(); Require(!alert.IsVisible, "Initial dismiss did not hide help");
            root.Child = null; await Settle(); await WaitLoaded(false);
            Require(!alert.IsLoaded, "Help did not unload");
            settings.ShowAlert();
            Require(alert.IsDismissed, "Unloaded help still observes its settings");
            root.Child = alert; await Settle(); await WaitLoaded(true);
            Require(alert.IsLoaded && alert.IsVisible && !alert.IsDismissed, "Reloaded help did not reconnect to unchanged settings");
            for (var index = 0; index < 3; index++) {
                settings.DismissAlert(); Require(!alert.IsVisible, "Dismiss after reload failed");
                root.Child = null; await Settle(); await WaitLoaded(false); root.Child = alert; await Settle(); await WaitLoaded(true);
                settings.ShowAlert(); Require(alert.IsVisible, "Show after reload failed");
            }
            var replacement = new AlertSettingsWrapper(); replacement.DismissAlert();
            alert.AlertSettings = replacement; Require(!alert.IsVisible, "Replacement settings were ignored");
            settings.DismissAlert(); settings.ShowAlert(); Require(!alert.IsVisible, "Old settings still control help");
            replacement.ShowAlert(); Require(alert.IsVisible, "Replacement settings do not control help");
            Console.WriteLine("PASS help lifecycle: detached settings suspended, reload resynchronized, three dismiss/show cycles and settings replacement");
        } finally { window.Close(); }
    }

    internal static async Task Live(Mo2LiveWorkspace shell, Window window)
    {
        var layout = Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT");
        if (layout is null || !Path.GetFullPath(layout).StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
            throw new Exception("Plugins help check requires a temporary workspace");
        async Task Wait(Func<bool> condition) {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!condition()) { if (DateTime.UtcNow > deadline) throw new Exception("Plugins help lifecycle did not settle"); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.IsConnected && !shell.Profile.SelectingProfile && shell.Profile.ProfilePath.Length > 0);
        await shell.ProfileMenu.LeftMenuItemExternalChanges!.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
        Mo2PluginsView? body = null;
        await Wait(() => (body = window.GetVisualDescendants().OfType<Mo2PluginsView>().FirstOrDefault(view => view.IsEffectivelyVisible)) is not null);
        var editor = body!.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Sorting.LoadOrderView>().Single();
        var alert = editor.FindControl<Alert>("LoadOrderAlert")!;
        var help = editor.FindControl<StandardButton>("InfoAlertButton")!;
        await Wait(() => alert.IsLoaded && alert.AlertSettings is not null && help.Command is not null);
        var settings = alert.AlertSettings!;
        var dismissed = settings.IsDismissed;
        var host = body!.GetVisualAncestors().OfType<ContentControl>().First(control => ReferenceEquals(control.Content, body));
        async Task ShowUsingHelpCommand() {
            if (help.Command?.CanExecute(help.CommandParameter) != true) throw new Exception("Plugins help command unavailable");
            help.Command.Execute(help.CommandParameter);
            await Wait(() => !settings.IsDismissed && alert.IsVisible);
        }
        try {
            settings.DismissAlert(); await ShowUsingHelpCommand();
            settings.DismissAlert(); host.Content = null; await Wait(() => !alert.IsLoaded);
            host.Content = body; await Wait(() => alert.IsLoaded && help.Command is not null);
            if (!ReferenceEquals(settings, alert.AlertSettings)) throw new Exception("Native help settings were replaced during test");
            await ShowUsingHelpCommand();
        } finally {
            if (!ReferenceEquals(host.Content, body)) host.Content = body;
            if (dismissed) settings.DismissAlert(); else settings.ShowAlert();
        }
        Console.WriteLine("PASS Plugins help: original command opens help before and after retained panel reload; preference restored");
    }

}
