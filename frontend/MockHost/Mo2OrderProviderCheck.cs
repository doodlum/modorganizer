using System.ComponentModel;
using System.Reactive.Linq;
using System.Reflection;
using DynamicData;
using NexusMods.Abstractions.Games;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages.Sorting;
using R3;

namespace Mo2.Frontend;

internal static class Mo2OrderProviderCheck
{
    public class CountingVariety : DispatchProxy
    {
        public ISortOrderVariety Target = null!;
        public int FullOrderReads;
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(ISortOrderVariety.GetSortOrderItems)) FullOrderReads++;
            return method.Invoke(Target, args);
        }
    }
    internal static async Task Run()
    {
        var order = new ScenarioPluginOrder(seedFixtures: false);
        var variety = DispatchProxy.Create<ISortOrderVariety, CountingVariety>();
        var counter = (CountingVariety)(object)variety; counter.Target = order;
        using var direction = new ReactiveProperty<ListSortDirection>(ListSortDirection.Ascending);
        var rows = new Dictionary<ISortItemKey, CompositeItemModel<ISortItemKey>>();
        using var activations = new System.Reactive.Disposables.CompositeDisposable();
        var activated = new HashSet<CompositeItemModel<ISortItemKey>>();
        using var subscription = new ScenarioOrderProvider().ObserveLoadOrder(variety, default, direction.AsObservable())
            .Subscribe(changes => {
                foreach (var change in changes) {
                    if (change.Reason == ChangeReason.Remove) rows.Remove(change.Key);
                    else {
                        rows[change.Key] = change.Current;
                        if (activated.Add(change.Current)) activations.Add(change.Current.Activate());
                    }
                }
            });
        void Replace(int count) {
            var reads = counter.FullOrderReads;
            order.Replace(Enumerable.Range(0, count).Select(i => new ScenarioPlugin($"Plugin{i}.esp", "Fixture", i)));
            if (counter.FullOrderReads != reads + 1) throw new Exception("Provider did not count exactly once per batch");
        }
        SharedComponents.IndexComponent At(int index) => rows[order.Plugins[index].Key].Get<SharedComponents.IndexComponent>(LoadOrderColumns.IndexColumn.IndexComponentKey);
        async Task Boundaries(int count) {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < until && (rows.Count != count || At(0).MoveUp.CanExecute() || !At(0).MoveDown.CanExecute()
                || !At(count - 1).MoveUp.CanExecute() || At(count - 1).MoveDown.CanExecute())) await Task.Delay(50);
            if (rows.Count != count || At(0).MoveUp.CanExecute() || !At(0).MoveDown.CanExecute()
                || !At(count - 1).MoveUp.CanExecute() || At(count - 1).MoveDown.CanExecute())
                throw new Exception($"Plugin movement boundaries do not match batch size {count}: rows={rows.Count}, first={At(0).MoveUp.CanExecute()}/{At(0).MoveDown.CanExecute()}, last={At(count - 1).MoveUp.CanExecute()}/{At(count - 1).MoveDown.CanExecute()}, stored={rows[order.Plugins[0].Key].Get<ValueComponent<int>>(ComponentKey.From("MO2.PluginCount")).Value.Value}");
        }
        Replace(151); await Boundaries(151);
        var first = rows[order.Plugins[0].Key];
        Replace(152); await Boundaries(152);
        Replace(2); await Boundaries(2);
        if (!ReferenceEquals(first, rows[order.Plugins[0].Key])) throw new Exception("Existing row identity changed");
        direction.Value = ListSortDirection.Descending; await Task.Delay(50);
        if (!At(0).MoveUp.CanExecute() || At(0).MoveDown.CanExecute()
            || At(1).MoveUp.CanExecute() || !At(1).MoveDown.CanExecute())
            throw new Exception("Descending movement boundaries are incorrect");
        Console.WriteLine("PASS: one full-order read per batch; movement bounds after 151/152/2-item batches, preserved row identity and reversed direction");
    }
}
