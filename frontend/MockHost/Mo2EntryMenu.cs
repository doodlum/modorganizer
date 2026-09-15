using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Avalonia.Media;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal static class Mo2EntryMenu
{
    public static Border Create(string kind, string name, Func<MenuItem[]> createItems)
    {
        var menu = new ContextMenu();
        menu.Opening += (_, _) => {
            if (menu.Items.Count != 0) return;
            foreach (var item in createItems()) menu.Items.Add(item);
        };
        var icon = new UnifiedIcon { Value = IconValues.DragVerticalDots, Size = 10,
            Foreground = Brush.Parse("#99999F"), Opacity = 0, IsHitTestVisible = false };
        var grip = new Border { Name = kind + "DragHandle", Tag = name, Width = 28, Height = 28,
            Background = Brushes.Transparent,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            Child = icon };
        TreeDataGridRow? row = null;
        void Refresh() => icon.Opacity = row is { } r && (r.IsPointerOver || r.IsSelected) ? 1 : 0;
        void Changed(object? sender, AvaloniaPropertyChangedEventArgs e) => Refresh();
        grip.AttachedToVisualTree += (_, _) => {
            row = grip.GetVisualAncestors().OfType<TreeDataGridRow>().FirstOrDefault();
            if (row is null) return;
            row.ContextMenu = menu;
            row.PropertyChanged += Changed;
            Refresh();
        };
        grip.DetachedFromVisualTree += (_, _) => {
            if (row is not null) {
                row.PropertyChanged -= Changed;
                if (ReferenceEquals(row.ContextMenu, menu)) row.ContextMenu = null;
            }
            row = null;
        };
        // A plain Border lets TreeDataGrid receive presses and begin its normal
        // selection/drag gesture. A Button captures those presses for its flyout.
        ToolTip.SetTip(grip, "Drag to reorder");
        Avalonia.Automation.AutomationProperties.SetName(grip, "Drag " + name + " to reorder");
        return grip;
    }
    public static MenuFlyout Flyout(Func<MenuItem[]> createItems)
    {
        var menu = new MenuFlyout();
        menu.Opening += (_, _) => {
            if (menu.Items.Count != 0) return;
            foreach (var item in createItems()) menu.Items.Add(item);
        };
        return menu;
    }
    // MO2's "Select Color..." as a menu of the colours it starts from, plus its own
    // "Reset Color". MO2 opens a Qt colour dialog; the frontend cannot draw that
    // dialog inside MO2's process, so the choices are offered here and MO2's own
    // action is what writes whichever one is picked.
    internal static readonly (string Name, string Value)[] Colors = [
        ("Red", "#B91C1C"), ("Orange", "#C2410C"), ("Yellow", "#A16207"), ("Green", "#15803D"),
        ("Teal", "#0F766E"), ("Blue", "#1D4ED8"), ("Purple", "#6D28D9"), ("Grey", "#44444B"),
    ];

    public static MenuItem ColorMenu(string title, bool hasColor, Func<string?, Task> choose, bool enabled = true)
    {
        var item = new MenuItem { Header = title, IsEnabled = enabled };
        foreach (var (name, value) in Colors) {
            var swatch = new Avalonia.Controls.Shapes.Rectangle { Width = 12, Height = 12, RadiusX = 2, RadiusY = 2,
                Fill = Brush.Parse(value), VerticalAlignment = VerticalAlignment.Center };
            var choice = new MenuItem { Header = name, Icon = swatch };
            var picked = value;
            choice.Click += async (_, _) => await choose(picked);
            item.Items.Add(choice);
        }
        item.Items.Add(new Separator());
        var reset = new MenuItem { Header = "Reset Color", IsEnabled = hasColor };
        reset.Click += async (_, _) => await choose(null);
        item.Items.Add(reset);
        return item;
    }

    public static MenuItem Action(string title, Func<Task> action, bool enabled = true)
    {
        Mo2UiLatencyProbe.Count("Entry menu items created");
        var item = new MenuItem { Header = title, IsEnabled = enabled };
        item.Click += async (_, _) => await action();
        return item;
    }
}
