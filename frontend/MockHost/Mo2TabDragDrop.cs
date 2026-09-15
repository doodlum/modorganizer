using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Dragging a tab header onto another panel moves the page there. Dropping over a
// panel's middle adds it as a tab; dropping near an edge splits that panel and
// puts the page in a new one. The drop region is previewed while dragging, so
// the split is visible before the pointer is released.
internal static class Mo2TabDragDrop
{
    internal const string Format = "mo2-workspace-tab";
    // Fraction of a panel's shorter side that counts as its edge band.
    private const double EdgeFraction = .28;
    private const double DragThreshold = 6;

    internal enum Zone { Tab, Left, Right, Top, Bottom }

    internal sealed record Target(PanelId Panel, Zone Zone);
    private sealed record Source(WorkspaceId Workspace, PanelId Panel, PanelTabId Tab);

    // Which region of a panel the pointer is over. The middle is the tab strip
    // target; the outer bands each split the panel on that side.
    internal static Zone ZoneFor(Rect bounds, Point point)
    {
        var band = Math.Min(bounds.Width, bounds.Height) * EdgeFraction;
        var left = point.X - bounds.X;
        var top = point.Y - bounds.Y;
        var right = bounds.Right - point.X;
        var bottom = bounds.Bottom - point.Y;
        var nearest = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
        if (nearest > band) return Zone.Tab;
        if (nearest == left) return Zone.Left;
        if (nearest == right) return Zone.Right;
        return nearest == top ? Zone.Top : Zone.Bottom;
    }

    // The rectangle the dropped page would occupy, in the same space as the
    // panel's own bounds, so it can be drawn as a preview and reused for the split.
    internal static Rect RegionFor(Rect bounds, Zone zone) => zone switch {
        Zone.Left => bounds.WithWidth(bounds.Width / 2),
        Zone.Right => new Rect(bounds.X + bounds.Width / 2, bounds.Y, bounds.Width / 2, bounds.Height),
        Zone.Top => bounds.WithHeight(bounds.Height / 2),
        Zone.Bottom => new Rect(bounds.X, bounds.Y + bounds.Height / 2, bounds.Width, bounds.Height / 2),
        _ => bounds,
    };

    // What the source panel keeps once the new panel takes its half.
    private static Rect RemainderFor(Rect bounds, Zone zone) => zone switch {
        Zone.Left => new Rect(bounds.X + bounds.Width / 2, bounds.Y, bounds.Width / 2, bounds.Height),
        Zone.Right => bounds.WithWidth(bounds.Width / 2),
        Zone.Top => new Rect(bounds.X, bounds.Y + bounds.Height / 2, bounds.Width, bounds.Height / 2),
        Zone.Bottom => bounds.WithHeight(bounds.Height / 2),
        _ => bounds,
    };

    // The workspace holds two columns and two rows. Halving a panel that is already
    // half of something asks for a third of one or the other, which is a layout the
    // workspace can neither draw nor save — the panels came out overlapping, or with
    // a strip of empty canvas between them.
    private const int MaxColumns = 2;
    private const int MaxRows = 2;

    // Whether a drop at this zone will split the panel it lands on, decided from what
    // is open rather than from the geometry, so the preview drawn under the pointer
    // and the drop that follows it cannot promise different things. A panel whose last
    // tab is being dragged out closes, which frees the room the split needs.
    internal static bool CanSplit(IWorkspaceViewModel workspace, PanelId sourcePanelId, IPanelViewModel target, Zone zone)
    {
        if (zone == Zone.Tab) return false;
        var source = workspace.Panels.FirstOrDefault(x => x.Id == sourcePanelId);
        if (source is null) return false;
        if (target.Id == sourcePanelId && source.Tabs.Count < 2) return false;
        var closing = source.Tabs.Count < 2 && target.Id != sourcePanelId ? 1 : 0;
        return workspace.Panels.Count - closing + 1 <= MaxColumns * MaxRows;
    }

    internal static bool Fits(WorkspaceGridState state)
    {
        var columns = new HashSet<double>();
        var rows = new HashSet<double>();
        foreach (var panel in state) {
            columns.Add(Math.Round(panel.Rect.X, 3));
            rows.Add(Math.Round(panel.Rect.Y, 3));
        }
        return columns.Count <= MaxColumns && rows.Count <= MaxRows;
    }

    // The rectangle drawn under the pointer while dragging: the half a split would
    // take, or the whole panel when the workspace has no room to split and the drop
    // will join it as a tab instead. Previewing a half the drop cannot give promised
    // a layout that never appeared.
    internal static Rect PreviewRegion(IWorkspaceController controller, WorkspaceId workspaceId,
        PanelId sourcePanelId, IPanelViewModel target, Rect bounds, Point point)
    {
        var zone = ZoneFor(bounds, point);
        return controller.TryGetWorkspace(workspaceId, out var workspace) &&
            !CanSplit(workspace, sourcePanelId, target, zone) ? bounds : RegionFor(bounds, zone);
    }

    internal static WorkspaceGridState SplitState(IWorkspaceViewModel workspace, IPanelViewModel panel, Zone zone)
    {
        var states = workspace.Panels.Select(x => new PanelGridState(x.Id,
            x.Id == panel.Id ? RemainderFor(panel.LogicalBounds, zone) : x.LogicalBounds)).ToList();
        states.Add(new PanelGridState(PanelId.DefaultValue, RegionFor(panel.LogicalBounds, zone)));
        return WorkspaceGridState.From(states, workspace.IsHorizontal);
    }

    internal static void Attach(Window window, IWorkspaceController controller)
    {
        var preview = new Border {
            Name = "Mo2TabDropPreview", IsVisible = false, IsHitTestVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
            BorderThickness = new Thickness(2), CornerRadius = new CornerRadius(8),
            HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        };
        Source? dragging = null;
        Point pressed = default;
        object? pressedTab = null;

        void HidePreview() { preview.IsVisible = false; dragging = null; }

        // The preview goes in the window's overlay layer, which covers the whole
        // window. Adding it to the layout grid instead confined it to one cell.
        void EnsureOverlay()
        {
            if (preview.Parent is not null) return;
            OverlayLayer.GetOverlayLayer(window)?.Children.Add(preview);
        }

        (IPanelViewModel Panel, Control View)? PanelUnder(Point point)
        {
            foreach (var view in window.GetVisualDescendants().OfType<PanelView>()) {
                if (view.ViewModel is not { } model) continue;
                var origin = view.TranslatePoint(default, window);
                if (origin is null) continue;
                var bounds = new Rect(origin.Value, view.Bounds.Size);
                if (bounds.Contains(point)) return (model, view);
            }
            return null;
        }

        window.AddHandler(InputElement.PointerPressedEvent, (_, args) => {
            pressedTab = null;
            if (args.Source is not Visual source) return;
            // Only a plain left press starts a drag, and never from the close button:
            // dragging must not swallow closing a tab or opening its context menu.
            if (!args.GetCurrentPoint(window).Properties.IsLeftButtonPressed) return;
            if (source.GetSelfAndVisualAncestors().OfType<Button>().Any()) return;
            var header = source.GetSelfAndVisualAncestors().OfType<PanelTabHeaderView>().FirstOrDefault();
            if (header?.ViewModel is null) return;
            var panel = header.GetVisualAncestors().OfType<PanelView>().FirstOrDefault();
            if (panel?.ViewModel is null) return;
            pressed = args.GetPosition(window);
            pressedTab = header;
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

        window.AddHandler(InputElement.PointerMovedEvent, async (_, args) => {
            if (pressedTab is not PanelTabHeaderView header || dragging is not null) return;
            var point = args.GetPosition(window);
            if (Math.Abs(point.X - pressed.X) < DragThreshold && Math.Abs(point.Y - pressed.Y) < DragThreshold) return;
            var panel = header.GetVisualAncestors().OfType<PanelView>().FirstOrDefault();
            if (panel?.ViewModel is null || header.ViewModel is null) return;
            // A panel's last tab has nowhere to move from; moving it would close the panel.
            pressedTab = null;
            // A maximised panel is drawn over the whole canvas while its place in the
            // layout is unchanged, so every zone under the pointer belongs to a panel
            // that is not where it appears to be. Dragging puts it back first.
            Mo2DeferredPanel.RestoreMaximised();
            dragging = new Source(controller.ActiveWorkspaceId, panel.ViewModel.Id, header.ViewModel.Id);
            EnsureOverlay();
            var data = new DataObject();
            data.Set(Format, dragging);
            try { await DragDrop.DoDragDrop(args, data, DragDropEffects.Move); }
            finally { HidePreview(); }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

        window.AddHandler(InputElement.PointerReleasedEvent, (_, _) => pressedTab = null,
            Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);

        void Over(object? _, DragEventArgs args)
        {
            if (dragging is null || !args.Data.Contains(Format)) return;
            args.DragEffects = DragDropEffects.Move;
            args.Handled = true;
            var point = args.GetPosition(window);
            var under = PanelUnder(point);
            if (under is null) { preview.IsVisible = false; return; }
            var origin = under.Value.View.TranslatePoint(default, window)!.Value;
            var bounds = new Rect(origin, under.Value.View.Bounds.Size);
            var region = PreviewRegion(controller, dragging.Workspace, dragging.Panel, under.Value.Panel, bounds, point);
            preview.Margin = new Thickness(region.X, region.Y, 0, 0);
            preview.Width = region.Width; preview.Height = region.Height;
            preview.IsVisible = true;
        }

        DragDrop.SetAllowDrop(window, true);
        window.AddHandler(DragDrop.DragOverEvent, Over);
        window.AddHandler(DragDrop.DragEnterEvent, Over);
        window.AddHandler(DragDrop.DragLeaveEvent, (_, _) => preview.IsVisible = false);
        window.AddHandler(DragDrop.DropEvent, (_, args) => {
            var source = dragging;
            HidePreview();
            if (source is null || args.Data.Get(Format) is not Source) return;
            args.Handled = true;
            var point = args.GetPosition(window);
            var under = PanelUnder(point);
            if (under is null) return;
            var origin = under.Value.View.TranslatePoint(default, window)!.Value;
            var bounds = new Rect(origin, under.Value.View.Bounds.Size);
            Move(controller, source.Workspace, source.Panel, source.Tab, under.Value.Panel, ZoneFor(bounds, point));
        });
    }

    internal static void Move(IWorkspaceController controller, WorkspaceId workspaceId,
        PanelId sourcePanelId, PanelTabId tabId, IPanelViewModel target, Zone zone)
    {
        if (!controller.TryGetWorkspace(workspaceId, out var workspace)) return;
        // A maximised panel is drawn over the whole canvas while its place in the
        // layout is unchanged. Rearranging around it would place the new panel behind
        // the one covering the screen, so it goes back first — the drag does this when
        // it starts, and this is here for every other way a move can be asked for.
        Mo2DeferredPanel.RestoreMaximised();
        var source = workspace.Panels.FirstOrDefault(x => x.Id == sourcePanelId);
        if (source is null) return;
        var tab = source.Tabs.FirstOrDefault(x => x.Id == tabId);
        if (tab?.Contents.PageData is not { } page) return;
        // Dropping a tab back where it already is should not rebuild the layout.
        if (zone == Zone.Tab && target.Id == sourcePanelId) return;
        // The only tab of the panel being split has nowhere to go: the source panel
        // would close underneath the new state and leave a hole in the grid.
        if (zone != Zone.Tab && target.Id == sourcePanelId && source.Tabs.Count < 2) return;

        // Closed first, and the split worked out afterwards. Dragging a panel's last
        // tab onto another panel closes the one it came from, and a state worked out
        // before that still named it — the workspace then looked up a panel that had
        // already gone and threw "Optional<T> has no value" out of the drop, taking
        // the window with it. Read after the close, the remaining panels are the ones
        // that are actually there, already grown into the space the closed one left.
        // Asked before the tab closes, because this is the same question the preview
        // answered while the pointer was still moving.
        var splitting = CanSplit(workspace, sourcePanelId, target, zone);
        source.CloseTab(tabId);
        // Landing as a tab is what a drop does when it cannot split: the page still
        // goes where it was dropped, which is the part the person doing the dragging
        // chose. Refusing outright would look like the drag had failed.
        OpenPageBehavior behavior = new OpenPageBehavior.NewTab(target.Id);
        if (splitting) {
            var split = SplitState(workspace, target, zone);
            if (Fits(split)) behavior = new OpenPageBehavior.NewPanel(split);
        }
        controller.OpenPage(workspaceId, page, behavior, selectTab: true, checkOtherPanels: false);
    }
}
