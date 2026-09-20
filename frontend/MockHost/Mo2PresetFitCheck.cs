using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Everything MO2 puts on a tab, drawn whole, in the layout MO2 itself uses.
//
// The frontend's own default is one page beside another; MO2's is the mod list on
// the left and its tabs on the right, which is narrower than any page was measured
// at. A widget that does not fit there is not missing — it is drawn, reported
// present, and cut in half — so this walks the preset's own tabs and fails on a
// control that is clipped by the panel holding it or squeezed below the size it
// asked for.
internal static class Mo2PresetFitCheck
{
    // A control may end up a pixel or two under what it asked for through rounding.
    private const double Slack = 2;
    // The least a list's name column can be and still read as a name rather than as
    // the icon in front of one.
    private const double NameColumn = 120;

    internal static async Task Run(Mo2LiveWorkspace live, Window window, string? directory)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();
        using var turn = await Mo2CheckTurn.Take();

        var workspace = live.WorkspaceController.ActiveWorkspace;
        var button = window.GetVisualDescendants().OfType<StandardButton>().Single(x => x.Name == "Mo2LayoutPresetButton");
        var menu = (MenuFlyout)button.Flyout!;
        var actions = menu.Items.OfType<MenuItem>().ToArray();
        var faults = new List<string>();
        var described = new List<string>();

        actions[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        try {
            await Until(() => workspace.Panels.Count == 2 && workspace.Panels.Any(x => x.Tabs.Count == 5),
                "The MO2 panel layout did not settle");
            var right = workspace.Panels.OrderBy(x => x.LogicalBounds.X).Last();
            var left = workspace.Panels.OrderBy(x => x.LogicalBounds.X).First();

            foreach (var panel in new[] { left, right })
                foreach (var tab in panel.Tabs.ToArray()) {
                    panel.SelectTab(tab.Id);
                    await Measure(panel, (tab.Contents.ViewModel as IPageViewModelInterface)?.TabTitle ?? "page",
                        tab.Contents.ViewModel);
                }

            // And the pages this frontend adds, in the same panel MO2's layout gives
            // them. MO2's six tabs are not everything this window opens: Tools, Logs,
            // Overwrite, External Files, Health Check and Profiles are reached from
            // the same sidebar, and a user in MO2's layout gets them in the same
            // half-width panel. Nothing measured them there. The preset carries MO2's
            // own tabs alone, and every other check that opens one of these pages
            // gives it a panel to itself — which is the width at which a control that
            // does not fit still does.
            right.IsSelected = true;
            await Until(() => ReferenceEquals(workspace.SelectedPanel, right), "MO2's right panel could not be selected");
            foreach (var (name, item) in new (string, NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel?)[] {
                         ("Tools", live.ProfileMenu.ToolsItem),
                         ("Logs", live.ProfileMenu.LogsItem),
                         ("Overwrite", live.ProfileMenu.OverwriteItem),
                         ("External Files", live.ProfileMenu.ExternalFilesItem),
                         ("Health Check", live.ProfileMenu.LeftMenuItemHealthCheck),
                         ("Profiles", live.ProfileMenu.ProfilesItem),
                     }) {
                if (item is null) { faults.Add($"{name} is not on the sidebar to open"); continue; }
                await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                    item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
                // Opened where it was asked for, or not measured at all: a page that
                // went into a panel of its own would be measured at a width MO2's
                // layout never gives it, and would pass for the wrong reason.
                object? page = null;
                await Until(() => workspace.Panels.Count == 2 && ReferenceEquals(workspace.SelectedPanel, right) &&
                    (page = right.SelectedTab.Contents.ViewModel) is IPageViewModelInterface model && model.TabTitle == name,
                    $"{name} did not open in MO2's right panel");
                await Measure(right, name, page);
            }
            var navigationFaults = faults.Count;
            var rightView = window.GetVisualDescendants().OfType<PanelView>().Single(x => ReferenceEquals(x.ViewModel, right));
            var tabScroll = rightView.GetVisualDescendants().OfType<ScrollViewer>().Single(x => x.Name == "TabHeaderScrollViewer");
            var beforeScroll = tabScroll.Offset.X;
            if (beforeScroll <= 0) faults.Add("tab scrolling was not exercised: last selected tab did not require scrolling");
            else {
                rightView.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "ScrollLeftButton")
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Task.Delay(350);
                window.UpdateLayout();
                if (tabScroll.Offset.X >= beforeScroll - 1) faults.Add("manual tab scrolling was overridden by selected-tab reveal");
            }
            foreach (var tab in new[] { right.Tabs[0], right.Tabs[^1] }) {
                right.SelectTab(tab.Id);
                try { await Until(() => SelectedTabVisible(rightView, right), "Selecting a distant tab did not reveal its label"); }
                catch (Exception error) {
                    var selected = rightView.GetVisualDescendants().OfType<PanelTabHeaderView>().SingleOrDefault(x => x.ViewModel?.Id == right.SelectedTab.Id);
                    throw new Exception(error.Message + $"; title={right.SelectedTab.Header.Title}; offset={tabScroll.Offset.X}; viewport={tabScroll.Viewport.Width}; extent={tabScroll.Extent.Width}; headerX={selected?.TranslatePoint(default, tabScroll)?.X}; headerWidth={selected?.Bounds.Width}");
                }
            }
            var originalWidth = window.Width;
            var originalState = window.WindowState;
            try {
                window.WindowState = WindowState.Normal;
                await Task.Delay(200);
                foreach (var width in new[] { 900.0, 640.0, 1280.0 }) {
                    window.Width = width;
                    await Until(() => Math.Abs(window.ClientSize.Width - width) < 2, $"Tab resize check did not reach requested width {width}");
                    await Task.Delay(300); window.UpdateLayout();
                    await Until(() => SelectedTabVisible(rightView, right), "Selected tab disappeared after resizing");
                }
            } finally { window.Width = originalWidth; window.WindowState = originalState; }
            if (faults.Count == navigationFaults)
                Console.WriteLine("PASS selected-tab navigation: manual scrolling retained; first/last selection and 900/640/1280-width resizing keep the selected label visible");
        } finally {
            actions[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            await Task.Delay(800);
        }

        if (faults.Count > 0) Console.WriteLine("FAIL MO2 preset fit: " + string.Join(" | ", faults));
        else Console.WriteLine("PASS MO2 preset fit: in MO2's own panel layout, every widget on " +
            string.Join(", ", described) + " is drawn whole inside its panel");

        static string Describe(Control control) =>
            (control.Name ?? control.GetType().Name) +
            (control is ContentControl { Content: string text } && text.Length > 0 ? $" (\"{text}\")" : "");

        static bool SelectedTabVisible(PanelView view, IPanelViewModel panel)
        {
            var scroll = view.GetVisualDescendants().OfType<ScrollViewer>().Single(x => x.Name == "TabHeaderScrollViewer");
            var header = view.GetVisualDescendants().OfType<PanelTabHeaderView>().SingleOrDefault(x => x.ViewModel?.Id == panel.SelectedTab.Id);
            var at = header?.TranslatePoint(default, scroll);
            return header is not null && at is not null && scroll.Viewport.Width > 0 && at.Value.X >= -Slack &&
                at.Value.X + Math.Min(header.Bounds.Width, scroll.Viewport.Width) <= scroll.Viewport.Width + Slack;
        }

        async Task Until(Func<bool> ready, string failure, int seconds = 20)
        {
            for (var attempt = 0; attempt < seconds * 10; attempt++) {
                if (ready()) return;
                await Task.Delay(100);
                window.UpdateLayout();
            }
            throw new Exception(failure);
        }

        // One page, in the panel it is drawn in.
        async Task Measure(IPanelViewModel panel, string title, object? page)
        {
            PanelView? view = null;
            await Until(() => (view = window.GetVisualDescendants().OfType<PanelView>()
                .FirstOrDefault(x => x.IsEffectivelyVisible && ReferenceEquals(x.ViewModel, panel))) is not null,
                $"{title} never drew its panel");
            // A panel reflows into its new page, so it is measured once it has
            // stopped moving rather than as soon as the page is there.
            var settled = default(Rect);
            for (var attempt = 0; attempt < 30; attempt++) {
                await Task.Delay(100);
                window.UpdateLayout();
                var now = view!.Bounds;
                if (now == settled && now.Height > 0) break;
                settled = now;
            }

            if (page is Mo2ExternalFilesPage) {
                Mo2ExternalFilesView? external = null;
                await Until(() => (external = view!.GetVisualDescendants().OfType<Mo2ExternalFilesView>()
                    .FirstOrDefault(x => ReferenceEquals(x.ViewModel, page))) is { IsReading: false },
                    "External Files scan did not finish", seconds: 120);
                var status = external!.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "ExternalFilesStatus").Text ?? "";
                if (!status.Contains("external files ·")) throw new Exception("External Files scan did not produce a classification: " + status);
                Console.WriteLine("CHECK populated External Files: " + status);
                await Task.Delay(300); window.UpdateLayout();
            }

            if (page is Mo2SavesPage savesPage) {
                var expected = await savesPage.Profile.ReadSaves(savesPage.Profile.CurrentTarget);
                TreeDataGrid? table = null;
                await Until(() => (table = view!.GetVisualDescendants().OfType<TreeDataGrid>()
                    .FirstOrDefault(x => x.IsEffectivelyVisible && x.Name == "SavesTable"))?.Source
                    is FlatTreeDataGridSource<Mo2Save> source && source.Items.SequenceEqual(expected),
                    "Saves did not display MO2's current rows");
                if (expected.Length > 0) {
                    await Until(() => table!.GetVisualDescendants().OfType<TextBlock>().Any(x =>
                        x.IsEffectivelyVisible && x.Bounds.Width >= 60 && x.Bounds.Height > 0 && x.Text == expected[0].Name),
                        "Populated Saves has no readable first name");
                    try {
                        foreach (var count in new[] { 1, Math.Min(2, expected.Length) }.Distinct()) {
                            table!.RowSelection!.Clear();
                            for (var i = 0; i < count; i++) table.RowSelection.Select(new IndexPath(i));
                            var files = table.RowSelection.SelectedItems.OfType<Mo2Save>().Select(row => row.File).ToArray();
                            var native = await savesPage.Profile.ReadListMenu("readFileMenu", files,
                                savesPage.Profile.CurrentTarget, "saves");
                            var menu = table.ContextMenu!;
                            menu.Open(table);
                            await Until(() => menu.Items.OfType<MenuItem>().Any(x =>
                                x.Header as string == $"Delete {count} save(s)" && x.IsEnabled),
                                "Saves menu did not finish its native availability lookup");
                            foreach (var item in menu.Items.OfType<MenuItem>()) {
                                var original = native.FirstOrDefault(x => x.Text == item.Header as string);
                                if (item.IsEnabled != (original?.Enabled == true))
                                    throw new Exception("Saves menu availability differs from MO2: " + item.Header);
                            }
                            menu.Close();
                        }
                    } finally { table!.ContextMenu!.Close(); table.RowSelection!.Clear(); }
                    Console.WriteLine($"PASS populated Saves: {expected.Length} native rows, readable name, single/multiple selection and native menu availability; no save action invoked");
                }
            }

            if (directory is not null) {
                Directory.CreateDirectory(directory);
                var imagePath = Path.Combine(directory, "preset-" + title.Replace(" ", "-").ToLowerInvariant() + ".png");
                if (Environment.GetEnvironmentVariable("MO2_CAPTURE_DESKTOP_PRESET") == "1") {
                    var handle = window.TryGetPlatformHandle();
                    if (handle?.HandleDescriptor != "XID") throw new Exception("Desktop preset capture requires an X11 frontend window");
                    var id = handle.Handle.ToInt64().ToString();
                    async Task<string> Command(string command, params string[] arguments) {
                        var info = new System.Diagnostics.ProcessStartInfo(command) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
                        foreach (var argument in arguments) info.ArgumentList.Add(argument);
                        using var process = System.Diagnostics.Process.Start(info) ?? throw new Exception("Could not start " + command);
                        var output = process.StandardOutput.ReadToEndAsync();
                        var error = process.StandardError.ReadToEndAsync();
                        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                        try { await process.WaitForExitAsync(timeout.Token); }
                        catch (OperationCanceledException) { process.Kill(); await process.WaitForExitAsync(); throw new Exception(command + " capture timed out"); }
                        if (process.ExitCode != 0) throw new Exception(command + ": " + await error);
                        return (await output).Trim();
                    }
                    await Command("xdotool", "windowactivate", id);
                    await Task.Delay(100);
                    if (await Command("xdotool", "getactivewindow") != id) throw new Exception("Owned frontend did not become the active capture window");
                    await Command("spectacle", "-b", "-n", "-a", "-e", "-S", "-o", Path.GetFullPath(imagePath));
                } else {
                    using var shot = new RenderTargetBitmap(new PixelSize(
                        Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height)));
                    shot.Render(window); shot.Save(imagePath);
                }
            }

            // Every widget the page draws: MO2's buttons, boxes, fields and the
            // captions beside them. A control cut off by the panel's edge, or
            // given less room than it asked for, is one the user cannot read.
            // The page in the tab that was selected, not everything the panel is
            // holding. This panel carries five tabs and keeps the view built for
            // each, so measuring the panel measured all of them at once: the
            // count of widgets examined moved between runs — Archives 24 one run
            // and 3 the next, Plugins 15 and 26 — and every one of those runs
            // passed, because a page measured with three of its widgets present
            // has no clipped ones either. Bounds stay the panel's: leaving the
            // panel is the fault being looked for.
            Control? host = null;
            await Until(() => (host = view!.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(x => ReferenceEquals(x.DataContext, page))) is not null,
                $"{title} never drew its own page inside the panel");
            static bool Widget(Control x) => x is Button or CheckBox or RadioButton or ComboBox or TextBox;
            static bool Caption(Control x) => x is TextBlock && !x.GetVisualAncestors().OfType<TreeDataGrid>().Any();
            // Data rebuilds its widgets whenever it renders, so the page can be
            // part-way through putting them back when it is measured: the count
            // examined came out 22 on two runs and 15 on a third. Wait for the
            // page to stop changing shape before reading it.
            var holding = 0;
            var seen = -1;
            for (var attempt = 0; attempt < 40 && holding < 3; attempt++) {
                await Task.Delay(100);
                window.UpdateLayout();
                var drawn = host!.GetVisualDescendants().OfType<Control>()
                    .Count(x => x.IsEffectivelyVisible && (Widget(x) || Caption(x)));
                holding = drawn == seen ? holding + 1 : 0;
                seen = drawn;
            }

            var bounds = new Rect(view!.Bounds.Size);
            // A pass is only worth reading if a fail were possible. The panel
            // cannot be made narrower from here — the live window is the one
            // the platform gave it — so the edge a control is measured against
            // is brought in instead, which drives the same comparison on the
            // same pages and shows which of them the fault path reaches.
            // MO2_PRESET_FIT_SENSITIVITY=1 is a one-off diagnostic; it is never
            // set by the suite, and a run with it on is not evidence of fit.
            if (Environment.GetEnvironmentVariable("MO2_PRESET_FIT_SENSITIVITY") == "1")
                bounds = bounds.WithWidth(bounds.Width * 0.6);
            var clipped = new List<string>();
            // A visible strip is insufficient when its selected label is scrolled
            // away. Compact pages rely on that label as their only visible title.
            if (!SelectedTabVisible(view!, panel))
                clipped.Add("selected tab label is outside the tab-strip viewport");
            // Reachability, in the layout MO2 itself uses. Every panel here
            // shares the workspace — the right one carries five tabs — which is
            // the exact condition that had been hiding every page's action row,
            // and nothing checked it under this layout: the reachability check
            // builds its pages alone, where a page is always the whole panel.
            // A control that is not there at all cannot be found to be clipped,
            // so "drawn whole" below has nothing to say about it.
            var reachable = 0;
            foreach (var handed in host!.GetVisualDescendants().OfType<Control>().Prepend(host!)
                         .Where(x => Mo2PanelChrome.Handed.TryGetValue(x, out _)).ToArray()) {
                Mo2PanelChrome.Handed.TryGetValue(handed, out var given);
                reachable += given?.Length ?? 0;
                var lost = (given ?? []).Where(action => !handed.GetVisualDescendants().OfType<Control>()
                    .Any(x => ReferenceEquals(x, action) && x.IsEffectivelyVisible && x.Bounds.Width > 0))
                    .Select(x => x.Name ?? x.GetType().Name).ToArray();
                if (lost.Length > 0) clipped.Add($"it cannot reach {string.Join(", ", lost)}");
            }
            // The page's own widgets, not the panel's tab strip: the strip
            // scrolls its tabs and squeezes its own close buttons by design,
            // which is chrome every panel shares rather than anything a page
            // draws.
            //
            // The captions count too. This said it covered "the captions beside
            // them" and then listed only the things that can be clicked, so
            // MO2's Profile, Active: and Filter labels — the words that say what
            // the widget beside them is for — were never measured at all.
            // A caption inside a table is left out: those are trimmed to an
            // ellipsis by design, which is a cell fitting its column rather than
            // a control leaving its panel.
            var examined = 0;
            foreach (var control in host!.GetVisualDescendants().OfType<Control>()
                         .Where(x => x.IsEffectivelyVisible && (Widget(x) || Caption(x)))
                         .Where(x => x.GetVisualAncestors().OfType<Control>().All(a => a.Name is not ("TabHeaderBorder" or "TabHeaderScrollViewer")))) {
                var at = control.TranslatePoint(default, view);
                if (at is null) continue;
                // A control the row had no room for at all. Skipping these is
                // how MO2's readout of what the mod list is narrowed to came
                // to be drawn 0px wide with nothing reporting it: what is
                // looked for here is a control that leaves its panel, and one
                // squeezed out of existence is not there to be found leaving
                // anything. A caption with nothing to say is not a fault —
                // MO2's currentCategoryLabel is empty until a filter is on.
                // A scrollbar's own parts are left out: its page-up and
                // page-down buttons are zero-sized by the theme's template
                // whatever room the panel has, which is the scrollbar
                // working rather than a page running out of width.
                if (control.Bounds.Width <= 0) {
                    if ((Widget(control) || control is TextBlock { Text.Length: > 0 }) &&
                        !control.GetVisualAncestors().OfType<ScrollBar>().Any())
                        clipped.Add($"{Describe(control)} was squeezed to nothing in a panel {bounds.Width:F0} wide");
                    continue;
                }
                examined++;
                var box = new Rect(at.Value, control.Bounds.Size);
                if (box.Right > bounds.Right + Slack || box.Left < bounds.Left - Slack)
                    clipped.Add($"{Describe(control)} runs to {box.Right:F0} of {bounds.Right:F0}");
                // And off the bottom, which nothing here was looking at. MO2's
                // layout is the narrow one, and what a narrow panel does to the
                // row of widgets under a list is push it out of the panel — the
                // side a width-only check cannot see. Anything inside a
                // ScrollViewer is exempt: being below the fold is what scrolling
                // is for.
                else if (!control.GetVisualAncestors().OfType<ScrollViewer>().Any() &&
                         (box.Bottom > bounds.Bottom + Slack || box.Top < bounds.Top - Slack))
                    clipped.Add($"{Describe(control)} sits at {box.Top:F0}–{box.Bottom:F0} of the panel's {bounds.Bottom:F0}");
                // What a control asked for includes the margin around it,
                // which its bounds do not: comparing the two directly reported
                // every widget with a margin as squeezed by exactly its margin.
                // Captions are left out of this one: a trimmed label asks for its
                // whole text and is meant to get less.
                else if (Widget(control) && control.DesiredSize.Width - control.Margin.Left - control.Margin.Right - control.Bounds.Width > Slack)
                    clipped.Add($"{Describe(control)} is {control.Bounds.Width:F0}px where it asked for " +
                        $"{control.DesiredSize.Width - control.Margin.Left - control.Margin.Right:F0}" +
                        $" (in {string.Join(" < ", control.GetVisualAncestors().OfType<Control>().Take(3).Select(a => $"{a.GetType().Name}#{a.Name} {a.Bounds.Width:F0}"))})");
            }
            // A page that drew nothing cannot have drawn anything badly, and
            // reported "every widget is drawn whole" for a panel that was empty.
            if (examined == 0) clipped.Add("no widget or caption was drawn to measure");
            // And the column every list is read by. A table of fixed columns
            // in a half-width panel spends its width on the ones beside the
            // name and leaves the name itself at its icon: Data drew five
            // columns of dates and sizes against a name column of nothing.
            // Only a table that has its rows: one still being given its source
            // has the columns it was born with, which is a placeholder column
            // 30px wide rather than anything the page drew.
            foreach (var table in host!.GetVisualDescendants().OfType<TreeDataGrid>()
                         .Where(x => x.Bounds.Width > 0 && x.Rows?.Count > 0)) {
                var first = table.GetVisualDescendants().OfType<TreeDataGridColumnHeader>()
                    .OrderBy(x => x.TranslatePoint(default, table)?.X ?? 0).FirstOrDefault();
                if (first is not null && first.Bounds.Width < NameColumn)
                    clipped.Add($"its name column is {first.Bounds.Width:F0}px of the table's {table.Bounds.Width:F0}");
            }
            if (clipped.Count > 0) faults.Add($"{title}: {string.Join("; ", clipped.Take(4))}");
            // The count of actions this page handed over is reported beside the
            // widgets examined, so a run where the reachability assertion found
            // nothing to judge reads as nothing rather than as agreement.
            else described.Add($"{title} ({examined}, {reachable} handed)");
        }
    }
}
