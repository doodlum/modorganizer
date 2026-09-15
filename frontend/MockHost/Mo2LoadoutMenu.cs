using ReactiveUI;
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
    private readonly ObservableCollection<ILeftMenuItemViewModel> _collections = new();
    public ReadOnlyObservableCollection<ILeftMenuItemViewModel> LeftMenuCollectionItems { get; }
    public ILeftMenuItemViewModel SavesItem { get; }
    public ILeftMenuItemViewModel DataItem { get; }
    public ILeftMenuItemViewModel ArchivesItem { get; }
    public ILeftMenuItemViewModel LogsItem { get; }
    public ILeftMenuItemViewModel OverwriteItem { get; }
    public ILeftMenuItemViewModel ExternalFilesItem { get; }
    public ILeftMenuItemViewModel ToolsItem { get; }
    public ILeftMenuItemViewModel ProfilesItem { get; }
    public bool HasSingleCollection => false;
    public IApplyControlViewModel ApplyControlViewModel => null!;
    public ILeftMenuItemViewModel LeftMenuItemNewCollection { get; }
    public ILeftMenuItemViewModel LeftMenuItemHealthCheck { get; }
    public ILeftMenuItemViewModel LeftMenuItemLibrary { get; }
    public ILeftMenuItemViewModel LeftMenuItemLoadout { get; }
    public ILeftMenuItemViewModel? LeftMenuItemExternalChanges { get; }
    public Mo2LoadoutMenu(IWorkspaceController controller, WorkspaceId workspace, PageData mods, PageData plugins, PageData downloads, PageData health, PageData profiles, PageData tools, PageData overwrite, PageData externalFiles, PageData logs, PageData archives, PageData data, PageData saves, Mo2LiveProfile profile, Func<Task> createCollection, Func<string,Task<bool>> removeCollection)
    {
        WorkspaceId = workspace;
        LeftMenuCollectionItems = new(_collections);
        LeftMenuItemNewCollection = new Mo2NewCollectionMenuItem(controller, workspace, mods, createCollection);
        ILeftMenuItemViewModel Item(string title, IconValue icon, PageData page) =>
            new LeftMenuItemViewModel(controller, workspace, page) { Text = new StringComponent(title), Icon = icon };
        SavesItem = Item("Saves", IconValues.Save, saves);
        DataItem = Item("Data", Mo2DataPage.DataIcon, data);
        ArchivesItem = Item("Archives", Mo2ArchivesPage.ArchiveIcon, archives);
        LogsItem = Item("Logs", Mo2LogsPage.LogsIcon, logs);
        OverwriteItem = Item("Overwrite", IconValues.Folder, overwrite);
        ExternalFilesItem = Item("External Files", IconValues.FolderEditOutline, externalFiles);
        ToolsItem = Item("Tools", Mo2ToolsPage.ToolIcon, tools);
        ProfilesItem = Item("Profiles", IconValues.Package, profiles);
        LeftMenuItemLibrary = Item("Downloads", IconValues.Download, downloads);
        LeftMenuItemLoadout = Item("My Mods", IconValues.CollectionsOutline, mods);
        LeftMenuItemHealthCheck = Item("Health Check", IconValues.Cardiology, health);
        LeftMenuItemExternalChanges = Item("Plugins", ScenarioLoadOrderPage.PluginIcon, plugins);
    }
}
