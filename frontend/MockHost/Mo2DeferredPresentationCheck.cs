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
        await CheckPanelGeometry(window);
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
            // Two panels, and so no page header on either — which is the opposite of
            // what this used to require.
            //
            // It wanted two visible page headers and had wanted them for as long as it
            // existed. A panel that shares the workspace is named by its own tab strip,
            // so its page header would say the same thing twice, and Mo2ResponsiveHeaders
            // stands the title, pictogram, description and rule down whenever
            // IsAlone is false — keeping the page's action row, which the tab strip does
            // not carry. MO2 does the same thing: its tabs are named on the strip and
            // its pages draw no header of their own.
            //
            // So the check asserted against a deliberate decision, and nothing caught
            // the contradiction because this check had never been in a sweep. Its
            // subject is what a restored paired workspace draws; that is what it reads
            // now, and a header coming back — the page named twice over — fails it.
            var settle = DateTime.UtcNow.AddSeconds(10);
            NexusMods.App.UI.Controls.PageHeader.PageHeader[] headers;
            do {
                window.UpdateLayout();
                headers = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.PageHeader.PageHeader>()
                    .Where(h => h.IsEffectivelyVisible).ToArray();
                if (headers.Length == 0) break;
                await Task.Delay(100);
            } while (DateTime.UtcNow < settle);
            var built = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.PageHeader.PageHeader>().ToArray();
            var shown = window.GetVisualDescendants().OfType<PanelView>().Where(p => p.IsEffectivelyVisible).ToArray();
            if (headers.Length > 0)
                throw new Exception($"{headers.Length} page header(s) are drawn beside a tab strip that already names " +
                    $"the panel — {string.Join(", ", headers.Select(h => $"\"{h.Title}\""))} — in {shown.Length} panel(s) " +
                    $"at {window.ClientSize.Width:F0}x{window.ClientSize.Height:F0}");
            if (built.Length != 2)
                throw new Exception($"Expected both game pages to have built a header to stand down, found {built.Length}");
            // What does not stand down with it. The action row is the only way a
            // shared panel's page offers its own actions, and hiding the whole stack
            // once took it with them.
            foreach (var panel in shown) {
                if (!panel.GetVisualDescendants().OfType<Control>().Any(x => x.Name == "TabHeaderBorder" && x.IsEffectivelyVisible))
                    throw new Exception("A shared panel draws no tab strip, so nothing names the page in it");
                // Every action this page handed to the chrome, still on screen.
                //
                // Requiring a visible PanelActionRow outright was wrong: My Mods and
                // Plugins hand over none — MO2 works those two lists from the row's
                // own menu — so the row has nothing to hold and does not draw, which
                // is not the same as a page whose actions went missing. Read from
                // Mo2PanelChrome.Handed, the record the reachability and preset-fit
                // checks read, rather than from a second rule beside it that could
                // disagree.
                foreach (var handed in panel.GetVisualDescendants().OfType<Control>()
                             .Where(x => Mo2PanelChrome.Handed.TryGetValue(x, out _))) {
                    Mo2PanelChrome.Handed.TryGetValue(handed, out var given);
                    var lost = (given ?? []).Where(action => !handed.GetVisualDescendants().OfType<Control>()
                        .Any(x => ReferenceEquals(x, action) && x.IsEffectivelyVisible && x.Bounds.Width > 0))
                        .Select(x => x.Name ?? x.GetType().Name).ToArray();
                    if (lost.Length > 0)
                        throw new Exception($"A shared panel's page cannot reach {string.Join(", ", lost)} — its " +
                            "actions stood down with its header");
                }
            }
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

    // A panel's drawn box against the box its view model says it has.
    //
    // This compared the two the instant it was called, which every caller does
    // straight after changing the window's size or its selected tab — the one
    // moment the arranged bounds are a frame behind the model they are catching up
    // to. It then threw a sentence with no numbers in it, so a run that failed
    // could not be told apart from a run that failed for a different reason. It
    // waits for the two to agree now, and says what they were if they never do.
    internal static async Task CheckPanelGeometry(Window window)
    {
        var panels = window.GetVisualDescendants().OfType<PanelView>().Where(p => p.IsEffectivelyVisible).ToArray();
        if (panels.Length == 0) throw new Exception("No visible native panels");
        foreach (var panel in panels) {
            var canvas = panel.GetVisualAncestors().OfType<Canvas>().First(p => p.Name == "WorkspaceCanvas");
            string? apart = null;
            var settle = DateTime.UtcNow.AddSeconds(10);
            do {
                window.UpdateLayout();
                var actual = panel.ViewModel!.ActualBounds;
                var origin = panel.TranslatePoint(default, canvas) ?? throw new Exception("Detached native panel");
                apart = Math.Abs(origin.X - actual.X) > 1 || Math.Abs(origin.Y - actual.Y) > 1 ||
                        Math.Abs(panel.Bounds.Width - actual.Width) > 1 || Math.Abs(panel.Bounds.Height - actual.Height) > 1
                    ? $"drawn at {origin.X:F0},{origin.Y:F0} {panel.Bounds.Width:F0}x{panel.Bounds.Height:F0} " +
                      $"where the workspace has it at {actual.X:F0},{actual.Y:F0} {actual.Width:F0}x{actual.Height:F0}"
                    : null;
                if (apart is null) break;
                await Task.Delay(100);
            } while (DateTime.UtcNow < settle);
            if (apart is not null) throw new Exception("Deferred panel no longer matches native workspace geometry: " + apart);
        }
    }
}
