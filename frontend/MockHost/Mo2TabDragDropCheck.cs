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
        // A press on the strip has to resolve to the tab under it, and a press on the
        // close button inside it must not: a drag that swallowed that button would
        // stop tabs being closed. This is the step before any of the moves below, and
        // the one that quietly stops working when the native tab header changes shape.
        var headerView = window.GetVisualDescendants().OfType<PanelTabHeaderView>()
            .FirstOrDefault(x => x.IsEffectivelyVisible && x.ViewModel is not null);
        if (headerView is null) throw new Exception("No tab header to press, so no drag could ever start");
        // The tab's own title, not the label inside its close button: that one is
        // meant to resolve to nothing, and is checked separately below.
        var title = headerView.GetVisualDescendants().OfType<TextBlock>()
            .FirstOrDefault(x => !x.GetVisualAncestors().OfType<Button>().Any()) ?? (Visual)headerView;
        var pressed = Mo2TabDragDrop.TabAt(title);
        if (pressed is null) {
            var chain = string.Join(" < ", title.GetSelfAndVisualAncestors().OfType<Control>().Take(8)
                .Select(x => $"{x.GetType().Name}#{x.Name}"));
            throw new Exception($"A press on a tab header resolved to no tab, so dragging it would do nothing: {chain}");
        }
        if (pressed.Value.Tab != headerView.ViewModel!.Id)
            throw new Exception("A press on a tab header resolved to a different tab");
        if (headerView.GetVisualDescendants().OfType<Button>().FirstOrDefault() is { } closer &&
            Mo2TabDragDrop.TabAt(closer) is not null)
            throw new Exception("A press on a tab's own button would start a drag instead of pressing the button");
        if (Mo2TabDragDrop.TabAt(window) is not null)
            throw new Exception("A press outside any tab header resolved to a tab");

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

        // Every arrangement a drag can leave behind has to be a grid: panels that
        // cover the workspace exactly once, with no gap and no overlap, and no more
        // than the two columns and two rows the workspace allows. A drop that leaves
        // anything else is a layout the workspace cannot draw or save.
        void Grid(string what)
        {
            var panels = workspace.Panels.Select(x => x.LogicalBounds).ToArray();
            var faults = new List<string>();
            var area = panels.Sum(x => x.Width * x.Height);
            if (Math.Abs(area - 1) > .001) faults.Add($"panels cover {area:F3} of the workspace");
            for (var i = 0; i < panels.Length; i++)
                for (var j = i + 1; j < panels.Length; j++) {
                    var overlap = panels[i].Intersect(panels[j]);
                    if (overlap.Width > .001 && overlap.Height > .001)
                        faults.Add($"{panels[i]} overlaps {panels[j]}");
                }
            var columns = panels.Select(x => Math.Round(x.X, 3)).Distinct().Count();
            var rows = panels.Select(x => Math.Round(x.Y, 3)).Distinct().Count();
            if (columns > 2 || rows > 2) faults.Add($"{columns} columns and {rows} rows, which the workspace cannot hold");
            if (panels.Any(x => x.Width < .01 || x.Height < .01)) faults.Add("a panel has no size");
            if (faults.Count > 0)
                throw new Exception($"{what} left {workspace.Panels.Count} panels that are not a grid: {string.Join("; ", faults)}");
        }
        Grid("the moves so far");

        // Splitting past what the workspace can hold. Four panels is the limit, and
        // the fifth split has to be refused rather than drawn as a grid with three
        // columns in it — the drop still has to do something predictable, so the page
        // goes to the panel it was dropped on as a tab.
        // Distinct pages, because opening the same one twice reuses its tab and the
        // splits below would run out of tabs to move before the workspace filled.
        live.OpenGames(); await Task.Delay(200);
        live.OpenConnections(); await Task.Delay(200);
        live.OpenComponents(); await Task.Delay(200);
        live.OpenProfiles(); await Task.Delay(600);

        IPanelViewModel At(double x, double y) => workspace.Panels
            .OrderBy(p => Math.Abs(p.LogicalBounds.X - x) + Math.Abs(p.LogicalBounds.Y - y)).First();
        void Drop(IPanelViewModel from, IPanelViewModel onto, Mo2TabDragDrop.Zone zone) =>
            Mo2TabDragDrop.Move(controller, workspaceId, from.Id, from.Tabs[^1].Id, onto, zone);

        // Fill the workspace the way a person would: split it in two, then split each
        // half. Four panels in two columns and two rows is everything it can hold.
        Drop(At(0, 0), At(0, 0), Mo2TabDragDrop.Zone.Right);
        await Task.Delay(400); Grid("splitting into two columns");
        Drop(At(0, 0), At(1, 0), Mo2TabDragDrop.Zone.Bottom);
        await Task.Delay(400); Grid("splitting the right column");
        Drop(At(0, 0), At(0, 0), Mo2TabDragDrop.Zone.Bottom);
        await Task.Delay(400); Grid("splitting the left column");
        var filled = workspace.Panels.Count;
        if (filled != 4) throw new Exception($"Three splits made {filled} panels, not the four the workspace holds");

        // Every further edge drop has to be refused rather than drawn: there is no
        // third column or row for it to go in. The page still has to land somewhere,
        // so it joins the panel it was dropped on as a tab.
        // Four pages across four panels leaves no panel with a tab to spare, and a
        // panel's last tab moving out closes it and frees the room a split needs —
        // which is not the case being checked here. A fifth page joins one of them, so
        // the workspace stays full while a panel still has something to give.
        live.ShowHome(); await Task.Delay(200);
        live.OpenGames(); await Task.Delay(500);
        if (workspace.Panels.Count != 4)
            throw new Exception($"Opening another page left {workspace.Panels.Count} panels, not the four the workspace holds");
        if (workspace.Panels.All(x => x.Tabs.Count < 2))
            throw new Exception("No panel has a tab to spare, so a refused split cannot be told from a panel closing");

        // Every further edge drop has to be refused rather than drawn: there is no
        // third column or row for it to go in. The page still has to land somewhere,
        // so it joins the panel it was dropped on as a tab.
        var refused = 0;
        foreach (var zone in new[] { Mo2TabDragDrop.Zone.Right, Mo2TabDragDrop.Zone.Bottom,
                     Mo2TabDragDrop.Zone.Left, Mo2TabDragDrop.Zone.Top }) {
            var from = workspace.Panels.FirstOrDefault(x => x.Tabs.Count > 1);
            if (from is null) break;
            var onto = workspace.Panels.First(x => x.Id != from.Id);
            // What the pointer promises has to be what the drop delivers. With no
            // room left the drop joins the panel as a tab, so the preview has to
            // cover that whole panel rather than the half it cannot give.
            var face = new Rect(0, 0, 400, 300);
            var at = zone switch {
                Mo2TabDragDrop.Zone.Right => new Point(395, 150),
                Mo2TabDragDrop.Zone.Left => new Point(5, 150),
                Mo2TabDragDrop.Zone.Top => new Point(200, 5),
                _ => new Point(200, 295),
            };
            var previewed = Mo2TabDragDrop.PreviewRegion(controller, workspaceId, from.Id, onto, face, at);
            if (previewed != face)
                throw new Exception($"A {zone} drop from a panel with {from.Tabs.Count} tabs, with {workspace.Panels.Count} " +
                    $"panels open, previewed {previewed} instead of the whole panel {face}");
            var moving5 = from.Tabs[^1].Header.Title;
            var wasIn = onto.Tabs.Count;
            Drop(from, onto, zone);
            await Task.Delay(400);
            Grid($"a {zone} drop with the workspace full");
            if (workspace.Panels.Count != 4) throw new Exception($"A {zone} drop on a full workspace left {workspace.Panels.Count} panels");
            if (onto.Tabs.Count != wasIn + 1 || !onto.Tabs.Any(x => x.Header.Title == moving5))
                throw new Exception($"A refused {zone} split did not land the page on the panel it was dropped on");
            refused++;
        }
        if (refused == 0) throw new Exception("No edge drop was made against a full workspace, so nothing was refused");

        // A maximised panel covers the canvas while its place in the layout is
        // unchanged, so a drop worked out against what is drawn would put the new
        // panel behind it. Moving a tab puts it back first.
        var shells = window.GetVisualDescendants().OfType<Mo2DeferredPanel>().ToArray();
        var maximising = shells.FirstOrDefault(x => x.CanMaximise);
        if (maximising is null) throw new Exception("No panel offered to maximise with the workspace full");
        maximising.SetMaximised(true);
        await Task.Delay(400);
        if (!maximising.IsMaximised) throw new Exception("The panel did not maximise");
        // Any tab will do here; what is being checked is that the move puts the
        // maximised panel back, not which page ends up where.
        var donor = workspace.Panels.FirstOrDefault(x => x.Tabs.Count > 1) ?? workspace.Panels[0];
        var receiver = workspace.Panels.FirstOrDefault(x => x.Id != donor.Id) ?? donor;
        Mo2TabDragDrop.Move(controller, workspaceId, donor.Id, donor.Tabs[^1].Id, receiver, Mo2TabDragDrop.Zone.Tab);
        await Task.Delay(400);
        if (shells.Any(x => x.IsMaximised)) throw new Exception("Moving a tab left a panel maximised over the layout it rearranged");
        Grid("a move while a panel was maximised");

        // Drops landing while the last one is still animating. The panels reflow over
        // 220ms, and a drop during that has to be worked out from where the layout is
        // going rather than from halfway there.
        for (var round = 0; round < 8; round++) {
            var from = workspace.Panels.FirstOrDefault(x => x.Tabs.Count > 1) ?? workspace.Panels[0];
            var onto = workspace.Panels.FirstOrDefault(x => x.Id != from.Id) ?? from;
            var zone = (round % 4) switch {
                0 => Mo2TabDragDrop.Zone.Right, 1 => Mo2TabDragDrop.Zone.Tab,
                2 => Mo2TabDragDrop.Zone.Bottom, _ => Mo2TabDragDrop.Zone.Tab,
            };
            Mo2TabDragDrop.Move(controller, workspaceId, from.Id, from.Tabs[^1].Id, onto, zone);
            await Task.Delay(40);
            Grid($"drop {round + 1} of a run landing mid-animation");
        }
        await Task.Delay(600);
        Grid("the layout settling after the run");

        // Back to one panel, so the check leaves the workspace as it found it and the
        // rejoining path is walked as many times as the splitting one.
        while (workspace.Panels.Count > 1) {
            var extra = workspace.Panels.OrderBy(x => x.LogicalBounds.X).ThenBy(x => x.LogicalBounds.Y).Last();
            var keep = workspace.Panels.First(x => x.Id != extra.Id);
            foreach (var tab in extra.Tabs.ToArray())
                Mo2TabDragDrop.Move(controller, workspaceId, extra.Id, tab.Id, keep, Mo2TabDragDrop.Zone.Tab);
            await Task.Delay(400);
            Grid("rejoining the panels");
        }

        Console.WriteLine($"PASS tab drag and drop: a press on a tab header resolves to that tab and a press on its close " +
            $"button does not, 5 drop zones and their previewed regions, edge drop split into 2 panels, " +
            $"a panel's last tab moved to another panel's edge without leaving a hole, tab drop rejoined to 1, self-drop " +
            $"ignored, {filled} panels filled the workspace and {refused} further edge drops were refused rather than drawn " +
            $"as a third column — each previewing the whole panel it would join — a move put a maximised panel back before " +
            $"rearranging around it, and eight drops landing mid-animation all left the workspace covered exactly once " +
            $"(workspace started with {startingPanels} panel)");
    }
}
