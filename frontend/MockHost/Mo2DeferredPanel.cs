using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Controls.Primitives;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.WorkspaceSystem;
using Avalonia.VisualTree;
using ReactiveUI;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace Mo2.Frontend;

// Position the lightweight shell immediately. The native panel is its child,
// so its own Canvas coordinates do not add a second offset inside this Grid.
internal sealed class Mo2DeferredPanel : ReactiveUserControl<IPanelViewModel>
{
    // Long enough to follow, short enough not to feel like waiting. Panels reflow
    // over this whenever one is added or closed.
    private static readonly TimeSpan ReflowDuration = TimeSpan.FromMilliseconds(220);
    // The panel's own corner actions are 24px, not the 28px the page toolbars use.
    private const double TabActionSize = 24;
    // Each panel owns its transitions. Replacing this collection on every bounds
    // notification tears down active animations, even when the mode is unchanged.
    private readonly Transitions _reflow = CreateReflow();

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
        });
        LayoutUpdated += (_, _) => {
            AdoptChrome();
            if (_inner is null) ApplyBounds();
        };
    }

    private PanelView? _inner;

    private void ApplyBounds()
    {
        if (ViewModel is null) return;
        var bounds = ViewModel.ActualBounds;
        var transitions = Mo2PanelPhysics.Resizing ? null : _reflow;
        if (!ReferenceEquals(Transitions, transitions)) Transitions = transitions;
        Width = bounds.Width; Height = bounds.Height;
        SetValue(Canvas.LeftProperty, bounds.X); SetValue(Canvas.TopProperty, bounds.Y);
        _inner ??= this.GetVisualDescendants().OfType<PanelView>().FirstOrDefault();
        if (_inner is not null) { _inner.Width = bounds.Width; _inner.Height = bounds.Height; }
    }

    private static Transitions CreateReflow() => new() {
        new DoubleTransition { Property = Layoutable.WidthProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Layoutable.HeightProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Canvas.LeftProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Canvas.TopProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
    };

    private StandardButton? _stripClose;

    private bool _adopted;
    private Border? _floatingClose;
    private void AdoptChrome()
    {
        // A panel carries no close button of its own, in the strip or floating over
        // its corner. Closing is the tab strip's job, so there is one X and it is
        // always in the same place. Upstream sets both from its own view model on
        // every change, so both are put back down on each layout pass rather than
        // once.
        _stripClose ??= this.GetVisualDescendants().OfType<StandardButton>().FirstOrDefault(x => x.Name == "ClosePanelButton");
        if (_stripClose is { IsVisible: true }) _stripClose.IsVisible = false;
        _floatingClose ??= this.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "FloatingClosePanelBorder");
        if (_floatingClose is { IsVisible: true }) _floatingClose.IsVisible = false;

        // Upstream takes the X off a tab as soon as its panel is down to one, because
        // there the panel's own floating close is what closes it. That button is gone
        // here, so a single-tab panel would have nothing left to close it with and a
        // panel could never be dismissed. The tab keeps its X instead, and closing the
        // last tab closes the panel, which is what upstream's floating close did.
        //
        // The one X that does go is the last tab of a panel filling the workspace:
        // there is nothing behind it to close back to.
        //
        // Set on the tab rather than on the button, so the command that button runs
        // agrees with whether it is shown. Upstream writes this whenever the tab count
        // reaches one, so it is written here on each layout pass rather than once.
        if (ViewModel is { } panel) {
            var closable = panel.Tabs.Count > 1 || !panel.IsAlone;
            foreach (var tab in panel.Tabs)
                if (tab.Header.CanClose != closable) tab.Header.CanClose = closable;
        }

        if (_adopted) return;

        var closers = this.GetVisualDescendants().OfType<StandardButton>()
            .Where(x => x.Name is "ClosePanelButton" or "ClosePanelButton2").ToArray();
        if (closers.Length == 0) return;
        _adopted = true;

        foreach (var close in closers)
            close.Command = ReactiveCommand.Create(() => {
                if (ViewModel is not { IsAlone: false } model) return;
                Mo2Physicality.AnimateOut(this, () => model.CloseCommand.Execute().Subscribe());
            });
    }
}
