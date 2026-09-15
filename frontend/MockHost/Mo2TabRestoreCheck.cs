using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

internal static class Mo2TabRestoreCheck
{
    public static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT")))
            throw new InvalidOperationException("Use an isolated layout for the restoration check");
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!ready()) { if (DateTime.UtcNow > until) throw new TimeoutException("Restored tab state did not settle"); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.IsConnected && shell.WorkspaceController.ActiveWorkspace.Panels.Any(p => p.Tabs.Count == 2));
        var panel = shell.WorkspaceController.ActiveWorkspace.Panels.Single();
        var width = window.Width;
        var state = window.WindowState;
        var original = panel.SelectedTab;
        try {
            window.WindowState = WindowState.Normal;
            await Task.Delay(200);
            foreach (var size in new[] { 1280d, 900d, 640d, 1280d }) {
                window.Width = size;
                await Wait(() => Math.Abs(window.ClientSize.Width - size) <= 1);
                foreach (var tab in panel.Tabs) {
                    tab.Header.IsSelected = true;
                    await Wait(() => panel.SelectedTab == tab && window.GetVisualDescendants().OfType<PanelTabHeaderView>()
                        .Any(v => v.ViewModel?.Id == tab.Id && v.IsEffectivelyVisible && v.FindControl<Border>("Container")!.Classes.Contains("Selected")));
                    await Task.Delay(200);
                    Mo2DeferredPresentationCheck.CheckPanelGeometry(window);
                }
            }
            if (!window.GetVisualDescendants().OfType<Mo2PluginsView>().Any(v => v.IsEffectivelyVisible))
                throw new InvalidOperationException("Restored Plugins tab did not become visible");
            Console.WriteLine("PASS two-tab restoration, selected header updates and repeated tab switching at 1280/900/640px without a layout loop");
        } finally { window.Width = width; window.WindowState = state; original.Header.IsSelected = true; }
    }
}
