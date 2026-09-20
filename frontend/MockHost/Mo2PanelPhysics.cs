using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// The workspace stops a divider at 30% of the canvas and says nothing about it, so
// pushing further simply stopped moving. The divider now gives against the push and
// springs back when released, which is the only signal that a limit was reached
// rather than the drag having failed.
internal static class Mo2PanelPhysics
{
    // Matches WorkspaceViewModel's clamp. Read from one place here so the resistance
    // starts exactly where the movement stops.
    private const double Limit = 0.3;

    // Panels track the pointer exactly while a divider is held; their reflow
    // animation would otherwise lag behind the drag.
    internal static bool Resizing { get; private set; }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<PanelResizerView, object> Watched = new();

    // Resizers are created and replaced as panels come and go, so this runs from the
    // canvas's layout rather than once at construction.
    internal static void Watch(Control workspace)
    {
        foreach (var resizer in FindResizers(workspace)) {
            if (Watched.TryGetValue(resizer, out _)) continue;
            Watched.Add(resizer, new object());
            Attach(resizer);
        }
    }

    internal static IEnumerable<PanelResizerView> FindResizers(Visual root)
    {
        // WorkspaceView creates resizers alongside panels in this canvas. Do not
        // walk through panel contents (including every realized table cell) on
        // each layout merely to find those sibling controls.
        if (root is WorkspaceView workspace) {
            if (workspace.FindControl<Canvas>("WorkspaceCanvas") is { } canvas)
                foreach (var resizer in canvas.Children.OfType<PanelResizerView>())
                    yield return resizer;
            yield break;
        }
        foreach (var child in root.GetVisualChildren())
            foreach (var resizer in FindResizers(child)) yield return resizer;
    }

    private static void Attach(PanelResizerView resizer)
    {
        var pressed = false;
        // Handlers remain attached to the control across reattachment. Weak
        // membership avoids retaining it and prevents installing them twice.
        resizer.DetachedFromVisualTree += (_, _) => { if (pressed) { pressed = false; Resizing = false; } };
        resizer.AddHandler(InputElement.PointerPressedEvent, (_, _) => { pressed = true; Resizing = true; },
            RoutingStrategies.Tunnel);
        void Release()
        {
            if (!pressed) return;
            pressed = false; Resizing = false;
            Mo2Physicality.Release(resizer, resizer.ViewModel?.IsHorizontal ?? false);
        }
        resizer.AddHandler(InputElement.PointerReleasedEvent, (_, _) => Release(), RoutingStrategies.Tunnel);
        resizer.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => Release(), RoutingStrategies.Tunnel);
        resizer.AddHandler(InputElement.PointerMovedEvent, (_, e) => {
            if (!pressed || resizer.ViewModel is not { } model || resizer.Parent is not Control canvas) return;
            var size = canvas.Bounds;
            if (size.Width <= 0 || size.Height <= 0) return;
            var horizontal = model.IsHorizontal;
            var position = e.GetPosition(canvas);
            var wanted = horizontal ? position.Y / size.Height : position.X / size.Width;
            // How far past the clamp the pointer is asking to go, in pixels along the
            // axis the divider actually moves.
            var span = horizontal ? size.Height : size.Width;
            var excess = wanted < Limit ? (wanted - Limit) * span
                : wanted > 1 - Limit ? (wanted - (1 - Limit)) * span : 0;
            if (excess == 0) Mo2Physicality.Release(resizer, horizontal);
            else Mo2Physicality.Resist(resizer, excess, horizontal);
        }, RoutingStrategies.Tunnel);
    }
}
