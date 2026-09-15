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
        Transitions = Mo2PanelPhysics.Resizing ? null : Reflow();
        Width = bounds.Width; Height = bounds.Height;
        SetValue(Canvas.LeftProperty, bounds.X); SetValue(Canvas.TopProperty, bounds.Y);
        _inner ??= this.GetVisualDescendants().OfType<PanelView>().FirstOrDefault();
        if (_inner is not null) { _inner.Width = bounds.Width; _inner.Height = bounds.Height; }
    }

    private Transitions Reflow() => new() {
        new DoubleTransition { Property = Layoutable.WidthProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Layoutable.HeightProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Canvas.LeftProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Canvas.TopProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
    };

    private StandardButton? _stripClose;

    private bool _adopted;
    private void AdoptChrome()
    {
        _stripClose ??= this.GetVisualDescendants().OfType<StandardButton>().FirstOrDefault(x => x.Name == "ClosePanelButton");
        if (_stripClose is { IsVisible: true }) _stripClose.IsVisible = false;
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
