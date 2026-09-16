using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// How much of each list the window actually shows, measured where it shows it.
//
// MO2 fits about forty rows in a window this size; the theme this frontend borrows
// draws cards, and every page inherited them. Row heights are set in four places —
// the theme, the deferred placeholder, the row's own content and the page's
// headings — so a page can be given the shorter row and still draw the tall one,
// which is exactly what happened and what nothing was measuring.
internal static class Mo2DensityCheck
{
    // A row is allowed to be a little taller than the metric — a focus ring, a
    // border — but not half as tall again.
    private const double Slack = 4;

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        var menu = live.ProfileMenu;
        var pages = new (string Name, Func<Task> Open)[] {
            ("my-mods", () => Navigate(menu.LeftMenuItemLoadout)),
            ("plugins", () => Navigate(menu.LeftMenuItemExternalChanges!)),
            ("archives", () => Navigate(menu.ArchivesItem)),
            ("data", () => Navigate(menu.DataItem)),
            ("saves", () => Navigate(menu.SavesItem)),
            ("overwrite", () => Navigate(menu.OverwriteItem)),
            ("external-files", () => Navigate(menu.ExternalFilesItem)),
            ("downloads", () => Navigate(menu.LeftMenuItemLibrary)),
            ("tools", () => Navigate(menu.ToolsItem)),
            ("logs", () => Navigate(menu.LogsItem)),
        };

        using var turn = await Mo2CheckTurn.Take();
        var panels = live.WorkspaceController.ActiveWorkspace.Panels;
        var selected = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
        var restore = selected?.SelectedTab.Contents.ViewModel is ScenarioLoadOrderPage
            ? menu.LeftMenuItemExternalChanges! : menu.LeftMenuItemLoadout;

        var faults = new List<string>();
        var measured = new List<string>();
        foreach (var (name, open) in pages) {
            await open();
            double tallest = 0;
            var rows = 0;
            var located = false;
            var settled = 0;
            var previous = -1;
            for (var attempt = 0; attempt < 80; attempt++) {
                await Task.Delay(100);
                window.UpdateLayout();
                // Rows that are actually on screen in the panel that was navigated;
                // a table being rebuilt briefly has none, and one off screen has no
                // height to compare. Scoped to that panel: the window shows two at
                // once, so measuring the whole window reported the neighbouring Mods
                // list under every page's name.
                if (Panel(live, window) is not { } panel) continue;
                // The page now in that panel, not the panel itself. A panel keeps the
                // view it was showing before, and the retained one stays visible in the
                // tree beside the new one: measured by panel, every page came out as
                // its own rows plus the page before it — 24 for the eleven-plugin list
                // after My Mods' thirteen, 32 for the archives after that. Each number
                // was wrong and every one of them passed, because both lists draw the
                // same 22px row.
                if (Page(live, panel) is not { } view) continue;
                located = true;
                var drawn = view.GetVisualDescendants().OfType<TreeDataGridRow>()
                    .Where(x => x.IsEffectivelyVisible && x.Bounds.Height > 0).ToArray();
                if (drawn.Length == 0) { previous = -1; settled = 0; continue; }
                if (drawn.Length == previous) settled++; else { settled = 1; previous = drawn.Length; }
                rows = drawn.Length;
                tallest = drawn.Max(x => x.Bounds.Height);
                // Wait for the count to hold still, not merely for the height to be
                // acceptable. Every row in every list is already the right height, so
                // the height alone was satisfied by the very first reading — taken
                // 100ms after navigating, while the page being left and the page
                // arriving were both in the panel. That read 24 rows for the
                // eleven-plugin list and passed, because adding the mod list next to
                // it changes the count and not the height.
                if (settled >= 3 && tallest <= Mo2Density.Row + Slack) break;
            }
            // Without the panel there is nothing this check may honestly measure.
            // Falling back to the window measured both panels at once and summed
            // them: after a check that navigates had run first, this reported 24
            // rows for the eleven-plugin list — thirteen mods next door added in —
            // and still passed, because every row in both panels is the right
            // height. A number that is wrong in a way the assertion cannot see is
            // worse than no number.
            if (!located) { faults.Add($"{name}'s panel could not be found, so its rows were not measured"); continue; }
            if (rows == 0) { measured.Add($"{name} has no rows to measure"); continue; }
            measured.Add($"{name} {rows}×{tallest:F0}px");
            if (tallest > Mo2Density.Row + Slack)
                faults.Add($"{name} draws {tallest:F0}px rows, not MO2's {Mo2Density.Row}");
        }

        await Navigate(restore);
        await Task.Delay(600);

        if (faults.Count > 0) Console.WriteLine("FAIL MO2 density: " + string.Join("; ", faults) +
            " — measured " + string.Join(", ", measured));
        else Console.WriteLine($"PASS MO2 density: every page's rows are within {Slack}px of MO2's " +
            $"{Mo2Density.Row}px line — " + string.Join(", ", measured));

        // The panel the navigation went to, which is the one holding the page being
        // measured. Null when it cannot be identified, so the caller can say so
        // rather than measure whatever else the window is showing.
        static Mo2DeferredPanel? Panel(Mo2LiveWorkspace live, Window window)
        {
            var panels = live.WorkspaceController.ActiveWorkspace.Panels;
            var chosen = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
            return window.GetVisualDescendants().OfType<Mo2DeferredPanel>()
                .FirstOrDefault(x => ReferenceEquals(x.ViewModel, chosen));
        }

        // The view built for the page the panel is showing now, found by the page's
        // own view model. Null while the panel is between pages, which is a reason
        // to look again rather than to measure what is left over from before.
        static Avalonia.Visual? Page(Mo2LiveWorkspace live, Mo2DeferredPanel panel)
        {
            var panels = live.WorkspaceController.ActiveWorkspace.Panels;
            var chosen = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
            if (chosen?.SelectedTab.Contents.ViewModel is not { } page) return null;
            return panel.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(x => ReferenceEquals(x.DataContext, page));
        }

        static async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
        {
            if (item is null) return;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
            await Task.Delay(300);
        }
    }
}
