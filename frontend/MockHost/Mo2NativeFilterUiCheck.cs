using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

internal static class Mo2NativeFilterUiCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        async Task Wait(Func<bool> ready, string message) {
            var end = DateTime.UtcNow.AddSeconds(20);
            while (!ready() && DateTime.UtcNow < end) { await Task.Delay(50); window.UpdateLayout(); }
            if (!ready()) throw new Exception(message);
        }
        await Wait(() => live.Profile.CanChangeOriginalUi, "Native host unavailable");
        window.Width = 1280; live.ShowProfile();
        await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(live.ProfileMenu.LeftMenuItemLoadout!.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
        await Wait(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any(x => ReferenceEquals(x.DataContext, live.ModsPage)), "Mods page missing");
        var view = window.GetVisualDescendants().OfType<Mo2ModsView>().First(x => ReferenceEquals(x.DataContext, live.ModsPage));
        T Named<T>(string name) where T : Control => view.GetVisualDescendants().OfType<T>().FirstOrDefault(x => x.Name == name)
            ?? throw new Exception("Missing rendered control: " + name);
        await Wait(() => view.GetVisualDescendants().OfType<Button>().Any(x => x.Name == "ModsListOptionsButton"),
            "Mods overflow button never rendered");
        var more = Named<Button>("ModsListOptionsButton");
        var menu = (MenuFlyout)more.Flyout!;
        void ShowFilters(bool shown) {
            menu.ShowAt(more);
            try {
                var item = menu.Items.OfType<MenuItem>().Single(x => x.Name == "ModsCategoriesMenuItem");
                if (((CheckBox)item.Icon!).IsChecked != shown)
                    item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            } finally { menu.Hide(); }
        }
        void Clear() {
            menu.ShowAt(more);
            try {
                var item = menu.Items.OfType<MenuItem>().Single(x => x.Name == "ModsClearFiltersMenuItem");
                var active = view.GetVisualDescendants().OfType<Mo2CategoryFilterList>()
                    .Any(x => x.Selection.Count + x.Excluded.Count > 0);
                if (active && !item.IsEnabled) throw new Exception("Clear all filters is disabled with selected native criteria");
                if (item.IsEnabled) item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
            } finally { menu.Hide(); }
        }
        ShowFilters(true);
        await Wait(() => view.GetVisualDescendants().OfType<Mo2CategoryFilterList>().Any(),
            "Native filter list never rendered after opening its pane");
        var component = view.GetVisualDescendants().OfType<Mo2CategoryFilterList>().First();
        var modeAnd = Named<RadioButton>("ModsFiltersAnd");
        var modeOr = Named<RadioButton>("ModsFiltersOr");
        var adapter = (Mo2ModsAdapter)view.ViewModel!.Adapter;
        try {
            await Wait(() => component.IsEffectivelyVisible && component.Items.OfType<CheckBox>().Any() && component.Items.OfType<CheckBox>().All(x => x.IsEnabled),
                "Native criteria never became visible and ready");
            Clear(); modeAnd.IsChecked = true;
            await Wait(() => adapter.VisibleOrder.ToHashSet().SetEquals(live.Profile.Mods.Where(x => !x.IsOverwrite).Select(x => x.Id.ToString())),
                "Unfiltered mod list did not contain every native mod");
            var all = adapter.VisibleOrder.ToHashSet();
            var nameById = live.Profile.Mods.ToDictionary(x => x.Id.ToString(), x => x.Name);
            var nodes = live.Profile.CategoryTree.SelectMany(x => x.Walk()).Where(x => x.Type is 0 or 2).ToArray();
            var expected = await live.Profile.ReadModFilterMatches(live.Profile.CurrentTarget, nodes);
            var boxes = component.Items.OfType<CheckBox>().Where(x => x.DataContext is Mo2CategoryNode node && node.Type is 0 or 2).ToArray();
            if (boxes.Length != nodes.Length) throw new Exception("Native special/content criteria are missing from the visible list");
            string Caption(CheckBox box) => box.Content is Panel panel ? panel.Children.OfType<TextBlock>().Single().Text! : ((TextBlock)box.Content!).Text!;
            foreach (var box in boxes) {
                var node = (Mo2CategoryNode)box.DataContext!;
                if (Caption(box) != $"{node.Name} ({expected[node.Key].Length})") throw new Exception("Native criterion label/count mismatch");
            }
            var enabled = boxes.Single(x => ((Mo2CategoryNode)x.DataContext!).Key == (0, 10000));
            var content = boxes.FirstOrDefault(x => ((Mo2CategoryNode)x.DataContext!).Type == 2 && expected[((Mo2CategoryNode)x.DataContext!).Key].Length > 0)
                ?? throw new Exception("No populated native content criterion in this profile");
            var enabledNames = expected[(0, 10000)].ToHashSet();
            var contentNames = expected[((Mo2CategoryNode)content.DataContext!).Key].ToHashSet();
            async Task Check(Func<string, bool> match, string message) {
                var wanted = all.Where(id => match(nameById[id])).ToHashSet();
                try { await Wait(() => adapter.VisibleOrder.ToHashSet().SetEquals(wanted), message); }
                catch {
                    Console.WriteLine($"Filter diagnostic: selection={component.Summary}, enabledState={enabled.IsChecked}, baseline={all.Count}, expected={wanted.Count}, actual={adapter.VisibleOrder.Length}, revision={live.Profile.FilterRevision}, reads={live.Profile.NativeFilterReads}");
                    Console.WriteLine("Missing rows: " + string.Join(", ", wanted.Except(adapter.VisibleOrder).Select(id => nameById[id])));
                    Console.WriteLine("Extra rows: " + string.Join(", ", adapter.VisibleOrder.Except(wanted).Select(id => nameById.GetValueOrDefault(id, id))));
                    throw;
                }
            }
            var click = typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            click.Invoke(enabled, null); await Check(enabledNames.Contains, "Enabled filter did not match native rows");
            ShowFilters(false);
            await Wait(() => !component.IsEffectivelyVisible, "Category pane did not close");
            await live.Profile.Refresh();
            await Check(enabledNames.Contains, "Closing or refreshing the pane lost its active filter");
            ShowFilters(true);
            await Wait(() => component.IsEffectivelyVisible && enabled.IsEnabled, "Category pane did not reopen");
            if (!component.Items.Contains(enabled) || enabled.IsChecked != true)
                throw new Exception("Reopening replaced or cleared the selected category control");
            var separators = Named<ComboBox>("ModsFiltersSeparators");
            var separatorNames = live.Profile.Mods.Where(x => x.IsSeparator).Select(x => x.Name).ToHashSet();
            separators.SelectedIndex = 1;
            await Check(name => enabledNames.Contains(name) || separatorNames.Contains(name), "Show separators did not override active criterion");
            separators.SelectedIndex = 2;
            await Check(name => enabledNames.Contains(name) && !separatorNames.Contains(name), "Hide separators did not override active criterion");
            separators.SelectedIndex = 0;
            await Check(enabledNames.Contains, "Filter separators did not restore native predicate");
            click.Invoke(enabled, null); await Check(name => !enabledNames.Contains(name), "Inverted enabled filter did not match native rows");
            click.Invoke(content, null); await Check(name => !enabledNames.Contains(name) && contentNames.Contains(name), "Mixed status/content And failed");
            modeOr.IsChecked = true; await Check(name => !enabledNames.Contains(name) || contentNames.Contains(name), "Mixed status/content Or failed");
            if (!component.Summary.Contains("Not " + ((Mo2CategoryNode)enabled.DataContext!).Name)) throw new Exception("Inverted native filter missing from summary");
            Clear(); modeAnd.IsChecked = true; await Check(_ => true, "Clear did not restore the full list");
            separators.SelectedIndex = 2;
            await Check(_ => true, "Separator mode affected rows with no active filter");
            separators.SelectedIndex = 0;
            var revision = live.Profile.FilterRevision; var reads = live.Profile.NativeFilterReads;
            await live.Profile.Refresh(); window.Width = 1120; await Task.Delay(150); window.Width = 1280; await Task.Delay(150);
            if (live.Profile.FilterRevision == revision && live.Profile.NativeFilterReads != reads) throw new Exception("Unchanged snapshots/resizing repeated native filter reads");
            if (Environment.GetEnvironmentVariable("MO2_FILTER_REFRESH_MOD") is { Length: > 0 } fixtureName) {
                if (!fixtureName.StartsWith("mo2-filter-refresh-probe-", StringComparison.Ordinal) ||
                    !live.Profile.CurrentTarget.Endpoint.Contains("mo2-fnv-host", StringComparison.Ordinal))
                    throw new Exception("Refresh mutation check requires its disposable mod in the isolated FNV host");
                var fixture = live.Profile.Mods.Single(x => x.Name == fixtureName);
                if ((fixture.State & 6) != 0 || fixture.IsSeparator || fixture.IsOverwrite)
                    throw new Exception("Refresh fixture must start as a disabled ordinary mod");
                click.Invoke(enabled, null); await Check(enabledNames.Contains, "Refresh baseline did not match native rows");
                // MO2 can change a separator's active predicate when a child mod
                // changes. Ordinary rows must remain continuous; separator results
                // are compared against a fresh native query after each mutation.
                var separatorIds = live.Profile.Mods.Where(x => x.IsSeparator).Select(x => x.Id.ToString()).ToHashSet();
                HashSet<string> OrdinaryRows() => adapter.VisibleOrder.Where(id => !separatorIds.Contains(id)).ToHashSet();
                var baselineRows = OrdinaryRows();
                var enabledRows = baselineRows.Append(fixture.Id.ToString()).ToHashSet();
                bool updatingSeen = false, disabledSeen = false;
                string? refreshFault = null;
                void Sample(object? sender, EventArgs args) {
                    var rows = OrdinaryRows();
                    if (!rows.SetEquals(baselineRows) && !rows.SetEquals(enabledRows))
                        refreshFault = $"Filter refresh exposed a partial/empty list ({rows.Count} rows)";
                    if (Named<TextBlock>("ModsNativeFilterStatus").Text == "Updating MO2 filters…") {
                        updatingSeen = true; disabledSeen |= !enabled.IsEnabled;
                    }
                }
                view.LayoutUpdated += Sample;
                try {
                    await live.Profile.ToggleMod(fixture.Id);
                    await Wait(() => enabled.IsEnabled && OrdinaryRows().SetEquals(enabledRows), "Enabled fixture never arrived in refreshed filter results");
                    var afterEnable = await live.Profile.ReadModFilterMatches(live.Profile.CurrentTarget, [(Mo2CategoryNode)enabled.DataContext!]);
                    await Check(afterEnable[(0, 10000)].ToHashSet().Contains, "Enabled fixture/separator results differ from MO2");
                    await live.Profile.ToggleMod(fixture.Id);
                    await Wait(() => enabled.IsEnabled && OrdinaryRows().SetEquals(baselineRows), "Disabled fixture remained in refreshed filter results");
                    var afterDisable = await live.Profile.ReadModFilterMatches(live.Profile.CurrentTarget, [(Mo2CategoryNode)enabled.DataContext!]);
                    await Check(afterDisable[(0, 10000)].ToHashSet().Contains, "Disabled fixture/separator results differ from MO2");
                    if (refreshFault is not null) throw new Exception(refreshFault);
                    if (!updatingSeen || !disabledSeen) throw new Exception("Did not observe the guarded refresh interval");
                    Console.WriteLine("PASS native filter refresh: real disposable-mod enable/disable, retained rows while updating, guarded controls and refreshed membership");
                } catch {
                    Console.WriteLine($"Refresh diagnostic: nativeState={live.Profile.FindMod(fixture.Id)?.State}, selection={component.Summary}, enabled={enabled.IsEnabled}, status={Named<TextBlock>("ModsNativeFilterStatus").Text}, revision={live.Profile.FilterRevision}, reads={live.Profile.NativeFilterReads}, rows={adapter.VisibleOrder.Length}, wanted={enabledRows.Count}, observedUpdating={updatingSeen}");
                    Console.WriteLine("Refresh missing: " + string.Join(", ", enabledRows.Except(adapter.VisibleOrder).Select(id => nameById.GetValueOrDefault(id, id))));
                    Console.WriteLine("Refresh extra: " + string.Join(", ", adapter.VisibleOrder.Except(enabledRows).Select(id => nameById.GetValueOrDefault(id, id))));
                    throw;
                } finally {
                    view.LayoutUpdated -= Sample;
                    if (live.Profile.FindMod(fixture.Id) is { } remaining && (remaining.State & 2) != 0)
                        await live.Profile.ToggleMod(fixture.Id);
                    Clear();
                }
            }
            using var screenshot = new Avalonia.Media.Imaging.RenderTargetBitmap(new Avalonia.PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
            screenshot.Render(window);
            var capture = Environment.GetEnvironmentVariable("MO2_SCREENSHOT") is { Length: > 0 } output
                ? Path.ChangeExtension(output, $"filters-{live.Profile.NexusGame}-{Guid.NewGuid():N}.png")
                : "frontend/artifacts/native-filter-visible.png";
            screenshot.Save(capture);
            Console.WriteLine($"PASS visible native filters: all {boxes.Length} status/content entries and counts, include/invert, mixed And/Or, summary, Clear and stable-cache reuse");
        } finally { Clear(); modeAnd.IsChecked = true; ShowFilters(false); }
    }
}
