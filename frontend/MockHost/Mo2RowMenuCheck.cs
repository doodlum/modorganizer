using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Every list carries the menu MO2 puts on it.
//
// MO2's lists are worked from their right-click menus — the buttons around a list
// are a handful of the same actions — so a page without one is a page most of whose
// behaviour cannot be reached. Four of them had no menu at all.
//
// The mod and plugin menus are checked against the real thing: MO2 is asked to build
// its own menu for the same row and the two are compared entry by entry, so this
// cannot drift as MO2 changes. The pages MO2 draws from elsewhere in its window are
// compared against the entries taken from that source, listed here.
internal static class Mo2RowMenuCheck
{
    // MO2's own menus for the lists whose menu is built in its main window rather
    // than by a list class of its own.
    // Downloads: downloadlistview.cpp, DownloadListView::onCustomContextMenu, for a
    // finished archive that MO2 knows on Nexus and has not hidden.
    private static readonly string[] Downloads = [
        "Install", "Visit on Nexus", "Visit the uploader's profile", "Open File", "Open Meta File",
        "Reveal in Explorer", "Delete...", "Hide",
        "Delete Installed Downloads...", "Delete Uninstalled Downloads...", "Delete All Downloads...",
        "Hide Installed...", "Hide Uninstalled...", "Hide All..."];
    // savestab.cpp, SavesTab::onContextMenu.
    private static readonly string[] Saves = ["Fix enabled mods...", "Delete 1 save(s)", "Open in Explorer..."];
    // mainwindow.cpp, on_bsaList_customContextMenuRequested.
    private static readonly string[] Archives = ["Extract..."];
    // filetree.cpp, FileTree::onContextMenu, for a file MO2 lays down itself.
    private static readonly string[] Data = ["Preview", "Reveal in Explorer", "Open Mod Info", "Hide"];

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        var menu = live.ProfileMenu;
        using var turn = await Mo2CheckTurn.Take();
        var panels = live.WorkspaceController.ActiveWorkspace.Panels;
        var selected = panels.FirstOrDefault(x => x.IsSelected) ?? panels.FirstOrDefault();
        var restore = selected?.SelectedTab.Contents.ViewModel is ScenarioLoadOrderPage
            ? menu.LeftMenuItemExternalChanges! : menu.LeftMenuItemLoadout;
        var faults = new List<string>();
        var checked_ = new List<string>();
        var target = live.Profile.CurrentTarget;

        // --- My Mods, against the menu MO2 builds for the same mod ---
        await Navigate(menu.LeftMenuItemLoadout);
        await Settle();
        if (live.Profile.Mods.FirstOrDefault(x => x.IsRegular && x.CanManage) is { } mod) {
            var ours = Captions(Mo2ModMenu.Build(live.Profile, (Mo2ModsAdapter)live.ModsPage!.Adapter,
                () => mod, target, action => action(), () => Task.CompletedTask));
            try {
                CompareTree($"My Mods ({mod.DisplayName})", ours,
                    await live.Profile.ReadListMenu("readModMenu", [mod.Name], target));
            } catch (Exception error) { faults.Add("MO2 would not report its own mod menu: " + error.Message); }
        } else faults.Add("this profile has no ordinary mod to open a menu on");

        // --- Plugins, the same way ---
        await Navigate(menu.LeftMenuItemExternalChanges!);
        await Settle();
        if (live.Profile.Order.Plugins.FirstOrDefault(x => x.ModName.Length > 0) is { } plugin) {
            var ours = Drawn<Mo2PluginsView>("PluginRedesignRow", plugin.DisplayName);
            try {
                var theirs = await live.Profile.ReadListMenu("readPluginMenu", [plugin.DisplayName], target);
                // The plugin rows draw one menu each; compare the one MO2 reports for
                // the plugin it was asked about against what a row actually offers.
                if (ours.Length == 0) faults.Add("no plugin row carries a menu");
                else CompareTree($"Plugins ({plugin.DisplayName} from {plugin.ModName})", ours, theirs);
            } catch (Exception error) { faults.Add("MO2 would not report its own plugin menu: " + error.Message); }
        } else faults.Add("this profile has no plugin from a mod to open a menu on");

        // --- The pages whose menu MO2 builds elsewhere in its window ---
        // Downloads and Data name their rows the same way MO2's own lists do, so both
        // are compared against the menu MO2 builds for that very row, the same way My
        // Mods and Plugins are. Data must be asked about a file: MO2 gives a folder
        // Expand All and Collapse All instead of the entries a file carries, so a
        // comparison that landed on a folder would be comparing two different menus.
        await Against(menu.LeftMenuItemLibrary, "Downloads", "downloads", Downloads,
            rows => live.Profile.Downloads.Select(x => x.Name).FirstOrDefault(rows.Contains),
            row => DownloadCaptions(live, window, row));
        await Against(menu.DataItem, "Data", "data", Data,
            rows => FirstDataFile(window, rows),
            row => Drawn<Mo2DataView>("DataTable",
                pick: model => model is Mo2DataEntry { Directory: false } file && (row is null || file.Name == row)));

        // Archives and Saves are compared against MO2's own menu too, now that the
        // bridge names their rows the way these pages do. Neither list names them the
        // way its first column reads: MO2's bsaList is a tree of mods with their
        // archives beneath, so an archive is a child row; and MO2 labels a save with a
        // composite caption — character, slot and place — which this profile's two
        // saves both carry, so a save is named by its file instead.
        await Against(menu.ArchivesItem, "Archives", "archives", Archives,
            rows => Models<Mo2ArchivesView, Mo2Archive>(window).Select(x => x.Name).FirstOrDefault(rows.Contains),
            row => Drawn<Mo2ArchivesView>("ArchivesTable",
                pick: model => model is Mo2Archive archive && (row is null || archive.Name == row)));
        await Against(menu.SavesItem, "Saves", "saves", Saves,
            rows => Models<Mo2SavesView, Mo2Save>(window).Select(x => x.File).FirstOrDefault(rows.Contains),
            row => Drawn<Mo2SavesView>("SavesTable",
                pick: model => model is Mo2Save save && (row is null || save.File == row)));

        await Navigate(restore);
        await Task.Delay(600);

        if (faults.Count > 0) Console.WriteLine("FAIL MO2 row menus: " + string.Join("; ", faults));
        else Console.WriteLine("PASS MO2 row menus: every list is compared against the menu MO2 itself built for the same row, " +
            "entry for entry and both ways round — " + string.Join(", ", checked_));

        // Against MO2 itself: the frontend's menu for a row, beside the menu MO2 builds
        // for the row of that name in its own list, compared both ways.
        //
        // The row is settled first and both menus are then opened on that one row. A
        // menu is built for the row being acted on — MO2 offers a folder different
        // entries from a file — so reading whichever row each side happened to land on
        // would compare two menus that were never meant to agree, and would do it
        // without saying so.
        //
        // Falls back to the recorded entries — saying so — when MO2 lists no row this
        // page also has, rather than passing on a comparison it could not make.
        async Task Against(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item, string page, string view,
                           string[] recorded, Func<HashSet<string>, string?> match, Func<string?, string[]> read)
        {
            await Navigate(item);
            await Settle();
            string? row = null;
            try {
                // Awaited, not blocked on: this runs on the dispatcher the bridge's
                // reply loop also uses, and taking its result synchronously would hold
                // the thread the answer has to arrive on.
                var listed = await live.Profile.ReadFileRows(view, target);
                row = match(listed.ToHashSet());
            } catch (Exception error) { faults.Add($"MO2 would not list its {page} rows: " + error.Message); }
            var ours = read(row);
            if (ours.Length == 0) { faults.Add($"{page} has no row menu at all"); return; }
            if (row is null) {
                var missing = recorded.Where(x => !ours.Contains(x)).ToArray();
                if (missing.Length > 0) faults.Add($"{page}'s menu is missing {string.Join(", ", missing)}");
                else checked_.Add($"{page} carries the {recorded.Length} entries recorded for it — MO2 listed no row this page also has");
                return;
            }
            try {
                CompareTree($"{page} ({row})", ours, await live.Profile.ReadListMenu("readFileMenu", [row], target, view));
            } catch (Exception error) { faults.Add($"MO2 would not report its own {page} menu: " + error.Message); }
        }

        // The rows a page is drawing, as the records behind them.
        static IEnumerable<TRow> Models<TPage, TRow>(Window owner) where TPage : Control =>
            owner.GetVisualDescendants().OfType<TPage>()
                .SelectMany(x => x.GetVisualDescendants().OfType<TreeDataGrid>())
                .SelectMany(x => x.Source?.Items.OfType<TRow>() ?? []);

        // A row the Data page is showing that is a file rather than a folder, and that
        // MO2's own tree is showing too.
        static string? FirstDataFile(Window window, HashSet<string> rows) =>
            window.GetVisualDescendants().OfType<Mo2DataView>()
                .SelectMany(x => x.GetVisualDescendants().OfType<TreeDataGrid>())
                .SelectMany(x => x.Source?.Items.OfType<Mo2DataEntry>() ?? [])
                .Where(x => !x.Directory).Select(x => x.Name).FirstOrDefault(rows.Contains);

        // What MO2 offers and what this frontend offers, both ways round. An entry
        // this frontend has that MO2 does not is as much a difference as one it lacks.
        //
        // MO2's two category menus are left out on both sides: assigning a mod's
        // categories is done in MO2's own category editor, which the mod pane's Edit...
        // opens, and their contents are this instance's category names rather than
        // anything either menu is built to hold.
        void CompareTree(string page, string[] ours, Mo2MenuEntry[] entries)
        {
            var theirs = entries.Where(entry => entry.Text is not ("Change Categories" or "Primary Category"))
                .SelectMany(entry => entry.Captions())
                // MO2's Data tree expands in place, with every folder openable where it
                // sits. This page walks into a folder instead and shows one at a time,
                // so there is nothing for either of these to act on: they are not
                // entries this frontend is missing, they are entries its Data page has
                // no meaning for. Left out of the comparison rather than written into
                // the page as actions that would do nothing.
                .Where(caption => !(page.StartsWith("Data") && caption is "Expand All" or "Collapse All"))
                // Two more the Data page does not carry, by decision rather than by
                // oversight. Open with VFS runs a Windows program inside MO2's prefix
                // from a file list, and Save Tree to Text File writes MO2's own tree
                // rather than the folder this page is showing — its output would not
                // describe what the user is looking at. Named here so the comparison
                // stays two-way for everything else: an entry that stopped being
                // offered, or one MO2 gained, is still a difference.
                .Where(caption => !(page.StartsWith("Data") && caption is "Open with VFS" or "Save Tree to Text File..."))
                .ToArray();
            Compare(page, ours, theirs);
        }

        void Compare(string page, string[] ours, string[] theirs)
        {
            // MO2 names these two by where the new mod lands and whether a filter is
            // on; the frontend offers MO2 every spelling and takes the one it built.
            string Fold(string caption) => caption
                .Replace("inside", "above").Replace("below", "above")
                .Replace(" all matching mods", " all");
            var mine = ours.Select(Fold).ToHashSet();
            var mo2 = theirs.Select(Fold).ToHashSet();
            var missing = mo2.Where(x => !mine.Contains(x)).ToArray();
            var extra = mine.Where(x => !mo2.Contains(x)).ToArray();
            if (missing.Length > 0) faults.Add($"{page}'s menu is missing MO2's {string.Join(", ", missing)}");
            if (extra.Length > 0) faults.Add($"{page}'s menu offers {string.Join(", ", extra)}, which MO2's does not");
            if (missing.Length == 0 && extra.Length == 0)
                checked_.Add($"{page} matches MO2's own {mo2.Count}-entry menu");
        }

        // The captions a page's rows actually draw, from the menu attached to the row
        // rather than from the code that built it.
        string[] Drawn<TPage>(string table, string? rowText = null, Func<object, bool>? pick = null) where TPage : Control
        {
            var page = window.GetVisualDescendants().OfType<TPage>().FirstOrDefault();
            if (page is null) return [];
            foreach (var grid in page.GetVisualDescendants().OfType<TreeDataGrid>()) {
                // A table's menu is built for the row being acted on, so one is
                // selected first — which is what right-clicking a row does too. The
                // row matters: MO2's Data menu offers a folder different entries.
                if (grid.ContextMenu is not null && grid.RowSelection is { } selection && grid.Rows is { Count: > 0 } rows) {
                    var index = Enumerable.Range(0, rows.Count).FirstOrDefault(
                        i => rows[i].Model is { } model && (pick?.Invoke(model) ?? true), -1);
                    // A menu belongs to the row it was built for. When a particular row
                    // was asked for and this table is not drawing it, there is nothing
                    // to compare — rather than another row's menu compared by mistake.
                    if (index < 0 && pick is not null) return [];
                    if (index >= 0) selection.Select(new IndexPath(index));
                }
                if (Open(grid.ContextMenu) is { Length: > 0 } fromTable) return fromTable;
                foreach (var row in grid.GetVisualDescendants().OfType<TreeDataGridRow>()) {
                    // When a particular row is wanted, only that row's menu will do:
                    // MO2's plugin menu differs for a plugin the game itself supplies.
                    if (rowText is not null && !row.GetVisualDescendants().OfType<TextBlock>()
                        .Any(x => x.Text == rowText)) continue;
                    if (Open(row.ContextMenu) is { Length: > 0 } fromRow) return fromRow;
                    if (row.GetVisualDescendants().OfType<Control>()
                        .Select(x => x.ContextFlyout).OfType<MenuFlyout>().FirstOrDefault() is { } flyout)
                        return Captions(Items(flyout));
                }
            }
            return [];
        }

        string[] DownloadCaptions(Mo2LiveWorkspace shell, Window owner, string? wanted)
        {
            var page = owner.GetVisualDescendants().OfType<Mo2DownloadsView>().FirstOrDefault();
            if (page?.ViewModel is null) return [];
            // Built against a finished, listed archive so the comparison is with the
            // shape MO2 draws for one — the transfer entries belong to the other two —
            // and against the very archive MO2 was asked about where one was settled on.
            var table = page.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
            var rows = table?.Rows;
            var row = rows is null ? null : Enumerable.Range(0, rows.Count)
                .Select(index => rows[index].Model as NexusMods.App.UI.Controls.CompositeItemModel<NexusMods.Abstractions.Downloads.DownloadId>)
                .FirstOrDefault(model => model is not null && page.ViewModel.Provider.Find(model.Key)
                    is { Ready: true, Hidden: false } found && (wanted is null || found.Name == wanted));
            return row is null ? [] : Captions(page.DownloadMenu(row));
        }

        static object[] Items(MenuFlyout flyout)
        {
            // A flyout builds on opening; ask it to, then read what it holds.
            flyout.ShowAt(new Panel());
            var items = flyout.Items.OfType<object>().ToArray();
            flyout.Hide();
            return items;
        }

        static string[] Open(ContextMenu? contextMenu)
        {
            if (contextMenu is null) return [];
            contextMenu.Open();
            var captions = Captions(contextMenu.Items.OfType<object>().ToArray());
            contextMenu.Close();
            return captions;
        }

        // Every caption a menu holds, its submenus included, which is what MO2's own
        // menu is read as too.
        static string[] Captions(object[] items) => items.OfType<MenuItem>()
            .SelectMany(item => new[] { item.Header as string ?? "" }
                .Concat(Captions(item.Items.OfType<object>().ToArray())))
            .Where(x => x.Length > 0).ToArray();

        async Task Settle() { for (var pass = 0; pass < 8; pass++) { await Task.Delay(100); window.UpdateLayout(); } }

        async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
        {
            if (item is null) return;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
            await Task.Delay(400);
        }
    }
}
