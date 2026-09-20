using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;

namespace Mo2.Frontend;

internal static class Mo2QtBarFitCheck
{
    internal static async Task Run(string? directory)
    {
        Console.WriteLine("CHECK Qt bar fit: starting rendered controls");
        var host = new ContentControl();
        var window = new Window { Width = 900, Height = 650, Content = host, ShowInTaskbar = false };
        window.Show();
        var faults = new List<string>();
        try {
            foreach (var create in new Func<Control>[] {
                () => new Mo2DataView(), () => new Mo2PluginsView(), () => new Mo2DownloadsView() }) {
                // Extract the real row without activating an unbound live page.
                // Profile adapters are irrelevant to the geometry being checked.
                var page = create();
                var bar = page.GetLogicalDescendants().OfType<Panel>().Single(x => x.Name?.EndsWith("QtBar") == true);
                ((Panel)bar.Parent!).Children.Remove(bar);
                bar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
                host.Content = new Border { Padding = new Thickness(bar.Margin.Left == 0 ? 24 : 0), Child = bar };
                foreach (var width in new[] { 900, 560, 420, 340, 900 }) {
                    Console.WriteLine($"CHECK Qt bar fit: {page.GetType().Name}@{width}");
                    window.Width = width;
                    for (var i = 0; i < 10; i++) { await Task.Delay(40); window.UpdateLayout(); }
                    foreach (var child in bar.Children.Where(x => x.IsVisible)) {
                        if (child is Button and not Avalonia.Controls.Primitives.ToggleButton && Math.Abs(child.Bounds.Height - Mo2TableRow.ActionSize) > 1)
                            faults.Add($"{bar.Name}@{width}: action {child.Name} height {child.Bounds.Height} differs from shared search/action size {Mo2TableRow.ActionSize}");
                        if (child is Button { Content: Control content } &&
                            (content.Bounds.Height + 1 < content.DesiredSize.Height || content.Bounds.Width + 1 < content.DesiredSize.Width))
                            faults.Add($"{bar.Name}@{width}: action {child.Name} clips its content");
                        var point = child.TranslatePoint(default, bar);
                        if (point is null || child.Bounds.Width <= 0 || child.Bounds.Height <= 0 ||
                            point.Value.X < -1 || point.Value.Y < -1 ||
                            point.Value.X + child.Bounds.Width > bar.Bounds.Width + 1 ||
                            point.Value.Y + child.Bounds.Height > bar.Bounds.Height + 1 ||
                            child.Bounds.Width + 1 < child.DesiredSize.Width)
                            faults.Add($"{bar.Name}@{width}: {child.Name ?? child.GetType().Name} escapes or is squeezed inside {bar.Bounds.Size}");
                    }
                    if (width == 340 && directory is not null) {
                        Directory.CreateDirectory(directory);
                        using var shot = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
                        shot.Render(window); shot.Save(Path.Combine(directory, bar.Name + ".png"));
                    }
                }
            }
        } finally { window.Close(); }
        if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
        Console.WriteLine("PASS Qt bar fit: Data, Plugins and Downloads controls fit at 900, 560, 420 and 340px, including widening again");
    }
}
