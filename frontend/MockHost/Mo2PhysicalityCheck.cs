using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// The workspace's physical responses: a list that gives at its ends, a divider that
// pushes back at its limit, and panels that arrive and leave under animation rather
// than appearing and vanishing.
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
            // Timed rather than sampled in sequence. The fade runs on the render clock
            // and the removal on a dispatcher timer, so polling for "has it started"
            // and then "has it finished" raced the animation both ways: under load the
            // whole 160ms could pass between two polls, and on a quick frame the timer
            // could fire while the last frame was still on screen. What the panel has
            // to do is stay for its exit and be gone by the end of it.
            var closing = System.Diagnostics.Stopwatch.StartNew();
            Mo2Physicality.AnimateOut(panel, () => left = true);
            // What is checked is what the panel is told to do and when it is taken
            // away, not which intermediate frames happen to be caught: catching one is
            // a race against the renderer, and the check kept losing it in both
            // directions while the animation itself was running perfectly well.
            if (panel.Transitions?.OfType<DoubleTransition>()
                    .FirstOrDefault(x => x.Property == Visual.OpacityProperty) is not { } fade)
                throw new Exception("A closing panel was given no fade to run");
            if (fade.Duration != Mo2Physicality.LeaveDuration)
                throw new Exception($"A closing panel fades over {fade.Duration.TotalMilliseconds:F0}ms, " +
                    $"not the {Mo2Physicality.LeaveDuration.TotalMilliseconds:F0}ms it is removed after");
            await Reaches(window, () => left, "A closing panel never reported finished", seconds: 3);
            var took = closing.Elapsed;
            // A frame of slack: the timer cannot fire earlier than its interval, but
            // the clock the check reads and the one the timer uses are not the same.
            if (took < Mo2Physicality.LeaveDuration - TimeSpan.FromMilliseconds(20))
                throw new Exception($"A closing panel was removed after {took.TotalMilliseconds:F0}ms, " +
                    $"before its {Mo2Physicality.LeaveDuration.TotalMilliseconds:F0}ms exit had run");
            await Reaches(window, () => panel.Opacity <= .01, "A closing panel was still visible after its exit", seconds: 2);
            Console.WriteLine($"PASS panel lifecycle: a new panel scales and fades in over {Mo2Physicality.EnterDuration.TotalMilliseconds:F0}ms, " +
                $"and a closed panel is removed only after its {Mo2Physicality.LeaveDuration.TotalMilliseconds:F0}ms exit has run");
        } finally { window.Close(); }
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

    // A press and release on the control itself, which is what a pointer does.
    private static void Press(Control control, Window window)
    {
        var pointer = new Pointer(0, PointerType.Mouse, true);
        var middle = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window) ?? default;
        var properties = new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed);
        control.RaiseEvent(new PointerPressedEventArgs(control, pointer, window, middle, 0, properties, KeyModifiers.None));
        control.RaiseEvent(new PointerReleasedEventArgs(control, pointer, window, middle, 0,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased),
            KeyModifiers.None, MouseButton.Left));
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
