using Avalonia;
using Avalonia.Input;
using DynamicData;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.MnemonicDB.Abstractions;
using System.Reactive.Linq;

namespace Mo2.Frontend;

internal static class Mo2ProfileBufferCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT")))
            throw new Exception("Use an isolated layout for profile-buffer checks");
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(25);
            while (!ready()) { if (DateTime.UtcNow > until) throw new Exception("Profile presentation did not settle"); await Task.Delay(50); }
        }
        var profile = shell.Profile;
        await Wait(() => profile.IsConnected && !profile.SelectingProfile && profile.ProfilePath.Length > 0 &&
            shell.CatalogEntries.Any(e => e.Registration.Endpoint == profile.Endpoint && e.Instance is not null));
        var original = shell.CatalogEntries.Single(e => e.Registration.Endpoint == profile.Endpoint);
        var selected = original.Instance!.Profiles.Single(p => Path.GetFullPath(p.Directory) == Mo2InstanceCatalog.LocalPath(profile.ProfilePath));
        var target = profile.CurrentTarget;
        using var retained = profile.ObserveFilteredMods(Observable.Return<Func<Mo2LiveMod, bool>>(_ => true), target).AsObservableCache();
        var retainedBefore = retained.Items.ToDictionary(model => model.Key);
        var retainedUpdates = 0;
        using var observeRetained = retained.Connect().Subscribe(_ => retainedUpdates++);
        using var retainedPlugins = new ScenarioOrderProvider(profile).ObserveLoadOrder(profile.Order, default,
            R3.Observable.Return(System.ComponentModel.ListSortDirection.Ascending)).AsObservableCache();
        var pluginModelsBefore = retainedPlugins.Items.ToDictionary(model => model.Key);
        var pluginUpdates = 0;
        using var observeRetainedPlugins = retainedPlugins.Connect().Subscribe(_ => pluginUpdates++);
        var nativeMods = profile.Mods.Select(m => (m.Name, m.Priority, Active: m.State & 6)).OrderBy(m => m.Name).ToArray();
        var nativePlugins = profile.Order.Plugins.Select(p => (p.DisplayName, p.SortIndex, p.IsActive)).OrderBy(p => p.DisplayName).ToArray();

        async Task<FlatTreeDataGridSource<CompositeItemModel<EntityId>>> CurrentModels()
        {
            if (!window.GetVisualDescendants().OfType<Mo2ModsView>().Any(v => v.IsEffectivelyVisible))
                await shell.ProfileMenu.LeftMenuItemLoadout.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
            FlatTreeDataGridSource<CompositeItemModel<EntityId>>? result = null;
            await Wait(() => {
                var view = window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault(v => v.IsEffectivelyVisible);
                if (view?.ViewModel?.Adapter.Source.Value is not FlatTreeDataGridSource<CompositeItemModel<EntityId>> source) return false;
                var models = source.Items.Select(m => m).ToArray();
                var expected = profile.Mods.Where(m => !m.IsOverwrite).ToDictionary(m => m.Id);
                if (!models.Select(m => m.Key).ToHashSet().SetEquals(expected.Keys)) return false;
                if (models.Any(m => m.Get<ValueComponent<int>>(Mo2ModsAdapter.StateKey).Value.Value != expected[m.Key].State ||
                    m.Get<ValueComponent<int>>(Mo2ModsAdapter.PriorityKey).Value.Value != expected[m.Key].Priority)) return false;
                result = source; return true;
            });
            return result!;
        }

        try {
            var source = await CurrentModels();
            var originalBody = window.GetVisualDescendants().OfType<Mo2ModsView>().Single(v => v.IsEffectivelyVisible);
            source.RowSelection!.Clear(); source.RowSelection.Select(new IndexPath(0));
            var picked = source.RowSelection.SelectedItem;
            if (!await profile.SelectProfile(original.Registration, selected)) throw new Exception("Same-profile selection failed");
            if (!ReferenceEquals(source, await CurrentModels()) || !ReferenceEquals(source.RowSelection.SelectedItem, picked))
                throw new Exception("Same-profile refresh replaced selection or its source");
            var missing = selected with { Directory = Path.Combine(selected.Directory, "__missing_buffer_check_profile__") };
            if (Directory.Exists(missing.Directory)) throw new Exception("Missing-profile test path unexpectedly exists");
            if (await profile.SelectProfile(original.Registration, missing)) throw new Exception("Unowned profile selection was accepted");
            if (profile.CurrentTarget != target || !ReferenceEquals(source, await CurrentModels()) ||
                !ReferenceEquals(source.RowSelection.SelectedItem, picked))
                throw new Exception("Rejected selection changed the current source or selection");
            Console.WriteLine("PASS buffered presentation: same-profile refresh and rejected selection preserve model identity and selection");

            var other = shell.CatalogEntries.Where(e => e.Instance is not null && e.Registration.Endpoint != original.Registration.Endpoint &&
                e.Instance.Game == "Skyrim Special Edition").OrderByDescending(e => e.Registration.Launcher is not null).First();
            var otherProfile = other.Instance!.Profiles.Single(p => p.Name == other.Instance.SelectedProfile);
            if (!await profile.SelectProfile(other.Registration, otherProfile)) throw new Exception("Other game did not connect");
            shell.ShowProfile(); await CurrentModels();
            if (retainedUpdates != 1 || retained.Count != retainedBefore.Count ||
                retained.Items.Any(model => !retainedBefore.TryGetValue(model.Key, out var before) || !ReferenceEquals(model, before)))
                throw new Exception("Inactive profile presentation consumed another game's snapshot");
            Console.WriteLine("PASS inactive Mods presentation ignores other-game updates and retains model identity while subscribed");
            if (pluginUpdates != 1 || retainedPlugins.Count != pluginModelsBefore.Count ||
                retainedPlugins.Items.Any(model => !pluginModelsBefore.TryGetValue(model.Key, out var before) || !ReferenceEquals(model, before)))
                throw new Exception("Inactive Plugins presentation consumed another game's snapshot");
            Console.WriteLine("PASS inactive Plugins presentation ignores other-game updates and retains model identity while subscribed");
            var otherBody = window.GetVisualDescendants().OfType<Mo2ModsView>().Single(v => v.IsEffectivelyVisible);
            await Wait(() => window.GetVisualDescendants().OfType<Mo2PluginsView>().Any(v => v.IsEffectivelyVisible &&
                v.GetVisualDescendants().OfType<TreeDataGrid>().Any(t => t.Source?.Columns.Count == 1)));
            var otherPlugins = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single(v => v.IsEffectivelyVisible);
            var pluginColumn = otherPlugins.GetVisualDescendants().OfType<TreeDataGrid>().Single().Source!.Columns[0];
            if (!await profile.SelectProfile(original.Registration, selected)) throw new Exception("Original profile did not reconnect");
            shell.ShowProfile(); await CurrentModels();
            if (Environment.GetEnvironmentVariable("MO2_REUSE_PAGE_BODIES") != "0") {
                if (!ReferenceEquals(originalBody, window.GetVisualDescendants().OfType<Mo2ModsView>().Single(v => v.IsEffectivelyVisible)))
                    throw new Exception("Returning to original game rebuilt its page body");
                if (!await profile.SelectProfile(other.Registration, otherProfile)) throw new Exception("Other game did not reconnect");
                shell.ShowProfile(); await CurrentModels();
                if (!ReferenceEquals(otherBody, window.GetVisualDescendants().OfType<Mo2ModsView>().Single(v => v.IsEffectivelyVisible)))
                    throw new Exception("Revisiting other game rebuilt its page body");
                await Wait(() => otherPlugins.GetVisualRoot() == window &&
                    otherPlugins.GetVisualDescendants().OfType<TreeDataGrid>().Single().Source?.Columns.Count == 1);
                if (!ReferenceEquals(pluginColumn, otherPlugins.GetVisualDescendants().OfType<TreeDataGrid>().Single().Source!.Columns[0]))
                    throw new Exception("Revisiting Plugins replaced its existing column");
                Console.WriteLine("PASS Plugins column identity retained across game switches");
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_CACHED_RESIZE") == "1") {
                    var savedState = window.WindowState;
                    var savedWidth = window.Width; var savedHeight = window.Height;
                    try {
                        foreach (var width in new[] { 900, 640, 1280 }) {
                            if (!await profile.SelectProfile(original.Registration, selected)) throw new Exception("Resize source game did not connect");
                            shell.ShowProfile(); await CurrentModels();
                            window.WindowState = WindowState.Normal;
                            await Task.Delay(200);
                            window.Width = width; window.Height = 750;
                            await Wait(() => Math.Abs(window.ClientSize.Width - width) <= 1 && Math.Abs(window.ClientSize.Height - 750) <= 1);
                            if (!await profile.SelectProfile(other.Registration, otherProfile)) throw new Exception("Resized cached game did not connect");
                            shell.ShowProfile(); await CurrentModels();
                            await Wait(() => {
                                var panels = window.GetVisualDescendants().OfType<PanelView>().Where(p => p.IsEffectivelyVisible).ToArray();
                                if (panels.Length != 2) return false;
                                var expectedWidth = ((Grid)window.Content!).ColumnDefinitions[2].ActualWidth - 24;
                                foreach (var panel in panels) {
                                    var canvas = panel.GetVisualAncestors().OfType<Canvas>().First(p => p.Name == "WorkspaceCanvas");
                                    if (Math.Abs(canvas.Bounds.Width - expectedWidth) > 1 ||
                                        Math.Abs(panel.Bounds.Width - panel.ViewModel!.ActualBounds.Width) > 1 ||
                                        panel.ViewModel.ActualBounds.Right > canvas.Bounds.Width + 1) return false;
                                }
                                return true;
                            });
                            Mo2DeferredPresentationCheck.CheckPanelGeometry(window);
                            if (!ReferenceEquals(otherBody, window.GetVisualDescendants().OfType<Mo2ModsView>().Single(v => v.IsEffectivelyVisible)))
                                throw new Exception("Hidden resize rebuilt the cached Mods body");
                            var divider = window.GetVisualDescendants().OfType<PanelResizerView>().Single(v => v.IsEffectivelyVisible);
                            var point = divider.TranslatePoint(new Avalonia.Point(divider.Bounds.Width / 2, divider.Bounds.Height / 2), window)!.Value;
                            var hit = window.InputHitTest(point) as Avalonia.Visual;
                            if (hit != divider && hit?.GetVisualAncestors().Contains(divider) != true)
                                throw new Exception("Resized cached divider cannot receive input");
                        }
                        await Mo2DeferredPresentationCheck.CheckHeaderRestoration(window);
                        Console.WriteLine("PASS cached workspace resize: hidden Skyrim returns at 900/640/1280 widths with retained body, correct panel bounds and reachable divider");
                    } finally { window.Width = savedWidth; window.Height = savedHeight; window.WindowState = savedState; }
                }
                await Mo2FilteredRowsCheck.Run(shell, window);
                if (!await profile.SelectProfile(original.Registration, selected)) throw new Exception("Original profile did not reconnect after reuse check");
                shell.ShowProfile(); await CurrentModels();
                Console.WriteLine("PASS page body reuse: both game bodies retained; revisited Skyrim filters, recycled rows and native highlights verified");
            }
            if (!nativeMods.SequenceEqual(profile.Mods.Select(m => (m.Name, m.Priority, Active: m.State & 6)).OrderBy(m => m.Name)) ||
                !nativePlugins.SequenceEqual(profile.Order.Plugins.Select(p => (p.DisplayName, p.SortIndex, p.IsActive)).OrderBy(p => p.DisplayName)))
                throw new Exception("Native activation or order changed across profile switches");
            Console.WriteLine("PASS buffered presentation: FNV/Skyrim/return models match native IDs, state and priority; native activation/order restored unchanged");
        } finally {
            await profile.SelectProfile(original.Registration, selected);
            shell.ShowProfile();
        }
    }
}
