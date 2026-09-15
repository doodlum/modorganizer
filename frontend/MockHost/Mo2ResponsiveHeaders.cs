using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
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
        public double DescriptionHeight = 48;
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

    internal static void Reveal(PanelView panel, bool wanted)
    {
        if (wanted == IsRevealed(panel)) return;
        if (wanted) Revealed.Add(panel, panel); else Revealed.Remove(panel);
        // Applied here as well as from the layout pass, so the strip appears with the
        // pointer rather than on whatever pass happens next.
        if (panel.FindControl<Control>("TabHeaderBorder") is { } tabs && panel.ViewModel?.Tabs.Count <= 1)
            tabs.IsVisible = wanted;
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
                var hidden = availableWidth < 260 || availableHeight < 300;
                // Running out of room collapses the header outright; scrolling walks
                // it there frame by frame. Easing the constrained case from inside
                // this handler produced different values on every layout pass, each
                // invalidating the next, which Avalonia ends as an infinite layout
                // loop — so the constraint stays a state change, not an animation.
                var progress = availableWidth < 360 || availableHeight < 400 ? 1 : state.ScrollAmount / ScrollDistance;
                var compact = progress >= 1;
                double Blend(double full, double small) => full + (small - full) * progress;
                Thickness ShrinkMargin(Thickness margin) => new(margin.Left, Blend(margin.Top, Math.Min(8, margin.Top)), margin.Right, Blend(margin.Bottom, 0));
                header.IsVisible = !hidden;
                if (panel is not null) WatchReveal(panel);
                if (panel?.FindControl<Control>("TabHeaderBorder") is { } tabs)
                    tabs.IsVisible = hidden || panel.ViewModel?.Tabs.Count > 1 || IsRevealed(panel);
                header.Margin = ShrinkMargin(state.Margin);
                if (state.ContainerMargin is { } margin && header.Parent is Panel container)
                    container.Margin = hidden ? new Thickness(0) : ShrinkMargin(margin);
                var icon = header.GetVisualDescendants().OfType<UnifiedIcon>().FirstOrDefault(x => x.Name == "Icon");
                if (icon is not null) {
                    icon.Size = Blend(IconSize, CompactIconSize);
                    if (icon.Parent is Border border) border.Width = border.Height = icon.Size;
                }
                foreach (var text in header.GetVisualDescendants().OfType<TextBlock>()) {
                    if (text.Name == "DescriptionTextBlock") {
                        if (double.IsPositiveInfinity(text.MaxHeight) && text.Bounds.Height > 0)
                            state.DescriptionHeight = text.Bounds.Height;
                        text.IsVisible = !compact;
                        text.Opacity = .65 * Math.Max(0, 1 - progress / .8);
                        text.ClipToBounds = true;
                        // Fade the whole description before reclaiming its height. Never
                        // clip partially visible lines while the user scrolls.
                        text.MaxHeight = progress < .8 ? double.PositiveInfinity : state.DescriptionHeight * Math.Clamp((1 - progress) / .2, 0, 1);
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
