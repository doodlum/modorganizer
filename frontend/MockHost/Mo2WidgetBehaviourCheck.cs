using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// What MO2's widgets do, not that they are there.
//
// The widget check finds them and the fit check draws them whole; neither notices
// a filter field that narrows nothing or a box whose choices change no list.
// Every control here is worked the way a user works it — typed in, ticked,
// chosen from — and what the list does is read back afterwards.
//
// Run on its own. It filters and unfilters the two live lists, which the checks
// that count rows cannot tolerate.
internal static class Mo2WidgetBehaviourCheck
{
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
        var worked = new List<string>();

        // --- The mod pane ---
        await Navigate(menu.LeftMenuItemLoadout);
        await Settle();
        var mods = Find<Mo2ModsView>();
        if (mods is null) faults.Add("My Mods never drew");
        else {
            var adapter = (Mo2ModsAdapter)mods.ViewModel!.Adapter;
            var all = adapter.VisibleRowCount;

            // The filter field under the list narrows it, and clearing it puts the
            // rest back.
            if (Named<TextBox>(mods, "ModsQtFilter") is { } filter) {
                var name = live.Profile.Mods.FirstOrDefault(x => !x.IsSeparator)?.DisplayName ?? "";
                filter.Text = name.Length > 3 ? name[..3] : "zzzzz";
                await Settle();
                var narrowed = adapter.VisibleRowCount;
                filter.Text = "";
                await Settle();
                if (narrowed >= all) faults.Add($"typing in the mod filter left {narrowed} of {all} rows");
                else if (adapter.VisibleRowCount != all) faults.Add("clearing the mod filter did not put the list back");
                else worked.Add($"the mod filter narrowed {all} rows to {narrowed}");
            } else faults.Add("My Mods has no filter field");

            // MO2's separators box: shown always, never, or subject to the filter.
            var separators = live.Profile.Mods.Count(x => x.IsSeparator);
            if (Named<ComboBox>(mods, "ModsFiltersSeparators") is { } separatorMode && separators > 0) {
                separatorMode.SelectedIndex = 2; await Settle();
                var hidden = adapter.VisibleRowCount;
                separatorMode.SelectedIndex = 1; await Settle();
                var shown = adapter.VisibleRowCount;
                separatorMode.SelectedIndex = 0; await Settle();
                if (shown - hidden != separators)
                    faults.Add($"hiding separators changed the list by {shown - hidden} rows, not by the {separators} it holds");
                else worked.Add($"the separators box took {separators} separator(s) out of the list and put them back");
            } else if (separators > 0) faults.Add("My Mods has no separators box");

            // The grouping box, which replaces the priority order with the chosen one.
            if (Named<ComboBox>(mods, "ModsGroupBox") is { } grouping) {
                var order = Names(adapter);
                grouping.SelectedIndex = 1; await Settle();
                var grouped = Names(adapter);
                grouping.SelectedIndex = 0; await Settle();
                if (grouped.SequenceEqual(order)) faults.Add("grouping by category left the list in the same order");
                else if (!Names(adapter).SequenceEqual(order)) faults.Add("dropping the grouping did not put the order back");
                else worked.Add("the grouping box reordered the list and gave the order back");
            } else faults.Add("My Mods has no grouping box");

            // The categories beside the list, and MO2's Clear.
            var categories = mods.GetVisualDescendants().OfType<CheckBox>()
                .Where(x => x.Tag is string).ToArray();
            if (categories.Length > 0) {
                categories[0].IsChecked = true; await Settle();
                var ticked = adapter.VisibleRowCount;
                if (ticked >= all) faults.Add($"ticking a category left {ticked} of {all} rows");
                if (Named<Button>(mods, "ModsFiltersClear") is { } clear) {
                    clear.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    await Settle();
                    if (adapter.VisibleRowCount != all) faults.Add("Clear did not put every mod back");
                    else if (categories[0].IsChecked == true) faults.Add("Clear left a category ticked");
                    else worked.Add($"a category narrowed the list to {ticked} and Clear put it back");
                } else faults.Add("the Filters group has no Clear");
            }

            // The count MO2 keeps beside the list.
            if (Named<TextBlock>(mods, "ActiveModsCounter") is { } counter) {
                var active = live.Profile.Mods.Count(x => !x.IsSeparator && !x.IsOverwrite && (x.State & 6) != 0);
                if (counter.Text != active.ToString()) faults.Add($"the active count reads {counter.Text}, not {active}");
                else worked.Add($"the active count reads MO2's own {active}");
            } else faults.Add("My Mods has no active count");

            // The list options menu, which MO2 fills with what to do with the list
            // as a whole and which of its columns to draw.
            if (Named<Button>(mods, "ModsListOptionsButton")?.Flyout is MenuFlyout options) {
                options.ShowAt(mods); await Settle(); options.Hide();
                var items = options.Items.OfType<MenuItem>().Select(x => x.Header as string).ToArray();
                foreach (var wanted in new[] { "Refresh", "Enable all", "Disable all" })
                    if (!items.Contains(wanted)) faults.Add($"the list options menu has no {wanted}");
                if (items.Length <= 3) faults.Add("the list options menu offers no columns to hide");
                else worked.Add($"the list options menu offers {items.Length} entries");
            } else faults.Add("My Mods has no list options menu");

            // And the open-folders menu, over folders that are actually there.
            if (Named<Button>(mods, "ModsOpenFolderButton")?.Flyout is MenuFlyout folders) {
                folders.ShowAt(mods); await Settle(); folders.Hide();
                var reachable = folders.Items.OfType<MenuItem>().Count(x => x.IsEnabled);
                if (reachable == 0) faults.Add("the open-folders menu reaches no folder");
                else worked.Add($"the open-folders menu reaches {reachable} folder(s)");
            } else faults.Add("My Mods has no open-folders menu");

            // What MO2's list does without a menu: the space bar switches a mod on and
            // off. Driven on a mod MO2 will actually let go of, and switched back.
            var table = mods.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault(x => x.RowSelection is not null);
            var subject = live.Profile.Mods.FirstOrDefault(x => !x.IsSeparator && !x.IsOverwrite && x.CanManage);
            if (table is null || subject is null) faults.Add("My Mods has no table to work a mod in");
            else {
                var index = adapter.VisibleOrder.ToList().IndexOf(subject.Id.ToString());
                if (index < 0) faults.Add("the mod the space bar was to be tried on is not in the list");
                else {
                    table.RowSelection!.Clear();
                    table.RowSelection.Select(new Avalonia.Controls.IndexPath(index));
                    await Settle();
                    var before = (live.Profile.FindMod(subject.Id)!.State & 2) != 0;
                    table.RaiseEvent(new Avalonia.Input.KeyEventArgs {
                        RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Space, Source = table });
                    await Until(() => (live.Profile.FindMod(subject.Id)!.State & 2) != 0 != before, seconds: 20);
                    var after = (live.Profile.FindMod(subject.Id)!.State & 2) != 0;
                    if (after == before) faults.Add($"the space bar did not switch {subject.DisplayName}");
                    else {
                        // MO2 keeps the row selected after switching it, and so does
                        // this list — but the rows are rebuilt around the change, so
                        // the selection is taken again before the second press rather
                        // than assumed.
                        if (table.RowSelection!.Count == 0) {
                            var again = adapter.VisibleOrder.ToList().IndexOf(subject.Id.ToString());
                            if (again >= 0) table.RowSelection.Select(new Avalonia.Controls.IndexPath(again));
                            await Settle();
                        }
                        table.RaiseEvent(new Avalonia.Input.KeyEventArgs {
                            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Space, Source = table });
                        await Until(() => (live.Profile.FindMod(subject.Id)!.State & 2) != 0 == before, seconds: 20);
                        if ((live.Profile.FindMod(subject.Id)!.State & 2) != 0 != before)
                            faults.Add($"{subject.DisplayName} was left switched {(after ? "on" : "off")}");
                        else worked.Add($"the space bar switched {subject.DisplayName} in MO2 and back");
                    }
                    table.RowSelection.Clear();
                    await Settle();
                }
            }

            // The menu MO2 puts on a row, and one of its entries driven through to
            // MO2: Send to top moves the mod the length of the list, and the order it
            // was in is put back afterwards.
            // An ordinary mod's row: MO2 gives its overwrite folder and the game's own
            // data a menu of their own, so picking whichever row is drawn first reads
            // one of those instead.
            var ordinary = live.Profile.Mods.FirstOrDefault(x => x.IsRegular && x.CanManage);
            var menuRow = mods.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(x => x.ContextFlyout is MenuFlyout && x.Name == "ModRedesignRow" &&
                    (ordinary is null || x.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == ordinary.DisplayName)));
            if (menuRow?.ContextFlyout is MenuFlyout rowMenu) {
                rowMenu.ShowAt(menuRow); await Settle(); rowMenu.Hide();
                // Every caption the menu holds, its submenus included: MO2 keeps its
                // ordering actions on a Send to... of its own.
                static string[] Captions(IEnumerable<object?> items) => items.OfType<MenuItem>()
                    .SelectMany(item => new[] { item.Header as string ?? "" }.Concat(Captions(item.Items.OfType<object>())))
                    .Where(x => x.Length > 0).ToArray();
                var entries = Captions(rowMenu.Items.OfType<object>());
                // MO2's own wording, from its mod menu (modlistcontextmenu.cpp).
                foreach (var wanted in new[] { "All Mods", "Send to... ", "Lowest priority", "Highest priority",
                                               "Open in Explorer", "Rename Mod...", "Remove Mod...", "Information..." })
                    if (!entries.Contains(wanted)) faults.Add($"a mod's menu has no {wanted}");
                if (!faults.Any(x => x.Contains("a mod's menu"))) worked.Add($"a mod's menu offers MO2's {entries.Length} entries");

                if (live.Profile.Mods.OrderBy(x => x.Priority).LastOrDefault(x => !x.IsSeparator && !x.IsOverwrite && x.CanManage) is { } last
                    && entries.Contains("Lowest priority")) {
                    var order = live.Profile.ModPriorityOrder.ToArray();
                    var was = live.Profile.FindMod(last.Id)!.Priority;
                    await adapter.Move(last.Id, 0, absolute: true);
                    await Until(() => live.Profile.FindMod(last.Id)!.Priority < was, seconds: 30);
                    if (live.Profile.FindMod(last.Id)!.Priority >= was) faults.Add($"Send to... Lowest priority did not move {last.DisplayName}");
                    else {
                        await adapter.Move(last.Id, was, absolute: true);
                        await Until(() => live.Profile.ModPriorityOrder.SequenceEqual(order), seconds: 30);
                        if (!live.Profile.ModPriorityOrder.SequenceEqual(order))
                            faults.Add("the order after Send to... Lowest priority was not put back");
                        else worked.Add($"Send to... Lowest priority moved {last.DisplayName} to the front of MO2's order and back");
                    }
                }
            } else faults.Add("a mod row carries no menu");

            // MO2 offers its colour actions on an ordinary mod where the colour is
            // shown — a right-click in the Notes column — so that is where they are.
            var notesCell = menuRow?.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(x => x.Name == "ModNotesCell" && x.ContextFlyout is MenuFlyout);
            if (notesCell?.ContextFlyout is MenuFlyout notesMenu) {
                notesMenu.ShowAt(notesCell); await Settle(); notesMenu.Hide();
                if (!notesMenu.Items.OfType<MenuItem>().Any(x => (x.Header as string) == "Select Color..."))
                    faults.Add("a mod's Notes cell has no Select Color...");
                else worked.Add("a mod's Notes cell carries MO2's Select Color...");
            } else faults.Add("no mod's Notes cell carries a menu");

            // MO2 installs an archive dropped onto its mod list. The drop itself needs
            // a pointer and a file manager, so what is checked here is that the list
            // takes drops at all and knows an archive from anything else.
            if (!DragDrop.GetAllowDrop(mods)) faults.Add("My Mods does not accept a dropped archive");
            else if (!Mo2ModsView.IsArchive("/tmp/Some Mod-1234.7z") || Mo2ModsView.IsArchive("/tmp/notes.txt"))
                faults.Add("My Mods does not tell an archive from another file");
            else worked.Add("My Mods takes dropped archives, as MO2 does");

            // The profile box, over the profiles of the instance this one belongs to.
            if (Named<ComboBox>(mods, "ModsProfileBox") is { } profiles) {
                var entry = mods.ViewModel!.Instances().FirstOrDefault(x => x.Registration.Endpoint == live.Profile.Endpoint);
                var names = entry?.Instance?.Profiles.Select(x => x.Name).ToArray() ?? [];
                if (profiles.Items.Count != names.Length)
                    faults.Add($"the profile box lists {profiles.Items.Count} of MO2's {names.Length} profiles");
                else if ((string?)profiles.SelectedItem != live.Profile.CollectionName.Value)
                    faults.Add($"the profile box shows {profiles.SelectedItem}, not the open {live.Profile.CollectionName.Value}");
                else worked.Add($"the profile box lists all {names.Length} profiles with the open one chosen");
            } else faults.Add("My Mods has no profile box");
        }

        // --- Plugins ---
        await Navigate(menu.LeftMenuItemExternalChanges!);
        await Settle();
        if (Find<Mo2PluginsView>() is { } plugins) {
            if (Named<TextBox>(plugins, "PluginsQtFilter") is { } filter) {
                // The rows the table is showing, not the adapter's source count: the
                // source holds every plugin whether the filter passes it or not.
                var rows = () => plugins.GetVisualDescendants().OfType<TreeDataGrid>()
                    .Select(x => x.Rows?.Count ?? 0).DefaultIfEmpty(0).Max();
                var before = rows();
                // Text off a real plugin. Filtering with "zzzzzz" and checking only that
                // the list got shorter is one-sided — a field that emptied the list
                // whatever was typed passed it — and says nothing about matches being
                // kept. This field feeds the page's own search box rather than a
                // comparison of its own, so rather than predict a count it is held to
                // both sides: the rows that do not carry the text go, and the plugin it
                // was taken from stays.
                var target = live.Profile.Order.Plugins.Select(x => x.DisplayName).FirstOrDefault(x => x.Length >= 5) ?? "";
                var needle = target.Length >= 5 ? target[..5] : "";
                if (needle.Length == 0) worked.Add("no plugin has a name long enough to filter on, so the plugin filter was not exercised");
                else {
                    filter.Text = needle; await Settle();
                    var narrowed = rows();
                    var kept = Shows(plugins, target);
                    filter.Text = ""; await Settle();
                    if (rows() != before) faults.Add("clearing the plugin filter did not put the list back");
                    else if (narrowed >= before) faults.Add($"the plugin filter left {narrowed} of {before} rows for \"{needle}\"");
                    else if (narrowed == 0 || !kept) faults.Add($"the plugin filter dropped {target}, which carries \"{needle}\"");
                    else worked.Add($"the plugin filter narrowed {before} rows to {narrowed} for \"{needle}\" and kept {target}");
                }
            } else faults.Add("Plugins has no filter field");
        } else faults.Add("Plugins never drew");

        // --- Data ---
        await Navigate(menu.DataItem);
        await Settle();
        if (Find<Mo2DataView>() is { } data) {
            var rows = () => data.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Rows?.Count ?? 0;
            var before = rows();
            if (Named<TextBox>(data, "DataQtFilter") is { } filter) {
                // Text off a row the tree is showing, not "zzzzzz": a field that emptied
                // the tree whatever was typed passed that. The page keeps a row whose
                // name carries the text, so the rows already listed say how many should
                // survive.
                var listed = data.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()
                    ?.Source?.Items.OfType<Mo2DataEntry>().Select(x => x.Name).ToArray() ?? [];
                var target = listed.FirstOrDefault(x => x.Length >= 4) ?? "";
                var needle = target.Length >= 4 ? target[..4] : "";
                if (needle.Length == 0) worked.Add("no Data row has a name long enough to filter on, so the Data filter was not exercised");
                else {
                    var expected = listed.Count(x => x.Contains(needle, StringComparison.OrdinalIgnoreCase));
                    filter.Text = needle; await Settle();
                    var narrowed = rows();
                    var kept = Shows(data, target);
                    filter.Text = ""; await Settle();
                    if (rows() != before) faults.Add("clearing the Data filter did not put the tree back");
                    else if (narrowed != expected) faults.Add($"the Data filter left {narrowed} of {before} rows for \"{needle}\", where {expected} row(s) carry it");
                    else if (!kept) faults.Add($"the Data filter dropped {target}, which carries \"{needle}\"");
                    else worked.Add($"the Data filter kept the {expected} of {before} row(s) named for \"{needle}\", {target} among them");
                }
            } else faults.Add("Data has no filter field");
            if (Named<CheckBox>(data, "DataFromArchives") is { } archives) {
                // What the box is meant to take away, counted off the rows the tree is
                // showing. Holding it to "did not add rows" passed while the box did
                // nothing at all — the tree went from 52 rows to 52 and that was
                // reported as the box working.
                Mo2DataEntry[] Listed() => data.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()
                    ?.Source?.Items.OfType<Mo2DataEntry>().ToArray() ?? [];
                int Served() => Listed().Count(x => !x.Directory && x.Archive.Length > 0);
                // MO2 lists a BSA's contents under the folders it puts them in, so the
                // Data root has none to take away and the box cannot be exercised
                // there. Descend until a folder has some.
                var reached = "Data";
                if (Served() == 0)
                    foreach (var folder in Listed().Where(x => x.Directory).Select(x => x.Name).Take(8).ToArray()) {
                        await data.ShowFolder(folder); await Settle();
                        if (Served() > 0) { reached = "Data/" + folder; break; }
                        await data.ShowFolder(""); await Settle();
                    }
                var here = rows();
                var served = Served();
                archives.IsChecked = false; await Settle();
                var without = rows();
                archives.IsChecked = true; await Settle();
                if (rows() != here) faults.Add($"reticking Archives did not put {reached} back");
                else if (served == 0)
                    worked.Add($"the Archives box found no archive-served file under Data to take away, so it was not exercised");
                else if (without != here - served)
                    faults.Add($"unticking Archives left {without} of {here} rows in {reached}, with {served} served out of an archive");
                else worked.Add($"the Archives box took the {served} archive-served row(s) out of {here} in {reached} and put them back");
                await data.ShowFolder(""); await Settle();
            } else faults.Add("Data has no Archives box");
        } else faults.Add("Data never drew");

        // --- Archives ---
        // MO2's tick beside an archive decides whether MO2 manages it, and MO2 writes
        // its profile's archive list when the tick changes. Driven through to MO2 and
        // put back, because a tick that only moves is the fault this check is for.
        await Navigate(menu.ArchivesItem);
        await Settle();
        if (Find<Mo2ArchivesView>() is { } archivesView) {
            CheckBox? Box() => archivesView.GetVisualDescendants().OfType<CheckBox>()
                .FirstOrDefault(x => x.Name == "ArchiveManagedBox" && x.IsEnabled);
            // MO2 decides which archives may be ticked at all: the game's own are
            // listed under <Unmanaged> and cannot be, and an instance with archive
            // management off offers none. That the frontend draws them disabled is
            // MO2's answer, not a fault of its own.
            if (Box() is not { } box)
                // Agreeing with MO2 on nothing is agreement, not a tick that was
                // driven. Said plainly, so this does not read like the round trip
                // below: on an instance with archive management off, every archive is
                // drawn disabled and the tick itself goes untested.
                worked.Add($"MO2 lets none of its {archivesView.GetVisualDescendants().OfType<CheckBox>().Count(x => x.Name == "ArchiveManagedBox")} archives be ticked, so the list draws none tickable either and no tick was exercised");
            else
            {
                var archive = ((TextBlock?)((StackPanel?)box.Parent)?.Children.OfType<TextBlock>().FirstOrDefault())?.Text ?? "an archive";
                var before = box.IsChecked == true;
                box.IsChecked = !before;
                box.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Until(() => Box()?.IsChecked == !before, seconds: 30);
                if (Box()?.IsChecked != !before) faults.Add($"ticking {archive} did not reach MO2");
                else {
                    var back = Box()!;
                    back.IsChecked = before;
                    back.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    await Until(() => Box()?.IsChecked == before, seconds: 30);
                    if (Box()?.IsChecked != before) faults.Add($"{archive} was left with MO2's tick changed");
                    else worked.Add($"{archive}'s tick reached MO2 and came back");
                }
            }
        } else faults.Add("Archives never drew");

        // --- Downloads ---
        await Navigate(menu.LeftMenuItemLibrary);
        await Settle();
        if (Find<Mo2DownloadsView>() is { } downloads) {
            var page = downloads.ViewModel!;
            var rows = () => downloads.GetVisualDescendants().OfType<TreeDataGrid>()
                .Select(x => x.Rows?.Count ?? 0).DefaultIfEmpty(0).Max();
            var before = rows();
            if (Named<TextBox>(downloads, "DownloadsQtFilter") is { } filter) {
                // Typed with text off a real download rather than "zzzzzz". Filtering to
                // nothing only shows the list reacts: a field that emptied the list
                // whatever was typed passed it. The provider keeps a download whose name
                // contains the text, so MO2's own list says how many should survive.
                var target = live.Profile.Downloads.Select(x => x.Name).FirstOrDefault(x => x.Length >= 4) ?? "";
                var needle = target.Length >= 4 ? target[..4] : "";
                if (needle.Length == 0) worked.Add("no download has a name long enough to filter on, so the downloads filter was not exercised");
                else {
                    var expected = live.Profile.Downloads.Count(x => x.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
                    filter.Text = needle; await Settle();
                    var narrowed = rows();
                    var kept = Shows(downloads, target);
                    filter.Text = ""; await Settle();
                    if (rows() != before) faults.Add("clearing the downloads filter did not put the list back");
                    else if (narrowed != expected) faults.Add($"the downloads filter left {narrowed} of {before} rows for \"{needle}\", where {expected} download(s) carry it");
                    else if (!kept) faults.Add($"the downloads filter dropped {target}, which carries \"{needle}\"");
                    else worked.Add($"the downloads filter kept the {expected} of {before} download(s) named for \"{needle}\", {target} among them");
                }
            } else faults.Add("Downloads has no filter field");
            if (Named<CheckBox>(downloads, "DownloadsHiddenFiles") is { } hidden) {
                hidden.IsChecked = true; await Settle();
                var shown = rows();
                hidden.IsChecked = false; await Settle();
                var withheld = live.Profile.HiddenDownloads.Count;
                var expected = before + withheld;
                // With nothing hidden, ticking the box is required to change nothing,
                // and this agrees with MO2 rather than showing the box works. Said
                // plainly: the number is still checked against MO2, but a box that was
                // not exercised does not get the same words as one that was.
                if (shown != expected) faults.Add($"showing hidden downloads listed {shown}, not the {expected} MO2 holds");
                else if (withheld == 0) worked.Add($"MO2 holds no hidden download, so the hidden-downloads box had none to bring back and was not exercised");
                else worked.Add($"the hidden-downloads box brought back MO2's {withheld} hidden archive(s), listing {expected}");
            } else faults.Add("Downloads has no hidden-files box");
        } else faults.Add("Downloads never drew");

        await Navigate(restore);
        await Task.Delay(600);

        if (faults.Count > 0) Console.WriteLine("FAIL MO2 widget behaviour: " + string.Join("; ", faults));
        else Console.WriteLine("PASS MO2 widget behaviour: " + string.Join(", ", worked));

        T? Find<T>() where T : Control =>
            window.GetVisualDescendants().OfType<T>().FirstOrDefault(x => x.IsEffectivelyVisible)
            ?? window.GetVisualDescendants().OfType<T>().FirstOrDefault();

        static TControl? Named<TControl>(Control page, string name) where TControl : Control =>
            page.GetVisualDescendants().OfType<TControl>().FirstOrDefault(x => x.Name == name);

        // Whether the page is still drawing a row by that name. A filter has two
        // halves — dropping what does not match and keeping what does — and only the
        // first can be read off a row count.
        static bool Shows(Control page, string text) =>
            text.Length > 0 && page.GetVisualDescendants().OfType<TextBlock>()
                .Any(x => x.IsEffectivelyVisible && x.Text is { } value &&
                          value.Contains(text, StringComparison.OrdinalIgnoreCase));

        static string[] Names(Mo2ModsAdapter adapter) => adapter.VisibleOrder;

        async Task Settle()
        {
            for (var pass = 0; pass < 8; pass++) { await Task.Delay(100); window.UpdateLayout(); }
        }

        // Anything that goes through MO2 takes as long as MO2 takes.
        async Task Until(Func<bool> ready, int seconds)
        {
            for (var attempt = 0; attempt < seconds * 10 && !ready(); attempt++) {
                await Task.Delay(100);
                window.UpdateLayout();
            }
        }

        async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
        {
            if (item is null) return;
            await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
                item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
            await Task.Delay(400);
        }
    }
}
