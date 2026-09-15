using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using NexusMods.App.UI.WorkspaceSystem;
using System.Reactive.Linq;

namespace Mo2.Frontend;

internal static class Mo2DeferredPresentationCheck
{
    internal static async Task CheckPairedWorkspace(Mo2LiveWorkspace shell, Window window)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while ((!shell.Profile.IsConnected || shell.Profile.SelectingProfile || shell.Profile.ProfilePath.Length == 0 ||
                window.GetVisualDescendants().OfType<PanelView>().Count(p => p.IsEffectivelyVisible) != 2) && DateTime.UtcNow < until)
            await Task.Delay(50);
        await Task.Delay(1500);
        await CheckHeaderRestoration(window);
        CheckPanelGeometry(window);
        var resizer = window.GetVisualDescendants().OfType<PanelResizerView>().Single(p => p.IsEffectivelyVisible);
        var point = resizer.TranslatePoint(new Point(resizer.Bounds.Width / 2, resizer.Bounds.Height / 2), window)!.Value;
        var hit = window.InputHitTest(point) as Visual;
        if (hit != resizer && hit?.GetVisualAncestors().Contains(resizer) != true)
            throw new Exception($"Native divider cannot receive input at {point}: {hit?.GetType().Name}");
        window.AddHandler(InputElement.PointerPressedEvent, (_, args) => Console.WriteLine(
            "CHECK window press " + args.GetPosition(window) + " source " + args.Source?.GetType().Name),
            Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        resizer.PointerPressed += (_, args) => Console.WriteLine("CHECK divider pressed " + args.GetPosition(window));
        resizer.PointerReleased += (_, args) => Console.WriteLine("CHECK divider released " + args.GetPosition(window));
        var moves = resizer.ViewModel!.DragStartCommand.Subscribe(value => Console.WriteLine("CHECK divider drag " + value));
        var ends = resizer.ViewModel.DragEndCommand.Subscribe(_ => Console.WriteLine("CHECK divider drag ended"));
        window.Closed += (_, _) => { moves.Dispose(); ends.Dispose(); };
        Console.WriteLine($"PASS paired native panel geometry and divider hit testing at {point}");
    }

    internal static async Task CheckHeaderRestoration(Window window)
    {
        var originalState = window.WindowState;
        var originalHeight = window.Height;
        var originalWidth = window.Width;
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("Responsive header restoration did not settle");
                await Task.Delay(25);
            }
        }
        try {
            window.WindowState = WindowState.Normal;
            await Task.Delay(200);
            window.Width = 1280;
            window.Height = 750;
            await Wait(() => Math.Abs(window.ClientSize.Height - 750) <= 1 && Math.Abs(window.ClientSize.Width - 1280) <= 1);
            var headers = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.PageHeader.PageHeader>()
                .Where(h => h.IsEffectivelyVisible).ToArray();
            if (headers.Length != 2) throw new Exception("Expected two visible game page headers");
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_HEADER_SCROLL") == "1") {
                var rails = window.GetVisualDescendants().OfType<Mo2ListScrollBar>()
                    .Where(r => r.IsEffectivelyVisible && r.Name is "ModsRailScrollBar" or "PluginRailScrollBar").ToArray();
                if (rails.Length != 2) throw new Exception("Expected both game list scrollbar rails");
                window.Height = 650;
                await Wait(() => Math.Abs(window.ClientSize.Height - 650) <= 1 && rails.All(r => r.Maximum >= 160));
                // Let the list viewport resize finish before sending scroll input;
                // viewport clamping must not be mistaken for a user's scroll step.
                await Task.Delay(200);
                foreach (var rail in rails) rail.Value = 0;
                await Task.Delay(100);
                for (var line = 1; line <= 4; line++) {
                    foreach (var rail in rails) rail.Value += 40;
                    var expectedSize = 48 - line * 5;
                    await Task.Delay(100);
                    Console.WriteLine($"HEADER STEP {line}: " + string.Join(", ", headers.Select(h => h.GetVisualDescendants()
                        .OfType<NexusMods.UI.Sdk.Icons.UnifiedIcon>().Single(i => i.Name == "Icon").Size)));
                    Console.WriteLine("HEADER OWNERS: " + string.Join("; ", headers.Select(h => h.GetVisualAncestors()
                        .OfType<UserControl>().First().Bounds)) + " window " + window.ClientSize);
                    await Wait(() => headers.All(h => Math.Abs(h.GetVisualDescendants().OfType<NexusMods.UI.Sdk.Icons.UnifiedIcon>()
                        .Single(i => i.Name == "Icon").Size - expectedSize) < .1));
                    if (line < 4 && headers.Any(h => h.GetVisualDescendants().OfType<TextBlock>()
                        .Single(t => t.Name == "DescriptionTextBlock") is not { IsVisible: true, Opacity: > 0 } text || !double.IsPositiveInfinity(text.MaxHeight)))
                        throw new Exception("Visible header description clipped during scroll");
                }
                if (headers.Any(h => h.GetVisualDescendants().OfType<TextBlock>()
                    .Single(t => t.Name == "DescriptionTextBlock").IsVisible))
                    throw new Exception("Description remains after four scroll lines");
                foreach (var rail in rails) rail.Value = 0;
                await Wait(() => headers.All(h => h.GetVisualDescendants().OfType<TextBlock>()
                    .Single(t => t.Name == "DescriptionTextBlock").IsVisible));
                Console.WriteLine("PASS four-line header collapse: 48 to 28px, visible descriptions unclipped, restored at top");
            }
            window.Height = 250;
            await Wait(() => Math.Abs(window.ClientSize.Height - 250) <= 1 && headers.All(h => !h.IsVisible));
            if (window.GetVisualDescendants().OfType<PanelView>().Where(p => p.IsEffectivelyVisible)
                .Any(p => p.FindControl<Control>("TabHeaderBorder")?.IsVisible != true))
                throw new Exception("Hidden headers lost their tab bars");
            window.Height = 750;
            await Wait(() => Math.Abs(window.ClientSize.Height - 750) <= 1 && headers.All(h => h.IsEffectivelyVisible));
            Console.WriteLine("PASS responsive headers hide at constrained height, retain tabs, and reappear after enlargement");
        } finally { window.Width = originalWidth; window.Height = originalHeight; window.WindowState = originalState; }
        await Task.Delay(200);
    }

    internal static async Task CheckRowLifecycle(Window window)
    {
        var root = (Grid)window.Content!;
        var host = new Grid { Width = 1, Height = 1, IsHitTestVisible = false };
        root.Children.Add(host);
        var builds = 0;
        var row = new Mo2DeferredRow(() => { builds++; return new Border(); });
        try {
            host.Children.Add(row); host.Children.Remove(row);
            await Task.Delay(150);
            if (builds != 0 || row.Child is not null)
                throw new Exception("Detached row ran its pending construction");
            host.Children.Add(row);
            var until = DateTime.UtcNow.AddSeconds(5);
            while (row.Child is null && DateTime.UtcNow < until) await Task.Delay(25);
            if (builds != 1 || row.Child is null) throw new Exception("Reattached row did not load");
            var body = row.Child;
            host.Children.Remove(row); host.Children.Add(row);
            await Task.Delay(150);
            if (builds != 1 || !ReferenceEquals(body, row.Child))
                throw new Exception("Reattached loaded row rebuilt its controls");
            Console.WriteLine("PASS deferred row lifecycle: cancel detached work, load on reattach, reuse loaded controls");
        } finally { root.Children.Remove(host); }
    }

    internal static void CheckPanelGeometry(Window window)
    {
        var panels = window.GetVisualDescendants().OfType<PanelView>().Where(p => p.IsEffectivelyVisible).ToArray();
        if (panels.Length == 0) throw new Exception("No visible native panels");
        foreach (var panel in panels) {
            var canvas = panel.GetVisualAncestors().OfType<Canvas>().First(p => p.Name == "WorkspaceCanvas");
            var actual = panel.ViewModel!.ActualBounds;
            var origin = panel.TranslatePoint(default, canvas) ?? throw new Exception("Detached native panel");
            if (Math.Abs(origin.X - actual.X) > 1 || Math.Abs(origin.Y - actual.Y) > 1 ||
                Math.Abs(panel.Bounds.Width - actual.Width) > 1 || Math.Abs(panel.Bounds.Height - actual.Height) > 1)
                throw new Exception("Deferred panel no longer matches native workspace geometry");
        }
    }
}
