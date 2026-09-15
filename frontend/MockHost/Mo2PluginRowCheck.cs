using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2PluginRowCheck
{
    public static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        async Task Wait(Func<bool> ready, string stage) {
            var end = DateTime.UtcNow.AddSeconds(20);
            while (!ready()) { if (DateTime.UtcNow > end) throw new TimeoutException("Plugin row did not settle: " + stage); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.CanChangeOriginalUi, "connection");
        var profile = shell.Profile; var target = profile.CurrentTarget;
        const string name = "The Mod Configuration Menu.esp";
        if (!profile.ProfilePath.Replace('\\','/').EndsWith("/Frontend Test") || profile.NexusGame != "newvegas") throw new InvalidOperationException("Use the FNV integration profile");
        var original = profile.Order.Plugins.Single(p => p.DisplayName == name).IsActive;
        var view = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single();
        var rail = view.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "PluginRailScrollBar");
        rail.Value = rail.Maximum;
        await Wait(() => { rail.Value = rail.Maximum; return view.GetVisualDescendants().OfType<ToggleButton>().Any(x => Equals(x.Tag,name)); }, "realize MCM row");
        var toggle = view.GetVisualDescendants().OfType<ToggleButton>().Single(x => Equals(x.Tag,name));
        try {
            foreach (var expected in new[] { !original, original, !original, original }) {
                if (!toggle.IsEnabled) throw new InvalidOperationException("Existing plugin toggle stayed disabled");
                toggle.IsChecked = expected;
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                try {
                    await Wait(() => profile.CurrentTarget == target && profile.Order.Plugins.Single(p => p.DisplayName == name).IsActive == expected && toggle.IsChecked == expected && toggle.IsEnabled, "activation " + expected);
                } catch (TimeoutException) {
                    throw new InvalidOperationException($"Activation expected {expected}; model={profile.Order.Plugins.Single(p => p.DisplayName == name).IsActive}, checkbox={toggle.IsChecked}, enabled={toggle.IsEnabled}, targetMatches={profile.CurrentTarget == target}, ready={profile.CanChangeOriginalUi}");
                }
                var snapshot = await new Mo2BridgeClient(target.Endpoint).SendAsync("snapshot");
                var native = snapshot.GetProperty("plugins").EnumerateArray().Single(p => p.GetProperty("name").GetString() == name);
                if ((native.GetProperty("state").GetInt32() == 2) != expected) throw new InvalidOperationException("Rendered plugin state differs from MO2");
            }
            Console.WriteLine("PASS: one plugin row handled four activation changes, stayed enabled, matched native snapshots and restored activation");
        } finally { if (profile.CurrentTarget == target) await profile.SetPluginsActive([name],original); }
    }
}
