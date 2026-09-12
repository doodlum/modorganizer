using System.Reactive.Linq;
using DynamicData;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Pages.LibraryPage;
using NexusMods.MnemonicDB.Abstractions;
using NexusMods.Sdk.Games;
using NexusMods.Sdk.Library;

namespace Mo2.Frontend;

internal sealed class ScenarioLibrary
{
    internal readonly SourceCache<ScenarioMod, EntityId> Items = new(x => x.Id);
    private ulong _next = 200;
    public ScenarioLibrary() { Add("Simple Reticle"); Add("Mojave Weather"); }
    public void Add(string name) => Items.AddOrUpdate(new ScenarioMod(EntityId.From(++_next), name, name + ".esp", true, DateTimeOffset.Now));
    public void Install(IEnumerable<EntityId> ids, ScenarioInstalledMods target)
    {
        foreach (var id in ids.ToArray()) { var item = Items.Lookup(id); if (item.HasValue) target.Install(item.Value); }
    }
    public void Remove(IEnumerable<EntityId> ids) => Items.RemoveKeys(ids.ToArray());
}

internal sealed class ScenarioLibraryProvider(ScenarioLibrary library, ScenarioInstalledMods installed) : ILibraryDataProvider
{
    public LibraryFile.ReadOnly[] GetAllFiles(GameId gameId, IDb? db = null) => [];
    public IObservable<int> CountLibraryItems(LibraryFilter filter) => library.Items.CountChanged;
    public IObservable<IChangeSet<CompositeItemModel<EntityId>, EntityId>> ObserveLibraryItems(LibraryFilter filter)
        => installed.CountLoadoutItems(null!).Select(_ => library.Items.Connect().Transform(item => {
            var model = new CompositeItemModel<EntityId>(item.Id);
            var existing = installed.Mods.FirstOrDefault(x => x.Id == item.Id);
            model.Add(SharedColumns.Name.NameComponentKey, new NameComponent(item.Name));
            model.Add(SharedColumns.ItemSize.ComponentKey, new SizeComponent(NexusMods.Paths.Size.From(1024UL * 256)));
            model.Add(LibraryColumns.ItemVersion.CurrentVersionComponentKey, new VersionComponent("1.0"));
            model.Add(LibraryColumns.DownloadedDate.ComponentKey, new DateComponent(item.Installed));
            model.Add(LibraryColumns.Actions.LibraryItemIdsComponentKey, new LibraryComponents.LibraryItemIds(LibraryItemId.From(item.Id)));
            model.Add(LibraryColumns.Actions.InstallComponentKey, new LibraryComponents.InstallAction(new ValueComponent<bool>(existing is not null)));
            model.Add(LibraryColumns.Actions.DeleteItemComponentKey, new LibraryComponents.DeleteItemAction());
            if (existing is not null) model.Add(SharedColumns.InstalledDate.ComponentKey, new DateComponent(existing.Installed));
            return model;
        })).Switch();
}
