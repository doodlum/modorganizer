using ObservableCollections;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
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
    public static readonly ComponentKey PriorityTextKey = ComponentKey.From("MO2.PriorityText");
    public static readonly ComponentKey ConflictsKey = ComponentKey.From("MO2.Conflicts");
    public static readonly ComponentKey FlagsKey = ComponentKey.From("MO2.Flags");
    protected override IColumn<CompositeItemModel<EntityId>>[] CreateColumns(bool viewHierarchical) => [
        new TextColumn<CompositeItemModel<EntityId>, string>("Priority", item => item.Get<ValueComponent<string>>(PriorityTextKey).Value.Value, width: new GridLength(60),
            options: new TextColumnOptions<CompositeItemModel<EntityId>> { CompareAscending = (a, b) => a!.Get<ValueComponent<int>>(PriorityKey).Value.Value.CompareTo(b!.Get<ValueComponent<int>>(PriorityKey).Value.Value) }),
        viewHierarchical ? ITreeDataGridItemModel<CompositeItemModel<EntityId>, EntityId>.CreateExpanderColumn(ColumnCreator.Create<EntityId, SharedColumns.Name>(width: new GridLength(1, GridUnitType.Star))) : ColumnCreator.Create<EntityId, SharedColumns.Name>(width: new GridLength(1, GridUnitType.Star)),
        new TemplateColumn<CompositeItemModel<EntityId>>("Conflicts / status", new FuncDataTemplate<CompositeItemModel<EntityId>>((item, _) => {
            if (item is null) return new TextBlock();
            var text = string.Join("\n", new[] { item.Get<ValueComponent<string>>(ConflictsKey).Value.Value, item.Get<ValueComponent<string>>(FlagsKey).Value.Value }.Where(x => x.Length > 0));
            var label = new TextBlock { Text = text, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            ToolTip.SetTip(label, text); return label;
        }), width: new GridLength(145)),
        ColumnCreator.Create<EntityId, LoadoutColumns.EnabledState>(width: new GridLength(70)),
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
        var header = new StackPanel { Spacing = 4 };
        header.Children.Add(title);
        var actions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Margin = new Thickness(12, 0), Spacing = 8 };
        foreach (var (label, delta) in new[] { ("Move earlier", -1), ("Move later", 1) }) {
            var button = new Button { Content = label };
            button.Click += async (_, _) => {
                if (ViewModel is { LiveProfile: { } profile } model && model.Adapter.SelectedModels.Count == 1)
                    await profile.MoveMod(model.Adapter.SelectedModels.Single().Key, delta);
            };
            ToolTip.SetTip(button, "Change the selected mod’s MO2 priority");
            actions.Children.Add(button);
        }
        var details = new Button { Content = "Details in MO2" };
        ToolTip.SetTip(details, "Open MO2’s conflict and file details for the selected mod");
        details.Click += async (_, _) => {
            if (ViewModel is { LiveProfile: { } profile } model && model.Adapter.SelectedModels.Count == 1)
                await profile.ShowModDetails(model.Adapter.SelectedModels.Single().Key);
        };
        actions.Children.Add(details);
        actions.IsEnabled = false;
        header.Children.Add(actions);
        DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header); layout.Children.Add(table); Content = layout;
        TreeDataGridViewHelper.SetupTreeDataGridAdapter<Mo2ModsView, ScenarioInstalledPage, CompositeItemModel<EntityId>, EntityId>(this, table, vm => vm.Adapter);
        this.WhenActivated(disposables => {
            ViewModel!.Adapter.SelectedModels.ObserveCountChanged(notifyCurrentCount: true)
                .Subscribe(count => actions.IsEnabled = count == 1).AddTo(disposables);
            this.OneWayBind(ViewModel, vm => vm.Adapter.Source.Value, view => view.Table.Source).AddTo(disposables);
        });
    }
}
