using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.PageHeader;

namespace Mo2.Frontend;

// A page header has to stay whole at every size the panel can be. It collapses as
// the panel runs out of room — the pictogram shrinks, the description fades and
// gives its height back — but nothing in it may ever be cut off, and the title
// least of all. This walks a panel down through the sizes a person can drag it to
// and checks the header at each one.
internal static class Mo2HeaderFitCheck
{
    private static readonly (double Width, double Height)[] Sizes = [
        (1000, 700), (760, 700), (560, 700), (420, 700), (340, 700),
        (1000, 520), (1000, 420), (1000, 380), (1000, 320), (1000, 260),
        (420, 420), (340, 320), (1000, 700),
    ];

    internal static async Task Run()
    {
        var host = new ContentControl();
        var window = new Window { Width = 1000, Height = 700, Content = host, ShowInTaskbar = false };
        Mo2ResponsiveHeaders.Attach(window);
        window.Show();
        var faults = new List<string>();
        try {
            foreach (var create in new Func<Control>[] { () => new Mo2ExternalFilesView(), () => new Mo2ToolsView(), () => new Mo2OverwriteView() }) {
                var page = create();
                var name = page.GetType().Name;
                host.Content = page;
                await Settle(window, 6);

                foreach (var (width, height) in Sizes) {
                    window.Width = width; window.Height = height;
                    await Settle(window, 8);
                    // Part-way through a collapse is where both of these went wrong,
                    // so every size is also checked mid-scroll rather than only at the
                    // two ends.
                    foreach (var scrolled in new[] { 0d, Mo2ResponsiveHeaders.ScrollDistance / 2, Mo2ResponsiveHeaders.ScrollDistance }) {
                    Mo2ResponsiveHeaders.SetScrollForTesting(page, scrolled);
                    await Settle(window, 4);
                    var header = page.GetVisualDescendants().OfType<PageHeader>().FirstOrDefault();
                    if (header is null) { faults.Add($"{name} {width}x{height}@{scrolled:F0}: no header at all"); continue; }
                    var row = page.GetVisualDescendants().OfType<Panel>().FirstOrDefault(x => x.Name == "PanelHeaderRow");
                    if (row is null) { faults.Add($"{name} {width}x{height}@{scrolled:F0}: no header row"); continue; }

                    var title = header.GetVisualDescendants().OfType<TextBlock>().FirstOrDefault(x => x.Name == "TitleTextBlock");
                    // A header given up so the actions can have the line is intended,
                    // not a fault; there is simply nothing left in it to measure.
                    if (header.Bounds.Width <= 0) continue;
                    if (title is null || !title.IsVisible) { faults.Add($"{name} {width}x{height}@{scrolled:F0}: no visible title"); continue; }

                    // The row must give the header everything it asked for. A row that
                    // clips is a row that is shorter than its own content.
                    if (row.Bounds.Height + .5 < row.DesiredSize.Height)
                        faults.Add($"{name} {width}x{height}@{scrolled:F0}: header row is {row.Bounds.Height:F0}px for {row.DesiredSize.Height:F0}px of content");

                    // And the title must be whole: its arranged height has to cover
                    // the text, and it has to sit inside the row that draws it.
                    if (title.Bounds.Height + .5 < title.DesiredSize.Height)
                        faults.Add($"{name} {width}x{height}@{scrolled:F0}: title is {title.Bounds.Height:F0}px tall for {title.DesiredSize.Height:F0}px of text");
                    if (title.TranslatePoint(new Point(0, title.Bounds.Height), row) is { } bottom && bottom.Y > row.Bounds.Height + .5)
                        faults.Add($"{name} {width}x{height}@{scrolled:F0}: title runs {bottom.Y - row.Bounds.Height:F0}px past the bottom of its row");
                    if (title.Text is { Length: > 0 } && title.Bounds.Height <= 0)
                        faults.Add($"{name} {width}x{height}@{scrolled:F0}: title has no height at all");

                    // The description is shown whole or not at all. Clipping it to a
                    // remembered height cut through the middle of its last line once
                    // it had wrapped to more lines than when that height was taken.
                    var description = header.GetVisualDescendants().OfType<TextBlock>()
                        .FirstOrDefault(x => x.Name == "DescriptionTextBlock");
                    if (description is { IsVisible: true, Opacity: > .05 } &&
                        description.Bounds.Height + .5 < description.DesiredSize.Height)
                        faults.Add($"{name} {width}x{height}@{scrolled:F0}: description is {description.Bounds.Height:F0}px for " +
                            $"{description.DesiredSize.Height:F0}px of text");

                    // The actions never become a vertical strip. They give way in
                    // order: title and actions side by side, then the title goes and
                    // the actions take the whole line, and only then do they wrap onto
                    // a second row. Capping them against the title from the start
                    // stacked them one per row instead.
                    var group = page.GetVisualDescendants().OfType<Panel>().FirstOrDefault(x => x.Name == "PanelHeaderActions");
                    var shown = group?.Children.Where(x => x.IsVisible && x.Bounds.Height > 0).ToArray() ?? [];
                    if (shown.Length > 1) {
                        var rows = shown.Select(x => Math.Round(x.Bounds.Y)).Distinct().Count();
                        if (rows > 2)
                            faults.Add($"{name} {width}x{height}@{scrolled:F0}: actions took {rows} rows");
                        else if ((double)shown.Length / rows < 2)
                            faults.Add($"{name} {width}x{height}@{scrolled:F0}: actions stacked {shown.Length} over {rows} rows");
                        if (rows > 1 && header.Bounds.Width > 0)
                            faults.Add($"{name} {width}x{height}@{scrolled:F0}: actions wrapped while the title was still shown");
                    }

                    // Once the description is gone the header is one line, and the
                    // pictogram has to sit on it. While the description is still shown
                    // the pictogram deliberately spans both, so it is not compared.
                    // What broke this was the description keeping height it had
                    // stopped drawing, which pushed the title down away from a
                    // pictogram that had already shrunk.
                    var plate = header.GetVisualDescendants().OfType<Border>()
                        .FirstOrDefault(x => x.Child is NexusMods.UI.Sdk.Icons.UnifiedIcon);
                    var oneLine = description is null || !description.IsVisible || description.Bounds.Height < 1;
                    if (oneLine && plate is not null && plate.Bounds.Height > 0 && title.Bounds.Height > 0 &&
                        plate.TranslatePoint(new Point(0, plate.Bounds.Height / 2), header) is { } plateMiddle &&
                        title.TranslatePoint(new Point(0, title.Bounds.Height / 2), header) is { } titleMiddle) {
                        var drift = Math.Abs(plateMiddle.Y - titleMiddle.Y);
                        // Half the title's own height: beyond that they no longer read
                        // as being on the same line.
                        var allowed = Math.Max(8, title.Bounds.Height / 2);
                        if (drift > allowed)
                            faults.Add($"{name} {width}x{height}@{scrolled:F0}: pictogram sits {drift:F0}px off the title (allowed {allowed:F0})");
                    }
                    }
                }
            }
            if (faults.Count > 0) throw new Exception(string.Join("; ", faults.Take(6)) +
                (faults.Count > 6 ? $" (+{faults.Count - 6} more)" : ""));
            Console.WriteLine($"PASS header fit: three panels kept their header whole at {Sizes.Length} sizes each, " +
                "from 1000x700 down to 340x320 and back");
        } finally { window.Close(); }
    }

    private static async Task Settle(Window window, int passes)
    {
        for (var pass = 0; pass < passes; pass++) {
            await Task.Delay(50);
            window.UpdateLayout();
        }
    }
}
