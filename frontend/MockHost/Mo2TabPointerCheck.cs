using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using NexusMods.App.UI.WorkspaceSystem;
using System.Text.Json;
using System.Reactive.Threading.Tasks;
using NexusMods.App.UI.Controls.Navigation;

namespace Mo2.Frontend;

internal static class Mo2TabPointerCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window, string phasePath)
    {
        using var turn = await Mo2CheckTurn.Take();
        async Task Wait(Func<bool> ready, string stage) {
            var deadline = DateTime.UtcNow.AddSeconds(25);
            while (!ready()) {
                if (DateTime.UtcNow > deadline) throw new Exception("Tab pointer: " + stage);
                await Task.Delay(50);
            }
        }
        await Wait(() => shell.Profile.IsConnected && shell.WorkspaceController.ActiveWorkspace.Panels.Count == 1 &&
            shell.WorkspaceController.ActiveWorkspace.Panels[0].Tabs.Count == 2, "two-tab profile layout");
        var workspace = shell.WorkspaceController.ActiveWorkspace;
        var initial = workspace.Panels.Single();
        var moving = initial.Tabs.Single(x => x.Contents.ViewModel is ScenarioLoadOrderPage);
        var retained = initial.Tabs.Single(x => x != moving);
        // Seed a real history in the tab before it changes panels.
        var pluginPage = moving.Contents.PageData;
        initial.SelectTab(moving.Id);
        await shell.ProfileMenu.DataItem.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)).ToTask();
        await Wait(() => moving.Contents.ViewModel is Mo2DataPage, "Data history entry");
        shell.WorkspaceController.OpenPage(workspace.Id, pluginPage,
            new OpenPageBehavior.ReplaceTab(initial.Id, moving.Id), selectTab: true, checkOtherPanels: false);
        await Wait(() => moving.Contents.ViewModel is ScenarioLoadOrderPage, "Plugins history entry");
        var pages = initial.Tabs.ToDictionary(x => x.Id, x => x.Contents.ViewModel);
        async Task CheckHistory(string stage) {
            var owner = workspace.Panels.Single(x => x.Tabs.Contains(moving));
            var retainedPage = retained.Contents.ViewModel;
            await moving.GoBackInHistoryCommand.Execute().ToTask();
            await Wait(() => moving.Contents.ViewModel is Mo2DataPage && moving.Header.Title == "Data",
                stage + " Back did not navigate the moved tab");
            await moving.GoForwardInHistoryCommand.Execute().ToTask();
            await Wait(() => moving.Contents.ViewModel is ScenarioLoadOrderPage && moving.Header.Title == "Plugins",
                stage + " Forward did not navigate the moved tab");
            if (!owner.Tabs.Contains(moving) || moving.Contents.ViewModel.PanelId != owner.Id ||
                moving.Contents.ViewModel.TabId != moving.Id || !ReferenceEquals(retainedPage, retained.Contents.ViewModel))
                throw new Exception("Moved tab history navigated the wrong panel or changed its neighbour");
            pages[moving.Id] = moving.Contents.ViewModel;
            Console.WriteLine("PASS tab history: " + stage + "; Back/Forward target the moved tab and leave its neighbour unchanged");
        }
        PanelView? Face(PanelId id) => window.GetVisualDescendants().OfType<PanelView>()
            .SingleOrDefault(x => x.ViewModel?.Id == id && x.IsEffectivelyVisible);
        PanelTabHeaderView? Header() => window.GetVisualDescendants().OfType<PanelTabHeaderView>()
            .SingleOrDefault(x => x.ViewModel?.Id == moving.Id && x.IsEffectivelyVisible);
        async Task Phase(string name, IPanelViewModel target, bool edge, Func<bool> complete) {
            window.Activate();
            await Wait(() => window.IsActive && Header() is { Bounds.Width: > 0 } && Face(target.Id) is not null,
                name + " visible tab handle and target");
            await Task.Delay(400);
            var header = Header()!;
            var title = header.GetVisualDescendants().OfType<TextBlock>()
                .First(x => x.IsEffectivelyVisible && !x.GetVisualAncestors().OfType<Button>().Any());
            var face = Face(target.Id)!;
            var start = title.PointToScreen(new Point(title.Bounds.Width / 2, title.Bounds.Height / 2));
            var end = face.PointToScreen(new Point(edge ? face.Bounds.Width - 18 : face.Bounds.Width / 2, face.Bounds.Height / 2));
            if (Mo2TabDragDrop.TabAt(window.InputHitTest(window.PointToClient(start)) as Visual)?.Tab != moving.Id)
                throw new Exception("Tab handle coordinates hit a different control");
            var request = JsonSerializer.Serialize(new { Phase = name, Start = new { start.X, start.Y }, End = new { end.X, end.Y } });
            File.WriteAllText(phasePath + ".tmp", request); File.Move(phasePath + ".tmp", phasePath, true);
            await Wait(complete, name + " pointer drop did not change panels");
            await Task.Delay(400);
            await Mo2DeferredPresentationCheck.CheckPanelGeometry(window);
            if (workspace.Panels.SelectMany(x => x.Tabs).Count() != pages.Count ||
                workspace.Panels.SelectMany(x => x.Tabs).Any(x => !pages.TryGetValue(x.Id, out var page) || !ReferenceEquals(page, x.Contents.ViewModel)))
                throw new Exception("Tab movement replaced or lost page state");
            Console.WriteLine("PASS tab pointer: " + name + "; physical input moved the tab, retained page instances and settled panel geometry");
        }
        try {
            await Phase("split", initial, true, () => workspace.Panels.Count == 2 &&
                workspace.Panels.Single(x => x.Tabs.Contains(moving)).LogicalBounds.X > 0);
            await CheckHistory("split");
            var destination = workspace.Panels.Single(x => x.Tabs.Contains(retained));
            await Phase("join", destination, false, () => workspace.Panels.Count == 1 && workspace.Panels[0].Tabs.Count == 2);
            await CheckHistory("join");
        } finally { File.Delete(phasePath); }
    }
}
