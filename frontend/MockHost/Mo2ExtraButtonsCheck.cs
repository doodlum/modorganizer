using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// The other direction.
//
// MO2_VERIFY_QT_WIDGETS asks whether everything MO2 draws is here. It says nothing
// about what is here that MO2 does not draw, and a page can drift by growing
// buttons just as easily as by losing them — a toolbar of the original app's, an
// action invented for a page, a second control for something MO2 already offers.
//
// So every button drawn on one of MO2's six pages has to be accounted for: either
// it is the counterpart of a widget in src/mainwindow.ui, which
// Mo2QtWidgetCheck.Answers already records, or it is named below with what it is
// for and why MO2 has no widget of its own for it. A button that is neither fails,
// and the answer is to take it off rather than to add a line here.
internal static class Mo2ExtraButtonsCheck
{
    // Buttons this frontend draws that MO2 has no widget for, with what each is.
    // Everything here has to be something MO2 does from somewhere other than a
    // widget on the tab — its list headers, its row menus, its own dialogs — or a
    // part of this frontend's own chrome that a page cannot do without.
    private static readonly Dictionary<string, string> Own = new() {
        // There is no column chooser here. The entry that used to sit above said MO2
        // opens one from any list header; it does not. Only DownloadListView sets
        // Qt::CustomContextMenu on its header, and only its menu lists the columns —
        // MO2's mod and plugin lists start from defaults set in code and are
        // remembered from the header's saved geometry, with nothing for a user to
        // press. The five buttons that stood for a chooser MO2 has not got are gone;
        // the downloads header's own menu, which is MO2's, stays.
        //
        // MO2's tabs each show their whole list; the file pages here are one panel
        // that navigates, so they need a way back up and a way to search in place.
        ["SearchToggleButton"] = "shows the page's filter row, which MO2 draws permanently under its list",
        ["DataParentFolder"] = "the way back up a folder, which this page needs because it navigates where MO2's Data tree expands in place",
        // The clear inside a filter field. MO2's LineEditClear carries the same
        // affordance inside the box itself.
        ["ModsQtFilterClear"] = "the clear inside MO2's own filter field",
        ["PluginsQtFilterClear"] = "the clear inside MO2's own filter field",
        ["DataQtFilterClear"] = "the clear inside MO2's own filter field",
        ["DownloadsQtFilterClear"] = "the clear inside MO2's own filter field",
    };

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        var menu = live.ProfileMenu;
        using var turn = await Mo2CheckTurn.Take();
        using var sidebar = await Mo2SidebarState.Open(window, live);

        // MO2's six tabs, and the view each is drawn by. Only the page's own view is
        // read: the window holds the other panel's page too, and its buttons are
        // that page's business.
        var pages = new (string Name, Func<Task> Open, Type View)[] {
            ("my-mods", () => Navigate(menu.LeftMenuItemLoadout), typeof(Mo2ModsView)),
            ("plugins", () => Navigate(menu.LeftMenuItemExternalChanges!), typeof(Mo2PluginsView)),
            ("data", () => Navigate(menu.DataItem), typeof(Mo2DataView)),
            ("archives", () => Navigate(menu.ArchivesItem), typeof(Mo2ArchivesView)),
            ("saves", () => Navigate(menu.SavesItem), typeof(Mo2SavesView)),
            ("downloads", () => Navigate(menu.LeftMenuItemLibrary), typeof(Mo2DownloadsView)),
        };

        // What MO2 has a widget for, by the name this frontend draws it under.
        var fromMo2 = Mo2QtWidgetCheck.Answers.Values
            .Where(x => x.Kind is Mo2QtWidgetCheck.Kind.Page or Mo2QtWidgetCheck.Kind.Window)
            .Select(x => x.Counterpart).ToHashSet(StringComparer.Ordinal);

        var panels = live.WorkspaceController.ActiveWorkspace.Panels;
        var selected = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
        var restore = selected?.SelectedTab.Contents.ViewModel is ScenarioLoadOrderPage
            ? menu.LeftMenuItemExternalChanges! : menu.LeftMenuItemLoadout;

        var faults = new List<string>();
        var described = new List<string>();
        var trace = Environment.GetEnvironmentVariable("MO2_TRACE_EXTRA_BUTTONS") == "1";

        foreach (var (name, open, view) in pages) {
            await open();
            Control? page = null;
            for (var attempt = 0; attempt < 80 && page is null; attempt++) {
                await Task.Delay(100);
                window.UpdateLayout();
                page = window.GetVisualDescendants().OfType<Control>()
                    .FirstOrDefault(x => x.GetType() == view && x.IsEffectivelyVisible && x.Bounds.Width > 0);
            }
            if (page is null) { faults.Add($"{name} never drew"); continue; }

            // Drawn buttons, not built ones, and not the ones inside a row: a row's
            // controls are the list's business and are compared against MO2's own
            // row menu by MO2_VERIFY_ROW_MENUS.
            // What is left out, and why none of it is a button a page drew:
            //   * a column heading, which is a Button so it can sort;
            //   * a scrollbar's own template parts;
            //   * the tab strip's close buttons, which every panel shares;
            //   * a row's controls, compared against MO2's own row menu by
            //     MO2_VERIFY_ROW_MENUS;
            //   * the ticks inside MO2's filter list, which are its categories.
            var drawn = page.GetVisualDescendants().OfType<Button>()
                .Where(x => x.IsEffectivelyVisible && x.Bounds.Width > 0)
                .Where(x => x is not TreeDataGridColumnHeader)
                .Where(x => !x.GetVisualAncestors().OfType<Control>().Any(a =>
                    a is TreeDataGridRow or ScrollBar ||
                    a.Name is "PanelHeaderRow" or "TabHeaderBorder" or "TabHeaderScrollViewer" or "ModCategories"))
                .ToArray();

            var extra = new List<string>();
            foreach (var button in drawn) {
                var id = button.Name ?? Describe(button);
                if (trace) Console.WriteLine($"BUTTON {name}: {id}");
                if (button.Name is { } named && (fromMo2.Contains(named) || Own.ContainsKey(named))) continue;
                if (!extra.Contains(id)) extra.Add(id);
            }
            if (extra.Count > 0) faults.Add($"{name} draws {extra.Count} button(s) MO2 has no widget for: {string.Join(", ", extra)}");
            else described.Add($"{name} ({drawn.Length})");
        }

        await Navigate(restore);
        await Task.Delay(600);

        // And the other way: an entry here for a button no page draws is an
        // exemption that has stopped meaning anything.
        if (faults.Count == 0) {
            if (described.Count == 0) faults.Add("no page was read, so nothing was judged");
            else Console.WriteLine($"PASS MO2 page buttons: every button drawn on MO2's six tabs is either a widget MO2 has " +
                $"or one of the {Own.Count} this frontend draws for something MO2 does elsewhere — " + string.Join(", ", described));
        }
        if (faults.Count > 0) Console.WriteLine("FAIL MO2 page buttons: " + string.Join("; ", faults));

        static string Describe(Control control) =>
            control is ContentControl { Content: string text } && text.Length > 0
                ? $"{control.GetType().Name} (\"{text}\")" : control.GetType().Name;

        static async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
        {
            if (item is null) return;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
            await Task.Delay(300);
        }
    }
}
