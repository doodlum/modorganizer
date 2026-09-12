using System.Collections.ObjectModel;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.LeftMenu.Items;
using NexusMods.App.UI.LeftMenu.Loadout;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// Native NMA navigation with pages backed by the connected MO2 profile.
internal sealed class Mo2LoadoutMenu : AViewModel<ILoadoutLeftMenuViewModel>, ILoadoutLeftMenuViewModel
{
    public WorkspaceId WorkspaceId { get; }
    public ReadOnlyObservableCollection<ILeftMenuItemViewModel> LeftMenuCollectionItems { get; } = new(new());
    public bool HasSingleCollection => false;
    public IApplyControlViewModel ApplyControlViewModel => null!;
    public ILeftMenuItemViewModel LeftMenuItemNewCollection => null!;
    public ILeftMenuItemViewModel LeftMenuItemHealthCheck { get; }
    public ILeftMenuItemViewModel LeftMenuItemLibrary { get; }
    public ILeftMenuItemViewModel LeftMenuItemLoadout { get; }
    public ILeftMenuItemViewModel? LeftMenuItemExternalChanges { get; }
    public Mo2LoadoutMenu(IWorkspaceController controller, WorkspaceId workspace, PageData mods, PageData plugins, PageData downloads, PageData health)
    {
        WorkspaceId = workspace;
        ILeftMenuItemViewModel Item(string title, IconValue icon, PageData page) =>
            new LeftMenuItemViewModel(controller, workspace, page) { Text = new StringComponent(title), Icon = icon };
        LeftMenuItemLibrary = Item("Downloads", IconValues.LibraryOutline, downloads);
        LeftMenuItemLoadout = Item("My Mods", IconValues.FormatAlignJustify, mods);
        LeftMenuItemHealthCheck = Item("Health Check", IconValues.Cardiology, health);
        LeftMenuItemExternalChanges = Item("Plugins", IconValues.Package, plugins);
    }
}
