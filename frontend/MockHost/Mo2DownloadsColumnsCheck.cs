using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2DownloadsColumnsCheck
{
    internal static async Task Run(Mo2DownloadsView view)
    {
        async Task Wait(Func<bool> ready, string message) {
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception(message);
                await Task.Delay(25);
            }
        }
        await Wait(() => view.GetVisualDescendants().OfType<TreeDataGrid>().Any(x => x.Rows?.Count > 0), "Downloads table not ready");
        var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        await Task.Delay(200);
        if (view.ViewModel!.Profile.Downloads.Any(x => x.Partial) || view.Bounds.Width < 460)
            throw new Exception("Column check needs a completed-download list in a half-width panel");
        double Width(string name) => table.Columns!.Single(x => Equals(x.Header, name)).ActualWidth;
        if (Width("Name") < 180 || Math.Abs(Width("Status") - 128) > 1)
            throw new Exception("Completed downloads did not reclaim filename space");
        if (table.Background is not Avalonia.Media.ISolidColorBrush { Color.A: 0 })
            throw new Exception("Downloads table retained the old dark surface");
        if (table.ContextFlyout is null && table.ContextMenu is null)
            throw new Exception("Downloads row menu did not attach");
        var nameWidth = Width("Name");
        var header = table.GetVisualDescendants().OfType<TreeDataGridColumnHeadersPresenter>().Single();
        var menu = header.ContextFlyout as MenuFlyout ?? throw new Exception("Downloads column menu missing");
        bool HasStatus() => table.Columns!.Any(x => Equals(x.Header, "Status"));
        void ToggleStatus() => menu.Items.OfType<MenuItem>().Single(x => Equals(x.Header, "Status"))
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.ShowAt(header);
        try {
            ToggleStatus();
            await Wait(() => !HasStatus() && Math.Abs(Width("Size") - 90) <= 1, "Hiding Status changed the Size column's allocation");
            if (Width("Name") <= nameWidth + 100) throw new Exception("Hiding Status did not return its space to Name");
        } finally {
            if (!HasStatus()) ToggleStatus();
            menu.Hide();
        }
        await Wait(() => HasStatus() && Math.Abs(Width("Name") - nameWidth) <= 1, "Column widths did not restore");
        Console.WriteLine($"PASS Downloads columns: filename {nameWidth:F0}px; completed status 128px; hiding/restoring Status preserves Size and returns filename space");
    }
}
