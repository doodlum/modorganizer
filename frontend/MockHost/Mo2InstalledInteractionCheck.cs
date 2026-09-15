using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Dialog;
using NexusMods.UI.Sdk.Dialog;
using NexusMods.UI.Sdk.Dialog.Enums;

namespace Mo2.Frontend;

internal static class Mo2InstalledInteractionCheck
{
    public static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!ready()) { if (DateTime.UtcNow > until) throw new TimeoutException("Installed interaction check timed out"); await Task.Delay(50); }
        }
        var profile = shell.Profile;
        await Wait(() => profile.IsConnected);
        if (!profile.ProfilePath.Replace('\\','/').EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test") || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT")))
            throw new InvalidOperationException("Use isolated FNV profile and layout");
        var originalMods = profile.Mods.ToArray();
        var originalPlugins = profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        var mods = window.GetVisualDescendants().OfType<Mo2ModsView>().Single();
        var plugins = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single();
        var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!;
        const string separator = "__Drag modal check_separator";
        if (profile.Mods.Any(x => x.Name == separator)) throw new InvalidOperationException("Fixture already exists");
        try {
            foreach (var accept in new[] { false, true }) {
                var pending = mods.ViewModel!.CreateSeparatorDialog(null);
                await Wait(() => desktop.Windows.OfType<DialogWindow>().Any(x => x.IsVisible));
                var dialog = desktop.Windows.OfType<DialogWindow>().Single(x => x.IsVisible);
                if (dialog.Owner != window) throw new InvalidOperationException("Separator modal has no frontend owner");
                ((IDialogStandardContentViewModel)dialog.ViewModel!.ContentViewModel!).InputText = "__Drag modal check";
                dialog.ViewModel.ButtonPressCommand.Execute(accept ? ButtonDefinitionId.Accept : ButtonDefinitionId.Cancel);
                await pending;
                if (profile.Mods.Any(x => x.Name == separator) != accept) throw new InvalidOperationException("Separator accept/cancel did not match native state");
            }
            Console.WriteLine("PASS frontend separator modal: owner, Cancel preserves state, Create adds native separator");
            Console.WriteLine("Workspace views: " + string.Join(";", window.GetVisualDescendants().OfType<NexusMods.App.UI.WorkspaceSystem.WorkspaceView>().Select(v => $"bounds={v.Bounds} visible={v.IsVisible} panels={v.GetVisualDescendants().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().Count()}")));
            Console.WriteLine("Model panels: " + string.Join(";", shell.WorkspaceController.ActiveWorkspace.Panels.Select(p => $"actual={p.ActualBounds} logical={p.LogicalBounds}")));

            await Wait(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any(v => v.Bounds.Width > 0));
            mods = window.GetVisualDescendants().OfType<Mo2ModsView>().Single(v => v.Bounds.Width > 0);
            plugins = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single(v => v.Bounds.Width > 0);
            var table = mods.NativeView.FindControl<TreeDataGrid>("TreeDataGrid")!;
            var adapter = (Mo2ModsAdapter)mods.ViewModel!.Adapter;
            var scroll = mods.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "ModsRailScrollBar");
            scroll.Value = scroll.Maximum;
            await Task.Delay(250);
            var moving = originalMods.Where(x => x.CanManage && !x.IsSeparator).Take(2).ToArray();
            if (moving.Length != 2) throw new InvalidOperationException("Two managed test mods required");
            var target = profile.Mods.Single(x => x.Name == separator);
            await Wait(() => {
                scroll.Value = scroll.Maximum;
                return table.GetVisualDescendants().OfType<TreeDataGridRow>().Any(r => r.RowIndex >= 0 && r.RowIndex < table.Rows!.Count && table.Rows[r.RowIndex].Model is CompositeItemModel<NexusMods.MnemonicDB.Abstractions.EntityId> m && m.Key == target.Id);
            });
            var targetRow = table.GetVisualDescendants().OfType<TreeDataGridRow>().Single(r => r.RowIndex >= 0 && r.RowIndex < table.Rows!.Count && table.Rows[r.RowIndex].Model is CompositeItemModel<NexusMods.MnemonicDB.Abstractions.EntityId> m && m.Key == target.Id);
            var selectedModels = table.Rows!.Select(r => r.Model).OfType<CompositeItemModel<NexusMods.MnemonicDB.Abstractions.EntityId>>().Where(m => moving.Any(x => x.Id == m.Key)).ToArray();
            var indices = selectedModels.Select(m => new IndexPath(table.Rows!.ToList().FindIndex(r => ReferenceEquals(r.Model, m))));
            var start = new TreeDataGridRowDragStartedEventArgs(selectedModels);
            adapter.OnRowDragStarted(table, start);
            if (!table.AutoDragDropRows || start.AllowedEffects != DragDropEffects.Move) throw new InvalidOperationException("Mod drag not enabled");
            var data = new DataObject(); data.Set("TreeDataGridDragInfo", new DragInfo(table.Source!, indices));
            var inner = new DragEventArgs(DragDrop.DropEvent, data, targetRow, new Point(4,targetRow.Bounds.Height - 2), KeyModifiers.None);
            var drop = new TreeDataGridRowDragEventArgs(DragDrop.DropEvent, targetRow, inner) { Position = TreeDataGridRowDropPosition.After };
            adapter.OnRowDrop(table, drop);
            await Wait(() => profile.Mods.Single(x => x.Name == separator).Priority < profile.FindMod(moving[0].Id)!.Priority && profile.FindMod(moving[0].Id)!.Priority < profile.FindMod(moving[1].Id)!.Priority);
            foreach (var mod in moving.OrderBy(x => x.Priority)) await profile.MoveMod(mod.Id, mod.Priority, absolute:true);
            Console.WriteLine("PASS multi-mod row-drop handler updates native priorities in selection order and restores them");

            var pluginTable = plugins.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            var pluginAdapter = plugins.ViewModel!.Adapter;
            var pluginScroll = plugins.GetVisualDescendants().OfType<ScrollBar>().Single(x => x.Name == "PluginRailScrollBar");
            pluginScroll.Value = 0;
            var movable = profile.Order.Plugins.Where(x => x.CanMove).Take(3).ToArray();
            var pluginTargetIndex = Enumerable.Range(0, pluginTable.Rows!.Count).Single(i => pluginTable.Rows[i].Model is CompositeItemModel<NexusMods.Abstractions.Games.ISortItemKey> m && m.Key.Equals(movable[2].Key));
            pluginScroll.Value = Math.Max(0, (pluginTargetIndex - 1) * 44);
            await Wait(() => pluginTable.GetVisualDescendants().OfType<TreeDataGridRow>().Any(r => r.RowIndex >= 0 && r.RowIndex < pluginTable.Rows.Count && pluginTable.Rows[r.RowIndex].Model is CompositeItemModel<NexusMods.Abstractions.Games.ISortItemKey> m && m.Key.Equals(movable[2].Key)));
            var pluginTarget = pluginTable.GetVisualDescendants().OfType<TreeDataGridRow>().Single(r => r.RowIndex >= 0 && r.RowIndex < pluginTable.Rows!.Count && pluginTable.Rows[r.RowIndex].Model is CompositeItemModel<NexusMods.Abstractions.Games.ISortItemKey> m && m.Key.Equals(movable[2].Key));
            var pluginIndices = movable.Take(2).Select(p => pluginTable.Rows!.RowIndexToModelIndex(Enumerable.Range(0, pluginTable.Rows.Count).Single(i => pluginTable.Rows[i].Model is CompositeItemModel<NexusMods.Abstractions.Games.ISortItemKey> m && m.Key.Equals(p.Key)))).ToArray();
            var pluginData = new DataObject(); pluginData.Set("TreeDataGridDragInfo", new DragInfo(pluginTable.Source!, pluginIndices));
            var pluginInner = new DragEventArgs(DragDrop.DropEvent, pluginData, pluginTarget, new Point(4,pluginTarget.Bounds.Height - 2), KeyModifiers.None);
            var pluginDrop = new TreeDataGridRowDragEventArgs(DragDrop.DropEvent, pluginTarget, pluginInner) { Position = TreeDataGridRowDropPosition.After };
            if (!pluginTable.AutoDragDropRows) throw new InvalidOperationException("Plugin drag not enabled");
            pluginAdapter.OnRowDrop(pluginTable, pluginDrop);
            await Wait(() => profile.Order.FindPlugin(movable[0].Key)!.SortIndex > profile.Order.FindPlugin(movable[2].Key)!.SortIndex);
            await profile.Order.MoveItems(default, movable.Take(2).Select(x => x.Key).ToArray(), movable[2].Key, NexusMods.Abstractions.Games.TargetRelativePosition.BeforeTarget);
            await Wait(() => originalPlugins.SequenceEqual(profile.Order.Plugins.Select(x => (x.DisplayName,x.SortIndex,x.IsActive))));
            Console.WriteLine("PASS multi-plugin row-drop handler updates native load order and restores it");
            foreach (var view in new Control[] { mods, plugins }) {
                var grip = view.GetVisualDescendants().OfType<Border>().First(x => x.Name is "ModDragHandle" or "PluginDragHandle");
                var row = grip.GetVisualAncestors().OfType<TreeDataGridRow>().First();
                if (row.ContextMenu is not { } menu) throw new InvalidOperationException("Row context actions missing");
                row.RaiseEvent(new ContextRequestedEventArgs()); await Task.Delay(150);
                if (menu.Items.Count < 3) throw new InvalidOperationException("Opened row context actions missing");
                menu.Close();
            }
            Console.WriteLine("PASS both lists use non-button drag grips and row context menus");
            var modSearch = mods.NativeView.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!;
            if (!modSearch.IsSearchVisible) modSearch.ToggleSearchPanelVisibility();
            modSearch.FindControl<TextBox>("SearchTextBox")!.Text = "MCM Author Examples";
            await Wait(() => table.Rows!.Count == 1);
            var named = originalMods.Single(m => m.Name == "MCM Author Examples");
            var activation = mods.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "ModActivationToggle" && Equals(b.Tag, named.Name));
            activation.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Wait(() => (profile.FindMod(named.Id)!.State & 2) != (named.State & 2));
            await profile.ToggleMod(named.Id);
            await Wait(() => profile.FindMod(named.Id)!.State == named.State);
            modSearch.ClearSearch();
            await Wait(() => table.Rows!.Count >= originalMods.Count(m => !m.IsOverwrite));
            var pluginSearch = plugins.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Search.SearchControl>().Single();
            if (!pluginSearch.IsSearchVisible) pluginSearch.ToggleSearchPanelVisibility();
            pluginSearch.FindControl<TextBox>("SearchTextBox")!.Text = "The Mod Configuration Menu.esp";
            await Wait(() => pluginTable.Rows!.Count == 1);
            var selectedPlugin = profile.Order.Plugins.Single(p => p.DisplayName == "The Mod Configuration Menu.esp");
            var wasActive = selectedPlugin.IsActive;
            plugins.GetVisualDescendants().OfType<ToggleButton>().Single(b => b.Name == "PluginActivationToggle").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Wait(() => profile.Order.FindPlugin(selectedPlugin.Key)!.IsActive != wasActive);
            await profile.SetPluginsActive([selectedPlugin.DisplayName], wasActive);
            pluginSearch.ClearSearch();
            await Wait(() => pluginTable.Rows!.Count == originalPlugins.Length);
            if (mods.GetVisualDescendants().OfType<Control>().Any(x => x.Name?.EndsWith("ColumnFilter") == true)) throw new InvalidOperationException("Removed filter bar is still present");
            Console.WriteLine("PASS matching Mods/Plugins redesign: expandable search, plain activation checkboxes, no filter row, native states restored");
            if (Environment.GetEnvironmentVariable("MO2_SKIP_RESIZE_CHECK") == "1") return;
            var width = window.Width; var height = window.Height; var windowState = window.WindowState;
            window.WindowState = WindowState.Normal;
            await Task.Delay(300);
            var headers = new[] { mods.NativeView.FindControl<NexusMods.App.UI.Controls.PageHeader.PageHeader>("AllPageHeader")!, plugins.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.PageHeader.PageHeader>().Single() };
            bool Descriptions(bool visible) => headers.All(h => h.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DescriptionTextBlock").IsVisible == visible);
            try {
                scroll.Value = 0; pluginScroll.Value = 0;
                await Task.Delay(150);
                window.Width = 1600;
                await Wait(() => Descriptions(true));
                window.Width = 1280;
                scroll.Value = 0; pluginScroll.Value = 0;
                await Wait(() => Descriptions(true) && headers.All(h => h.IsVisible));
                window.Height = 520;
                await Wait(() => scroll.Maximum > 80 && pluginScroll.Maximum > 80);
                for (var line = 1; line <= 4; line++) {
                    scroll.Value += 40; pluginScroll.Value += 40;
                    var expectedSize = 48 - line * 5;
                    await Wait(() => headers.All(h => Math.Abs(h.GetVisualDescendants().OfType<NexusMods.UI.Sdk.Icons.UnifiedIcon>().Single(i => i.Name == "Icon").Size - expectedSize) < .1));
                    if (line < 4 && headers.Any(h => h.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "DescriptionTextBlock") is not { IsVisible: true, Opacity: > 0 } text || !double.IsPositiveInfinity(text.MaxHeight)))
                        throw new InvalidOperationException("Visible descriptions must not be clipped during the four-line fade");
                }
                await Wait(() => Descriptions(false) && headers.All(h => h.IsVisible));
                Console.WriteLine("PASS headers collapse over exactly four scroll lines, with no clipping of visible descriptions");
                scroll.Value = 0; pluginScroll.Value = 0;
                await Wait(() => Descriptions(true));
                window.Height = 650;
                window.Width = 700;
                await Task.Delay(400);
                Console.WriteLine($"Narrow bounds: window={window.Bounds} mods={mods.Bounds} plugins={plugins.Bounds}");
                await Wait(() => new Control[] { mods, plugins }.All(v => v.GetVisualDescendants().OfType<Grid>().Where(g => g.Name is "ModRedesignRow" or "PluginRedesignRow").All(g => g.ColumnDefinitions[3].Width.Value == 0)));
                window.MinHeight = 240; window.Height = 320;
                await Wait(() => headers.All(h => !h.IsVisible));
                if (window.GetVisualDescendants().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().Where(p => p.IsEffectivelyVisible).Any(p => p.FindControl<Control>("TabHeaderBorder")?.IsVisible != true))
                    throw new InvalidOperationException("Hidden page headers must retain tab bars");
                window.Height = height; window.Width = width;
                await Wait(() => headers.All(h => h.IsVisible));
                Console.WriteLine("PASS responsive headers: full, compact, hidden, restored; secondary columns hide in narrow panels");
            } finally { window.Width = width; window.Height = height; window.WindowState = windowState; }

        } finally {
            foreach (var mod in originalMods.Where(x => x.CanManage && !x.IsSeparator))
                if (profile.FindMod(mod.Id) is { } current && (current.State & 2) != (mod.State & 2)) await profile.ToggleMod(mod.Id);
            foreach (var mod in originalMods.Where(x => x.CanManage).OrderBy(x => x.Priority))
                if (profile.FindMod(mod.Id)?.Priority != mod.Priority) await profile.MoveMod(mod.Id, mod.Priority, true);
            foreach (var plugin in originalPlugins.OrderBy(x => x.SortIndex)) {
                var current = profile.Order.Plugins.SingleOrDefault(x => x.DisplayName == plugin.DisplayName);
                if (current is not null && current.IsActive != plugin.IsActive) await profile.SetPluginsActive([current.DisplayName], plugin.IsActive);
                if (current is not null && current.SortIndex != plugin.SortIndex) await profile.Order.MoveItemDelta(default, current.Key, plugin.SortIndex-current.SortIndex);
            }
            if (profile.Mods.Any(x => x.Name == separator)) await profile.RemoveCollection(separator, profile.CurrentTarget);
        }
    }
}
