using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Pages.LoadoutPage;
using NexusMods.MnemonicDB.Abstractions;
using R3;
using ReactiveUI;

namespace Mo2.Frontend;

// Reuses the upstream table and activation messages with MO2-specific columns.
internal sealed class Mo2ModsAdapter(IServiceProvider services) : LoadoutTreeDataGridAdapter(services, new LoadoutFilter { LoadoutId = default, CollectionGroupId = default })
{
    public static readonly ComponentKey PriorityKey = ComponentKey.From("MO2.Priority");
    protected override IColumn<CompositeItemModel<EntityId>>[] CreateColumns(bool viewHierarchical) => [
        new TextColumn<CompositeItemModel<EntityId>, int>("Priority", item => item.Get<ValueComponent<int>>(PriorityKey).Value.Value,
            options: new TextColumnOptions<CompositeItemModel<EntityId>> { CompareAscending = (a, b) => a!.Get<ValueComponent<int>>(PriorityKey).Value.Value.CompareTo(b!.Get<ValueComponent<int>>(PriorityKey).Value.Value) }),
        viewHierarchical ? ITreeDataGridItemModel<CompositeItemModel<EntityId>, EntityId>.CreateExpanderColumn(ColumnCreator.Create<EntityId, SharedColumns.Name>()) : ColumnCreator.Create<EntityId, SharedColumns.Name>(),
        ColumnCreator.Create<EntityId, LoadoutColumns.EnabledState>(),
    ];
}

internal sealed class Mo2ModsView : ReactiveUserControl<ScenarioInstalledPage>
{
    private TreeDataGrid Table { get; } = new();
    public Mo2ModsView()
    {
        var table = Table;
        table.ShowColumnHeaders = true; table.CanUserResizeColumns = true; table.CanUserSortColumns = true; table.Margin = new Thickness(12);
        table.Classes.Add("MainListsStyling");
        var layout = new DockPanel();
        var title = new TextBlock { Text = "Mods", FontSize = 22, Margin = new Thickness(16) };
        DockPanel.SetDock(title, Dock.Top); layout.Children.Add(title); layout.Children.Add(table); Content = layout;
        TreeDataGridViewHelper.SetupTreeDataGridAdapter<Mo2ModsView, ScenarioInstalledPage, CompositeItemModel<EntityId>, EntityId>(this, table, vm => vm.Adapter);
        this.WhenActivated(disposables => {
            this.OneWayBind(ViewModel, vm => vm.Adapter.Source.Value, view => view.Table.Source).AddTo(disposables);
        });
    }
}
