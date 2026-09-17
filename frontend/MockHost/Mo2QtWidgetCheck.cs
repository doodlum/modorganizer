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
// What MO2 draws is read out of src/mainwindow.ui by Mo2QtWidgetSource, not typed
// out here. The list that used to be typed out held 38 names over five pages and
// MO2's file holds 73 over six: the mod list's own "Filter:" readout and its Clear
// all Filters button, both lists, the Saves tab entirely, and the run row were
// never looked for. A hand-written expectation agrees with MO2 only on the day it
// is written, and nothing said when it had stopped.
//
// So every name in MO2's file has to be accounted for below, and every account has
// to name a widget that is still in MO2's file. A widget added to mainwindow.ui
// fails this check until it is answered for; an answer for a widget MO2 has
// removed fails it too.
//
// Run this on its own, not alongside MO2_VERIFY_ROW_STYLE or
// MO2_VERIFY_SHARED_LISTS. Reaching a page means navigating the selected panel,
// which tears down and rebuilds the view that was in it; the two list checks then
// read a Plugins table that is part-way through being rebuilt and report its rows
// and selection missing. Putting the original page back is not enough — the
// rebuild itself is what they cannot tolerate.
internal static class Mo2QtWidgetCheck
{
    // How a widget of MO2's is answered for here.
    internal enum Kind
    {
        // Drawn on the page named, as the tab MO2 puts it on is drawn.
        Page,
        // Drawn in the window whatever page is open: MO2 keeps its run row and its
        // menus outside the tabs, and so does this frontend.
        Window,
        // A Qt container with nothing of its own on screen — a splitter, a layout
        // host, the central widget. Its contents are answered for separately.
        Container,
        // A menu or a toolbar: no widget of its own, only the actions it carries.
        // Answered by requiring every action MO2 puts on it to be one
        // MO2_VERIFY_QT_ACTIONS offers here, which is where they are driven.
        Carries,
        // Deliberately not drawn where MO2 draws it, because this frontend offers
        // the same thing from a page of its own. Still found, on that page, and
        // reported apart from the widgets that are where MO2 puts them — so the
        // difference is stated in every run rather than left to a comment.
        Elsewhere,
    }

    internal sealed record Answer(Kind Kind, string Page, string Counterpart, Func<Control, bool> Match, string Note = "");

    private static Answer Named(string page, string name, string note = "") =>
        new(Kind.Page, page, name, x => x.Name == name, note);
    private static Answer List(string page, string note) =>
        new(Kind.Page, page, "its list", x => x is Avalonia.Controls.TreeDataGrid, note);
    private static Answer Chrome(string name, string note = "") =>
        new(Kind.Window, "", name, x => x.Name == name, note);
    private static Answer Holds(string reason) => new(Kind.Container, "", reason, _ => true, reason);
    private static Answer Carries(string note = "") => new(Kind.Carries, "", "the actions it carries", _ => true, note);
    private static Answer Elsewhere(string page, string name, string note) =>
        new(Kind.Elsewhere, page, name, x => x.Name == name, note);

    // MO2's widget, and what this frontend draws for it. Everything in
    // src/mainwindow.ui appears here exactly once.
    internal static readonly Dictionary<string, Answer> Answers = new() {
        // The window itself and the boxes Qt lays its widgets out in.
        ["MainWindow"] = Holds("the window"),
        ["centralWidget"] = Holds("the window's body"),
        ["categoriesSplitter"] = Holds("the split between the filter list and the mod pane"),
        ["splitter"] = Holds("the split between the mod pane and the tabs"),
        ["widget"] = Holds("the row holding And, Or and the separators box"),
        ["widget_2"] = Holds("the row holding Clear and Edit..."),
        ["layoutWidget"] = Holds("the column holding the mod list and the rows around it"),
        ["layoutWidget_2"] = Holds("the column holding the run row and the tabs"),
        ["dockWidgetContents"] = Holds("the log dock's body"),
        ["tabWidget"] = Holds("the tab strip, which is the workspace's own panel of tabs here"),
        ["espTab"] = Holds("the Plugins tab, walked by this check as a page"),
        ["bsaTab"] = Holds("the Archives tab, walked by this check as a page"),
        ["dataTab"] = Holds("the Data tab, walked by this check as a page"),
        ["savesTab"] = Holds("the Saves tab, walked by this check as a page"),
        ["downloadTab"] = Holds("the Downloads tab, walked by this check as a page"),

        // The filter list beside the mod list, and the two rows of controls in it.
        ["categoriesGroup"] = Named("my-mods", "ModCategoriesGroup"),
        ["filters"] = Named("my-mods", "ModCategories"),
        ["filtersClear"] = Named("my-mods", "ModsFiltersClear"),
        ["filtersEdit"] = Named("my-mods", "ModsFiltersEdit"),
        ["filtersAnd"] = Named("my-mods", "ModsFiltersAnd"),
        ["filtersOr"] = Named("my-mods", "ModsFiltersOr"),
        ["filtersSeparators"] = Named("my-mods", "ModsFiltersSeparators"),

        // The line above the mod list.
        // MO2 heads its mod pane with a "Profile" caption and a box to change it in.
        // This frontend has a page for the instances it is connected to and the
        // profiles in each, which is where a profile is chosen and where the one in
        // use is named; a second chooser above the mod list offered the same switch
        // from a place that showed none of what it would switch to.
        ["label_3"] = Elsewhere("connections", "Mo2ChooseProfile", "MO2's \"Profile\" caption; the page names each profile on its own entry"),
        ["profileBox"] = Elsewhere("connections", "Mo2ChooseProfile", "MO2's profile box"),
        ["listOptionsBtn"] = Named("my-mods", "ModsListOptionsButton"),
        ["openFolderMenu"] = Named("my-mods", "ModsOpenFolderButton"),
        ["restoreModsButton"] = Named("my-mods", "RestoreModsButton"),
        ["saveModsButton"] = Named("my-mods", "SaveModsButton"),
        ["activeModslabel"] = Named("my-mods", "ActiveModsLabel", "MO2's \"Active:\" caption"),
        ["activeModsCounter"] = Named("my-mods", "ActiveModsCounter"),
        ["modList"] = List("my-mods", "MO2's mod list"),

        // The line under it.
        ["displayCategoriesBtn"] = Named("my-mods", "ModsDisplayCategoriesButton"),
        ["label_2"] = Named("my-mods", "ModsFilterLabel", "MO2's \"Filter\" caption"),
        ["currentCategoryLabel"] = Named("my-mods", "ModsCurrentCategoryLabel", "what the list is narrowed to"),
        ["clearFiltersButton"] = Named("my-mods", "ModsClearFiltersButton"),
        ["groupCombo"] = Named("my-mods", "ModsGroupBox"),
        ["modFilterEdit"] = Named("my-mods", "ModsQtFilter"),

        // MO2's run row, which it keeps beside the tabs rather than on one.
        ["startGroup"] = Chrome("Mo2RunRow", "the box MO2 runs an executable from"),
        ["executablesListBox"] = Chrome("ExecutablesListBox"),
        ["startButton"] = new(Kind.Window, "", "the Run button",
            x => x is NexusMods.App.UI.LeftMenu.Items.LaunchButtonView, "MO2's startButton"),
        ["linkButton"] = Chrome("ShortcutMenuButton", "MO2's shortcut menu"),

        // The Plugins tab.
        ["sortButton"] = Named("plugins", "SortPluginsButton"),
        ["restoreButton"] = Named("plugins", "RestorePluginsButton"),
        ["saveButton"] = Named("plugins", "SavePluginsButton"),
        ["activePluginsLabel"] = Named("plugins", "ActivePluginsLabel", "MO2's \"Active:\" caption"),
        ["activePluginsCounter"] = Named("plugins", "ActivePluginsCounter"),
        ["espList"] = List("plugins", "MO2's plugin list"),
        ["espFilterEdit"] = Named("plugins", "PluginsQtFilter"),

        // The Archives tab.
        ["managedArchiveLabel"] = Named("archives", "ManagedArchiveLabel"),
        ["bsaList"] = Named("archives", "ArchivesTable"),

        // The Data tab.
        ["dataTabRefresh"] = Named("data", "DataRefreshButton"),
        ["dataTree"] = List("data", "MO2's Data tree"),
        ["dataTabShowOnlyConflicts"] = Named("data", "DataConflictsOnly"),
        ["dataTabShowFromArchives"] = Named("data", "DataFromArchives"),
        ["dataTabShowHiddenFiles"] = Named("data", "DataHiddenFiles"),
        ["dataTabFilter"] = Named("data", "DataQtFilter"),

        // The Saves tab, which MO2 draws as its list alone.
        ["savegameList"] = Named("saves", "SavesTable"),

        // The Downloads tab.
        ["btnRefreshDownloads"] = Named("downloads", "DownloadsRefreshButton"),
        ["btnQueryDownloadsInfo"] = Named("downloads", "DownloadsQueryButton"),
        ["downloadView"] = List("downloads", "MO2's download list"),
        ["showHiddenBox"] = Named("downloads", "DownloadsHiddenFiles"),
        ["downloadFilterEdit"] = Named("downloads", "DownloadsQtFilter"),

        // The window's chrome. MO2's menu bar and toolbar carry actions rather than
        // widgets of their own, so what is asked of them is that every action they
        // carry is one MO2_VERIFY_QT_ACTIONS answers for and drives.
        ["menuBar"] = Carries("MO2's menu bar, whose five menus are answered one by one"),
        ["menuFile"] = Carries(),
        ["menuView"] = Carries(),
        ["menuTools"] = Carries(),
        ["menuHelp"] = Carries(),
        ["menuToolbars"] = Carries(),
        ["toolBar"] = Carries("MO2's toolbar, whose executables are the pinned row here"),
        // MO2 fills its Run menu at runtime from the executables it is configured
        // with, so its own file declares it empty; the executables box and the
        // pinned row beside it are where those are run from here.
        ["menuRun"] = Chrome("ExecutablesListBox", "MO2 fills its Run menu from its executables"),
        ["statusBar"] = Chrome("Mo2StatusBar", "what MO2 reports along the bottom of its window"),
        ["logDock"] = Chrome("Mo2LogsMenuItem", "the way to MO2's log, which MO2 docks and this frontend opens as a page"),
        ["logList"] = Chrome("Mo2LogsMenuItem", "the same; the lines themselves are the Logs page's list"),
    };

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
            ["saves"] = () => Navigate(menu.SavesItem),
            ["downloads"] = () => Navigate(menu.LeftMenuItemLibrary),
            // The home workspace's own page, for the widgets this frontend offers
            // there rather than beside MO2's list.
            ["connections"] = () => { live.OpenConnections(); return Task.CompletedTask; },
        };

        using var turn = await Mo2CheckTurn.Take();
        // MO2 keeps its run row on screen always; this frontend can fold it away.
        // Compared against MO2 with it open, and put back as it was found.
        using var sidebar = await Mo2SidebarState.Open(window, live);
        var faults = new List<string>();
        var described = new List<string>();

        // MO2's own file is the list of what has to be here. Read first, so a run
        // that cannot find it fails rather than checking nothing.
        var source = Mo2QtWidgetSource.Locate();
        var widgets = Mo2QtWidgetSource.Read(source);
        if (widgets.Length == 0) throw new Exception("No widget was read from " + source);

        // Both ways round. A widget MO2 has and this file does not answer for is the
        // fault this check exists to catch; an answer for a widget MO2 no longer has
        // is an expectation that has quietly stopped meaning anything.
        var unanswered = widgets.Where(x => !Answers.ContainsKey(x.Name)).Select(x => $"{x.Name} ({x.Class})").ToArray();
        if (unanswered.Length > 0)
            faults.Add($"src/mainwindow.ui names {unanswered.Length} widget(s) nothing answers for: {string.Join(", ", unanswered)}");
        var stale = Answers.Keys.Where(x => !widgets.Any(w => w.Name == x)).ToArray();
        if (stale.Length > 0)
            faults.Add($"{stale.Length} answer(s) name a widget MO2 has removed: {string.Join(", ", stale)}");
        // Everything below reads an answer by name. A widget with none is already a
        // fault above; looking one up anyway would end the run in a
        // KeyNotFoundException and report that instead of what was actually wrong.
        widgets = widgets.Where(x => Answers.ContainsKey(x.Name)).ToArray();

        // Navigating replaces the selected panel's tab, so whatever was in it has to
        // go back. Left on My Mods, this took Plugins off screen for good, and the
        // row-style check that runs next then had no plugin rows to read at all.
        var panels = live.WorkspaceController.ActiveWorkspace.Panels;
        var selected = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
        var restore = selected?.SelectedTab.Contents.ViewModel is ScenarioLoadOrderPage
            ? menu.LeftMenuItemExternalChanges! : menu.LeftMenuItemLoadout;

        // MO2's menus and its toolbar hold actions rather than widgets. Every one
        // they hold has to be an action MO2_VERIFY_QT_ACTIONS offers here and
        // drives; a menu that carries nothing answered is the same fault as a
        // missing widget.
        var menus = widgets.Where(x => Answers[x.Name].Kind == Kind.Carries).ToArray();
        var carried = 0;
        foreach (var host in menus) {
            var entries = Mo2QtWidgetSource.Carries(host.Name, source);
            // A menu bar carries menus, which are answered in their own right.
            var wanted = entries.Where(x => !widgets.Any(w => w.Name == x)).ToArray();
            var lost = wanted.Where(x => !Mo2QtActions.Answers.ContainsKey(x)).ToArray();
            if (entries.Length == 0) faults.Add($"{host.Name} carries nothing in {Path.GetFileName(source)}");
            else if (lost.Length > 0) faults.Add($"{host.Name} carries {string.Join(", ", lost)}, which nothing offers here");
            else carried += wanted.Length;
        }
        if (menus.Length > 0 && faults.Count == 0)
            described.Add($"MO2's {menus.Length} menus and its toolbar carry {carried} action(s), every one offered here");

        // The chrome first, from wherever the window happens to be: MO2 keeps these
        // outside its tabs and so does this frontend.
        var chrome = widgets.Where(x => Answers[x.Name].Kind == Kind.Window).ToArray();
        var missingChrome = (await Missing(window, live, chrome)).ToArray();
        if (missingChrome.Length > 0) faults.Add("the window is missing " + string.Join(", ", missingChrome));
        else if (chrome.Length > 0) described.Add($"the window carries all {chrome.Length} MO2 keeps outside its tabs");

        foreach (var page in open.Keys) {
            var wanted = widgets.Where(x => Answers[x.Name] is { Kind: Kind.Page or Kind.Elsewhere } answer && answer.Page == page).ToArray();
            if (wanted.Length == 0) continue;
            await open[page]();
            // MO2's filter list starts hidden and so does this one —
            // restoreVisibility(ui->categoriesGroup, false) in mainwindow.cpp is what
            // a profile with nothing saved gets, whatever the .ui this check reads
            // declares. A widget behind its own button is not a missing widget, so it
            // is opened through that button to be measured and put back the way it
            // was found: what this check is for is whether the widget is there and
            // can be drawn, not which way its button happens to be set.
            var folded = page == "my-mods" ? await OpenCategories(window, true) : (bool?)null;
            var missing = (await Missing(window, live, wanted)).ToArray();
            if (folded is { } was) await OpenCategories(window, was);
            if (missing.Length > 0) faults.Add($"{page} is missing {string.Join(", ", missing)}");
            else if (wanted.All(x => Answers[x.Name].Kind == Kind.Elsewhere))
                described.Add($"{page} carries the {wanted.Length} MO2 puts beside its mod list — " +
                    string.Join(", ", wanted.Select(x => $"{x.Name} as {Answers[x.Name].Counterpart}")));
            else described.Add($"{page} carries all {wanted.Length}");
        }

        // MO2's displayCategoriesBtn does something as well as being there: it is what
        // shows and hides the filter list beside the mod list, and a list of mods is
        // what the pane is for. Driven here rather than only looked up, because the
        // fault this check exists to catch — a widget that draws but does nothing —
        // is exactly what a lookup passes.
        // Back to the profile's own workspace first: the page walk above ends on the
        // home workspace's Connections page, and the profile sidebar's items navigate
        // a workspace that is no longer the active one — so this drove nothing and
        // reported the mod pane as having no filter-list button at all.
        live.ShowProfile();
        await Task.Delay(400);
        await open["my-mods"]();
        await Task.Delay(400);
        window.UpdateLayout();
        var toggle = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
            .FirstOrDefault(x => x.Name == "ModsDisplayCategoriesButton");
        Control? Group() => window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(x => x.Name == "ModCategoriesGroup");
        if (toggle is null) faults.Add("my-mods has no button to show and hide its filter list");
        else {
            var wasShowing = Group()?.IsEffectivelyVisible == true;
            toggle.IsChecked = false;
            await Task.Delay(300); window.UpdateLayout();
            if (Group()?.IsEffectivelyVisible == true) faults.Add("the filter list stayed when its button was switched off");
            toggle.IsChecked = true;
            await Task.Delay(300); window.UpdateLayout();
            if (Group()?.IsEffectivelyVisible != true) faults.Add("the filter list did not come back when its button was switched on");
            else described.Add("the filter list follows its button");
            toggle.IsChecked = wasShowing;
        }

        await Navigate(restore);
        await Task.Delay(600);

        if (faults.Count > 0) Console.WriteLine("FAIL MO2 widgets: " + string.Join("; ", faults));
        else Console.WriteLine($"PASS MO2 widgets: all {widgets.Length} widgets MO2 names in {Path.GetFileName(source)} " +
            "have a counterpart here and it is drawn — " + string.Join(", ", described) +
            (sidebar.WasCollapsed ? ", with the sidebar opened for the run row as MO2 keeps it and closed again" : ""));

        // Shows or hides MO2's filter list through its own button, and answers with
        // how it was set before.
        static async Task<bool> OpenCategories(Window window, bool open)
        {
            var toggle = window.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
                .FirstOrDefault(x => x.Name == "ModsDisplayCategoriesButton");
            var was = toggle?.IsChecked == true;
            if (toggle is not null && was != open) {
                toggle.IsChecked = open;
                await Task.Delay(400);
                window.UpdateLayout();
            }
            return was;
        }

        // Drawn, not merely built. A collapsed control is still a visual child, so
        // looking it up by name alone passed for a category filter that was hidden at
        // every panel width the default layout uses. The exception is a widget MO2
        // itself declares hidden — its Clear all Filters button — which only has to
        // be there to be shown when a filter goes on.
        static async Task<List<string>> Missing(Window window, Mo2LiveWorkspace live, Mo2QtWidgetSource.Widget[] wanted)
        {
            var missing = new List<string>();
            // A page is built when it is first shown, so the frame right after
            // navigating is still the one before it.
            for (var attempt = 0; attempt < 60; attempt++) {
                await Task.Delay(100);
                window.UpdateLayout();
                // MO2's run row sits in the profile sidebar, which moves into a
                // flyout when that sidebar is collapsed and is then not in the
                // window's tree at all. The workspace holds it so it can be read
                // wherever it currently is.
                var roots = live.LaunchPanel is { } panel ? new Control[] { window, panel } : [window];
                var drawn = roots.SelectMany(root => root.GetVisualDescendants().OfType<Control>().Prepend(root)).ToArray();
                // Said two ways round, because "there is no such control" and "it is
                // there and nothing of it is on screen" are different faults and the
                // fix for one is not the fix for the other.
                missing = wanted.Select(x => {
                    var answer = Answers[x.Name];
                    var built = drawn.Where(answer.Match).ToArray();
                    if (built.Length == 0) return $"{x.Name}: nothing named {answer.Counterpart} is built";
                    if (!x.Starts) return "";
                    var shown = built.FirstOrDefault(c => c.IsEffectivelyVisible && c.Bounds.Width > 0);
                    if (shown is not null) return "";
                    var first = built[0];
                    // What squeezed it out, not only that it is out: a control with
                    // no width is nearly always in a row that ran out of room, and
                    // the row's own width and what its neighbours took is the part
                    // that says which of them to give way.
                    var row = first.GetVisualParent() as Control;
                    var neighbours = row is null ? "" : " in " + row.GetType().Name + $"#{row.Name} {row.Bounds.Width:F0} holding " +
                        string.Join(", ", row.GetVisualChildren().OfType<Control>()
                            .Select(c => $"{c.Name ?? c.GetType().Name} {c.Bounds.Width:F0}"));
                    return $"{x.Name} ({answer.Counterpart}) is built but not drawn — " +
                        $"visible={first.IsEffectivelyVisible} bounds={first.Bounds.Width:F0}x{first.Bounds.Height:F0}{neighbours}";
                }).Where(x => x.Length > 0).ToList();
                if (missing.Count == 0) break;
            }
            return missing;
        }

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
