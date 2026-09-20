using Avalonia.Controls;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;
using System.Reactive.Threading.Tasks;
using NexusMods.App.UI.Controls;
using NexusMods.Abstractions.Games;
namespace Mo2.Frontend;

internal static class Mo2GameSwitchCheck
{
    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        async Task Wait(Func<bool> ready, string message) {
            var until = DateTime.UtcNow.AddSeconds(110);
            while (!ready()) { if (DateTime.UtcNow > until) throw new Exception(message); await Task.Delay(50); }
        }
        await Wait(() => live.Profile.IsConnected && live.CatalogEntries.Any(x => x.Registration.Endpoint == live.Profile.CurrentTarget.Endpoint), "Initial host unavailable");
        using var turn = await Mo2CheckTurn.Take();
        var original = live.Profile.CurrentTarget;
        var originalGame = live.CatalogEntries.Single(x => x.Registration.Endpoint == original.Endpoint).Instance!.Game;
        if (!originalGame.Contains("Vegas")) throw new Exception("Switch check must start on FNV");
        var runningSkyrim = live.CatalogEntries.Where(x => x.Instance?.Game == "Skyrim Special Edition" && Mo2HostStartup.IsRunning(x.Registration)).ToArray();
        if (runningSkyrim.Length != 1) throw new Exception("Switch check requires one already-running Skyrim instance");
        var expectedSkyrim = runningSkyrim[0].Registration.Endpoint;
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        async Task Click(string name) => await spine.LoadoutSpineItems.Single(x => x.Name == name).Click.Execute().ToTask();
        async Task CheckLauncher() {
            var target = live.Profile.CurrentTarget;
            var launcher = live.LaunchPanel ?? throw new Exception("Game workspace has no launcher");
            await Wait(() => live.Profile.CanChangeOriginalUi && launcher.IsEffectivelyVisible,
                "Game launcher did not become available");
            var expected = new[] { Mo2LaunchPanel.EditEntry }.Concat(live.Profile.Executables).ToArray();
            if (!(launcher.Executable.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(expected))
                throw new Exception("Launcher choices do not match the selected host");
            if (launcher.Model.SelectedExecutable != live.Profile.SelectedExecutable ||
                !Equals(launcher.Executable.SelectedItem, live.Profile.SelectedExecutable))
                throw new Exception("Game switch did not select the native host's current executable");
            if (!launcher.Model.CanLaunch || !launcher.NativeButton.IsEnabled)
                throw new Exception("Connected game's PLAY button is unavailable");
            var previous = launcher.Model.SelectedExecutable;
            var alternate = live.Profile.Executables.FirstOrDefault(x => x != previous);
            if (alternate is null) throw new Exception("Launcher check needs two native executables");
            try {
                launcher.Executable.SelectedItem = alternate;
                await live.Profile.Refresh();
                if (target != live.Profile.CurrentTarget || launcher.Model.SelectedExecutable != alternate ||
                    !Equals(launcher.Executable.SelectedItem, alternate))
                    throw new Exception("Unchanged native snapshot overwrote the local executable choice");
            } finally { launcher.Executable.SelectedItem = previous; }
            Console.WriteLine($"PASS game launcher: {live.Profile.GameName}, native executable choices/selection and PLAY availability; local choice survives refresh and is restored");
        }
        async Task CheckData() {
            var target = live.Profile.CurrentTarget;
            Console.WriteLine($"Checking Data: {live.Profile.GameName}, {target.Endpoint}");
            var native = await live.Profile.ReadDataDirectory(target, "");
            var expected = native.Where(x => x.MatchesVisibility(false, true, false)).Select(x => x.Name).ToHashSet();
            await live.ProfileMenu.DataItem!.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)).ToTask();
            await Wait(() => {
                var view = window.GetVisualDescendants().OfType<Mo2DataView>().FirstOrDefault(x => x.IsEffectivelyVisible);
                var rows = view?.GetVisualDescendants().OfType<TreeDataGrid>().SingleOrDefault()?.Source?.Items.OfType<Mo2DataEntry>();
                return rows is not null && rows.Select(x => x.Name).ToHashSet().SetEquals(expected);
            }, "Rendered Data does not match the selected game's native tree");
            if (target != live.Profile.CurrentTarget) throw new Exception("Host changed during Data comparison");
        }
        async Task CheckTools() {
            var target = live.Profile.CurrentTarget;
            var native = await live.Profile.ReadTools();
            var expected = live.Profile.Executables.Select(name => (Name: "Open " + name, Enabled: true))
                .Concat(native.Select(tool => (Name: "Open " + tool.Name, Enabled: tool.Enabled)))
                .OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Enabled).ToArray();
            await live.ProfileMenu.ToolsItem.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)).ToTask();
            Mo2ToolsView? view = null;
            TextBox? searchBox = null;
            await Wait(() => (view = window.GetVisualDescendants().OfType<Mo2ToolsView>().FirstOrDefault(x => x.IsEffectivelyVisible)) is not null &&
                !view.IsReading && (searchBox = view.GetVisualDescendants().OfType<TextBox>().FirstOrDefault(x => x.Name == "ToolsSearch")) is not null,
                "Tools page controls did not finish attaching after game switch");
            var toolsView = view!;
            var search = searchBox!;
            var originalSearch = search.Text;
            try {
                search.Text = "";
                await Wait(() => toolsView.GetVisualDescendants().OfType<Button>().Where(x => x.Name == "LaunchToolButton")
                    .Select(button => (Name: ToolTip.GetTip(button) as string ?? "", Enabled: button.IsEnabled))
                    .OrderBy(x => x.Name, StringComparer.Ordinal).ThenBy(x => x.Enabled).SequenceEqual(expected),
                    "Tools entries or availability do not match the selected native host");
                if (target != live.Profile.CurrentTarget) throw new Exception("Host changed during Tools comparison");
                Console.WriteLine($"PASS game tools: {live.Profile.GameName}, {live.Profile.Executables.Count} programs and {native.Length} native extension tools");
            } finally { search.Text = originalSearch; }
            await Mo2ToolbarSearchCheck.Run(window, toolsView);
            foreach (var panel in live.WorkspaceController.ActiveWorkspace.Panels)
            foreach (var tab in panel.Tabs)
                if (tab.Header.Title != tab.Contents.ViewModel.TabTitle)
                    throw new Exception("Game switch retained a stale page caption");
            await CheckLauncher();
        }
        async Task CheckModsAndPlugins() {
            var target = live.Profile.CurrentTarget;
            Console.WriteLine($"Checking Mods/Plugins: {live.Profile.GameName}, {target.Endpoint}");
            await Mo2NativeFilterUiCheck.Run(live, window);
            var expected = live.Profile.Order.Plugins.Select(x => x.Key).ToHashSet();
            var knownNames = live.Profile.Order.Plugins.Select(x => x.DisplayName).ToHashSet();
            await live.ProfileMenu.LeftMenuItemExternalChanges!.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)).ToTask();
            await Wait(() => {
                var view = window.GetVisualDescendants().OfType<Mo2PluginsView>().FirstOrDefault(x => x.IsEffectivelyVisible && ReferenceEquals(x.DataContext, live.PluginsPage));
                var table = view?.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
                var rows = table?.Source?.Items.OfType<CompositeItemModel<ISortItemKey>>();
                return rows is not null && rows.Select(x => x.Key).ToHashSet().SetEquals(expected) &&
                    Mo2PluginRenderCheck.HasReadableRows(table!, knownNames);
            }, "Rendered Plugins do not match the selected game's native plugin identities");
            if (target != live.Profile.CurrentTarget) throw new Exception("Host changed during Mods/Plugins comparison");
            Console.WriteLine($"PASS game panels: {live.Profile.GameName}, all mod identities, native filters, {expected.Count} plugin identities");
        }
        try {
            await CheckModsAndPlugins();
            await CheckTools();
            await Click("Skyrim Special Edition");
            await Wait(() => live.Profile.IsConnected && live.Profile.GameName.Contains("Skyrim") && live.Profile.CurrentTarget.Endpoint != original.Endpoint,
                "Skyrim game icon did not connect to its host");
            if (live.Profile.CurrentTarget.Endpoint != expectedSkyrim) throw new Exception("Sidebar bypassed the running Skyrim host for another installation");
            await CheckData();
            await CheckModsAndPlugins();
            await CheckTools();
            await Click(originalGame);
            await Wait(() => live.Profile.IsConnected && live.Profile.CurrentTarget == original, "FNV game icon did not restore the original profile");
            await CheckData();
            await CheckModsAndPlugins();
            await CheckTools();
            Console.WriteLine("PASS game switch: sidebar FNV → Skyrim → FNV; Mods/native filters, Plugins, Data and Tools match each host and original FNV profile is restored");
        } finally {
            if (live.Profile.CurrentTarget != original) {
                await Click(originalGame);
                await Wait(() => live.Profile.IsConnected && live.Profile.CurrentTarget == original, "Could not restore original FNV profile after switch check");
            }
        }
    }
}
