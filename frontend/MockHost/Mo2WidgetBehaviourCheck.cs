using Avalonia;
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

            // MO2's filtersAnd and filtersOr, which decide whether a mod has to carry
            // every ticked category or only one of them. Two categories are ticked and
            // the list read back against MO2's own category text both ways round: a
            // radio pair that narrowed nothing, or that narrowed the same way whichever
            // was chosen, is the fault here — and a one-sided "And shows fewer" passes
            // for a pair that simply empties the list.
            static string[] Owned(Mo2LiveMod mod) =>
                mod.Category.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            var andRadio = Named<RadioButton>(mods, "ModsFiltersAnd");
            var orRadio = Named<RadioButton>(mods, "ModsFiltersOr");
            var regular = live.Profile.Mods.Where(x => x.IsRegular).ToArray();
            // Two categories that no single mod's ticks can answer alike: at least one
            // mod carries one of them without the other, so And and Or must differ.
            var owners = categories.Select(x => (string)x.Tag!)
                .Select(name => (Name: name, Mods: regular.Where(mod => Owned(mod).Contains(name, StringComparer.OrdinalIgnoreCase))
                    .Select(mod => mod.Id).ToHashSet())).Where(x => x.Mods.Count > 0).ToArray();
            var pair = owners.SelectMany(first => owners.Where(second => second.Name != first.Name)
                .Select(second => (First: first, Second: second)))
                .FirstOrDefault(x => !x.First.Mods.SetEquals(x.Second.Mods));
            if (andRadio is null || orRadio is null) faults.Add("the Filters group has no And/Or pair");
            else if (pair.First.Mods is null)
                worked.Add($"MO2's mods carry no two categories that And and Or would answer differently, so the pair was not exercised");
            else {
                foreach (var box in categories)
                    box.IsChecked = (string)box.Tag! == pair.First.Name || (string)box.Tag! == pair.Second.Name;
                await Settle();
                orRadio.IsChecked = true; await Settle();
                var either = adapter.VisibleOrder.ToHashSet();
                andRadio.IsChecked = true; await Settle();
                var both = adapter.VisibleOrder.ToHashSet();
                var wantedEither = pair.First.Mods.Union(pair.Second.Mods).Select(x => x.ToString()).ToHashSet();
                var wantedBoth = pair.First.Mods.Intersect(pair.Second.Mods).Select(x => x.ToString()).ToHashSet();
                // Only the mods are judged: separators and Overwrite are in the list on
                // their own terms, which these two radios do not decide.
                var shownRegular = regular.Select(x => x.Id.ToString()).ToHashSet();
                var underEither = either.Intersect(shownRegular).ToHashSet();
                var underBoth = both.Intersect(shownRegular).ToHashSet();
                if (!underEither.SetEquals(wantedEither))
                    faults.Add($"Or listed {underEither.Count} mod(s) where {wantedEither.Count} carry {pair.First.Name} or {pair.Second.Name}");
                else if (!underBoth.SetEquals(wantedBoth))
                    faults.Add($"And listed {underBoth.Count} mod(s) where {wantedBoth.Count} carry both {pair.First.Name} and {pair.Second.Name}");
                else worked.Add($"Or listed the {wantedEither.Count} mod(s) carrying {pair.First.Name} or {pair.Second.Name} and And the {wantedBoth.Count} carrying both");
                // MO2's own default is And, and the ticks come off.
                if (Named<Button>(mods, "ModsFiltersClear") is { } clear) {
                    clear.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    await Settle();
                }
                foreach (var box in categories) box.IsChecked = false;
                andRadio.IsChecked = true;
                await Settle();
                if (adapter.VisibleRowCount != all) faults.Add("the list was left narrowed after the And/Or pair was tried");
            }

            // MO2 says a filtered list is filtered rather than leaving it looking like
            // mods that have gone: it rings the list in red and its count with it, and
            // rings a grouped list in green (ModListView::onModFilterActive). It also
            // offers a Clear beside the field, which it shows only while there is
            // something to clear. Driven here through the filter field a user types
            // in, and each state read off the border that is actually drawn.
            var frame = Named<Border>(mods, "ModsFilterFrame");
            var countFrame = Named<Border>(mods, "ModsCounterFrame");
            var clearAll = Named<Button>(mods, "ModsClearFiltersButton");
            var whatFor = Named<TextBlock>(mods, "ModsCurrentCategoryLabel");
            if (frame is null || countFrame is null) faults.Add("the mod list has no frame to say it is filtered");
            else if (clearAll is null || whatFor is null) faults.Add("the mod list has no Clear beside its filter field");
            else if (Named<TextBox>(mods, "ModsQtFilter") is not { } field) faults.Add("the mod list has no filter field to try its frame with");
            else {
                var restBrush = frame.BorderBrush;
                if (clearAll.IsVisible) faults.Add("Clear is offered with nothing filtered");
                else if (!ReferenceEquals(restBrush, Avalonia.Media.Brushes.Transparent))
                    faults.Add("the mod list is ringed with nothing filtered");
                else {
                    var name = live.Profile.Mods.FirstOrDefault(x => !x.IsSeparator)?.DisplayName ?? "";
                    var typed = name.Length > 3 ? name[..3] : "zzzzz";
                    field.Text = typed;
                    await Settle();
                    var ringed = ReferenceEquals(frame.BorderBrush, Mo2ModsView.FilteredInk);
                    var counted = ReferenceEquals(countFrame.BorderBrush, Mo2ModsView.FilteredInk);
                    var offered = clearAll.IsVisible;
                    var said = whatFor.Text ?? "";
                    // Painted, not merely assigned. A brush on a control that never
                    // reaches the screen reads the same from every property on it,
                    // which is the fault this whole check exists for — so the pixels
                    // the ring changes in the rendered window are counted.
                    var painted = await Ink(frame);
                    // MO2's Clear empties the field and the ticks together.
                    clearAll.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                    await Settle();
                    if (!ringed) faults.Add("the mod list was not ringed while it was filtered");
                    else if (painted < 100) faults.Add($"the ring around the filtered list painted {painted} pixels");
                    else if (!counted) faults.Add("the active count was not ringed while the list was filtered");
                    else if (!offered) faults.Add("Clear was not offered while the list was filtered");
                    else if (!said.Contains(typed, StringComparison.OrdinalIgnoreCase))
                        faults.Add($"the label beside the field read \"{said}\" rather than saying the list was narrowed to \"{typed}\"");
                    else if ((field.Text ?? "").Length > 0) faults.Add("Clear did not empty the filter field");
                    else if (adapter.VisibleRowCount != all) faults.Add("Clear did not put every mod back");
                    else if (!ReferenceEquals(frame.BorderBrush, Avalonia.Media.Brushes.Transparent))
                        faults.Add("the mod list stayed ringed after Clear");
                    else worked.Add($"a filtered list is ringed in MO2's red with its count over {painted} painted pixels, says it is narrowed to {said}, and Clear empties the field and takes the ring off");
                }
                // And MO2's green, which is the grouping rather than the filter.
                if (Named<ComboBox>(mods, "ModsGroupBox") is { } groupChoice) {
                    groupChoice.SelectedIndex = 1; await Settle();
                    var green = ReferenceEquals(frame.BorderBrush, Mo2ModsView.GroupedInk);
                    var countClear = ReferenceEquals(countFrame.BorderBrush, Avalonia.Media.Brushes.Transparent);
                    groupChoice.SelectedIndex = 0; await Settle();
                    if (!green) faults.Add("a grouped list was not ringed in MO2's green");
                    else if (!countClear) faults.Add("the active count was ringed by a grouping, which only MO2's filter does");
                    else if (!ReferenceEquals(frame.BorderBrush, Avalonia.Media.Brushes.Transparent))
                        faults.Add("the list stayed ringed after the grouping came off");
                    else worked.Add("a grouped list is ringed in MO2's green, and its count is not");
                }
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

            // MO2's saveModsButton, driven through to the file MO2 writes. Its slot
            // flushes the mod list and copies it beside itself under the time it was
            // taken, so the backup appearing in MO2's own profile folder is the
            // evidence — and the one this check made is taken away again afterwards.
            await Backup(mods, "SaveModsButton", "the mod list", ["modlist.txt"]);
            // Its restoreModsButton opens MO2's picker, which is modal and waits for
            // whoever opened it, so it is not pressed here. What is checked is that the
            // frontend's own is live exactly when MO2 will take it — the same route as
            // Save, which was driven above.
            if (Named<Button>(mods, "RestoreModsButton") is { } restoreMods) {
                if (restoreMods.IsEnabled != live.Profile.CanChangeOriginalUi)
                    faults.Add($"Restore is {(restoreMods.IsEnabled ? "live" : "greyed out")} where MO2 {(live.Profile.CanChangeOriginalUi ? "will" : "will not")} take it");
                else worked.Add($"the mod list's Restore is {(restoreMods.IsEnabled ? "live" : "greyed out")}, as MO2 is — its picker is modal and was not opened");
            } else faults.Add("My Mods has no Restore");
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

            // The count MO2 keeps beside its three plugin buttons.
            if (Named<TextBlock>(plugins, "ActivePluginsCounter") is { } counter) {
                var active = live.Profile.Order.Plugins.Count(x => x.IsActive);
                if (counter.Text != active.ToString()) faults.Add($"the active plugin count reads {counter.Text}, not MO2's {active}");
                else worked.Add($"the active plugin count reads MO2's own {active}");
            } else faults.Add("Plugins has no active count");

            // MO2's saveButton takes three files at once — the plugin list, the order
            // and the locked order — so all three have to appear for the press to have
            // reached it.
            await Backup(plugins, "SavePluginsButton", "the plugin order", ["plugins.txt", "loadorder.txt", "lockedorder.txt"]);
            if (Named<Button>(plugins, "RestorePluginsButton") is { } restorePlugins) {
                if (restorePlugins.IsEnabled != live.Profile.CanChangeOriginalUi)
                    faults.Add($"the plugin order's Restore is {(restorePlugins.IsEnabled ? "live" : "greyed out")} where MO2 {(live.Profile.CanChangeOriginalUi ? "will" : "will not")} take it");
                else worked.Add($"the plugin order's Restore is {(restorePlugins.IsEnabled ? "live" : "greyed out")}, as MO2 is — its picker is modal and was not opened");
            } else faults.Add("Plugins has no Restore");

            // MO2 decides whether its own sortButton can be pressed — the managed game
            // may have no sorting at all, and its whole pane is dead while a dialog is
            // up — and says why on the button it greys out. This one stood drawn live
            // whatever MO2 answered, and pressing it then went nowhere at all. The
            // sort itself is MO2's LOOT run over the real load order and is not
            // started here; what is checked is that the button agrees with MO2 about
            // whether it can be, and carries MO2's reason when it cannot.
            if (Named<Button>(plugins, "SortPluginsButton") is { } sort) {
                var allowed = live.Profile.CanChangeOriginalUi && live.Profile.CanSortPlugins;
                var tip = ToolTip.GetTip(sort) as string ?? "";
                if (sort.IsEnabled != allowed)
                    faults.Add($"Sort is {(sort.IsEnabled ? "live" : "greyed out")} where MO2 {(allowed ? "will" : "will not")} sort");
                else if (!live.Profile.CanSortPlugins && tip != live.Profile.SortPluginsUnavailableReason)
                    faults.Add($"Sort is greyed out reading \"{tip}\", not MO2's \"{live.Profile.SortPluginsUnavailableReason}\"");
                else if (allowed) worked.Add("Sort is live, as MO2's own is — MO2's LOOT run over the real load order was not started");
                else worked.Add($"Sort is greyed out with MO2's own reason: \"{live.Profile.SortPluginsUnavailableReason}\"");
            } else faults.Add("Plugins has no Sort");
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

            // MO2's other two boxes over the same tree, each counted off the rows the
            // tree is already showing rather than held to "it changed something".
            Mo2DataEntry[] Rows() => data.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()
                ?.Source?.Items.OfType<Mo2DataEntry>().ToArray() ?? [];
            if (Named<CheckBox>(data, "DataConflictsOnly") is { } conflicts) {
                var here = Rows();
                // MO2 keeps every folder — it is how the rest is reached — and the files
                // more than one mod provides.
                var expected = here.Count(x => x.Directory || x.Origins.Distinct().Count() > 1);
                conflicts.IsChecked = true; await Settle();
                var narrowed = Rows().Length;
                conflicts.IsChecked = false; await Settle();
                if (Rows().Length != here.Length) faults.Add("unticking Conflicts only did not put the tree back");
                else if (narrowed != expected)
                    faults.Add($"Conflicts only left {narrowed} of {here.Length} rows, where {expected} are folders or served by more than one mod");
                else if (expected == here.Length)
                    worked.Add($"no file under Data is served by one mod alone, so Conflicts only had nothing to take away and was not exercised");
                else worked.Add($"Conflicts only kept the {expected} conflicting or folder row(s) of {here.Length} and put the rest back");
            } else faults.Add("Data has no Conflicts only box");
            if (Named<CheckBox>(data, "DataHiddenFiles") is { } hiddenFiles) {
                var here = Rows();
                static bool Hidden(Mo2DataEntry row) => !row.Directory && row.Name.EndsWith(".mohidden", StringComparison.OrdinalIgnoreCase);
                if (here.Any(Hidden)) faults.Add("Data lists a hidden file before its box is ticked");
                hiddenFiles.IsChecked = true; await Settle();
                var shown = Rows();
                hiddenFiles.IsChecked = false; await Settle();
                var brought = shown.Count(Hidden);
                if (Rows().Length != here.Length) faults.Add("unticking hidden files did not put the tree back");
                else if (shown.Length != here.Length + brought)
                    faults.Add($"ticking hidden files listed {shown.Length} rows against {here.Length} plus the {brought} hidden one(s) it brought back");
                else if (brought == 0)
                    worked.Add("MO2 hides no file under Data, so the hidden-files box had none to bring back and was not exercised");
                else worked.Add($"the hidden-files box brought back {brought} hidden file(s), listing {shown.Length}, and took them away again");
            } else faults.Add("Data has no hidden-files box");
            // MO2's own Refresh over the same tree: it reads the folder again through
            // MO2 rather than redrawing what is already held, so the rows have to go
            // and come back rather than merely still be there.
            if (Named<Button>(data, "DataRefreshButton") is { } refresh) {
                var here = Rows().Select(x => x.Name).ToArray();
                // The tree the page is drawing now. The page builds a new one out of
                // whatever MO2 answers with, so a Refresh that did nothing at all
                // leaves this one in place — where counting the rows again passes,
                // since they never left.
                object? Table() => data.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault()?.Source;
                var was = Table();
                refresh.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Until(() => !ReferenceEquals(Table(), was) && Rows().Length > 0, seconds: 60);
                var after = Rows().Select(x => x.Name).ToArray();
                if (ReferenceEquals(Table(), was)) faults.Add("Refresh did not read Data again");
                else if (!after.SequenceEqual(here))
                    faults.Add($"Refresh left Data showing {after.Length} row(s) against the {here.Length} MO2 had listed");
                else worked.Add($"Refresh read Data again through MO2 and listed the same {here.Length} row(s)");
            } else faults.Add("Data has no Refresh");
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
            // MO2's own Refresh reads its downloads folder again. This one used to
            // recompute which of the page's controls were live and stop there, which
            // looks identical from the outside — so what is read back is that MO2 was
            // asked, and that the list MO2 answered with is the one on screen.
            if (Named<Button>(downloads, "DownloadsRefreshButton") is { } refresh) {
                var reads = live.Profile.DownloadRefreshes;
                var here = rows();
                refresh.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Until(() => live.Profile.DownloadRefreshes > reads, seconds: 120);
                await Settle();
                if (live.Profile.DownloadRefreshes == reads) faults.Add("Refresh did not ask MO2 to read its downloads again");
                else if (rows() != live.Profile.Downloads.Count)
                    faults.Add($"after Refresh the list shows {rows()} of MO2's {live.Profile.Downloads.Count} download(s)");
                else worked.Add($"Refresh had MO2 read its downloads folder again and listed the {here} it came back with");
            } else faults.Add("Downloads has no Refresh");

            // MO2 offers a column chooser on this one list, on its header's right-click
            // (DownloadListView::onHeaderCustomContextMenu), and starts with four of
            // its eight columns hidden (setManager). This page drew all eight and
            // offered nothing. The menu is opened where MO2 puts it, a column is
            // chosen from it, and the table is read back — a menu that lists the
            // columns and changes none of them is the fault this is for.
            var headings = downloads.GetVisualDescendants()
                .OfType<Avalonia.Controls.Primitives.TreeDataGridColumnHeadersPresenter>().FirstOrDefault();
            string[] Drawn() => downloads.GetVisualDescendants().OfType<TreeDataGrid>()
                .Select(x => x.Columns?.Select(c => c.Header?.ToString() ?? "").ToArray() ?? [])
                .FirstOrDefault(x => x.Length > 0) ?? [];
            if (headings?.ContextFlyout is not MenuFlyout columnMenu) faults.Add("the downloads header carries no column menu");
            else {
                columnMenu.ShowAt(headings); await Settle(); columnMenu.Hide();
                var entries = columnMenu.Items.OfType<MenuItem>().Select(x => x.Header as string ?? "").ToArray();
                // MO2's own eight, less the name it never offers to hide.
                string[] offered = ["Status", "Size", "Filetime", "Mod name", "Version", "Nexus ID", "Source Game"];
                var missing = offered.Where(x => !entries.Contains(x)).ToArray();
                if (missing.Length > 0) faults.Add($"the column menu does not offer {string.Join(", ", missing)}");
                else if (entries.Contains("Name")) faults.Add("the column menu offers to hide the name, which MO2 keeps off it");
                else {
                    // MO2's four start hidden, and this page used to draw them anyway.
                    string[] hiddenByMo2 = ["Mod name", "Version", "Nexus ID", "Source Game"];
                    var drawn = Drawn();
                    var extra = hiddenByMo2.Where(drawn.Contains).ToArray();
                    if (extra.Length > 0) faults.Add($"the list draws {string.Join(", ", extra)}, which MO2 starts with hidden");
                    else if (!drawn.Contains("Filetime")) faults.Add("the list does not draw Filetime, which MO2 starts with shown");
                    else {
                        // Chosen from the menu itself rather than through the object
                        // behind it: the entry is what a user reaches.
                        var entry = columnMenu.Items.OfType<MenuItem>().First(x => (x.Header as string) == "Version");
                        entry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                        await Until(() => Drawn().Contains("Version"), seconds: 10);
                        var chosen = Drawn();
                        entry.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                        await Until(() => !Drawn().Contains("Version"), seconds: 10);
                        var back = Drawn();
                        if (!chosen.Contains("Version")) faults.Add("choosing Version from the column menu did not draw it");
                        else if (chosen.Length != drawn.Length + 1)
                            faults.Add($"choosing Version drew {chosen.Length} columns against the {drawn.Length} before it");
                        else if (!back.SequenceEqual(drawn))
                            faults.Add($"unchoosing Version left {string.Join(", ", back)} rather than {string.Join(", ", drawn)}");
                        else worked.Add($"the downloads header carries MO2's column menu over its {entries.Length} entries, " +
                            $"starts with MO2's {string.Join(", ", hiddenByMo2)} hidden, and drew Version among {chosen.Length} columns when chosen and took it away again");
                    }
                }
            }
        } else faults.Add("Downloads never drew");

        // --- MO2's run row ---
        // MO2 puts three things under its lists: the executables box, Run beside it,
        // and the shortcut menu. The box's first row is MO2's own <Edit...>, which
        // opens its Edit Executables dialog and puts the previous choice back
        // (MainWindow::refreshExecutablesList and on_executablesListBox_currentIndexChanged).
        // The frontend reached that dialog from Tools and from nowhere else.
        //
        // The entry is read where it sits rather than chosen: choosing it hands over
        // to a modal MO2 dialog that waits for whoever opened it, as Restore and the
        // category editor do, so it is not driven here.
        // Read off the panel itself, not off the window: a collapsed sidebar keeps the
        // whole run row in its tool flyout, where nothing in the window's tree can
        // find it, and the user's sidebar is collapsed on this host.
        if (live.LaunchPanel?.Executable is { } executables) {
            var listed = (executables.ItemsSource as IEnumerable<string> ?? []).ToArray();
            var wanted = new[] { Mo2LaunchPanel.EditEntry }.Concat(live.Profile.Executables).ToArray();
            if (listed.FirstOrDefault() != Mo2LaunchPanel.EditEntry)
                faults.Add($"the executables box opens with {listed.FirstOrDefault() ?? "nothing"}, not MO2's {Mo2LaunchPanel.EditEntry}");
            else if (!listed.SequenceEqual(wanted))
                faults.Add($"the executables box lists {listed.Length - 1} of MO2's {live.Profile.Executables.Count} executables");
            else if (executables.SelectedItem as string == Mo2LaunchPanel.EditEntry)
                faults.Add("the executables box is sitting on MO2's edit entry rather than on an executable");
            else worked.Add($"the executables box opens with MO2's {Mo2LaunchPanel.EditEntry} above its {live.Profile.Executables.Count} executable(s), " +
                $"with {executables.SelectedItem} chosen — its dialog is modal and was not opened");
        } else faults.Add("there is no executables box to run anything from");

        // MO2's linkButton beside that box: the three places it will put a shortcut to
        // the chosen executable, each drawn with an add or a remove icon by whether
        // the shortcut is already there (on_linkButton_pressed). Read from MO2, and
        // one of them driven through to MO2 and back.
        if (live.LaunchPanel?.ShortcutMenu is { } shortcuts) {
            var chosen = live.LaunchPanel.Model.SelectedExecutable;
            var entries = await live.Profile.ReadShortcuts(chosen);
            // MO2's own three, by its own wording (MainWindow's linkMenu).
            string[] wanted = ["Toolbar and Menu", "Desktop", "Start Menu"];
            var missing = wanted.Where(x => !entries.Any(e => e.Text == x)).ToArray();
            // With the reason MO2 gave, not just the emptiness: a route that answers
            // nothing and says nothing is the shape of fault this check exists for,
            // and "no entries" alone does not say whether MO2 refused or was not asked.
            if (entries.Length == 0) faults.Add($"MO2 offered no shortcut entry for {chosen} — {live.Profile.Status}");
            else if (missing.Length > 0) faults.Add($"MO2's shortcut menu is missing {string.Join(", ", missing)}");
            else {
                // Toolbar and Menu is the one driven: it is MO2's own executable
                // setting rather than a file in its Windows prefix, MO2 reports it
                // back from the icon it draws, and it is put back the same way.
                var toolbar = entries.First(x => x.Text == "Toolbar and Menu");
                if (!toolbar.Enabled)
                    worked.Add($"MO2 offers its three shortcut entries for {chosen} and will not take Toolbar and Menu, so none was driven");
                else {
                    await live.Profile.ToggleShortcut(chosen, "Toolbar and Menu", live.Profile.CurrentTarget);
                    // Asked again rather than assumed: the toggle goes through MO2 and
                    // MO2 is what says whether it took.
                    var after = (await live.Profile.ReadShortcuts(chosen)).FirstOrDefault(x => x.Text == "Toolbar and Menu");
                    await live.Profile.ToggleShortcut(chosen, "Toolbar and Menu", live.Profile.CurrentTarget);
                    var back = (await live.Profile.ReadShortcuts(chosen)).FirstOrDefault(x => x.Text == "Toolbar and Menu");
                    if (after.Exists == toolbar.Exists)
                        faults.Add($"Toolbar and Menu did not change what MO2 reports for {chosen}");
                    else if (back.Exists != toolbar.Exists)
                        faults.Add($"{chosen} was left {(after.Exists ? "on" : "off")} MO2's toolbar");
                    else worked.Add($"MO2's three shortcut entries are offered for {chosen}, and Toolbar and Menu went " +
                        $"{(toolbar.Exists ? "off and back on" : "on and back off")} in MO2");
                }
            }
            // The menu a user opens, over the same answer.
            if (shortcuts.Flyout is MenuFlyout shortcutMenu && entries.Length > 0) {
                shortcutMenu.ShowAt(shortcuts);
                await Until(() => shortcutMenu.Items.OfType<MenuItem>().Any(x => (x.Header as string) != "Asking MO2…"), seconds: 30);
                var captions = shortcutMenu.Items.OfType<MenuItem>().Select(x => x.Header as string ?? "").ToArray();
                shortcutMenu.Hide();
                // MO2 says add or remove with an icon; this says it in words, so the
                // caption has to follow what MO2 reported rather than being fixed.
                var expected = entries.Select(x => (x.Exists ? "Remove from " : "Add to ") + x.Text).ToArray();
                if (!captions.SequenceEqual(expected))
                    faults.Add($"the shortcut menu reads {string.Join(", ", captions)}, not {string.Join(", ", expected)}");
                else worked.Add($"the shortcut menu reads {string.Join(", ", captions)}, following what MO2 reported");
            }
        } else faults.Add("there is no shortcut menu beside the executables box");

        // The row of pinned shortcuts beside Run. MO2 keeps that list itself — the
        // executables it shows on its own toolbar — and this row was drawn out of a
        // file of the frontend's own, so the same idea was held in two places and
        // could disagree. Read against MO2's toolbar, and moved by asking MO2.
        if (live.LaunchPanel?.GetVisualDescendants().OfType<Panel>().FirstOrDefault(x => x.Name == "PinnedToolShortcuts") is { } pinRow) {
            string[] Pinned() => pinRow.GetVisualDescendants().OfType<Button>()
                .Where(x => x.Name == "PinnedToolShortcut")
                .Select(x => (ToolTip.GetTip(x) as string) ?? "").ToArray();
            var mo2Pins = live.Profile.PinnedExecutables.ToArray();
            var shown = Pinned();
            var missing = mo2Pins.Where(x => !shown.Contains(x)).ToArray();
            if (missing.Length > 0)
                faults.Add($"MO2 shows {string.Join(", ", missing)} on its toolbar and this row does not");
            else {
                var subject = live.Profile.Executables.FirstOrDefault(x => !mo2Pins.Contains(x));
                if (subject is null)
                    worked.Add($"the pinned row draws MO2's {mo2Pins.Length} toolbar executable(s), and MO2 already pins them all, so none was moved");
                else {
                    // Pinned through MO2's own Toolbar and Menu and taken off again,
                    // with the row read back both times.
                    await live.Profile.ToggleShortcut(subject, "Toolbar and Menu", live.Profile.CurrentTarget);
                    await Until(() => Pinned().Contains(subject), seconds: 30);
                    var after = Pinned();
                    await live.Profile.ToggleShortcut(subject, "Toolbar and Menu", live.Profile.CurrentTarget);
                    await Until(() => !Pinned().Contains(subject), seconds: 30);
                    var back = Pinned();
                    if (!after.Contains(subject)) faults.Add($"pinning {subject} in MO2 did not put it on the row beside Run");
                    else if (back.Contains(subject)) faults.Add($"{subject} was left pinned");
                    else if (!back.SequenceEqual(shown)) faults.Add($"the pinned row was left reading {string.Join(", ", back)} rather than {string.Join(", ", shown)}");
                    else worked.Add($"the pinned row follows MO2's toolbar: {subject} went on it and came off again with MO2's own Toolbar and Menu");
                }
            }
        } else faults.Add("there is no row of pinned shortcuts beside Run");

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

        // MO2's own Save beside a list: its slot flushes the list and copies each of
        // the files behind it beside itself, named for the second it was taken
        // (MainWindow::createBackup). Pressing the button and watching those copies
        // appear in MO2's profile folder is the whole of the evidence — nothing the
        // frontend holds changes — and the copies this check caused are taken away
        // again, so MO2 is left with the backups it had.
        async Task Backup(Control page, string button, string what, string[] files)
        {
            if (Named<Button>(page, button) is not { } save) { faults.Add($"{what} has no Save"); return; }
            var folder = live.Profile.ProfilePath.Length == 0 ? "" : Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath);
            if (folder.Length == 0 || !Directory.Exists(folder)) { faults.Add($"MO2's profile folder for {what} was not found"); return; }
            string[] Backups() => files.SelectMany(name => Directory.GetFiles(folder, name + ".*")).ToArray();
            var before = Backups().ToHashSet();
            if (!save.IsEnabled) {
                if (live.Profile.CanChangeOriginalUi) faults.Add($"Save for {what} is greyed out where MO2 will take it");
                else worked.Add($"Save for {what} is greyed out, as MO2's pane is");
                return;
            }
            save.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Until(() => Backups().Except(before).Count() >= files.Length, seconds: 60);
            var made = Backups().Except(before).ToArray();
            if (made.Length < files.Length) {
                faults.Add($"Save for {what} left {made.Length} of the {files.Length} backup(s) MO2 takes");
                foreach (var path in made) TryDelete(path);
                return;
            }
            var missing = files.Where(name => !made.Any(path => Path.GetFileName(path).StartsWith(name + ".", StringComparison.OrdinalIgnoreCase))).ToArray();
            foreach (var path in made) TryDelete(path);
            if (missing.Length > 0) faults.Add($"Save for {what} took no backup of {string.Join(", ", missing)}");
            else worked.Add($"Save for {what} had MO2 back up {string.Join(", ", files)}, and the {made.Length} file(s) this check caused were removed");
        }

        void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (IOException error) { faults.Add($"the backup this check caused was left behind: {Path.GetFileName(path)} ({error.Message})"); }
            catch (UnauthorizedAccessException error) { faults.Add($"the backup this check caused was left behind: {Path.GetFileName(path)} ({error.Message})"); }
        }

        async Task Settle()
        {
            for (var pass = 0; pass < 8; pass++) { await Task.Delay(100); window.UpdateLayout(); }
        }

        // How many pixels of the rendered window a control is responsible for: the
        // window is drawn, the control faded out, drawn again, and the two compared
        // over its own rectangle. Opacity rather than IsVisible, so the rectangle
        // does not move between the two. The same technique Mo2PhysicalityCheck uses,
        // and for the same reason — a control can be laid out, visible and carrying
        // the right brush while painting nothing at all.
        async Task<int> Ink(Control control)
        {
            var origin = control.TranslatePoint(default, window) ?? default;
            var rect = new Avalonia.PixelRect((int)origin.X, (int)origin.Y,
                Math.Max(1, (int)Math.Ceiling(control.Bounds.Width)), Math.Max(1, (int)Math.Ceiling(control.Bounds.Height)));
            byte[] Shot()
            {
                using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(
                    new Avalonia.PixelSize(Math.Max(1, (int)window.ClientSize.Width), Math.Max(1, (int)window.ClientSize.Height)));
                bitmap.Render(window);
                var stride = rect.Width * 4;
                var buffer = new byte[stride * rect.Height];
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(buffer, System.Runtime.InteropServices.GCHandleType.Pinned);
                try { bitmap.CopyPixels(rect, handle.AddrOfPinnedObject(), buffer.Length, stride); }
                finally { handle.Free(); }
                return buffer;
            }
            var shown = Shot();
            control.Opacity = 0;
            await Task.Delay(80); window.UpdateLayout();
            var hidden = Shot();
            control.Opacity = 1;
            await Task.Delay(80); window.UpdateLayout();
            var lit = 0;
            for (var index = 0; index < shown.Length; index += 4)
                if (Math.Abs(shown[index] - hidden[index]) > 8) lit++;
            return lit;
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
