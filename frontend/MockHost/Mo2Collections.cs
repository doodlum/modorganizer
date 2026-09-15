using System.Reactive;
using System.Collections.ObjectModel;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.Dialog.Enums;
using NexusMods.App.UI.Dialog;
using NexusMods.UI.Sdk.Dialog.Enums;
using NexusMods.App.UI.LeftMenu.Items;
using NexusMods.App.UI.Pages.LoadoutPage;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Dialog;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed record Mo2Collection(string Key, string Title, IReadOnlyList<Mo2LiveMod> Mods);
internal static class Mo2Collections
{
    public const string Ungrouped = "";
    public static IReadOnlyList<Mo2Collection> Build(IEnumerable<Mo2LiveMod> source)
    {
        var result = new List<Mo2Collection>();
        var members = new List<Mo2LiveMod>();
        result.Add(new(Ungrouped, "Ungrouped", members));
        foreach (var mod in source.Where(x => !x.IsOverwrite).OrderBy(x => x.Priority)) {
            if (mod.IsSeparator) {
                members = new();
                result.Add(new(mod.Name, mod.DisplayName, members));
            } else members.Add(mod);
        }
        return result;
    }
    public static async Task Rename(IWindowManager windows, Mo2LiveProfile profile, string key)
    {
        var target = profile.CurrentTarget;
        var collection = Build(profile.Mods).FirstOrDefault(x => x.Key == key);
        if (key.Length == 0 || collection is null) return;
        var result = await windows.ShowDialog(LoadoutDialogs.RenameCollection(collection.Title), DialogWindowType.Modal);
        if (result.ButtonId == ButtonDefinitionId.Accept && !string.IsNullOrWhiteSpace(result.InputText))
            await profile.RenameCollection(key, result.InputText.Trim(), target);
    }
    public static async Task<bool> Remove(IWindowManager windows, Mo2LiveProfile profile, string key)
    {
        var target = profile.CurrentTarget;
        var collection = Build(profile.Mods).FirstOrDefault(x => x.Key == key);
        if (key.Length == 0 || collection is null) return false;
        var dialog = DialogFactory.CreateStandardDialog($"Remove {collection.Title}?", new StandardDialogParameters {
            Text = "Remove this collection’s separator from every MO2 profile in this instance? Mods stay installed and keep their enabled states. They will join the preceding collection, or Ungrouped."
        }, [DialogStandardButtons.Cancel, new DialogButtonDefinition("Remove collection", ButtonDefinitionId.Accept, ButtonAction.Accept, ButtonStyling.Primary)], DialogWindowSize.Small);
        var result = await windows.ShowDialog(dialog, DialogWindowType.Modal);
        return result.ButtonId == ButtonDefinitionId.Accept && await profile.RemoveCollection(key, target);
    }
    public static async Task Create(IWindowManager windows, Mo2LiveProfile profile)
    {
        var target = profile.CurrentTarget;
        var result = await windows.ShowDialog(LoadoutDialogs.CreateCollection(), DialogWindowType.Modal);
        if (result.ButtonId != ButtonDefinitionId.Accept || string.IsNullOrWhiteSpace(result.InputText)) return;
        if (target != profile.CurrentTarget) return;
        await profile.CreateSeparator(null, result.InputText.Trim());
    }
}

internal sealed record Mo2CollectionContext(string Key) : IPageFactoryContext;
internal sealed class Mo2CollectionFactory(IServiceProvider services, IWindowManager windows, Mo2LiveProfile profile, Action openDownloads) : IPageFactory
{
    public static PageFactoryId StaticId { get; } = PageFactoryId.From(Guid.Parse("bcde2778-955d-4b57-a14e-85a878b82114"));
    public PageFactoryId Id => StaticId;
    public DynamicData.Kernel.Optional<OpenPageBehaviorType> DefaultOpenPageBehavior => default;
    public static PageData Data(string key) => new() { FactoryId = StaticId, Context = new Mo2CollectionContext(key) };
    public Page Create(IPageFactoryContext context) => new() {
        PageData = Data(((Mo2CollectionContext)context).Key),
        ViewModel = new ScenarioInstalledPage(services, windows, profile, profile.Order, isCollection: true, openDownloads: openDownloads) {
            CollectionKey = ((Mo2CollectionContext)context).Key,
            CreateCollection = () => Mo2Collections.Create(windows, profile)
        }
    };
    public IEnumerable<PageDiscoveryDetails?> GetDiscoveryDetails(IWorkspaceContext context) => [];

}

internal sealed class Mo2CollectionMenuItem : LeftMenuItemViewModel, ILeftMenuItemWithToggleViewModel
{
    private bool _enabled;
    public bool IsEnabled { get => _enabled; set => this.RaiseAndSetIfChanged(ref _enabled, value); }
    public bool IsToggleVisible { get; private set; }
    public ReactiveCommand<Unit, Unit> ToggleIsEnabledCommand { get; }
    public string Key { get; }
    public Mo2CollectionMenuItem(IWorkspaceController controller, WorkspaceId workspace, Mo2LiveProfile profile, Mo2Collection collection, Func<string,Task<bool>> remove, PageData modsPage)
        : base(controller, workspace, Mo2CollectionFactory.Data(collection.Key))
    {
        Key = collection.Key;
        if (Key.Length > 0) AdditionalContextMenuItems = [new ContextMenuItem {
            Header = "Remove collection…", Icon = IconValues.DeleteOutline, Styling = ContextMenuItemStyling.Critical,
            Command = ReactiveCommand.CreateFromTask(async () => {
                if (await remove(Key)) controller.OpenPage(workspace, modsPage, controller.GetOpenPageBehavior(modsPage, NavigationInformation.From(NavigationInput.Default)));
            })
        }];
        Icon = IconValues.CollectionsOutline;
        Text = new StringComponent(collection.Title);
        ToggleIsEnabledCommand = ReactiveCommand.CreateFromTask(() => profile.SetCollectionEnabled(Key, IsEnabled));
        Update(collection);
    }
    public void Update(Mo2Collection collection)
    {
        var toggleable = collection.Mods.Where(x => (x.State & 4) == 0).ToArray();
        IsEnabled = toggleable.Length > 0 && toggleable.All(x => (x.State & 2) != 0);
        var visible = toggleable.Length > 0;
        if (visible != IsToggleVisible) { IsToggleVisible = visible; this.RaisePropertyChanged(nameof(IsToggleVisible)); }

    }
}

internal sealed class Mo2NewCollectionMenuItem : LeftMenuItemViewModel
{
    public Mo2NewCollectionMenuItem(IWorkspaceController controller, WorkspaceId workspace, PageData page, Func<Task> create)
        : base(controller, workspace, null!)
    {
        Text = new StringComponent("New Collection"); Icon = IconValues.Add;
        NavigateCommand = ReactiveCommand.CreateFromTask<NavigationInformation>(_ => create());
    }
}
