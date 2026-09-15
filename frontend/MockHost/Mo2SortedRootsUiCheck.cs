using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.VisualTree;
using NexusMods.Abstractions.Games;
using NexusMods.App.UI.Controls;
using System.Reactive.Linq;

namespace Mo2.Frontend;

internal static class Mo2SortedRootsUiCheck
{
    internal static async Task Run(Mo2LiveWorkspace shell, Window window)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT")))
            throw new InvalidOperationException("Use an isolated layout for sorting checks");
        async Task Wait(Func<bool> ready) {
            var until = DateTime.UtcNow.AddSeconds(15);
            while (!ready()) { if (DateTime.UtcNow >= until) throw new TimeoutException("Plugin sorting check did not settle"); await Task.Delay(50); }
        }
        await Wait(() => shell.Profile.IsConnected && !shell.Profile.SelectingProfile && shell.Profile.ProfilePath.Length > 0);
        await shell.ProfileMenu.LeftMenuItemExternalChanges!.NavigateCommand.Execute(
            NexusMods.App.UI.Controls.Navigation.NavigationInformation.From(NexusMods.App.UI.WorkspaceSystem.NavigationInput.Default));
        await Wait(() => window.GetVisualDescendants().OfType<Mo2PluginsView>()
            .Any(v => v.IsEffectivelyVisible && v.ViewModel?.Adapter.SourceCount.Value >= 2));
        await Task.Delay(500);
        var page = window.GetVisualDescendants().OfType<Mo2PluginsView>().First(v => v.IsEffectivelyVisible).ViewModel!;
        var adapter = page.Adapter;
        var source = (FlatTreeDataGridSource<CompositeItemModel<ISortItemKey>>)adapter.Source.Value;
        var comparer = adapter.CustomSortComparer.Value ?? throw new Exception("Plugin comparator missing");
        // The synchronized view supports enumeration, but deliberately does not
        // implement ICollection.CopyTo (which LINQ ToArray otherwise selects).
        var original = source.Items.Select(item => item).ToArray();
        var originalSelection = source.RowSelection!.SelectedIndexes.ToArray();
        var target = shell.Profile.CurrentTarget;
        var nativeOrder = shell.Profile.Order.Plugins.Select(p => (p.DisplayName, p.SortIndex, p.IsActive)).ToArray();
        try {
            source.RowSelection.Clear(); source.RowSelection.Select(new IndexPath(0));
            var selected = source.RowSelection.SelectedItem;
            adapter.CustomSortComparer.Value = Comparer<CompositeItemModel<ISortItemKey>>.Create(comparer.Compare);
            await Task.Delay(500);
            if (!ReferenceEquals(source.RowSelection.SelectedItem, selected) || !source.Items.SequenceEqual(original))
                throw new Exception("Equivalent comparator cleared selection or changed row order");
            adapter.CustomSortComparer.Value = Comparer<CompositeItemModel<ISortItemKey>>.Create((a,b) => comparer.Compare(b,a));
            await Wait(() => source.Items.SequenceEqual(original.Reverse()));
            adapter.CustomSortComparer.Value = comparer;
            await Wait(() => source.Items.SequenceEqual(original));
            if (shell.Profile.CurrentTarget != target || !shell.Profile.Order.Plugins.Select(p => (p.DisplayName,p.SortIndex,p.IsActive)).SequenceEqual(nativeOrder))
                throw new Exception("Presentation sort modified native plugin state");
            Console.WriteLine("PASS live sorted roots: equivalent comparator preserves selection; reversed/restored order renders; native plugin state unchanged");
        } finally {
            adapter.CustomSortComparer.Value = comparer;
            await Wait(() => source.Items.SequenceEqual(original));
            source.RowSelection.Clear(); foreach (var index in originalSelection) source.RowSelection.Select(index);
        }
    }
}
