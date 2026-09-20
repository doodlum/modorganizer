using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Input;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.PageHeader;

namespace Mo2.Frontend;

internal static class Mo2HeaderScrollCheck
{
    internal static async Task Run()
    {
        async Task Wait(Func<bool> ready, string message) {
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!ready()) { if (DateTime.UtcNow > until) throw new Exception(message); await Task.Delay(40); }
        }
        (Window Window, Grid Grid, TreeDataGrid Table) Make() {
            var source = new FlatTreeDataGridSource<string>(Enumerable.Range(0, 100).Select(x => "Row " + x)) {
                Columns = { new TextColumn<string, string>("Name", row => row) }
            };
            var table = new TreeDataGrid { Source = source };
            var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            Grid.SetRow(table, 1); grid.Children.Add(table);
            var window = new Window { Width = 500, Height = 600, ShowInTaskbar = false, Content = new UserControl { Content = grid } };
            Mo2ResponsiveHeaders.Attach(window); window.Show();
            return (window, grid, table);
        }
        var first = Make(); var second = Make();
        var margin = new Thickness(0, 40, 0, 20);
        var header = new PageHeader { Title = "Header lifecycle", Description = "Scroll ownership follows the page.", Margin = margin };
        ScrollViewer? Viewer(TreeDataGrid table) => table.GetVisualDescendants().OfType<ScrollViewer>()
            .FirstOrDefault(x => x.GetVisualDescendants().Any(v => v.GetType().Name == "TreeDataGridRowsPresenter"));
        try {
            first.Grid.Children.Add(header);
            await Wait(() => Viewer(first.Table)?.Extent.Height > 1000 && Viewer(second.Table)?.Extent.Height > 1000 &&
                Mo2ResponsiveHeaders.ObservedScrollForTesting(header) == 0, "Header did not bind its first viewer");
            var oldViewer = Viewer(first.Table)!; var newViewer = Viewer(second.Table)!;
            oldViewer.Offset = new Vector(0, 80);
            await Wait(() => Mo2ResponsiveHeaders.ObservedScrollForTesting(header) == 80, "First viewer did not collapse the header");
            // Drain the old root's pending layout before transferring the control
            // to another window's layout manager, as deferred page transfer does.
            first.Grid.Children.Remove(header);
            first.Window.UpdateLayout();
            await Task.Delay(80);
            second.Grid.Children.Add(header);
            await Wait(() => Mo2ResponsiveHeaders.ObservedScrollForTesting(header) == 0 && header.Margin == margin,
                "Moving between windows retained the old scroll or compounded header margins");
            newViewer.Offset = new Vector(0, 80);
            await Wait(() => Mo2ResponsiveHeaders.ObservedScrollForTesting(header) == 80, "New viewer did not own header collapse");
            oldViewer.Offset = default;
            oldViewer.RaiseEvent(new PointerWheelEventArgs(oldViewer, new Pointer(0, PointerType.Mouse, true), oldViewer,
                default, 0, new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other), KeyModifiers.None, new Vector(0, 1)) {
                RoutedEvent = InputElement.PointerWheelChangedEvent
            });
            if (Mo2ResponsiveHeaders.ObservedScrollForTesting(header) != 80)
                throw new Exception("The previous viewer's wheel changed the moved header");
            second.Grid.Children.Remove(header);
            newViewer.Offset = default;
            await Task.Delay(80);
            if (Mo2ResponsiveHeaders.ObservedScrollForTesting(header) != 80)
                throw new Exception("A detached header still observes its old viewer");
            second.Grid.Children.Add(header);
            await Wait(() => Mo2ResponsiveHeaders.ObservedScrollForTesting(header) == 0 && header.Margin == margin,
                "Reopened header did not bind the current scroll state");
            Console.WriteLine("PASS header scroll lifecycle: moving between windows preserves original margins, switches scroll ownership, ignores the old viewer and unsubscribes while detached");
        } finally { first.Window.Close(); second.Window.Close(); }
    }
}
