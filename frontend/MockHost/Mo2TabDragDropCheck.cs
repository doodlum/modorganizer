using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Opt-in check for moving tabs between panels: the drop zones and their previewed
// regions, then real moves through the workspace controller.
internal static class Mo2TabDragDropCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        await Task.Delay(1500);
        var bounds = new Rect(0, 0, 1000, 600);
        void Zone(double x, double y, Mo2TabDragDrop.Zone expected)
        {
            var actual = Mo2TabDragDrop.ZoneFor(bounds, new Point(x, y));
            if (actual != expected) throw new Exception($"({x},{y}) resolved to {actual}, expected {expected}");
        }
        Zone(500, 300, Mo2TabDragDrop.Zone.Tab);
        Zone(20, 300, Mo2TabDragDrop.Zone.Left);
        Zone(980, 300, Mo2TabDragDrop.Zone.Right);
        Zone(500, 10, Mo2TabDragDrop.Zone.Top);
        Zone(500, 590, Mo2TabDragDrop.Zone.Bottom);

        void Region(Mo2TabDragDrop.Zone zone, Rect expected)
        {
            var actual = Mo2TabDragDrop.RegionFor(bounds, zone);
            if (actual != expected) throw new Exception($"{zone} previews {actual}, expected {expected}");
        }
        Region(Mo2TabDragDrop.Zone.Left, new Rect(0, 0, 500, 600));
        Region(Mo2TabDragDrop.Zone.Right, new Rect(500, 0, 500, 600));
        Region(Mo2TabDragDrop.Zone.Top, new Rect(0, 0, 1000, 300));
        Region(Mo2TabDragDrop.Zone.Bottom, new Rect(0, 300, 1000, 300));
        Region(Mo2TabDragDrop.Zone.Tab, bounds);

        var controller = live.WorkspaceController;
        live.ShowHome();
        // The strip is the handle tabs are dragged by, so it has to be there even
        // when a panel holds a single tab. Waited for rather than sampled once after
        // a fixed delay: Home's page is built on demand like every other, so a single
        // look a second later reports a workspace that has not finished arriving.
        Border? strip = null;
        for (var attempt = 0; attempt < 120 && strip is null; attempt++) {
            await Task.Delay(100);
            // A panel with one tab keeps its strip out of the way until the pointer
            // reaches the top of it, which is what this stands in for.
            foreach (var shown in window.GetVisualDescendants().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>())
                Mo2ResponsiveHeaders.Reveal(shown, true);
            window.UpdateLayout();
            strip = window.GetVisualDescendants().OfType<Border>()
                .Where(x => x.Name == "TabHeaderBorder" && x.IsEffectivelyVisible)
                .FirstOrDefault(x => x.Bounds.Height > 0);
        }
        if (strip is null) throw new Exception("The shown panel has no tab strip at all, even with the pointer at its top edge");
        if (strip.Bounds.Height <= 0)
            throw new Exception($"Tab strip has no height with a single tab (visible={strip.IsVisible})");
        var workspaceId = controller.ActiveWorkspaceId;
        if (!controller.TryGetWorkspace(workspaceId, out var workspace)) throw new Exception("No active workspace");
        var startingPanels = workspace.Panels.Count;

        live.OpenGames();
        live.OpenComponents();
        await Task.Delay(600);
        var panel = workspace.Panels.Single();
        if (panel.Tabs.Count < 2) throw new Exception("Expected two tabs to move between panels, found " + panel.Tabs.Count);
        var moving = panel.Tabs[^1];
        var movingTitle = moving.Header.Title;

        // Split: the last tab becomes its own panel on the right.
        var split = Mo2TabDragDrop.SplitState(workspace, panel, Mo2TabDragDrop.Zone.Right);
        if (split.Count != 2) throw new Exception("Split state should describe two panels, has " + split.Count);
        Mo2TabDragDrop.Move(controller, workspaceId, panel.Id, moving.Id, panel, Mo2TabDragDrop.Zone.Right);
        await Task.Delay(600);
        if (workspace.Panels.Count != 2) throw new Exception("Edge drop did not create a panel: " + workspace.Panels.Count);
        var left = workspace.Panels.OrderBy(x => x.LogicalBounds.X).First();
        var right = workspace.Panels.OrderBy(x => x.LogicalBounds.X).Last();
        if (right.LogicalBounds.X <= left.LogicalBounds.X) throw new Exception("New panel is not to the right");
        if (!right.Tabs.Any(x => x.Header.Title == movingTitle)) throw new Exception("Moved page is not in the new panel");
        if (left.Tabs.Any(x => x.Header.Title == movingTitle)) throw new Exception("Moved page is still in the old panel");

        // The panel being emptied is itself part of the layout being rebuilt. Dragging
        // a panel's last tab onto another panel's edge closes the one it came from and
        // splits the one it lands on, and the split worked out before that close named
        // a panel that had already gone — which threw out of the drop handler and took
        // the window with it.
        var last = right.Tabs.Single(x => x.Header.Title == movingTitle);
        Mo2TabDragDrop.Move(controller, workspaceId, right.Id, last.Id, left, Mo2TabDragDrop.Zone.Bottom);
        await Task.Delay(600);
        if (workspace.Panels.Count != 2) throw new Exception("Last-tab edge drop left " + workspace.Panels.Count + " panels");
        var lower = workspace.Panels.OrderBy(x => x.LogicalBounds.Y).Last();
        var upper = workspace.Panels.OrderBy(x => x.LogicalBounds.Y).First();
        if (lower.LogicalBounds.Y <= upper.LogicalBounds.Y) throw new Exception("Last-tab drop did not split downwards");
        if (!lower.Tabs.Any(x => x.Header.Title == movingTitle)) throw new Exception("Last tab is not in the new lower panel");

        // Rejoin: dropping over the other panel's middle adds it back as a tab.
        var returning = lower.Tabs.Single(x => x.Header.Title == movingTitle);
        Mo2TabDragDrop.Move(controller, workspaceId, lower.Id, returning.Id, upper, Mo2TabDragDrop.Zone.Tab);
        await Task.Delay(600);
        if (workspace.Panels.Count != 1) throw new Exception("Emptied panel did not close: " + workspace.Panels.Count);
        if (!workspace.Panels.Single().Tabs.Any(x => x.Header.Title == movingTitle))
            throw new Exception("Page did not return as a tab");

        // Dropping a tab onto its own panel's middle must be a no-op.
        var settled = workspace.Panels.Single();
        var before = settled.Tabs.Count;
        Mo2TabDragDrop.Move(controller, workspaceId, settled.Id, settled.Tabs[0].Id, settled, Mo2TabDragDrop.Zone.Tab);
        await Task.Delay(300);
        if (workspace.Panels.Single().Tabs.Count != before) throw new Exception("Dropping a tab on its own panel changed the layout");

        Console.WriteLine($"PASS tab drag and drop: 5 drop zones and their previewed regions, edge drop split into 2 panels, " +
            $"a panel's last tab moved to another panel's edge without leaving a hole, tab drop rejoined to 1, self-drop " +
            $"ignored (workspace started with {startingPanels} panel)");
    }
}
