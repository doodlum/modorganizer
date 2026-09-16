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
        // The original app explains its load order in a banner above the list, with
        // a help button in the rail that reopens it, and this check used to drive
        // that pair through a dismiss/show lifecycle across a retained panel. MO2's
        // espTab has neither — it is Sort, Restore, Save, a count and the list — so
        // both are undrawn now and the lifecycle is not a thing this page has.
        //
        // What is checked is that they stay undrawn. The controls are still in the
        // original view's markup, so this asks whether either is on screen rather
        // than whether it exists.
        var alert = editor.FindControl<Alert>("LoadOrderAlert")!;
        var help = editor.FindControl<StandardButton>("InfoAlertButton")!;
        await Wait(() => alert.IsLoaded);
        if (alert.IsEffectivelyVisible || help.IsEffectivelyVisible)
            throw new Exception($"Plugins draws the original app's help: banner={alert.IsEffectivelyVisible}, button={help.IsEffectivelyVisible}");
        Console.WriteLine("PASS Plugins help: neither the original app's load-order banner nor the help button beside the list is drawn, as MO2's plugin tab carries neither");
    }

}
