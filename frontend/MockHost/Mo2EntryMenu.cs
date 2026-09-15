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
    public static MenuItem Action(string title, Func<Task> action, bool enabled = true)
    {
        Mo2UiLatencyProbe.Count("Entry menu items created");
        var item = new MenuItem { Header = title, IsEnabled = enabled };
        item.Click += async (_, _) => await action();
        return item;
    }
}
