using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2ToolbarSearchCheck
{
    internal static async Task Run(Window window, Control? onlyPage = null)
    {
        async Task Wait(Func<bool> ready, string phase) {
            var until = DateTime.UtcNow.AddSeconds(10);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("Toolbar search: " + phase);
                await Task.Delay(25);
            }
        }
        foreach (var page in window.GetVisualDescendants().OfType<Control>()
            .Where(x => x.IsEffectivelyVisible && (onlyPage is null ? x is Mo2ModsView || x is Mo2PluginsView : ReferenceEquals(x, onlyPage))).ToArray()) {
            if (page is Mo2ModsView mods) {
                var more = page.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "ModsListOptionsButton");
                var menu = (MenuFlyout)more.Flyout!;
                var adapter = (Mo2ModsAdapter)mods.ViewModel!.Adapter;
                var originalGrouping = adapter.Grouping;
                MenuItem Item(string name) => menu.Items.OfType<MenuItem>().Single(x => x.Name == name);
                void Click(MenuItem item) { item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); menu.Hide(); }
                try {
                    menu.ShowAt(more);
                    Click(Item("ModsCategoriesMenuItem"));
                    await Wait(() => page.GetVisualDescendants().OfType<Border>().Any(x => x.Name == "ModCategoriesGroup" && x.IsEffectivelyVisible),
                        "category filters open from overflow");
                    var oldWidth = page.Width;
                    var oldHeight = page.Height;
                    try {
                        foreach (var height in new[] { 480d, 320d }) {
                            page.Width = 340; page.Height = height;
                            var pane = page.GetVisualDescendants().OfType<ScrollViewer>().Single(x => x.Name == "ModsCategoryPane");
                            var results = page.GetVisualDescendants().OfType<TreeDataGrid>().Single();
                            await Wait(() => Math.Abs(page.Bounds.Width - 340) < 1 && Math.Abs(page.Bounds.Height - height) < 1 &&
                                pane.Viewport.Height > 0 && results.Bounds.Height > 40, "narrow filters and results have usable viewports");
                            await Task.Delay(200);
                            var top = pane.TranslatePoint(default, page)!.Value;
                            var bottom = top.Y + pane.Bounds.Height;
                            var resultTop = results.TranslatePoint(default, page)!.Value.Y;
                            if (!pane.IsEffectivelyVisible || top.X < 0 || top.X + pane.Bounds.Width > page.Bounds.Width + 1 ||
                                bottom > resultTop + 1 || bottom > page.Bounds.Height)
                                throw new Exception("Narrow category filters overflow or cover the results");
                            foreach (var name in new[] { "ModsFiltersSeparators", "ModsFiltersEdit" }) {
                                var control = pane.GetVisualDescendants().OfType<Control>().Single(x => x.Name == name);
                                control.BringIntoView();
                                await Wait(() => control.TranslatePoint(default, pane) is { } at && at.Y >= -1 &&
                                    at.Y + control.Bounds.Height <= pane.Bounds.Height + 1, "filter footer reachable: " + name);
                            }
                            pane.Offset = default;
                            await Task.Delay(100);
                            if (Environment.GetEnvironmentVariable("MO2_SCREENSHOT") is { } screenshot) {
                                // Capture the root: rendering a translated page alone
                                // retains ancestor offsets and clips the evidence.
                                using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.Bounds.Width), (int)Math.Ceiling(window.Bounds.Height)));
                                bitmap.Render(window);
                                bitmap.Save(Path.ChangeExtension(screenshot, $"categories-340x{height:0}.png"));
                            }
                            Console.WriteLine($"PASS narrow category filters: 340x{height:0}, filters above {results.Bounds.Height:0}px results; footer controls reachable");
                        }
                    } finally { page.Width = oldWidth; page.Height = oldHeight; }
                    await Wait(() => page.Bounds.Width > 340, "restore Mods panel width");
                    menu.ShowAt(more);
                    Click(Item("ModsCategoriesMenuItem"));
                    await Wait(() => !page.GetVisualDescendants().OfType<Border>().Any(x => x.Name == "ModCategoriesGroup" && x.IsEffectivelyVisible),
                        "category filters close from overflow");
                    menu.ShowAt(more);
                    Click(Item("ModsGroupingMenu").Items.OfType<MenuItem>().ElementAt(1));
                    await Wait(() => adapter.Grouping == 1, "category grouping from overflow");
                    menu.ShowAt(more);
                    Click(Item("ModsGroupingMenu").Items.OfType<MenuItem>().ElementAt(originalGrouping));
                    await Wait(() => adapter.Grouping == originalGrouping, "restore grouping");
                    Console.WriteLine("PASS Mods overflow: category pane opens/closes and grouping changes/restores through menu actions");
                } finally { menu.Hide(); adapter.SetGrouping(originalGrouping); }
            }
            await Wait(() => page.GetVisualDescendants().OfType<Mo2ToolbarSearch>().Any(), "toolbar attached after navigation");
            var search = page.GetVisualDescendants().OfType<Mo2ToolbarSearch>().Single();
            await Wait(() => search.GetVisualDescendants().OfType<TextBox>().Any() &&
                search.GetVisualDescendants().OfType<Button>().Any(), "search controls attached");
            var box = search.GetVisualDescendants().OfType<TextBox>().Single();
            var button = search.GetVisualDescendants().OfType<Button>().First();
            var accessibleName = Avalonia.Automation.AutomationProperties.GetName(box);
            if (string.IsNullOrWhiteSpace(accessibleName) || accessibleName == "Search" ||
                Avalonia.Automation.AutomationProperties.GetName(button) != accessibleName)
                throw new Exception("Search field/button lack a matching page-specific accessible name");
            int RowCount() => page is Mo2ToolsView
                ? page.GetVisualDescendants().OfType<Button>().Count(x => x.Name == "LaunchToolButton")
                : page.GetVisualDescendants().OfType<TreeDataGrid>().Single().Rows?.Count ?? 0;
            await Wait(() => RowCount() > 0, "populated rows after menu changes");
            await Task.Delay(150);
            var count = RowCount();
            if (count == 0) throw new Exception("Toolbar search requires populated lists");
            void Key(Key key, KeyModifiers modifiers = KeyModifiers.None) {
                var focused = window.FocusManager?.GetFocusedElement() as InputElement
                    ?? throw new Exception("Search lost keyboard focus");
                var args = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
                focused.RaiseEvent(args);
                if (!args.Handled) throw new Exception("Search shortcut was not handled");
            }
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => box.IsEffectivelyVisible && box.IsFocused, "open and focus " + search.Name);
            try {
                if (Avalonia.Automation.AutomationProperties.GetHelpText(button)?.StartsWith("Expanded") != true)
                    throw new Exception("Expanded search state not exposed to accessibility");
                var at = search.TranslatePoint(default, page)!.Value;
                if (at.X < 0 || at.X + search.Bounds.Width > page.Bounds.Width + 1)
                    throw new Exception("Expanded search leaves its panel");
                box.Text = "___toolbar_no_such_mod___";
                await Wait(() => RowCount() == 0, "filter actual rows " + search.Name);
                if (page is Mo2ModsView) {
                    var more = page.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "ModsListOptionsButton");
                    var menu = (MenuFlyout)more.Flyout!;
                    await Task.Delay(150);
                    menu.ShowAt(more);
                    try {
                        var clear = menu.Items.OfType<MenuItem>().Single(x => x.Name == "ModsClearFiltersMenuItem");
                        if (!clear.IsEnabled) throw new Exception("Clear all filters is disabled with an active query");
                        clear.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    } finally { menu.Hide(); }
                    await Wait(() => RowCount() == count && string.IsNullOrEmpty(box.Text), "overflow clears search and restores rows");
                    box.Focus();
                    box.Text = "___toolbar_no_such_mod___";
                    await Wait(() => RowCount() == 0 && box.IsFocused, "search remains usable after clear-all");
                    Console.WriteLine("PASS Mods clear filters: overflow clears the query and restores native rows without hiding search");
                }
                var originalWidth = page.Width;
                try {
                    foreach (var width in new[] { 340d, 280d, 240d }) {
                        page.Width = width;
                        await Task.Delay(200);
                        var searchAt = search.TranslatePoint(default, page)!.Value;
                        var boxAt = box.TranslatePoint(default, page)!.Value;
                        if (Math.Abs(page.Bounds.Width - width) > 1)
                            throw new Exception($"{search.Name} requested {width}px but rendered {page.Bounds.Width:F0}px; narrow layout was not exercised");
                        var padding = Mo2PanelChrome.PaddingFor(page.Bounds.Height);
                        if (searchAt.X < padding.Left - 1 || searchAt.X + search.Bounds.Width > page.Bounds.Width - padding.Right + 1 ||
                            box.Bounds.Width < 60 || boxAt.X + box.Bounds.Width > page.Bounds.Width - padding.Right + 1)
                            throw new Exception($"{search.Name} loses panel padding at {width}px: search right {searchAt.X + search.Bounds.Width:F0}, page {page.Bounds.Width:F0}");
                        foreach (var action in search.GetVisualDescendants().OfType<Button>().Where(x => x.IsEffectivelyVisible)) {
                            var actionAt = action.TranslatePoint(default, page)!.Value;
                            if (actionAt.X < padding.Left - 1 || actionAt.X + action.Bounds.Width > page.Bounds.Width - padding.Right + 1)
                                throw new Exception(action.Name + " extends into panel padding at " + width + "px");
                        }
                        foreach (var pill in page.GetVisualDescendants().OfType<Border>().Where(x => x.Name?.EndsWith("ToolbarActions") == true)) {
                            var pillAt = pill.TranslatePoint(default, page)!.Value;
                            if (pillAt.X < padding.Left - 1 || pillAt.X + pill.Bounds.Width > page.Bounds.Width - padding.Right + 1)
                                throw new Exception(pill.Name + " clips at " + width + "px");
                        }
                        if (page is Mo2OverwriteView && Environment.GetEnvironmentVariable("MO2_SCREENSHOT") is { } shotPath) {
                            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
                            bitmap.Render(window); bitmap.Save(Path.ChangeExtension(shotPath, $"overwrite-search-{width:0}.png"));
                        }
                    }
                    Console.WriteLine("PASS responsive search: " + search.Name + " fits 340/280/240px panels with an active query");
                } finally { page.Width = originalWidth; }
                await Task.Delay(200);
                Key(Avalonia.Input.Key.F, KeyModifiers.Control);
                await Wait(() => !box.IsEffectivelyVisible && button.IsFocused, "collapse and retain focus");
                if (string.IsNullOrEmpty(box.Text)) throw new Exception("Collapsing search discarded the query");
                if (Avalonia.Automation.AutomationProperties.GetHelpText(button)?.StartsWith("Collapsed") != true)
                    throw new Exception("Collapsed search state not exposed to accessibility");
                Key(Avalonia.Input.Key.F, KeyModifiers.Control);
                await Wait(() => box.IsEffectivelyVisible && box.IsFocused, "reopen from keyboard");
                Key(Avalonia.Input.Key.Escape);
                await Wait(() => !box.IsEffectivelyVisible && button.IsFocused && RowCount() == count,
                    "Escape clears, collapses and restores rows");
                Console.WriteLine("PASS toolbar search: " + search.Name + " fits; button opens; list rows filter; Ctrl+F retains query; Escape restores rows and focus");
            } finally { box.Text = ""; }
        }
    }
}
