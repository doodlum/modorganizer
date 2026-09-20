using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Runtime.CompilerServices;

namespace Mo2.Frontend;

// Physical responses the workspace gives back when it cannot do what the pointer
// asked for. A list that is already at its end still moves a little and returns;
// a panel that has hit its size limit pushes back against the divider. Both are
// purely visual: no offset, bound or saved layout is changed by them.
internal static class Mo2Physicality
{
    // Far enough to read as give, short enough that it never looks like scrolling.
    internal const double MaxPull = 28;
    private const double PullPerNotch = 11;
    internal static readonly TimeSpan PullDuration = TimeSpan.FromMilliseconds(110);
    internal static readonly TimeSpan SpringDuration = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan SpringDelay = TimeSpan.FromMilliseconds(90);

    private static readonly ConditionalWeakTable<Control, object> OverscrollOwners = new();
    private static readonly ConditionalWeakTable<ScrollViewer, OverscrollState> OverscrollStates = new();

    static Mo2Physicality()
    {
        InputElement.PointerWheelChangedEvent.AddClassHandler<ScrollViewer>((scroll, args) => {
            if (OverscrollOwners.TryGetValue(scroll, out _) || scroll.GetVisualAncestors().OfType<Control>()
                    .Any(owner => OverscrollOwners.TryGetValue(owner, out _)))
                OverscrollStates.GetValue(scroll, viewer => new OverscrollState(viewer)).Wheel(args);
        }, RoutingStrategies.Tunnel);
    }

    // Scope the behavior to a page. Class routing includes viewers added later,
    // so there is no need to census the visual tree on every layout pass.
    internal static void AttachOverscrollToPage(Control page) => OverscrollOwners.GetValue(page, _ => new object());

    private static Transitions Pull(AvaloniaProperty property) => new() {
        new DoubleTransition { Property = property, Duration = PullDuration, Easing = new CubicEaseOut() } };
    private static Transitions Spring(AvaloniaProperty property) => new() {
        // Elastic rather than cubic: the list comes back with a little weight behind
        // it, which is what makes the pull read as a pull instead of a jump.
        new DoubleTransition { Property = property, Duration = SpringDuration, Easing = new ElasticEaseOut() } };

    // Wheeling past either end of a scroll viewer. The presenter moves, not the
    // offset, so scrollbars and hit testing are untouched and nothing to recover
    // from if the pointer leaves mid-pull.
    internal static void AttachOverscroll(ScrollViewer scroll) => AttachOverscrollToPage(scroll);

    private sealed class OverscrollState
    {
        private readonly ScrollViewer _scroll;
        private readonly TranslateTransform _translate = new();
        private double _pull;
        private long _version;

        internal OverscrollState(ScrollViewer scroll)
        {
            _scroll = scroll;
            scroll.DetachedFromVisualTree += (_, _) => {
                ++_version; _pull = 0;
                _translate.Transitions = null; _translate.Y = 0;
            };
        }

        private void Apply(double wanted, bool springing)
        {
            if (_scroll.Presenter is not Control target) return;
            if (!ReferenceEquals(target.RenderTransform, _translate)) target.RenderTransform = _translate;
            _translate.Transitions = springing ? Spring(TranslateTransform.YProperty) : Pull(TranslateTransform.YProperty);
            _translate.Y = wanted;
        }

        internal void Wheel(PointerWheelEventArgs e)
        {
            var down = e.Delta.Y < 0;
            if (e.Delta.Y == 0) return;
            var scrollable = _scroll.Extent.Height - _scroll.Viewport.Height;
            var atEnd = down ? _scroll.Offset.Y >= scrollable - 0.5 : _scroll.Offset.Y <= 0.5;
            if (!atEnd) { if (_pull != 0) { _pull = 0; _version++; Apply(0, springing: true); } return; }
            var remaining = 1 - Math.Abs(_pull) / MaxPull;
            _pull = Math.Clamp(_pull + (down ? -1 : 1) * PullPerNotch * remaining, -MaxPull, MaxPull);
            Apply(_pull, springing: false);
            var current = ++_version;
            DispatcherTimer.RunOnce(() => {
                if (current != _version) return;
                _pull = 0; Apply(0, springing: true);
            }, SpringDelay);
        }
    }

    // The give a control shows while it is being pushed past a limit. Used by the
    // panel dividers, which the workspace clamps without telling the pointer.
    internal static void Resist(Control control, double excess, bool horizontal)
    {
        var transform = control.RenderTransform as TranslateTransform;
        if (transform is null) control.RenderTransform = transform = new TranslateTransform();
        var property = horizontal ? TranslateTransform.YProperty : TranslateTransform.XProperty;
        // Quarter of the overshoot, tapering to a hard stop: pushing twice as far
        // past the limit does not move it twice as much.
        var give = Math.Sign(excess) * MaxPull * (1 - Math.Exp(-Math.Abs(excess) / 90));
        transform.Transitions = Pull(property);
        transform.SetValue(property, give);
    }

    internal static void Release(Control control, bool horizontal)
    {
        if (control.RenderTransform is not TranslateTransform transform) return;
        var property = horizontal ? TranslateTransform.YProperty : TranslateTransform.XProperty;
        if ((double)transform.GetValue(property)! == 0) return;
        transform.Transitions = Spring(property);
        transform.SetValue(property, 0.0);
    }

    // Panels arriving and leaving. Both run on the control the workspace canvas
    // holds, so the reflow transitions on the other panels carry the rest.
    internal static readonly TimeSpan EnterDuration = TimeSpan.FromMilliseconds(220);
    internal static readonly TimeSpan LeaveDuration = TimeSpan.FromMilliseconds(160);

    internal static void AnimateIn(Control control)
    {
        var scale = new ScaleTransform(.97, .97);
        control.RenderTransform = scale;
        control.RenderTransformOrigin = RelativePoint.Center;
        control.Opacity = 0;
        control.Transitions = new Transitions {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = EnterDuration, Easing = new CubicEaseOut() } };
        scale.Transitions = new Transitions {
            new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = EnterDuration, Easing = new CubicEaseOut() },
            new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = EnterDuration, Easing = new CubicEaseOut() } };
        // One frame later, so the starting values are the ones being animated from.
        Dispatcher.UIThread.Post(() => { control.Opacity = 1; scale.ScaleX = scale.ScaleY = 1; }, DispatcherPriority.Render);
    }

    internal static void AnimateOut(Control control, Action finished)
    {
        var scale = control.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
        control.RenderTransform = scale;
        control.RenderTransformOrigin = RelativePoint.Center;
        control.Transitions = new Transitions {
            new DoubleTransition { Property = Visual.OpacityProperty, Duration = LeaveDuration, Easing = new CubicEaseIn() } };
        scale.Transitions = new Transitions {
            new DoubleTransition { Property = ScaleTransform.ScaleXProperty, Duration = LeaveDuration, Easing = new CubicEaseIn() },
            new DoubleTransition { Property = ScaleTransform.ScaleYProperty, Duration = LeaveDuration, Easing = new CubicEaseIn() } };
        control.Opacity = 0; scale.ScaleX = scale.ScaleY = .97;
        // The workspace removes the panel itself; this only delays that until the
        // panel has visibly gone, and must run even if the animation is cut short.
        DispatcherTimer.RunOnce(finished, LeaveDuration);
    }
}
