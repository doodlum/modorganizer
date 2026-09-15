using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.WorkspaceSystem;
using Avalonia.VisualTree;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Mo2.Frontend;

// Position the lightweight shell immediately. The native panel is its child,
// so its own Canvas coordinates do not add a second offset inside this Grid.
internal sealed class Mo2DeferredPanel : ReactiveUserControl<IPanelViewModel>
{
    // Long enough to follow, short enough not to feel like waiting. Panels reflow
    // over this whenever one is added, closed or maximised.
    private static readonly TimeSpan ReflowDuration = TimeSpan.FromMilliseconds(220);

    private bool _maximised;
    // Only one panel can be maximised at a time, and a second panel taking over has
    // to put the first one back.
    private static Mo2DeferredPanel? _current;

    internal bool IsMaximised => _maximised;
    // The pages' shared chrome carries the action and has to follow the state, which
    // another panel taking over can change without this panel being pressed.
    internal event Action? MaximisedChanged;
    internal bool CanMaximise => ViewModel?.IsAlone == false;

    internal Mo2DeferredPanel(IPanelViewModel model)
    {
        ViewModel = model;
        Name = "Mo2Panel";
        var grid = new Grid();
        grid.Children.Add(Mo2DeferredView<IPanelViewModel>.For(model, "panel",
            panel => new PanelView { ViewModel = panel }));
        Content = grid;
        Mo2Physicality.AnimateIn(this);
        this.WhenActivated(disposables => {
            this.WhenAnyValue(view => view.ViewModel!.ActualBounds).Subscribe(_ => ApplyBounds()).DisposeWith(disposables);
            // A lone panel has nothing to maximise over, and the native close action
            // is disabled in the same case.
            this.WhenAnyValue(view => view.ViewModel!.IsAlone).Subscribe(alone => {
                if (alone && _maximised) SetMaximised(false);
                MaximisedChanged?.Invoke();
            }).DisposeWith(disposables);
            Disposable.Create(() => { if (ReferenceEquals(_current, this)) _current = null; }).DisposeWith(disposables);
        });
        // The native panel is built on demand, so its chrome appears later than this
        // shell. Adopt it the first layout pass after it exists.
        LayoutUpdated += (_, _) => AdoptChrome();
    }

    private void ApplyBounds()
    {
        if (ViewModel is null) return;
        var bounds = ViewModel.ActualBounds;
        if (_maximised && Parent is Canvas canvas && canvas.Bounds.Width > 0)
            bounds = new Rect(0, 0, canvas.Bounds.Width, canvas.Bounds.Height);
        // Dragging a divider has to track the pointer exactly; everything else is a
        // rearrangement and animates.
        Transitions = Mo2PanelPhysics.Resizing ? null : Reflow();
        Width = bounds.Width; Height = bounds.Height;
        SetValue(Canvas.LeftProperty, bounds.X); SetValue(Canvas.TopProperty, bounds.Y);
        ZIndex = _maximised ? 1000 : 0;
    }

    private Transitions Reflow() => new() {
        new DoubleTransition { Property = Layoutable.WidthProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Layoutable.HeightProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Canvas.LeftProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Canvas.TopProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
    };

    internal void SetMaximised(bool wanted)
    {
        if (wanted == _maximised) return;
        if (wanted && _current is { } previous && !ReferenceEquals(previous, this)) previous.SetMaximised(false);
        _maximised = wanted;
        _current = wanted ? this : ReferenceEquals(_current, this) ? null : _current;
        ApplyBounds();
        MaximisedChanged?.Invoke();
    }

    private bool _adopted;
    private void AdoptChrome()
    {
        if (_adopted) return;
        var closers = this.GetVisualDescendants().OfType<StandardButton>()
            .Where(x => x.Name is "ClosePanelButton" or "ClosePanelButton2").ToArray();
        if (closers.Length == 0) return;
        _adopted = true;
        // Closing goes through the panel's own animation first. Re-pointing the
        // command replaces the binding PanelView makes, so the native command still
        // runs, only after the panel has visibly left.
        foreach (var close in closers)
            close.Command = ReactiveCommand.Create(() => {
                if (ViewModel is not { IsAlone: false } model) return;
                if (_maximised) SetMaximised(false);
                Mo2Physicality.AnimateOut(this, () => model.CloseCommand.Execute().Subscribe());
            });
    }
}
