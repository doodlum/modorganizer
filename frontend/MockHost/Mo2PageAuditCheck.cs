using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Walks every page the frontend can reach and reports one row of chrome metrics per
// page, then fails on any page that departs from the shared design. The offline
// chrome check covers the file/utility panels in isolation; this one covers the
// pages that need a live profile, and covers them where they actually live — in a
// workspace panel, at the size the user sees.
internal static class Mo2PageAuditCheck
{
    private sealed record Metrics(string Page, double Padding, int Actions, double ActionSize,
        double Pictogram, bool Separator, bool Maximise, bool Description);

    internal static async Task Run(Mo2LiveWorkspace live, Window window, string? directory)
    {
        var menu = live.ProfileMenu;
        // What the selected tab is actually showing is what proves the audit is
        // looking at the page it just asked for. A page mid-navigation still shows
        // the previous one, and capturing that reads as a finding about a page the
        // picture is not even of — Downloads was audited twice as External Files.
        // Matched on the page model rather than the header's title, because two of
        // them take their title from the native view and had nothing to match on.
        var pages = new (string Name, Func<object?, bool> Shows, Func<Task> Open)[] {
            ("my-mods", x => x is ScenarioInstalledPage, () => Navigate(menu.LeftMenuItemLoadout)),
            ("plugins", x => x is ScenarioLoadOrderPage, () => Navigate(menu.LeftMenuItemExternalChanges!)),
            ("archives", x => x is Mo2ArchivesPage, () => Navigate(menu.ArchivesItem)),
            ("data", x => x is Mo2DataPage, () => Navigate(menu.DataItem)),
            ("saves", x => x is Mo2SavesPage, () => Navigate(menu.SavesItem)),
            ("overwrite", x => x is Mo2OverwritePage, () => Navigate(menu.OverwriteItem)),
            ("external-files", x => x is Mo2ExternalFilesPage, () => Navigate(menu.ExternalFilesItem)),
            ("downloads", x => x is Mo2DownloadsPage, () => Navigate(menu.LeftMenuItemLibrary)),
            ("profiles", x => x is Mo2LoadoutsPage { GameScoped: true }, () => Navigate(menu.ProfilesItem)),
            ("health-check", x => x is Mo2HealthPage, () => Navigate(menu.LeftMenuItemHealthCheck)),
            ("tools", x => x is Mo2ToolsPage, () => Navigate(menu.ToolsItem)),
            ("logs", x => x is Mo2LogsPage, () => Navigate(menu.LogsItem)),
            ("my-games", x => x is Mo2GamesPage, () => { live.OpenGames(); return Task.CompletedTask; }),
            ("my-loadouts", x => x is Mo2LoadoutsPage { GameScoped: false }, () => { live.OpenProfiles(); return Task.CompletedTask; }),
            ("instances", x => x is Mo2ProfilesPage, () => { live.OpenConnections(); return Task.CompletedTask; }),
        };

        // Navigation replaces the selected panel's tab, so the audit has to put back
        // whatever was there before it ran: the layout is saved when the window closes.
        var restored = Selected(live)?.SelectedTab.Contents.ViewModel is ScenarioLoadOrderPage
            ? menu.LeftMenuItemExternalChanges! : menu.LeftMenuItemLoadout;

        var seen = new List<Metrics>();
        var faults = new List<string>();
        foreach (var (name, shows, open) in pages) {
            await open();
            // Pages are built on demand, so the first frame after navigating is the
            // placeholder rather than the page. Each page is read where it actually
            // lives — in the selected panel — because reading "the first header in the
            // window" silently reported the neighbouring panel's page every time.
            await Reaches(window, () => shows(Selected(live)?.SelectedTab.Contents.ViewModel) &&
                Body(live, window) is { } body && body.Bounds.Height > 100, $"{name} did not open");
            // Pages rendered by a native NMA view adopt the shared chrome on a later
            // layout pass than the one that first shows them, so reading immediately
            // reports a page as unchromed that is about to be chromed.
            var home = name is "my-games" or "my-loadouts" or "instances";
            await Task.Delay(450);
            window.UpdateLayout();
            // The page that is up mid-navigation is the previous one, and a native
            // page adopts the chrome a pass after it first draws. Waiting on one
            // sample and reading on another reported a chromed page as unchromed, so
            // the wait and the read are the same observation.
            if (!home) await Reaches(window, () => Chrome(live, window) is not null,
                $"{name} never adopted the shared header chrome", seconds: 8);
            var body = Body(live, window)!;
            // Pages can carry more than one header and hide the ones they do not use;
            // a hidden header has no applied template, so reading it reports a page
            // with no pictogram at all.
            var header = body.GetVisualDescendants().OfType<PageHeader>().FirstOrDefault(x => x.IsVisible)
                ?? body.GetVisualDescendants().OfType<PageHeader>().FirstOrDefault();
            if (directory is not null) Capture(window, Path.Combine(directory, "audit-" + name + ".png"));
            if (header is null) { faults.Add($"{name}: no page header"); continue; }

            var stack = body.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(x => x.Name == "PanelHeaderStack");
            // Read from the header row, not just the actions group: the maximise
            // action has a column of its own beside the group so a page with one wide
            // toolbar cannot push it off the line.
            var row = body.GetVisualDescendants().OfType<Panel>().FirstOrDefault(x => x.Name == "PanelHeaderRow");
            var buttons = row?.GetVisualDescendants().OfType<Button>()
                .Where(x => x.IsVisible && x.Bounds.Height > 0).ToArray() ?? [];
            var plate = header.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(x => Math.Abs(x.Width - Mo2PanelChrome.IconSize) < .5 || Math.Abs(x.Width - Mo2PanelChrome.CompactIconSize) < .5);
            var root = stack?.Parent as Control;
            var metrics = new Metrics(name,
                Padding: root?.Margin.Left ?? -1,
                Actions: buttons.Length,
                ActionSize: buttons.Select(x => x.Bounds.Height).DefaultIfEmpty(0).Max(),
                Pictogram: plate?.Width ?? -1,
                Separator: stack?.Children.OfType<Divider>().Any(x => x.Name == "PanelHeaderSeparator") == true,
                Maximise: buttons.Any(x => x.Name == "MaximisePanelButton"),
                Description: !string.IsNullOrWhiteSpace(header.Description));
            seen.Add(metrics);
            Console.WriteLine($"AUDIT {metrics.Page}: padding={metrics.Padding} actions={metrics.Actions}@{metrics.ActionSize:F0}px " +
                $"pictogram={metrics.Pictogram:F0} separator={metrics.Separator} maximise={metrics.Maximise} " +
                $"description={metrics.Description}");

            // Game icons show the artwork whole. The badge control draws a filled
            // strip across their top right corner carrying a profile number that
            // means nothing here, and it is only ever hidden by the frontend.
            // A game icon must read as artwork on a light plate. The Steam icon for
            // Skyrim is opaque black, so plating it changed nothing and the icon in
            // the spine and on every profile card stayed a black square.
            if (name == "my-loadouts") {
                foreach (var art in live.CatalogEntries.Where(x => x.Instance is not null)
                             .Select(x => x.Instance!.Game).Distinct()) {
                    var corner = Corner(Mo2GameArt.PlatedIcon(art));
                    if (corner < 140) faults.Add($"{art}'s icon sits on a plate at brightness {corner}, not on white");
                }
            }

            var badges = window.GetVisualDescendants()
                .OfType<NexusMods.App.UI.Controls.LoadoutBadge.LoadoutBadge>()
                .Count(x => x.IsVisible && x.Bounds.Width > 0);
            if (badges > 0) faults.Add($"{name}: {badges} game icons carry a badge strip");

            if (stack is null) faults.Add($"{name}: does not use the shared header stack");
            if (!metrics.Separator) faults.Add($"{name}: no separator under its header");
            // Home's workspace holds one panel, and a lone panel has nothing to
            // maximise over, so the action hides itself there.
            if (!home && !metrics.Maximise) faults.Add($"{name}: no maximise action on its header");
            if (!metrics.Description) faults.Add($"{name}: header has no description");
            // A header stood down for the actions has nothing on screen to measure.
            if (header.Bounds.Width <= 0) continue;
            if (Math.Abs(metrics.Padding - Mo2PanelChrome.Padding) > .5 &&
                Math.Abs(metrics.Padding - Mo2PanelChrome.CompactPadding) > .5)
                faults.Add($"{name}: padding is {metrics.Padding}, not the shared {Mo2PanelChrome.Padding}");
            if (metrics.Actions > 0 && Math.Abs(metrics.ActionSize - Mo2TableRow.ActionSize) > .5)
                faults.Add($"{name}: header actions are {metrics.ActionSize:F0}px, not the shared {Mo2TableRow.ActionSize}");
            if (metrics.Pictogram < 0)
                faults.Add($"{name}: header has no pictogram at either the full or compact size");
        }

        // The home pages switch the active workspace, so returning to the game's
        // workspace has to come before navigating inside it.
        if (live.Profile.IsConnected) live.ShowProfile();
        await Reaches(window, () => live.WorkspaceController.ActiveWorkspace.Context is Mo2WorkspaceContext,
            "The game workspace did not come back after the home pages", seconds: 6);
        await Navigate(restored);
        await Task.Delay(300);
        if (faults.Count > 0) throw new Exception(string.Join("; ", faults));
        Console.WriteLine($"PASS page audit: all {seen.Count} pages captured and share " +
            $"{Mo2PanelChrome.Padding}px padding, {Mo2TableRow.ActionSize}px header actions, a pictogram, a description and " +
            "a header separator; the 12 in a game workspace also carry the maximise action, and no game icon carries a badge strip");
    }

    private static StackPanel? Chrome(Mo2LiveWorkspace live, Window window) =>
        Body(live, window)?.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault(x => x.Name == "PanelHeaderStack");

    // Average brightness of the icon's four corners, which is its plate wherever the
    // artwork does not reach.
    private static int Corner(Avalonia.Media.Imaging.Bitmap icon)
    {
        var size = icon.PixelSize;
        var buffer = new byte[size.Width * size.Height * 4];
        var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { icon.CopyPixels(new PixelRect(size), handle.AddrOfPinnedObject(), buffer.Length, size.Width * 4); }
        finally { handle.Free(); }
        int At(int x, int y) { var i = (y * size.Width + x) * 4; return (buffer[i] + buffer[i + 1] + buffer[i + 2]) / 3; }
        return (At(1, 1) + At(size.Width - 2, 1) + At(1, size.Height - 2) + At(size.Width - 2, size.Height - 2)) / 4;
    }

    private static IPanelViewModel? Selected(Mo2LiveWorkspace live)
    {
        var panels = live.WorkspaceController.ActiveWorkspace.Panels;
        return panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
    }

    // The view actually drawing the selected tab's page. Matched by the page model
    // it is bound to, not by "the first visible header in the panel": the tab model
    // swaps as soon as navigation runs while the view behind it is still the
    // previous page, so the looser match audited Downloads as External Files and
    // captured a picture to go with it.
    private static Control? Body(Mo2LiveWorkspace live, Window window)
    {
        var selected = Selected(live);
        if (selected?.SelectedTab.Contents.ViewModel is not { } page) return null;
        var host = window.GetVisualDescendants().OfType<Mo2DeferredPanel>()
            .FirstOrDefault(x => ReferenceEquals(x.ViewModel, selected));
        var root = (Control?)host ?? window;
        // Bound to the page model and reading the page's own title. The model alone
        // was not enough: the tab swaps the moment navigation runs while the panel
        // is still drawing the page before it, and an audit that reads then reports
        // one page's chrome under another page's name, with a picture to match.
        var title = (page as NexusMods.App.UI.WorkspaceSystem.IPageViewModelInterface)?.TabTitle;
        // A header the chrome has given up so the actions can have the line is still
        // the right page; only a visible one can be matched on its title.
        return root.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(x => x.IsEffectivelyVisible && ReferenceEquals(x.DataContext, page) &&
                x.GetVisualDescendants().OfType<PageHeader>()
                    .Any(h => !h.IsVisible || title is null || h.Title == title));
    }

    private static Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel item) =>
        System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
            item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));

    private static void Capture(Window window, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new RenderTargetBitmap(new PixelSize(
            Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height)));
        bitmap.Render(window);
        bitmap.Save(path);
    }

    private static async Task Reaches(Window window, Func<bool> wanted, string failure, int seconds = 10)
    {
        for (var attempt = 0; attempt < seconds * 1000 / 50; attempt++) {
            if (wanted()) return;
            await Task.Delay(50);
            window.UpdateLayout();
        }
        throw new Exception(failure);
    }
}
