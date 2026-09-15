using NexusMods.App.UI.Controls;
using NexusMods.App.UI.LeftMenu.Home;
using NexusMods.App.UI.LeftMenu.Items;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

internal sealed class ScenarioHomeMenu : AViewModel<IHomeLeftMenuViewModel>, IHomeLeftMenuViewModel
{
    public WorkspaceId WorkspaceId { get; }
    public ILeftMenuItemViewModel LeftMenuItemMyGames { get; }
    public ILeftMenuItemViewModel LeftMenuItemMyLoadouts { get; }
    // Not part of the native home menu interface: the view inserts it below
    // My Loadouts, so the shared-control gallery sits with the other home pages.
    public ILeftMenuItemViewModel? LeftMenuItemComponents { get; }
    public ScenarioHomeMenu(IWorkspaceController controller, PageData games, PageData loadouts, PageData? components = null)
    {
        WorkspaceId = controller.ActiveWorkspaceId;
        LeftMenuItemMyGames = new LeftMenuItemViewModel(controller, WorkspaceId, games) {
            Text = new StringComponent("My Games"), Icon = IconValues.GamepadOutline };
        LeftMenuItemMyLoadouts = new LeftMenuItemViewModel(controller, WorkspaceId, loadouts) {
            Text = new StringComponent("My Loadouts"), Icon = IconValues.Package };
        if (components is { } page)
            LeftMenuItemComponents = new LeftMenuItemViewModel(controller, WorkspaceId, page) {
                Text = new StringComponent("Components"), Icon = Mo2ComponentsPage.ComponentsIcon };
    }
}
