using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2PluginRenderCheck
{
    internal static bool HasReadableRows(TreeDataGrid table, IReadOnlySet<string> known, int minimum = 1)
    {
        var names = table.GetVisualDescendants().OfType<TextBlock>().Where(x => x.Name == "PluginName" && x.IsEffectivelyVisible).ToArray();
        var rows = table.GetVisualDescendants().OfType<TreeDataGridRow>().Count(x => x.IsEffectivelyVisible);
        var toggles = table.GetVisualDescendants().OfType<ToggleButton>().Where(x => x.Name == "PluginActivationToggle" && x.IsEffectivelyVisible).ToArray();
        return names.Length >= minimum && names.Length == rows && toggles.Length == rows &&
            names.All(x => x.Bounds.Width >= 60 && x.Bounds.Height > 0 && x.Text is not null && known.Contains(x.Text)) &&
            toggles.All(x => x.Bounds.Width >= Mo2Density.Check && x.Bounds.Height >= Mo2Density.Check) &&
            table.Columns is { Count: 1 } columns && Math.Abs(columns[0].ActualWidth - table.Bounds.Width) <= 2;
    }

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        async Task Wait(Func<bool> ready, string message) {
            var end = DateTime.UtcNow.AddSeconds(20);
            while (!ready() && DateTime.UtcNow < end) await Task.Delay(50);
            if (!ready()) throw new Exception(message);
        }
        window.Width = 1280; window.Height = 800;
        Mo2PluginsView? view = null;
        await Wait(() => (view = window.GetVisualDescendants().OfType<Mo2PluginsView>().FirstOrDefault(x => x.IsEffectivelyVisible)) is not null &&
            live.Profile.Order.Plugins.Count >= 8, "Populated Plugins view never appeared");
        var table = view!.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        var filter = view!.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "PluginsQtFilter");
        var originalFilter = filter.Text;
        var known = live.Profile.Order.Plugins.Select(x => x.DisplayName).ToHashSet();
        var measuredSizes = new List<string>();
        TextBlock[] Names() => table.GetVisualDescendants().OfType<TextBlock>()
            .Where(x => x.Name == "PluginName" && x.IsEffectivelyVisible).ToArray();
        TreeDataGridRow[] Rows() => table.GetVisualDescendants().OfType<TreeDataGridRow>().Where(x => x.IsEffectivelyVisible).ToArray();
        bool Readable(int minimum) => HasReadableRows(table, known, minimum);
        try {
            filter.Text = "";
            foreach (var size in new[] { (1280, 800), (1120, 600), (1280, 800) }) {
                window.WindowState = size.Item1 == 1280 ? WindowState.Maximized : WindowState.Normal;
                window.Width = size.Item1; window.Height = size.Item2;
                await Wait(() => Math.Abs(window.ClientSize.Width - size.Item1) < 1,
                    "Window resize did not reach the requested width");
                await Task.Delay(100);
                await Wait(() => Readable(8), $"Plugin names/toggles do not fill their cells at {size.Item1}x{size.Item2}");
                measuredSizes.Add($"{window.ClientSize.Width:F0}x{window.ClientSize.Height:F0}");
            }
            var before = Names().Select(x => x.Text).ToArray();
            var viewer = table.GetVisualDescendants().OfType<ScrollViewer>().Single(x => x.GetVisualDescendants().OfType<TreeDataGridRowsPresenter>().Any());
            viewer.Offset = new Vector(0, viewer.Extent.Height - viewer.Viewport.Height);
            await Wait(() => Readable(8) && !Names().Select(x => x.Text).SequenceEqual(before), "Scrolling did not realize readable new plugin rows");
            var last = Names().Last().Text!;
            filter.Text = last;
            await Wait(() => Readable(1) && Names().All(x => x.Text == last), "Filtered plugin row is missing or unreadable");
            filter.Text = "";
            viewer.Offset = default;
            await Wait(() => Readable(8) && table.Rows?.Count == known.Count, "Clearing the filter did not restore readable plugin rows");
            var idle = new TaskCompletionSource();
            Avalonia.Threading.Dispatcher.UIThread.Post(() => idle.TrySetResult(), Avalonia.Threading.DispatcherPriority.Background);
            await Task.WhenAny(idle.Task, Task.Delay(1000));
            if (!idle.Task.IsCompleted) throw new Exception("Background work remained starved after rendering Plugins");
            Console.WriteLine($"PASS plugin render: every realized row has readable name and toggle; {string.Join("/", measuredSizes)} resize, scroll, filter and clear over {known.Count} plugins");
        } catch {
            Console.WriteLine($"Plugin render bounds: window={window.ClientSize}, state={window.WindowState}, requested={window.Width}x{window.Height}, table={table.Bounds}, column={table.Columns?.FirstOrDefault()?.ActualWidth}, names={Names().Length}, rows={Rows().Length}, firstName={Names().FirstOrDefault()?.Bounds}");
            throw;
        } finally { filter.Text = originalFilter; }
    }
}
