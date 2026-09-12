using System.ComponentModel;
using System.Reactive.Linq;
using DynamicData;
using DynamicData.Kernel;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Loadouts;
using NexusMods.MnemonicDB.Abstractions;
using OneOf;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class ScenarioPlugin : ReactiveObject, IReactiveSortItem
{
    public ISortItemKey Key { get; }
    public string DisplayName { get; }
    public string[] Masters { get; }
    private int _index;
    public int SortIndex { get => _index; set => this.RaiseAndSetIfChanged(ref _index, value); }
    public string ModName { get; set; }
    public Optional<LoadoutItemGroupId> ModGroupId { get; set; }
    public bool IsActive { get; set; } = true;
    public ISortItemLoadoutData? LoadoutData { get; set; }
    public ScenarioPlugin(string name, string mod, int index, params string[] masters)
    { Key = new SortItemKey<string>(name); DisplayName = name; ModName = mod; _index = index; Masters = masters; }
}

internal sealed class ScenarioPluginOrder : ISortOrderVariety
{
    private readonly SourceCache<ScenarioPlugin, ISortItemKey> _items = new(item => item.Key);
    public SortOrderVarietyId SortOrderVarietyId { get; } = SortOrderVarietyId.From(Guid.Parse("483a120b-e7e3-4e01-8862-08735e534e30"));
    public ListSortDirection SortDirectionDefault => ListSortDirection.Ascending;
    public IndexOverrideBehavior IndexOverrideBehavior => IndexOverrideBehavior.GreaterIndexWins;
    public SortOrderUiMetadata SortOrderUiMetadata { get; } = new() {
        SortOrderName = "Plugin load order", IndexColumnHeader = "LOAD ORDER", DisplayNameColumnHeader = "PLUGIN",
        OverrideInfoTitle = "Plugin load order — last loaded wins",
        OverrideInfoMessage = "Plugins loaded later override records from earlier plugins. Masters must load before plugins that depend on them.",
        WinnerIndexToolTip = "Last Loaded Plugin Wins", EmptyStateMessageTitle = "No plugins detected",
        EmptyStateMessageContents = "Install mods containing ESM or ESP plugins to manage their load order.",
        LearnMoreUrl = "https://loot.github.io/docs/help/introduction-to-load-orders/",
    };
    public ScenarioPluginOrder()
    {
        _items.AddOrUpdate(new[] {
            new ScenarioPlugin("FalloutNV.esm", "Game files", 0),
            new ScenarioPlugin("DeadMoney.esm", "Dead Money", 1, "FalloutNV.esm"),
            new ScenarioPlugin("YUP - Base Game + All DLC.esm", "Yukichigai Unofficial Patch (fixture)", 2, "FalloutNV.esm", "DeadMoney.esm"),
            new ScenarioPlugin("Desert Lighting.esp", "Desert Lighting (fixture)", 3, "FalloutNV.esm"),
            new ScenarioPlugin("Mojave Encounters.esp", "Mojave Encounters (fixture)", 4, "FalloutNV.esm"),
            new ScenarioPlugin("Compatibility Patch.esp", "Compatibility Patch (fixture)", 5, "Desert Lighting.esp", "Mojave Encounters.esp"),
        });
    }
    public void CopyFrom(ScenarioPluginOrder source)
    {
        _items.Edit(cache => {
            cache.Clear();
            cache.AddOrUpdate(source.Plugins.Select(x => new ScenarioPlugin(x.DisplayName, x.ModName, x.SortIndex, x.Masters.ToArray()) { IsActive = x.IsActive }));
        });
    }
    public void SetPluginActive(string name, bool enabled)
    {
        var plugin = _items.Items.Single(x => x.DisplayName == name);
        plugin.IsActive = enabled;
        _items.AddOrUpdate(plugin);
    }
    public IReadOnlyList<ScenarioPlugin> Plugins => _items.Items.OrderBy(item => item.SortIndex).ToArray();
    public Optional<SortOrderId> GetSortOrderIdFor(OneOf<LoadoutId, CollectionGroupId> parentEntity, IDb? db = null) => Optional<SortOrderId>.Create(default);
    public ValueTask<SortOrderId> GetOrCreateSortOrderFor(LoadoutId loadoutId, OneOf<LoadoutId, CollectionGroupId> parentEntity, CancellationToken token = default) => ValueTask.FromResult(default(SortOrderId));
    public IObservable<IChangeSet<IReactiveSortItem, ISortItemKey>> GetSortOrderItemsChangeSet(SortOrderId sortOrderId) => _items.Connect().Transform(item => (IReactiveSortItem)item);
    public IReadOnlyList<IReactiveSortItem> GetSortOrderItems(SortOrderId sortOrderId, IDb? db = null) => Plugins.Cast<IReactiveSortItem>().ToArray();
    public Task MoveItemDelta(SortOrderId sortOrderId, ISortItemKey sourceItem, int delta, IDb? db = null, CancellationToken token = default)
    {
        var order = Plugins.ToList();
        var index = order.FindIndex(item => item.Key.Equals(sourceItem));
        if (index < 0 || delta == 0) return Task.CompletedTask;
        var target = Math.Clamp(index + delta, 0, order.Count - 1);
        return MoveItems(sortOrderId, [sourceItem], order[target].Key,
            delta < 0 ? TargetRelativePosition.BeforeTarget : TargetRelativePosition.AfterTarget, db, token);
    }
    public Task MoveItems(SortOrderId sortOrderId, ISortItemKey[] itemsToMove, ISortItemKey dropTargetItem, TargetRelativePosition relativePosition, IDb? db = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var order = Plugins.ToList();
        var moving = order.Where(item => itemsToMove.Any(key => key.Equals(item.Key))).ToArray();
        if (moving.Any(item => item.Key.Equals(dropTargetItem))) return Task.CompletedTask;
        order.RemoveAll(item => moving.Contains(item));
        var target = order.FindIndex(item => item.Key.Equals(dropTargetItem));
        if (target < 0) return Task.CompletedTask;
        order.InsertRange(target + (relativePosition == TargetRelativePosition.AfterTarget ? 1 : 0), moving);
        var positions = order.Select((item, index) => (item.DisplayName, index)).ToDictionary(x => x.DisplayName, x => x.index);
        if (order.Any(item => item.Masters.Any(master => positions[master] >= positions[item.DisplayName]))) return Task.CompletedTask;
        _items.Edit(cache => {
            for (var index = 0; index < order.Count; index++) order[index].SortIndex = index;
            // Publish after every index has settled; the native table sorts each change batch.
            cache.AddOrUpdate(order);
        });
        return Task.CompletedTask;
    }
    public ValueTask ReconcileSortOrder(SortOrderId sortOrderId, IDb? db = null, CancellationToken token = default) => ValueTask.CompletedTask;
    public ValueTask DeleteSortOrder(SortOrderId sortOrderId, CancellationToken token = default) { _items.Clear(); return ValueTask.CompletedTask; }
}
