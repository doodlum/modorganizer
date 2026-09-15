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

    internal static void Add(Grid grid, Control control, int column)
    {
        Grid.SetColumn(control, column);
        grid.Children.Add(control);
    }

    // One size for every action a row or a panel header draws, so the two never
    // drift apart. Panel chrome sizes its non-button controls to match.
    internal const double ActionSize = 28;

    internal static Button IconButton(string icon, string tip, Action click)
    {
        var button = new Button { Width = ActionSize, Height = ActionSize, Padding = new Thickness(4), Background = Brushes.Transparent,
            Content = new UnifiedIcon { Value = new ProjektankerIcon(icon), Size = 16 } };
        ToolTip.SetTip(button, tip);
        Avalonia.Automation.AutomationProperties.SetName(button, tip);
        button.Click += (_, e) => { e.Handled = true; click(); };
        return button;
    }

    // Responsive column fitting shared by both tables: each entry names a column,
    // its natural width, and the panel width below which it is dropped. Callers may
    // also hide a column outright, which the panel width cannot override.
    internal static void Fit(Grid grid, double width, (int Column, double Width, double Threshold)[] optional,
        Func<int, bool>? hidden = null)
    {
        foreach (var (column, natural, threshold) in optional) {
            var show = width >= threshold && hidden?.Invoke(column) != true;
            grid.ColumnDefinitions[column].Width = new GridLength(show ? natural : 0);
        }
        var columns = optional.Select(x => x.Column).ToHashSet();
        foreach (var child in grid.Children) {
            var column = Grid.GetColumn(child);
            if (columns.Contains(column)) child.IsVisible = grid.ColumnDefinitions[column].Width.Value > 0;
        }
    }

    // Hover and selection read as a darkened bar rather than the default accent
    // fill, with no outline. Installed on the table so both pages share one look.
    internal static void InstallRowStyles(Control table)
    {
        Avalonia.Styling.Style Row(string pseudo, string brush) => new(x => Avalonia.Styling.Selectors.Class(Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRow>(x), pseudo)) {
            Setters = {
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.BackgroundProperty,
                    Application.Current!.FindResource(brush)),
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.BorderThicknessProperty, new Thickness(0)),
                new Avalonia.Styling.Setter(Avalonia.Controls.Primitives.TemplatedControl.CornerRadiusProperty, new CornerRadius(8)),
            }
        };
        table.Styles.Add(Row(":pointerover", "SurfaceMidBrush"));
        table.Styles.Add(Row(":selected", "SurfaceHighBrush"));
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
