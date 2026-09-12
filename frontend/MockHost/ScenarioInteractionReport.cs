using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// Opt-in observation for external pointer/keyboard tests. It reports state and
// rendered control bounds; it never invokes commands or changes the workspace.
internal static class ScenarioInteractionReport
{
    public static void Attach(Window window, ScenarioWorkspace scenario, string path)
    {
        string? previous = null;
        var timer = DispatcherTimer.Run(() => {
            var workspace = scenario.WorkspaceController.ActiveWorkspace;
            var controls = window.GetVisualDescendants().OfType<Control>()
                .Where(x => x.IsEffectivelyVisible && (x is NexusMods.App.UI.WorkspaceSystem.PanelView ||
                    x.GetType().Name.Contains("TabHeader") || x is GridSplitter ||
                    (x.Name is not null && (x.Name.Contains("Tab") || x.Name.Contains("Panel")))))
                .Select(x => {
                    var p = x.TranslatePoint(default, window) ?? default;
                    return new { type = x.GetType().Name, name = x.Name, x = p.X, y = p.Y,
                        width = x.Bounds.Width, height = x.Bounds.Height };
                }).ToArray();
            var state = JsonSerializer.Serialize(new {
                workspace = workspace.Id.ToString(), selectedPanel = workspace.SelectedPanel.Id.ToString(),
                panels = workspace.Panels.Select(panel => new { id = panel.Id.ToString(), selectedTab = panel.SelectedTab.Id.ToString(),
                    tabs = panel.Tabs.Select(tab => new { id = tab.Id.ToString(), title = tab.Contents.ViewModel.TabTitle }).ToArray() }).ToArray(),
                controls,
            }, new JsonSerializerOptions { WriteIndented = true });
            if (state != previous) { File.WriteAllText(path + ".tmp", state); File.Move(path + ".tmp", path, true); previous = state; }
            return true;
        }, TimeSpan.FromMilliseconds(250));
        window.Closed += (_, _) => timer.Dispose();
    }
}
