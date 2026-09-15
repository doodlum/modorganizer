using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

internal static class Mo2LayoutPresetCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        var path = Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT");
        if (path is null || !Path.GetFullPath(path).StartsWith(Path.GetTempPath(), StringComparison.Ordinal))
            throw new Exception("Preset verification requires a temporary layout file");
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(20);
            while (!ready()) {
                if (DateTime.UtcNow > until) throw new Exception("MO2 layout preset did not settle");
                await Task.Delay(25);
            }
        }
        await Wait(() => shell.Profile.IsConnected && !shell.Profile.SelectingProfile && shell.Profile.ProfilePath.Length > 0);
        shell.ShowProfile();
        var workspace = shell.WorkspaceController.ActiveWorkspace;
        await Wait(() => window.GetVisualDescendants().OfType<PanelView>().Count(p => p.IsEffectivelyVisible) == workspace.Panels.Count);
        var original = Mo2WorkspaceLayout.Capture(workspace);
        var before = JsonSerializer.Serialize(original);
        var button = window.GetVisualDescendants().OfType<StandardButton>().Single(b => b.Name == "Mo2LayoutPresetButton");
        var menu = (MenuFlyout)button.Flyout!;
        var actions = menu.Items.OfType<MenuItem>().ToArray();
        var applied = false;
        try {
            actions[0].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); applied = true;
            await Wait(() => workspace.Panels.Count == 2 && workspace.Panels.Any(p => p.Tabs.Count == 5));
            var left = workspace.Panels.OrderBy(p => p.LogicalBounds.X).First();
            var right = workspace.Panels.OrderBy(p => p.LogicalBounds.X).Last();
            if (left.Tabs.Count != 1 || left.Tabs[0].Contents.ViewModel is not ScenarioInstalledPage ||
                right.Tabs[0].Contents.ViewModel is not ScenarioLoadOrderPage ||
                right.Tabs[1].Contents.ViewModel is not Mo2DataPage ||
                right.Tabs[2].Contents.ViewModel is not Mo2ArchivesPage ||
                right.Tabs[3].Contents.ViewModel is not Mo2SavesPage ||
                right.Tabs[4].Contents.ViewModel is not Mo2DownloadsPage)
                throw new Exception("MO2 preset page arrangement differs from the intended layout");
            var saved = JsonSerializer.Deserialize<Mo2WorkspaceLayout.Layout>(File.ReadAllText(path))!;
            var persisted = saved.Workspaces.Single(w => w.Endpoint == original.Endpoint && w.ProfilePath == original.ProfilePath);
            if (JsonSerializer.Serialize(persisted) != JsonSerializer.Serialize(Mo2WorkspaceLayout.Capture(workspace)))
                throw new Exception("MO2 preset was not persisted as displayed");
            foreach (var tab in right.Tabs.ToArray()) {
                right.SelectTab(tab.Id);
                await Wait(() => window.GetVisualDescendants().OfType<PanelView>().Any(p => p.IsEffectivelyVisible &&
                    ReferenceEquals(p.ViewModel, right) && p.GetVisualDescendants().OfType<Control>().Any(c =>
                        c.IsEffectivelyVisible && (c is Mo2PluginsView or Mo2DataView or Mo2ArchivesView or Mo2SavesView or Mo2DownloadsView) &&
                        ReferenceEquals(c.DataContext, tab.Contents.ViewModel))));
            }
            actions[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); applied = false;
            await Wait(() => JsonSerializer.Serialize(Mo2WorkspaceLayout.Capture(workspace)) == before);
            saved = JsonSerializer.Deserialize<Mo2WorkspaceLayout.Layout>(File.ReadAllText(path))!;
            persisted = saved.Workspaces.Single(w => w.Endpoint == original.Endpoint && w.ProfilePath == original.ProfilePath);
            if (JsonSerializer.Serialize(persisted) != before) throw new Exception("Undo did not persist the original layout");
            Console.WriteLine("PASS MO2 layout preset: Mods left, five right tabs render, saved layout matches and toolbar Undo restores original panels/tabs/search/selection");
        } finally {
            if (applied) actions[1].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
    }
}
