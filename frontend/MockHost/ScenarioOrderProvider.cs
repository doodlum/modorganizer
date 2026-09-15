using System.ComponentModel;
using System.Reactive.Linq;
using DynamicData;
using Humanizer;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Loadouts;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages.Sorting;
using R3;

namespace Mo2.Frontend;

internal sealed class ScenarioOrderProvider(Mo2LiveProfile? profile = null) : ILoadOrderDataProvider
{
    private static readonly ComponentKey CanMoveKey = ComponentKey.From("MO2.PluginCanMove");
    private static readonly ComponentKey CountKey = ComponentKey.From("MO2.PluginCount");
    public IObservable<IChangeSet<CompositeItemModel<ISortItemKey>, ISortItemKey>> ObserveLoadOrder(ISortOrderVariety variety, LoadoutId loadout, R3.Observable<ListSortDirection> direction)
    {
        var id = variety.GetSortOrderIdFor(loadout).Value;
        var orderChanged = false;
        var itemCount = 0;
        CompositeItemModel<ISortItemKey> Create(IReactiveSortItem item) {
            var model = new CompositeItemModel<ISortItemKey>(item.Key);
            if (item is ScenarioPlugin { GameArt.Length: > 0 } gameItem) model.Add(LoadOrderColumns.DisplayNameColumn.ImageComponentKey, new ImageComponent(Mo2GameArt.Thumbnail(gameItem.GameArt)));
            if (item is ScenarioPlugin { ModArt: { } art }) model.Add(LoadOrderColumns.DisplayNameColumn.ImageComponentKey,new ImageComponent(art));
            model.Add(LoadOrderColumns.DisplayNameColumn.DisplayNameComponentKey, new StringComponent(item.DisplayName));
            model.Add(LoadOrderColumns.ModNameColumn.ModNameComponentKey, new StringComponent(item.ModName));
            model.Add(LoadOrderColumns.IsActiveComponentKey, new ValueComponent<bool>(item.IsActive));
            var index = new ValueComponent<int>(item.SortIndex);
            var canMove = new ValueComponent<bool>(item is not ScenarioPlugin plugin || plugin.CanMove);
            var count = new ValueComponent<int>(itemCount);
            model.Add(CanMoveKey, canMove); model.Add(CountKey, count);
            model.Add(LoadOrderColumns.IndexColumn.IndexComponentKey, new SharedComponents.IndexComponent(
                index, new ValueComponent<string>((item.SortIndex + 1).Ordinalize()),
                R3.Observable.CombineLatest(index.Value.AsObservable(), direction, canMove.Value.AsObservable(), count.Value.AsObservable(),
                    (i, d, movable, total) => movable && (d == ListSortDirection.Ascending ? i > 0 : i < total - 1)),
                R3.Observable.CombineLatest(index.Value.AsObservable(), direction, canMove.Value.AsObservable(), count.Value.AsObservable(),
                    (i, d, movable, total) => movable && (d == ListSortDirection.Ascending ? i < total - 1 : i > 0))));
            Update(model, item);
            return model;
        }
        void Update(CompositeItemModel<ISortItemKey> model, IReactiveSortItem item) {
            model.Get<StringComponent>(LoadOrderColumns.DisplayNameColumn.DisplayNameComponentKey).Value.Value = (item is ScenarioPlugin { HasWarning: true } ? "⚠ " : "") + item.DisplayName;
            model.Get<StringComponent>(LoadOrderColumns.ModNameColumn.ModNameComponentKey).Value.Value = item.ModName;
            model.Get<ValueComponent<bool>>(LoadOrderColumns.IsActiveComponentKey).Value.Value = item.IsActive;
            model.Get<ValueComponent<bool>>(CanMoveKey).Value.Value = item is not ScenarioPlugin plugin || plugin.CanMove;
            model.Get<ValueComponent<int>>(CountKey).Value.Value = itemCount;
            var index = model.Get<SharedComponents.IndexComponent>(LoadOrderColumns.IndexColumn.IndexComponentKey);
            orderChanged |= index.Index.Value.Value != item.SortIndex;
            index.Index.Value.Value = item.SortIndex;
            index.DisplaySortIndexComponent.Value.Value = (item.SortIndex + 1).Ordinalize();
        }
        // Preserve native row identity and selection when MO2 reports state changes.
        var changes = profile is not null && ReferenceEquals(variety, profile.Order)
            ? profile.ObservePresentationPlugins(profile.CurrentTarget)
            : variety.GetSortOrderItemsChangeSet(id);
        return changes
            // The native snapshot is already committed before a batch arrives.
            // Count once, rather than sorting/materializing the complete order
            // again for every row's movement-boundary component.
            .Do(_ => itemCount = variety.GetSortOrderItems(id).Count)
            .TransformWithInlineUpdate(Create, Update)
            .Where(changes => {
                // NMA sorts on every list change; that resets row selection. State-only
                // changes already flow through the existing reactive components.
                var publish = orderChanged || changes.Any(x => x.Reason is ChangeReason.Add or ChangeReason.Remove or ChangeReason.Moved);
                orderChanged = false;
                return publish;
            });
    }
}
