using System.ComponentModel;
using System.Reactive.Linq;
using DynamicData;
using Humanizer;
using NexusMods.Abstractions.Games;
using NexusMods.Abstractions.Loadouts;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages.Sorting;
using R3;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class ScenarioOrderProvider : ILoadOrderDataProvider
{
    public IObservable<IChangeSet<CompositeItemModel<ISortItemKey>, ISortItemKey>> ObserveLoadOrder(ISortOrderVariety variety, LoadoutId loadout, R3.Observable<ListSortDirection> direction)
    {
        var id = variety.GetSortOrderIdFor(loadout).Value;
        return variety.GetSortOrderItemsChangeSet(id).Transform(item => {
            var model = new CompositeItemModel<ISortItemKey>(item.Key);
            model.Add(LoadOrderColumns.DisplayNameColumn.DisplayNameComponentKey, new StringComponent(item.DisplayName));
            model.Add(LoadOrderColumns.ModNameColumn.ModNameComponentKey, new StringComponent(item.ModName));
            model.Add(LoadOrderColumns.IsActiveComponentKey, new ValueComponent<bool>(item.IsActive));
            var index = item.WhenAnyValue(x => x.SortIndex).ToObservable();
            var count = variety.GetSortOrderItems(id).Count;
            var canMove = item is not ScenarioPlugin plugin || plugin.CanMove;
            model.Add(LoadOrderColumns.IndexColumn.IndexComponentKey, new SharedComponents.IndexComponent(
                new ValueComponent<int>(item.SortIndex, index, subscribeWhenCreated: true),
                new ValueComponent<string>((item.SortIndex + 1).Ordinalize(), index.Select(x => (x + 1).Ordinalize()), subscribeWhenCreated: true),
                R3.Observable.CombineLatest(index, direction, (i, d) => canMove && (d == ListSortDirection.Ascending ? i > 0 : i < count - 1)),
                R3.Observable.CombineLatest(index, direction, (i, d) => canMove && (d == ListSortDirection.Ascending ? i < count - 1 : i > 0))));
            return model;
        });
    }
}
