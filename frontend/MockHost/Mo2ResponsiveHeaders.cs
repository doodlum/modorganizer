using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal static class Mo2ResponsiveHeaders
{
    internal const double ScrollDistance = 160; // Four scroll lines at 40px per line.
    // The header's pictogram at rest and when collapsed. The one owner of header
    // collapse, so the checks and the panel chrome read these rather than each
    // carrying their own idea of the size.
    internal const double IconSize = 48;
    internal const double CompactIconSize = 28;
    private sealed class State(PageHeader header)
    {
        public Thickness Margin = header.Margin;
        public ScrollViewer? Viewer;
        public double ScrollAmount;
        public void ObserveScroll(UserControl owner)
        {
            if (Viewer?.GetVisualRoot() is not null && Viewer.GetVisualAncestors().Contains(owner)) return;
            var viewer = owner.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?
                .GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault(v =>
                    v.GetVisualDescendants().Any(c => c.GetType().Name == "TreeDataGridRowsPresenter"));
            if (viewer is null || ReferenceEquals(Viewer, viewer)) return;
            Viewer = viewer;
            ScrollAmount = Math.Clamp(viewer.Offset.Y, 0, ScrollDistance);
            viewer.ScrollChanged += (_, e) => {
                // Resizing the header can clamp the offset. Only a scroll, rather
                // than a viewport resize, should change its collapsed state.
                if (ReferenceEquals(Viewer, viewer) && Math.Abs(e.ViewportDelta.Y) < .1 && Math.Abs(e.OffsetDelta.Y) > .1) {
                    ScrollAmount = viewer.Offset.Y <= .1 ? 0 : Math.Clamp(ScrollAmount + e.OffsetDelta.Y, 0, ScrollDistance);
                    header.InvalidateMeasure();
                }
            };
            viewer.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) => {
                if (e.Delta.Y > 0 && viewer.Offset.Y <= .1 && ScrollAmount > 0) {
                    ScrollAmount = Math.Max(0, ScrollAmount - e.Delta.Y * 40);
                    header.InvalidateMeasure();
                }
            }, RoutingStrategies.Tunnel);
        }
        public Thickness? ContainerMargin = header.Name == "AllPageHeader" && header.Parent?.GetType() == typeof(Panel) ? ((Panel)header.Parent).Margin : null;
    }
    // A panel holding one tab hides its tab strip, which keeps the page clean but
    // also takes away the only thing a tab can be dragged by — and with the default
    // Mods/Plugins layout that is every panel. The strip comes back when the pointer
    // reaches the top of the panel, so it is there when it is reached for and gone
    // the rest of the time.
    internal const double RevealBand = 44;
    private static readonly ConditionalWeakTable<PanelView, object> Revealed = new();
    private static readonly HashSet<PanelView> Watched = [];

    internal static bool IsRevealed(PanelView panel) => Revealed.TryGetValue(panel, out _);

    // Drives the collapse from a check without a scroll viewer to scroll. The
    // header's two faults both showed up part-way through a collapse, which is a
    // state no fixed window size reaches on its own.
    private static readonly ConditionalWeakTable<PageHeader, object> Forced = new();
    internal static void SetScrollForTesting(Control page, double amount)
    {
        foreach (var header in FindHeaders(page)) {
            Forced.Remove(header);
            Forced.Add(header, amount);
            header.InvalidateMeasure();
        }
    }
    private static double? ForcedScroll(PageHeader header) =>
        Forced.TryGetValue(header, out var value) ? (double)value : null;

    internal static void Reveal(PanelView panel, bool wanted)
    {
        if (wanted == IsRevealed(panel)) return;
        if (wanted) Revealed.Add(panel, panel); else Revealed.Remove(panel);
        if (panel.FindControl<Control>("TabHeaderBorder") is { } tabs && panel.ViewModel?.Tabs.Count <= 1)
            tabs.IsVisible = panel.ViewModel?.IsAlone == false || wanted;
    }

    private static void WatchReveal(PanelView panel)
    {
        if (!Watched.Add(panel)) return;
        panel.DetachedFromVisualTree += (_, _) => { Watched.Remove(panel); Reveal(panel, false); };
        panel.AddHandler(InputElement.PointerMovedEvent, (_, e) =>
            Reveal(panel, e.GetPosition(panel).Y <= RevealBand), RoutingStrategies.Tunnel, handledEventsToo: true);
        panel.PointerExited += (_, _) => Reveal(panel, false);
    }

    public static void Attach(Window window)
    {
        var states = new ConditionalWeakTable<PageHeader, State>();
        window.LayoutUpdated += (_, _) => {
            foreach (var header in FindHeaders(window)) {
                var owner = header.GetVisualAncestors().OfType<UserControl>().FirstOrDefault();
                if (owner is null || !owner.IsEffectivelyVisible || owner.Bounds.Width <= 0 || owner.Bounds.Height <= 0) continue;
                if (!header.IsVisible && !states.TryGetValue(header, out _)) continue;
                var state = states.GetValue(header, h => new State(h));
                state.ObserveScroll(owner);
                var panel = header.GetVisualAncestors().OfType<PanelView>().FirstOrDefault();
                var availableWidth = Math.Min(owner.Bounds.Width, panel?.Bounds.Width ?? owner.Bounds.Width);
                var availableHeight = Math.Min(owner.Bounds.Height, panel?.Bounds.Height ?? owner.Bounds.Height);
                // Whether the header is shown at all is the header line's decision —
                // it is the only thing that knows how much room the actions beside it
                // need — and it expresses that by arranging the header at nothing
                // rather than by setting a property this handler would then react to.
                var hidden = header.Bounds.Width <= 0;
                // Running out of room collapses the header outright; scrolling walks
                // it there frame by frame. Easing the constrained case from inside
                // this handler produced different values on every layout pass, each
                // invalidating the next, which Avalonia ends as an infinite layout
                // loop — so the constraint stays a state change, not an animation.
                var scroll = ForcedScroll(header) ?? state.ScrollAmount;
                var progress = availableWidth < 360 || availableHeight < 400 ? 1 : scroll / ScrollDistance;
                var compact = progress >= 1;
                double Blend(double full, double small) => full + (small - full) * progress;
                Thickness ShrinkMargin(Thickness margin) => new(margin.Left, Blend(margin.Top, Math.Min(8, margin.Top)), margin.Right, Blend(margin.Bottom, 0));
                if (panel is not null) WatchReveal(panel);
                var alone = panel?.ViewModel?.IsAlone != false;
                var stack = header.GetVisualAncestors().OfType<StackPanel>()
                    .FirstOrDefault(x => x.Name == "PanelHeaderStack");
                if (stack is not null && stack.IsVisible != alone) stack.IsVisible = alone;
                if (panel?.FindControl<Control>("TabHeaderBorder") is { } tabs)
                    tabs.IsVisible = !alone || hidden || panel.ViewModel?.Tabs.Count > 1 || IsRevealed(panel);
                header.Margin = ShrinkMargin(state.Margin);
                if (state.ContainerMargin is { } margin && header.Parent is Panel container)
                    container.Margin = hidden ? new Thickness(0) : ShrinkMargin(margin);
                var icon = header.GetVisualDescendants().OfType<UnifiedIcon>().FirstOrDefault(x => x.Name == "Icon");
                if (icon is not null) {
                    icon.Size = Blend(IconSize, CompactIconSize);
                    // Top-aligned, as the header's own template has it: at rest the
                    // pictogram is meant to span the title and the description
                    // together. What put it badly out of line with the title was the
                    // description holding height it was no longer drawing — see below.
                    if (icon.Parent is Border border) border.Width = border.Height = icon.Size;
                }
                foreach (var text in header.GetVisualDescendants().OfType<TextBlock>()) {
                    if (text.Name == "DescriptionTextBlock") {
                        // Faded out, then taken out of the layout once it is invisible.
                        // It used to be clipped to a remembered height instead, and that
                        // height was captured once, before the description had wrapped
                        // to its final number of lines — so a description that grew
                        // afterwards was cut through the middle of its last line, which
                        // is what the header looked clipped by.
                        text.Opacity = .65 * Math.Max(0, 1 - progress / .8);
                        text.IsVisible = text.Opacity > .01;
                    }
                    if (text.Name == "TitleTextBlock") {
                        text.FontSize = Blend(22, 18);
                        text.TextWrapping = compact ? TextWrapping.NoWrap : TextWrapping.Wrap;
                        text.TextTrimming = TextTrimming.CharacterEllipsis;
                    }
                }
            }
        };
    }

    private static IEnumerable<PageHeader> FindHeaders(Visual root)
    {
        foreach (var child in root.GetVisualChildren()) {
            if (child is PageHeader header) yield return header;
            // Hidden page headers must still be found so resizing can restore
            // them. Hidden ancestor branches cannot display a header, however.
            // List cells never contain page headers.
            else if (child.IsVisible && child is not TreeDataGrid)
                foreach (var nested in FindHeaders(child)) yield return nested;
        }
    }
}
