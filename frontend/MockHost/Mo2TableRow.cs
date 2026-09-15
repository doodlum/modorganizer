using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// Table entry pieces shared by the Mods and Plugins rows. Both pages previously
// reached into Mo2ModRow for these, which made the mod row look like the owner of
// something both depend on. The metrics follow the Plugins row, which is the
// preferred treatment: a narrower leading gutter than Mods used to draw.
internal static class Mo2TableRow
{
    // Leading gutter for the drag grip. Plugins reads better with less space here
    // than the 24px the mod row used.
    internal const int GripColumn = 16;
    internal const int GripWidth = 20;
    // The status and actions columns are the same on both tables; only the columns
    // between them differ, because the two pages describe different things. The
    // status column is the enable box and the gap MO2 leaves after it.
    internal const int StatusColumn = 24;
    internal const int ActionsColumn = 60;

    // The rail of scroll and conflict indicators beside the rows. Mods pinned this at
    // 36 and Plugins let the native markup size it from its own help button, which
    // came out 24px wider — so the two tables began together and ended 24px apart.
    internal const int RailWidth = 36;

    // The band the column headings sit in. Pinned, because each page puts something
    // else of its own in that band — Mods its priority help, Plugins its conflict
    // help — and whichever page's happened to be taller set where its headings sat.
    internal const double HeadingBand = Mo2Density.Heading;

    // One inset for everything that sits in a table cell, headings included. Mods
    // used 3px on its title and 4px on version, category and the update pill while
    // Plugins used 3px throughout, so the two tables did not line up with each other
    // and Mods did not line up with its own column headings.
    internal static readonly Thickness CellMargin = Mo2Density.Cell;

    // A cell that fills its column: the shared inset, centred, and trimmed rather
    // than wrapped. Both rows build their text cells through this so neither can
    // drift from the other.
    internal static TextBlock Cell(string text, double opacity = 1)
    {
        return new TextBlock { Text = text, Opacity = opacity, Margin = CellMargin, FontSize = Mo2Density.FontSize,
            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    }

    // The heading above a cell, with the same inset so a column and its label share
    // an edge.
    internal static TextBlock Heading(string text)
    {
        // Centred like the cells below it. Left to stretch, a heading took the height
        // of whatever else shared its row — the help button on Mods, nothing on
        // Plugins — and the two tables' headings sat at different heights.
        return new TextBlock { Text = text, Margin = CellMargin, FontWeight = FontWeight.SemiBold, FontSize = Mo2Density.FontSize,
            VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
    }

    // The heading of one of MO2's glyph columns, which is the glyph itself: the column
    // is one icon wide, and a word in it is drawn as two letters and an ellipsis.
    internal static Control GlyphHeading(string icon, string name)
    {
        var glyph = new UnifiedIcon { Value = new ProjektankerIcon(icon), Size = Mo2Density.Glyph, Opacity = .75,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(glyph, name);
        Avalonia.Automation.AutomationProperties.SetName(glyph, name);
        return glyph;
    }

    internal static void Add(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    // One size for every action a row or a panel header draws, so the two never
    // drift apart. Panel chrome sizes its non-button controls to match.
    internal const double ActionSize = 24;

    // The glyph inside an action, whatever draws the action. A button built by hand
    // with a UnifiedIcon and no size gets the icon's own default of 24, which is how
    // the same set of dots came out two different sizes on the two lists' toolbars.
    internal const double GlyphSize = Mo2Density.Glyph;

    internal static Button IconButton(string icon, string tip, Action click)
    {
        var button = new Button { Width = ActionSize, Height = ActionSize, Padding = new Thickness(2), Background = Brushes.Transparent,
            Content = new UnifiedIcon { Value = new ProjektankerIcon(icon), Size = GlyphSize } };
        ToolTip.SetTip(button, tip);
        Avalonia.Automation.AutomationProperties.SetName(button, tip);
        button.Click += (_, e) => { e.Handled = true; click(); };
        return button;
    }

    // Responsive column fitting shared by both tables: each entry names a column,
    // its natural width, and the panel width below which it is dropped. Callers may
    // also hide a column outright, which the panel width cannot override.
    // What the name column is never taken below. Everything else is optional, and a
    // list whose names are all drawn as "DLC: Triba…" says less than one carrying a
    // column fewer.
    internal const double NameFloor = 180;

    // `width` is the panel the list sits in, which is what a column's threshold is
    // written against; `chrome` is what that panel spends on everything beside the
    // columns — the filter group, the rail, the page's padding — so the floor is
    // measured against the room the columns actually have.
    internal static void Fit(Grid grid, double width, (int Column, double Width, double Threshold)[] optional,
        Func<int, bool>? hidden = null, double chrome = 0)
    {
        var shown = new List<(int Column, double Width, double Threshold)>();
        foreach (var entry in optional)
            if (width >= entry.Threshold && hidden?.Invoke(entry.Column) != true) shown.Add(entry);
        // The name column takes what the others leave, so the others are given up —
        // the one that says least about a mod first — until it has enough to read.
        while (shown.Count > 0 && width - chrome - shown.Sum(x => x.Width) < NameFloor)
            shown.Remove(shown.MaxBy(x => x.Threshold));
        foreach (var (column, natural, _) in optional) {
            var show = shown.Any(x => x.Column == column);
            grid.ColumnDefinitions[column].Width = new GridLength(show ? natural : 0);
        }
        var columns = optional.Select(x => x.Column).ToHashSet();
        foreach (var child in grid.Children) {
            var column = Grid.GetColumn(child);
            if (columns.Contains(column)) child.IsVisible = grid.ColumnDefinitions[column].Width.Value > 0;
        }
    }

    // Row styling shared by both tables: no surface of its own at rest, a
    // translucent overlay on hover, and a border outline on selection. Targeting
    // the CellsPresenter for hover and selection keeps the row's own background
    // free for conflict highlights, and produces the outline the SortOrder theme
    // uses rather than a filled bar the default theme uses.
    internal static void InstallRowStyles(Control table)
    {
        // Neither the table nor its rows draw a surface, so what shows behind an
        // entry is the panel it sits in. Both the NMA base theme (SurfaceLow on the
        // table) and its SortOrder styles (a card on each row) paint one otherwise,
        // and the two themes disagree about which — so both are cleared here.
        if (table is TreeDataGrid tdg) tdg.Background = Brushes.Transparent;

        // Resting: nothing of the row's own, so the panel behind it shows through.
        // The corner radius still matters — the hover overlay and selection outline
        // are rounded against it.
        table.Styles.Add(new Avalonia.Styling.Style(x =>
            Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRow>(x)) {
            Setters = {
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, Brushes.Transparent),
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.CornerRadiusProperty, new CornerRadius(Mo2Density.Corner)),
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.BorderThicknessProperty, new Thickness(0)),
            }
        });

        // Hover: a translucent overlay on the cells, not a fill on the row.
        Func<Avalonia.Styling.Selector?, Avalonia.Styling.Selector> CellsIn(string pseudo) => x =>
            Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridCellsPresenter>(
                Avalonia.Styling.Selectors.Template(
                    Avalonia.Styling.Selectors.Class(
                        Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRow>(x), pseudo)));

        table.Styles.Add(new Avalonia.Styling.Style(CellsIn(":pointerover")) {
            Setters = {
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty,
                    Application.Current!.FindResource("SurfaceTranslucentLowBrush")),
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.CornerRadiusProperty, new CornerRadius(Mo2Density.Corner)),
            }
        });

        // Selected: a border outline, not a background fill.
        table.Styles.Add(new Avalonia.Styling.Style(CellsIn(":selected")) {
            Setters = {
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty, (IBrush?)null),
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.BorderBrushProperty,
                    Application.Current!.FindResource("StrokeTranslucentModerateBrush")),
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.BorderThicknessProperty, new Thickness(2)),
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.CornerRadiusProperty, new CornerRadius(Mo2Density.Corner)),
            }
        });

        // Selected + hover: overlay plus outline.
        var selectedHover = new Avalonia.Styling.Style(x =>
            Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridCellsPresenter>(
                Avalonia.Styling.Selectors.Template(
                    Avalonia.Styling.Selectors.Class(
                        Avalonia.Styling.Selectors.Class(
                            Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRow>(x),
                            ":selected"), ":pointerover")))) {
            Setters = {
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty,
                    Application.Current!.FindResource("SurfaceTranslucentLowBrush")),
            }
        };
        table.Styles.Add(selectedHover);
    }

    // The Vortex view-options control: a gear listing the optional columns plus a
    // reset. Both tables already re-run Fit from their headings' LayoutUpdated, so a
    // toggle only has to change the set and ask for another layout pass.
    internal static Button ColumnsButton((int Column, string Name)[] optional, HashSet<int> hidden, Control table, Action? persist = null)
    {
        var button = IconButton("mdi-tune-variant", "Choose columns", () => { });
        button.Name = "ColumnsButton";
        var menu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        button.Flyout = menu;
        void Refresh() { persist?.Invoke(); table.InvalidateMeasure(); table.InvalidateArrange(); }
        menu.Opening += (_, _) => {
            menu.Items.Clear();
            foreach (var (column, label) in optional) {
                var shown = !hidden.Contains(column);
                var item = new MenuItem { Header = label, StaysOpenOnClick = true,
                    Icon = new CheckBox { IsChecked = shown, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center } };
                var target = column;
                item.Click += (_, _) => {
                    if (!hidden.Remove(target)) hidden.Add(target);
                    ((CheckBox)item.Icon!).IsChecked = !hidden.Contains(target);
                    Refresh();
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new MenuItem { Header = "-" });
            var reset = new MenuItem { Header = "Reset to default", IsEnabled = hidden.Count > 0 };
            reset.Click += (_, _) => { hidden.Clear(); Refresh(); menu.Hide(); };
            menu.Items.Add(reset);
        };
        return button;
    }
}
