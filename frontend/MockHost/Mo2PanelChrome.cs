using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.PageHeader;

namespace Mo2.Frontend;

// One definition of panel chrome, so every panel pads, compacts and separates its
// header the same way. Views keep their own content and control names; they hand
// their root grid and header here instead of each choosing its own metrics.
internal static class Mo2PanelChrome
{
    internal const double Padding = 24;
    // The narrowest a page title may be before it stands down and gives the line to
    // the actions. It has to clear the longest title the frontend uses — "External
    // Files" — at the header's own type size, and no more: set at 200 it took the
    // title off pages with seven actions in an ordinary half-width panel, which left
    // nothing on screen saying which page you were looking at.
    internal const double TitleFloor = 140;
    internal const double CompactPadding = 12;
    // Below this height the header/toolbar eat the content, so everything tightens.
    // The same threshold Mo2ResponsiveHeaders collapses the header at, so the padding
    // and the header stop fighting over when a panel counts as short.
    internal const double CompactBelowHeight = 400;
    internal static readonly TimeSpan CompactDuration = TimeSpan.FromMilliseconds(160);

    internal static Thickness PaddingFor(double height) =>
        new(height > 0 && height < CompactBelowHeight ? CompactPadding : Padding);

    // Header, its actions and its separator live in one stack, so the separator
    // always sits directly under the header as the reference shows, and the panel's
    // actions sit on the title's line instead of in a toolbar row of their own.
    internal static void Apply(Control view, Panel root, PageHeader header, params Control[] actions)
    {
        if (AlreadyApplied(header)) return;
        var row = Grid.GetRow(header);
        var column = Grid.GetColumn(header);
        var span = Grid.GetColumnSpan(header);
        var dock = DockPanel.GetDock(header);
        var index = root.Children.IndexOf(header);
        // Detached from whoever actually holds it, not from the root it was named
        // with: a page can nest its header inside a stack of its own, and removing it
        // from the wrong parent leaves it parented and the re-parent throws.
        if (header.Parent is Panel owner) owner.Children.Remove(header);
        header.Margin = new Thickness(0);
        var stack = new StackPanel { Name = "PanelHeaderStack", Spacing = 12 };
        stack.Children.Add(HeaderLine(header, actions));
        stack.Children.Add(new Divider { Name = "PanelHeaderSeparator", Height = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch });
        Grid.SetRow(stack, row); Grid.SetColumn(stack, column); Grid.SetColumnSpan(stack, span);
        DockPanel.SetDock(stack, dock);
        // A DockPanel gives its last child the remaining space, so the header has to
        // go back where it was rather than at the front.
        root.Children.Insert(root is DockPanel ? Math.Max(0, index) : 0, stack);
        WatchScrollers(view);

        root.Margin = PaddingFor(view.Bounds.Height);
        root.Transitions = new Transitions { new ThicknessTransition {
            Property = Layoutable.MarginProperty, Duration = CompactDuration, Easing = new CubicEaseOut() } };
        view.LayoutUpdated += (_, _) => {
            var wanted = PaddingFor(view.Bounds.Height);
            if (root.Margin != wanted) root.Margin = wanted;
        };
    }

    // Plugins docks its header rather than placing it in a grid row. Same header
    // stack, separator, padding and compaction, different parent.
    // Reusable page bodies mean a view can be set up more than once. Re-parenting a
    // header that already sits in a header stack throws, so applying twice is a no-op.
    // Matched by name, not by type: the header row changed from a DockPanel to a
    // Grid and a type-specific guard silently stopped matching, so reused bodies had
    // the chrome applied twice.
    private static bool AlreadyApplied(PageHeader header) =>
        header.Parent is Control { Name: "PanelHeaderStack" or "PanelHeaderRow" };

    internal static void ApplyDocked(Control view, DockPanel layout, PageHeader header, params Control[] actions)
    {
        if (AlreadyApplied(header)) return;
        if (header.Parent is Panel owner) owner.Children.Remove(header);
        header.Margin = new Thickness(0);
        var stack = new StackPanel { Name = "PanelHeaderStack", Spacing = 12 };
        stack.Children.Add(HeaderLine(header, actions));
        stack.Children.Add(new Divider { Name = "PanelHeaderSeparator", Height = 1,
            HorizontalAlignment = HorizontalAlignment.Stretch });
        DockPanel.SetDock(stack, Dock.Top);
        layout.Children.Insert(0, stack);
        layout.Transitions ??= new Transitions { new ThicknessTransition {
            Property = Layoutable.MarginProperty, Duration = CompactDuration, Easing = new CubicEaseOut() } };
        layout.Margin = PaddingFor(view.Bounds.Height);
        view.LayoutUpdated += (_, _) => {
            var wanted = PaddingFor(view.Bounds.Height);
            if (layout.Margin != wanted) layout.Margin = wanted;
        };
        WatchScrollers(view);
    }

    // Pages the frontend renders with a native NMA view — Downloads, Profiles and
    // Health Check — have no constructor of ours to call Apply from. They are brought
    // into the same chrome here instead of being left as the three pages that look
    // different: the header is found once the view is in the tree, its own toolbar
    // becomes the header's actions, and the rest is the shared path.
    internal static void Adopt(Control view)
    {
        void Try()
        {
            var header = view.GetVisualDescendants().OfType<PageHeader>().FirstOrDefault(x => x.IsVisible);
            if (header is null || AlreadyApplied(header)) return;
            if (header.Parent is not Panel root) return;
            // A page's toolbar moves onto the header line whole. Moving its children
            // individually fails: it is an ItemsControl and regenerates them.
            var actions = view.GetVisualDescendants().OfType<Toolbar>().FirstOrDefault();
            // The native pages space their header off the page edge themselves; the
            // shared chrome owns that spacing, so it is given back here.
            header.Margin = new Thickness(0);
            if (actions is not null) {
                actions.Margin = new Thickness(0);
                // A toolbar laid out in one row runs off the panel's edge once it is
                // on the header's line — Downloads lost its pause and resume actions
                // that way. Wrapping is the same treatment Mods gives its toolbar.
                actions.ItemsPanel = new Avalonia.Controls.Templates.FuncTemplate<Panel?>(
                    () => new WrapPanel { Orientation = Orientation.Horizontal });
                // The native toolbar draws its buttons at its own toolbar size, which
                // is 24px where every action the frontend builds is 28. Side by side
                // on one line that reads as two sets of controls. Its buttons are
                // realised by the items panel after this runs, so they are sized as
                // they appear rather than once here.
                // Applied as a style rather than by walking the tree: the toolbar
                // realises its buttons when it pleases, and a walk only ever caught
                // the ones that already existed. Size="Toolbar" also carries a
                // MaxHeight of 24 from the theme, so the cap is lifted with it.
                actions.Styles.Add(new Avalonia.Styling.Style(x => Avalonia.Styling.Selectors.OfType<StandardButton>(x)) {
                    Setters = {
                        new Avalonia.Styling.Setter(Layoutable.MaxHeightProperty, Mo2TableRow.ActionSize),
                        new Avalonia.Styling.Setter(Layoutable.MinHeightProperty, Mo2TableRow.ActionSize),
                        new Avalonia.Styling.Setter(Layoutable.HeightProperty, Mo2TableRow.ActionSize),
                    },
                });
            }
            Apply(view, root, header, actions is null ? [] : [actions]);
        }
        view.AttachedToVisualTree += (_, _) => Try();
        view.LayoutUpdated += (_, _) => Try();
        Try();
    }

    // The search control every page shows: a magnifier on the header's line that
    // reveals the page's own filter row beneath the separator. Mods and Plugins
    // already worked this way; Archives, Data, Logs and Saves kept their search box,
    // level picker and file picker on the header line itself, which is why those four
    // headers looked unlike the rest and ran out of room first.
    internal static Button SearchAction(Control filters, string tip = "Search")
    {
        filters.IsVisible = false;
        Button? action = null;
        action = Mo2TableRow.IconButton("mdi-magnify", tip, () => {
            filters.IsVisible = !filters.IsVisible;
            if (!filters.IsVisible) return;
            // Opening it puts the caret where the typing goes, as the Mods search does.
            filters.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()?.Focus();
        });
        action.Name = "SearchToggleButton";
        return action;
    }



    // The title line: actions docked right, header filling the rest. Shared so the
    // grid and docked entry points cannot drift apart.
    private static Control HeaderLine(PageHeader header, Control[] actions)
    {
        var group = new WrapPanel { Name = "PanelHeaderActions", Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var action in actions) {
            // Actions come from each panel's old toolbar row; take them out of it so
            // the row collapses and they sit on the title's line instead.
            if (action.Parent is Panel owner) owner.Children.Remove(action);
            action.Margin = new Thickness(2, 2, 0, 2);
            action.VerticalAlignment = VerticalAlignment.Center;
            group.Children.Add(action);
        }
        header.MinWidth = TitleFloor;
        // Only as tall as its own content. Stretched, the header filled whatever
        // height the actions beside it needed — and once those wrap in a narrow panel
        // that is ~100px — while its own template pins the pictogram to the top and
        // centres the title in the space. The two ended up on different lines.
        header.VerticalAlignment = VerticalAlignment.Top;
        return new Mo2HeaderLine(header, group);
    }

    // Header collapse belongs to Mo2ResponsiveHeaders, which blends the pictogram,
    // title and description against both the scroll position and the space the panel
    // has. This class used to carry a second, binary version of the same thing keyed
    // on a 60px pictogram; the responsive one runs from the window's layout, resizes
    // the pictogram to 48 first, and so the binary one never matched its plate and
    // never ran. One owner now, and the sizes live with it.
    internal static double IconSize => Mo2ResponsiveHeaders.IconSize;
    internal static double CompactIconSize => Mo2ResponsiveHeaders.CompactIconSize;

    // Lists are built lazily, so a page's scroll viewers often do not exist when it
    // attaches, and a page can own several. Each one gets the same give at its ends
    // as it appears.
    private static void WatchScrollers(Control view)
    {
        var watched = new HashSet<ScrollViewer>();
        void Watch()
        {
            foreach (var scroll in view.GetVisualDescendants().OfType<ScrollViewer>())
                if (watched.Add(scroll)) Mo2Physicality.AttachOverscroll(scroll);
        }
        view.AttachedToVisualTree += (_, _) => Watch();
        view.LayoutUpdated += (_, _) => Watch();
    }
}
