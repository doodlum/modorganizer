using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// The workspace's physical responses: a list that gives at its ends, a divider that
// pushes back at its limit, and panels that arrive, maximise and leave under
// animation rather than appearing and vanishing.
internal static class Mo2PhysicalityCheck
{
    internal static async Task Run()
    {
        var rows = new StackPanel();
        for (var index = 0; index < 60; index++) rows.Children.Add(new TextBlock { Text = "Row " + index, Height = 24 });
        var scroll = new ScrollViewer { Content = rows };
        var window = new Window { Width = 420, Height = 300, Content = scroll, ShowInTaskbar = false };
        window.Show();
        try {
            await Settle(window);
            Mo2Physicality.AttachOverscroll(scroll);
            await Settle(window);
            var presenter = scroll.Presenter as Control ?? throw new Exception("Scroll viewer has no presenter");

            double Offset() => presenter.RenderTransform is TranslateTransform translate ? translate.Y : 0;

            // At the top, wheeling further up cannot scroll, so the list gives instead.
            if (scroll.Offset.Y != 0) throw new Exception("List did not start at the top");
            Wheel(scroll, up: true);
            await Reaches(window, () => Offset() > 1, "Wheeling above the top gave nothing");
            var pulled = Offset();
            if (pulled > Mo2Physicality.MaxPull + .5) throw new Exception($"Overscroll pulled {pulled:F0}px, past the {Mo2Physicality.MaxPull}px limit");
            // Further notches give progressively less rather than adding the same
            // amount each time, and never pass the limit.
            for (var notch = 0; notch < 8; notch++) Wheel(scroll, up: true);
            await Settle(window);
            if (Offset() > Mo2Physicality.MaxPull + .5) throw new Exception($"Repeated overscroll reached {Offset():F0}px");
            await Reaches(window, () => Math.Abs(Offset()) < .5, "The list did not spring back", seconds: 3);
            if (scroll.Offset.Y != 0) throw new Exception("Overscroll moved the actual scroll offset");

            // Scrolling within the list moves the offset and gives nothing.
            scroll.Offset = new Vector(0, 100);
            await Settle(window);
            Wheel(scroll, up: true);
            await Settle(window);
            if (Math.Abs(Offset()) > .5) throw new Exception("A list that can still scroll gave instead of scrolling");
            Console.WriteLine($"PASS overscroll: wheeling past the top pulls up to {Mo2Physicality.MaxPull}px with diminishing " +
                "notches, springs back to rest, leaves the scroll offset untouched, and does nothing mid-list");

            // A divider being pushed past the workspace's clamp gives and returns.
            var divider = new Border { Width = 8, Height = 200 };
            window.Content = divider;
            await Settle(window);
            Mo2Physicality.Resist(divider, excess: -120, horizontal: false);
            await Reaches(window, () => divider.RenderTransform is TranslateTransform { X: < -1 }, "The divider did not give");
            var give = ((TranslateTransform)divider.RenderTransform!).X;
            if (give < -Mo2Physicality.MaxPull - .5) throw new Exception($"The divider gave {give:F0}px, past its limit");
            // Pushing much further must not give proportionally more.
            Mo2Physicality.Resist(divider, excess: -1200, horizontal: false);
            await Settle(window);
            var harder = ((TranslateTransform)divider.RenderTransform!).X;
            if (harder < -Mo2Physicality.MaxPull - .5) throw new Exception($"Pushing ten times harder gave {harder:F0}px");
            Mo2Physicality.Release(divider, horizontal: false);
            await Reaches(window, () => Math.Abs(((TranslateTransform)divider.RenderTransform!).X) < .5,
                "The divider did not spring back", seconds: 3);
            Console.WriteLine($"PASS divider resistance: pushing past the 30% clamp gives at most {Mo2Physicality.MaxPull}px, " +
                "ten times the overshoot does not give ten times as far, and releasing springs it back");

            // Panels fade and scale rather than appearing and vanishing.
            var panel = new Border { Width = 100, Height = 100 };
            window.Content = panel;
            Mo2Physicality.AnimateIn(panel);
            await Settle(window);
            if (panel.RenderTransform is not ScaleTransform { ScaleX: < 1 } && panel.Opacity >= 1)
                throw new Exception("A new panel did not start small and transparent");
            await Reaches(window, () => panel.Opacity > .99 && ((ScaleTransform)panel.RenderTransform!).ScaleX > .999,
                "A new panel did not settle at full size", seconds: 3);
            var left = false;
            Mo2Physicality.AnimateOut(panel, () => left = true);
            // The fade is driven by the render clock, which under load can be a few
            // frames behind the call; what matters is that it starts and that the
            // panel is not removed until it has finished.
            await Reaches(window, () => panel.Opacity < 1, "A closing panel did not start fading", seconds: 1);
            if (left) throw new Exception("A closing panel was removed before it had animated");
            await Reaches(window, () => left, "A closing panel never reported finished", seconds: 3);
            if (panel.Opacity > .01) throw new Exception("A closing panel was still visible when it was removed");
            Console.WriteLine($"PASS panel lifecycle: a new panel scales and fades in over {Mo2Physicality.EnterDuration.TotalMilliseconds:F0}ms, " +
                $"and a closed panel is removed only after its {Mo2Physicality.LeaveDuration.TotalMilliseconds:F0}ms exit has run");
        } finally { window.Close(); }
    }

    // The maximise action needs the real workspace: two panels, a canvas to fill and
    // a native close action to leave alone.
    internal static async Task Maximise(Mo2LiveWorkspace live, Window window)
    {
        var workspace = live.WorkspaceController.ActiveWorkspace;
        if (workspace.Panels.Count < 2) throw new Exception("Maximise needs a workspace with more than one panel");
        var panels = window.GetVisualDescendants().OfType<Mo2DeferredPanel>().ToArray();
        if (panels.Length != workspace.Panels.Count) throw new Exception($"Found {panels.Length} panel controls for {workspace.Panels.Count} panels");
        var canvas = panels[0].Parent as Canvas ?? throw new Exception("Panels are not on the workspace canvas");
        var target = panels[0];
        // The native panel chrome is built on demand, so the action appears on the
        // layout pass after the panel does rather than with it.
        Button?[] Actions() => panels.Select(panel => panel.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(x => x.Name == "MaximisePanelButton")).ToArray();
        // Panels are built on demand: the second one can still be loading when the
        // first is ready, so this waits for every panel rather than sampling once.
        await Reaches(window, () => Actions().All(x => x is { IsVisible: true }),
            "A panel has no visible maximise action while panels are shared", seconds: 15);
        var buttons = Actions();
        // Visible is not the same as drawn: the action has to occupy the header line
        // beside the close button, with a glyph in it. A StandardButton built in code
        // passed every property check here while painting nothing at all, so this
        // also compares the drawn pixels against the close action beside it.
        foreach (var button in buttons) {
            await Reaches(window, () => button!.Bounds.Width >= 8 && button.Bounds.Height >= 8,
                $"The maximise action renders {button!.Bounds.Width:F0}x{button.Bounds.Height:F0}");
            // Laid out and visible is not the same as drawn: the tab-strip placement
            // this replaced satisfied every property assertion while painting nothing,
            // which only a pixel count catches. Panels are built on demand, so the
            // second one can still be loading when the first is ready.
            // Measured against a still window: while a panel is still reflowing, the
            // rectangle sampled belongs to where the button was, not where it is.
            var ink = 0;
            for (var attempt = 0; attempt < 12 && ink < 8; attempt++) {
                await Task.Delay(120); window.UpdateLayout();
                ink = await Ink(window, button!);
            }
            if (ink < 8) throw new Exception($"The maximise action changed only {ink} pixels when hidden");
        }

        var original = panels.Select(x => (x.Bounds.Width, x.Bounds.Height)).ToArray();
        var bounds = workspace.Panels.Select(x => x.LogicalBounds).ToArray();
        try {
            buttons[0]!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            if (!target.IsMaximised) throw new Exception("Pressing maximise did not maximise the panel");
            // Mid-animation the panel is on its way, not already there.
            await Task.Delay(40); window.UpdateLayout();
            var partial = target.Bounds.Width;
            if (partial >= canvas.Bounds.Width - 1) throw new Exception("The panel jumped to full width instead of animating");
            await Reaches(window, () => Math.Abs(target.Bounds.Width - canvas.Bounds.Width) < 2 &&
                Math.Abs(target.Bounds.Height - canvas.Bounds.Height) < 2, "The panel did not reach the full canvas", seconds: 3);
            if (Canvas.GetLeft(target) > 1 || Canvas.GetTop(target) > 1) throw new Exception("The maximised panel is not at the canvas origin");
            if (target.ZIndex <= 0) throw new Exception("The maximised panel is not drawn above the other panels");
            if (!workspace.Panels.Select(x => x.LogicalBounds).SequenceEqual(bounds))
                throw new Exception("Maximising changed the workspace's saved panel bounds");

            // Maximising another panel puts the first one back.
            buttons[1]!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            if (target.IsMaximised) throw new Exception("Two panels were maximised at once");
            buttons[1]!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            if (panels[1].IsMaximised) throw new Exception("Pressing maximise a second time did not restore the panel");
            await Reaches(window, () => panels.Select((x, i) => Math.Abs(x.Bounds.Width - original[i].Width) < 2 &&
                Math.Abs(x.Bounds.Height - original[i].Height) < 2).All(x => x), "The panels did not return to their own sizes", seconds: 3);
            Console.WriteLine($"PASS panel maximise: {panels.Length} panels each carry the action, maximising animates to the " +
                "full canvas above the others, a second panel takes over from the first, pressing again restores every panel, " +
                "and the saved layout is untouched");
        } finally {
            foreach (var panel in panels) panel.SetMaximised(false);
        }
    }

    // Pixels the control actually contributes to the window. The whole window is
    // rendered twice — once as it is, once with the control faded out — and the
    // control's own rectangle compared. Rendering a control on its own instead gives
    // nothing for some templated controls, and property assertions alone cannot tell
    // a control that is laid out and visible from one that reaches the screen: the
    // tab-strip placement this replaced passed every one of them while painting
    // nothing at all. Opacity is used rather than IsVisible so the layout, and
    // therefore the rectangle being compared, does not move between the two renders.
    private static async Task<int> Ink(Window window, Control control)
    {
        var origin = control.TranslatePoint(default, window) ?? default;
        var rect = new PixelRect((int)origin.X, (int)origin.Y,
            Math.Max(1, (int)Math.Ceiling(control.Bounds.Width)), Math.Max(1, (int)Math.Ceiling(control.Bounds.Height)));
        byte[] Shot()
        {
            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                new PixelSize(Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height)));
            bitmap.Render(window);
            var stride = rect.Width * 4;
            var buffer = new byte[stride * rect.Height];
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
            try { bitmap.CopyPixels(rect, handle.AddrOfPinnedObject(), buffer.Length, stride); }
            finally { handle.Free(); }
            return buffer;
        }
        var shown = Shot();
        control.Opacity = 0;
        await Task.Delay(60); window.UpdateLayout();
        var hidden = Shot();
        control.Opacity = 1;
        await Task.Delay(60); window.UpdateLayout();
        var lit = 0;
        for (var index = 0; index < shown.Length; index += 4)
            if (Math.Abs(shown[index] - hidden[index]) > 8) lit++;
        return lit;
    }

    private static void Wheel(ScrollViewer scroll, bool up) =>
        scroll.RaiseEvent(new PointerWheelEventArgs(scroll, new Pointer(0, PointerType.Mouse, true),
            scroll, default, 0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
            KeyModifiers.None, new Vector(0, up ? 1 : -1)) { RoutedEvent = InputElement.PointerWheelChangedEvent });

    private static async Task Settle(Window window)
    {
        await Task.Delay(60);
        window.UpdateLayout();
    }

    private static async Task Reaches(Window window, Func<bool> wanted, string failure, int seconds = 2)
    {
        for (var attempt = 0; attempt < seconds * 1000 / 40; attempt++) {
            if (wanted()) return;
            await Task.Delay(40);
            window.UpdateLayout();
        }
        throw new Exception(failure);
    }
}
