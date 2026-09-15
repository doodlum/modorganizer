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
    // The panel's own corner actions are 24px, not the 28px the page toolbars use.
    private const double TabActionSize = 24;

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
                SyncMaximise();
                MaximisedChanged?.Invoke();
            }).DisposeWith(disposables);
            Disposable.Create(() => { if (ReferenceEquals(_current, this)) _current = null; }).DisposeWith(disposables);
        });
        // The native panel is built on demand, so its chrome appears later than this
        // shell. Adopt it the first layout pass after it exists.
        LayoutUpdated += (_, _) => {
            AdoptChrome();
            // The native panel is built on demand, so the first ApplyBounds runs
            // before there is anything inside to size.
            if (_inner is null) ApplyBounds();
        };
    }

    private PanelView? _inner;

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
        // The native panel sizes itself from the workspace's own bounds for the panel,
        // and maximising deliberately leaves those alone so the saved layout survives.
        // Without this the shell grew to the whole canvas while the panel inside it
        // stayed its old width and sat in the middle of the gap — which looked exactly
        // like maximise doing nothing but moving the panel.
        _inner ??= this.GetVisualDescendants().OfType<PanelView>().FirstOrDefault();
        if (_inner is not null) { _inner.Width = bounds.Width; _inner.Height = bounds.Height; }
    }

    private Transitions Reflow() => new() {
        new DoubleTransition { Property = Layoutable.WidthProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Layoutable.HeightProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Canvas.LeftProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
        new DoubleTransition { Property = Canvas.TopProperty, Duration = ReflowDuration, Easing = new CubicEaseOut() },
    };

    // Put back whichever panel is maximised, if any. Anything that rearranges the
    // layout has to start from the layout as it is stored rather than as it is drawn.
    internal static void RestoreMaximised() => _current?.SetMaximised(false);

    internal void SetMaximised(bool wanted)
    {
        if (wanted == _maximised) return;
        if (wanted && _current is { } previous && !ReferenceEquals(previous, this)) previous.SetMaximised(false);
        _maximised = wanted;
        _current = wanted ? this : ReferenceEquals(_current, this) ? null : _current;
        ApplyBounds();
        MaximisedChanged?.Invoke();
    }

    private Button? _maximise;
    private StandardButton? _stripClose;

    private void SyncMaximise()
    {
        if (_maximise is null) return;
        if (_maximise.IsVisible != CanMaximise) _maximise.IsVisible = CanMaximise;
        if (_maximise.Content is UnifiedIcon glyph)
            glyph.Value = new ProjektankerIcon(_maximised ? "mdi-window-restore" : "mdi-window-maximize");
        var label = _maximised ? "Restore panel" : "Maximise panel";
        ToolTip.SetTip(_maximise, label);
        Avalonia.Automation.AutomationProperties.SetName(_maximise, label);
    }

    private bool _adopted;
    private void AdoptChrome()
    {
        // The tab strip comes and goes with the pointer, and it carries a close
        // action of its own. Left alone, revealing the strip put a second X beside
        // the one already floating in the corner. The floating pair is the panel's
        // controls; the strip's copy stays out of the way.
        _stripClose ??= this.GetVisualDescendants().OfType<StandardButton>().FirstOrDefault(x => x.Name == "ClosePanelButton");
        if (_stripClose is { IsVisible: true }) _stripClose.IsVisible = false;
        if (_adopted) return;

        var floating = this.GetVisualDescendants().OfType<Border>().FirstOrDefault(x => x.Name == "FloatingClosePanelBorder");
        var closers = this.GetVisualDescendants().OfType<StandardButton>()
            .Where(x => x.Name is "ClosePanelButton" or "ClosePanelButton2").ToArray();
        if (floating is null || closers.Length == 0) return;
        _adopted = true;

        // Maximising is a panel action, so it sits with the panel's close action in
        // the corner rather than among the page's own actions on the header line.
        _maximise = Mo2TableRow.IconButton("mdi-window-maximize", "Maximise panel", () => SetMaximised(!_maximised));
        _maximise.Name = "MaximisePanelButton";
        _maximise.Width = _maximise.Height = TabActionSize;
        if (floating.Child is Control existing) {
            floating.Child = null;
            var pair = new StackPanel { Name = "PanelCornerActions", Orientation = Orientation.Horizontal, Spacing = 2 };
            pair.Children.Add(_maximise); pair.Children.Add(existing);
            floating.Child = pair;
        }
        MaximisedChanged += SyncMaximise;
        SyncMaximise();

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
