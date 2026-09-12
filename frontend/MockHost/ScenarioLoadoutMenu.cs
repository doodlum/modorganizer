using System.Collections.ObjectModel;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.LeftMenu.Items;
using NexusMods.App.UI.LeftMenu.Loadout;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal sealed class ScenarioLoadoutMenu : AViewModel<ILoadoutLeftMenuViewModel>, ILoadoutLeftMenuViewModel
{
    public WorkspaceId WorkspaceId { get; }
    public ReadOnlyObservableCollection<ILeftMenuItemViewModel> LeftMenuCollectionItems { get; }
    public bool HasSingleCollection => true;
    public IApplyControlViewModel ApplyControlViewModel { get; } = new ApplyControlDesignViewModel();
    public ILeftMenuItemViewModel LeftMenuItemLoadout { get; }
    public ILeftMenuItemViewModel LeftMenuItemLibrary { get; }
    public ILeftMenuItemViewModel LeftMenuItemNewCollection { get; } = Item("New Collection", IconValues.Add);
    public ILeftMenuItemViewModel LeftMenuItemHealthCheck { get; } = Item("Health Check", IconValues.Cardiology);
    public ILeftMenuItemViewModel? LeftMenuItemExternalChanges { get; } = Item("External Changes", IconValues.Folder);
    private static ILeftMenuItemViewModel Item(string text, IconValue icon) => new LeftMenuItemDesignViewModel { Text = new StringComponent(text), Icon = icon };
    public ScenarioLoadoutMenu(IWorkspaceController controller, WorkspaceId workspace, PageData all, PageData collection, PageData library, ScenarioInstalledMods mods)
    {
        WorkspaceId = workspace;
        LeftMenuItemLibrary = new LeftMenuItemViewModel(controller, workspace, library) { Text = new StringComponent("Library"), Icon = IconValues.LibraryOutline };
        LeftMenuCollectionItems = new(new ObservableCollection<ILeftMenuItemViewModel> {
            new LeftMenuItemViewModel(controller, workspace, collection) { Text = new StringComponent(mods.CollectionName.Value, mods.CollectionName, subscribeWhenCreated: true), Icon = IconValues.CollectionsOutline }
        });
        LeftMenuItemLoadout = new LeftMenuItemViewModel(controller, workspace, all) {
            Text = new StringComponent("All"), Icon = IconValues.FormatAlignJustify };
    }
}

internal sealed record ScenarioWorkspaceContext(int Number, string Name) : IWorkspaceContext
{
    public bool IsValid(IServiceProvider services) =>
        services.GetService(typeof(ScenarioData)) is ScenarioData data && data.Section.Loadouts.Any(x => x.Number == Number);
}
