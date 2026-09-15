using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Every page carries the widgets MO2 puts beside that tab's list. The columns are
// checked elsewhere; this is the furniture around them, which is what a tab is
// actually worked from — and which nothing else looks at, so a page that quietly
// stopped drawing its Refresh or its filter would not have been noticed.
//
// Run this on its own, not alongside MO2_VERIFY_ROW_STYLE or
// MO2_VERIFY_SHARED_LISTS. Reaching a page means navigating the selected panel,
// which tears down and rebuilds the view that was in it; the two list checks then
// read a Plugins table that is part-way through being rebuilt and report its rows
// and selection missing. Putting the original page back is not enough — the
// rebuild itself is what they cannot tolerate.
internal static class Mo2QtWidgetCheck
{
    // What MO2 puts on each tab, by the name the widget is built under. Taken from
    // src/mainwindow.ui: espTab's sort and backup actions, dataTab's refresh and its
    // three filter checkboxes, downloadTab's two buttons, bsaTab's note, and the
    // mod pane's own filter group.
    private static readonly (string Page, string[] Widgets)[] Wanted = [
        ("my-mods", ["ModsQtBar", "ModsFiltersClear", "ModsFiltersEdit", "ModsQtFilter", "ModCategoriesGroup"]),
        ("plugins", ["PluginsQtBar", "SortPluginsButton", "RestorePluginsButton", "SavePluginsButton",
                     "ActivePluginsCounter", "PluginsQtFilter"]),
        ("data", ["DataQtBar", "DataRefreshButton", "DataConflictsOnly", "DataFromArchives",
                  "DataHiddenFiles", "DataQtFilter"]),
        ("archives", ["ManagedArchiveLabel"]),
        ("downloads", ["DownloadsQtBar", "DownloadsRefreshButton", "DownloadsQueryButton"]),
    ];

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        var menu = live.ProfileMenu;
        var open = new Dictionary<string, Func<Task>> {
            ["my-mods"] = () => Navigate(menu.LeftMenuItemLoadout),
            ["plugins"] = () => Navigate(menu.LeftMenuItemExternalChanges!),
            ["data"] = () => Navigate(menu.DataItem),
            ["archives"] = () => Navigate(menu.ArchivesItem),
            ["downloads"] = () => Navigate(menu.LeftMenuItemLibrary),
        };

        using var turn = await Mo2CheckTurn.Take();
        var faults = new List<string>();
        var described = new List<string>();

        // Navigating replaces the selected panel's tab, so whatever was in it has to
        // go back. Left on My Mods, this took Plugins off screen for good, and the
        // row-style check that runs next then had no plugin rows to read at all.
        var panels = live.WorkspaceController.ActiveWorkspace.Panels;
        var selected = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
        var restore = selected?.SelectedTab.Contents.ViewModel is ScenarioLoadOrderPage
            ? menu.LeftMenuItemExternalChanges! : menu.LeftMenuItemLoadout;

        foreach (var (page, widgets) in Wanted) {
            await open[page]();
            // A page is built when it is first shown, so the frame right after
            // navigating is still the one before it.
            string[] missing = [];
            for (var attempt = 0; attempt < 60; attempt++) {
                await Task.Delay(100);
                window.UpdateLayout();
                // Drawn, not merely built. A collapsed control is still a visual child,
                // so looking it up by name alone passed for a category filter that was
                // hidden at every panel width the default layout uses.
                var present = window.GetVisualDescendants().OfType<Control>()
                    .Where(x => x.Name is { } name && widgets.Contains(name) && x.IsEffectivelyVisible && x.Bounds.Width > 0)
                    .Select(x => x.Name!).ToHashSet();
                missing = widgets.Where(x => !present.Contains(x)).ToArray();
                if (missing.Length == 0) break;
            }
            if (missing.Length > 0) faults.Add($"{page} is missing {string.Join(", ", missing)}");
            else described.Add($"{page} carries all {widgets.Length}");
        }

        await Navigate(restore);
        await Task.Delay(600);

        if (faults.Count > 0) Console.WriteLine("FAIL MO2 widgets: " + string.Join("; ", faults));
        else Console.WriteLine("PASS MO2 widgets: every page draws the widgets MO2 puts beside its list — " +
            string.Join(", ", described));

        // The same route the page audit takes. Reached for by reflection first, which
        // found no Command on these items and so never left the page it started on —
        // and three pages then reported every widget missing.
        static async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
        {
            if (item is null) return;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
            await Task.Delay(300);
        }
    }
}
