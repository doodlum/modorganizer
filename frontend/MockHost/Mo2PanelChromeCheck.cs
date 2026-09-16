using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.PageHeader;

namespace Mo2.Frontend;

// Opt-in check that every panel shares one chrome: the same root padding, the
// same compact behaviour at small heights, and a separator under the header.
internal static class Mo2PanelChromeCheck
{
    // Every panel's header actions are built with the shared action size. Read from
    // the same constant so a panel that draws its own button is caught.
    private const double ActionSize = Mo2TableRow.ActionSize;

    private static readonly Func<Control>[] Panels = [
        () => new Mo2ToolsView(), () => new Mo2ArchivesView(), () => new Mo2DataView(),
        () => new Mo2SavesView(), () => new Mo2OverwriteView(), () => new Mo2LogsView(),
        () => new Mo2ExternalFilesView(),
    ];

    internal static async Task Run()
    {
        var host = new ContentControl();
        var window = new Window { Width = 1000, Height = 700, Content = host, ShowInTaskbar = false };
        // Header collapse lives in Mo2ResponsiveHeaders, which the live window
        // attaches for every page. Attaching it here too means this check exercises
        // the same code the user sees rather than a second implementation.
        Mo2ResponsiveHeaders.Attach(window);
        window.Show();
        var report = new List<string>();
        try {
            foreach (var create in Panels) {
                var panel = create();
                var name = panel.GetType().Name;
                host.Content = panel;
                await Settle(window);

                var header = panel.GetVisualDescendants().OfType<PageHeader>().FirstOrDefault()
                    ?? throw new Exception(name + " has no page header");
                var stack = panel.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(x => x.Name == "PanelHeaderStack")
                    ?? throw new Exception(name + " does not use the shared header stack");
                // The header line is a Grid, not the DockPanel it started as: a
                // DockPanel let the actions take their width first and starved the
                // title. Matching on the old type silently stopped checking anything.
                if (!stack.Children.Contains(header) &&
                    !stack.Children.OfType<Panel>().Any(x => x.Children.Contains(header)))
                    throw new Exception(name + " header is not inside the shared stack");
                var separator = stack.Children.OfType<Control>().FirstOrDefault(x => x.Name == "PanelHeaderSeparator")
                    ?? throw new Exception(name + " has no separator under its header");
                // Not NMA's Divider, which is the 4x4 dot its status bar puts between
                // two pieces of text rather than a rule.
                if (separator is Divider) throw new Exception(name + " separates its header with a status-bar dot");
                if (stack.Children.IndexOf(separator) <= stack.Children.IndexOf(header))
                    throw new Exception(name + " separator is not below the header");
                if (separator.Bounds.Width <= 0) throw new Exception(name + " separator has no width");
                // And it runs the width of the page. It is a rule under the header,
                // not a mark in the middle of it, and at a glance a short centred dash
                // reads as a stray character rather than as the page's own divider.
                if (separator.Bounds.Width < stack.Bounds.Width - 1)
                    throw new Exception($"{name} separator is {separator.Bounds.Width:F0}px across a " +
                        $"{stack.Bounds.Width:F0}px header");

                var root = (Grid)stack.Parent!;
                // The margin animates, so every assertion waits for it to arrive.
                async Task Reaches(double height, string what)
                {
                    window.Height = height;
                    for (var attempt = 0; attempt < 40; attempt++) {
                        await Settle(window);
                        if (root.Margin == Mo2PanelChrome.PaddingFor(height)) return;
                    }
                    throw new Exception($"{name} {what} at {height}px: margin is {root.Margin.Top}");
                }
                await Reaches(700, "did not use the shared padding");

                // The group the title line keeps for actions. MO2 carries no toolbar on
                // a tab — what a row can have done to it is on its right-click menu,
                // and what a list can be filtered by is a field in the tab itself — so
                // this group is empty, and a page that drew buttons here would be
                // drawing furniture MO2 has not got. It stays in the tree because
                // Mo2PanelChrome still takes each page's actions and detaching them is
                // what stops the page drawing them a second time in a row of its own.
                //
                // This used to demand the group hold something, which was written when
                // the toolbar was still drawn. It failed on the first panel it reached
                // from that day on, and took the rest of the panels with it — the check
                // was reporting the design it was written against, not the one the app
                // has, which is the one thing a check must not do.
                var actions = panel.GetVisualDescendants().OfType<Panel>().FirstOrDefault(x => x.Name == "PanelHeaderActions")
                    ?? throw new Exception(name + " has no actions group on its header line");
                if (actions.Children.Count > 0)
                    throw new Exception($"{name} draws {actions.Children.Count} action(s) on its header line, where MO2 puts no toolbar on a tab");
                if (!stack.Children.OfType<Panel>().Any(x => x.Name == "PanelHeaderRow"))
                    throw new Exception(name + " actions are not in the header row");
                // Only icon actions belong on the header line. Search boxes and
                // pickers live in the page's own filter row beneath the separator,
                // which is what Mods and Plugins do and what Archives, Data, Logs and
                // Saves did not — they carried a text box, and Logs two pickers too,
                // which is why those four headers looked unlike the rest.
                foreach (var stray in actions.Children.Where(x => x is TextBox or ComboBox))
                    throw new Exception($"{name} has a {stray.GetType().Name} on its header line");

                // Every panel's header actions are the same size, so the row of
                // buttons reads as one control group rather than a collection.
                // Hidden actions have no size; only what is actually on the line has
                // to match.
                foreach (var button in actions.Children.OfType<Button>().Where(x => x.IsVisible)) {
                    if (Math.Abs(button.Bounds.Height - ActionSize) > .5 || Math.Abs(button.Bounds.Width - ActionSize) > .5)
                        throw new Exception($"{name} header action is {button.Bounds.Width:F0}x{button.Bounds.Height:F0}, not {ActionSize}");
                }
                for (var attempt = 0; attempt < 40 && header.GetVisualDescendants().OfType<Border>()
                         .All(x => Math.Abs(x.Width - Mo2PanelChrome.IconSize) > .5); attempt++) await Settle(window);
                var plate = header.GetVisualDescendants().OfType<Border>()
                    .FirstOrDefault(x => Math.Abs(x.Width - Mo2PanelChrome.IconSize) < .5)
                    ?? throw new Exception(name + " header pictogram is not at its full size");
                var description = header.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(x => x.Name == "DescriptionTextBlock");

                await Reaches(360, "did not compact");
                // The collapse eases rather than snapping, so it is mid-way before it
                // arrives; only the settled value is asserted.
                for (var attempt = 0; attempt < 60 && plate.Width > Mo2PanelChrome.CompactIconSize; attempt++) await Settle(window);
                if (Math.Abs(plate.Width - Mo2PanelChrome.CompactIconSize) > .5)
                    throw new Exception($"{name} pictogram stayed at {plate.Width:F0} when compact");
                for (var attempt = 0; attempt < 60 && description is { Opacity: > 0 }; attempt++) await Settle(window);
                if (description is { Opacity: > 0.01 }) throw new Exception(name + " kept its description visible when compact");

                await Reaches(700, "did not return to the shared padding");
                for (var attempt = 0; attempt < 60 && plate.Width < Mo2PanelChrome.IconSize; attempt++) await Settle(window);
                if (Math.Abs(plate.Width - Mo2PanelChrome.IconSize) > .5)
                    throw new Exception($"{name} pictogram stayed at {plate.Width:F0} after expanding");
                for (var attempt = 0; attempt < 60 && description is { Opacity: < .64 }; attempt++) await Settle(window);
                if (description is { Opacity: < .64 }) throw new Exception(name + " did not restore its description");
                if (root.Transitions is null or { Count: 0 })
                    throw new Exception(name + " compaction is not animated");
                report.Add(name);
            }
            Console.WriteLine($"PASS panel chrome: {report.Count} panels share {Mo2PanelChrome.Padding}px padding, " +
                $"an animated compaction to {Mo2PanelChrome.CompactPadding}px below {Mo2PanelChrome.CompactBelowHeight}px, " +
                $"and a header separator ({string.Join(", ", report)})");
        } finally { window.Close(); }
    }

    private static async Task Settle(Window window)
    {
        await Task.Delay(60);
        window.UpdateLayout();
    }
}
