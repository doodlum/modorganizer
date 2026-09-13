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
using NexusMods.UI.Sdk.Icons;
using R3;
using ReactiveUI;

namespace Mo2.Frontend;

// Reuses the upstream table and activation messages with MO2-specific columns.
internal sealed class Mo2ModsAdapter : LoadoutTreeDataGridAdapter
{
    public Mo2ModsAdapter(IServiceProvider services) : base(services, new LoadoutFilter { LoadoutId = default, CollectionGroupId = default })
    {
        CustomSortComparer.Value = Comparer<CompositeItemModel<EntityId>>.Create((a, b) =>
            a.Get<ValueComponent<int>>(PriorityKey).Value.Value.CompareTo(b.Get<ValueComponent<int>>(PriorityKey).Value.Value));
    }
    public static readonly ComponentKey PriorityKey = ComponentKey.From("MO2.Priority");
    public static readonly ComponentKey PriorityTextKey = ComponentKey.From("MO2.PriorityText");
    public static readonly ComponentKey ConflictsKey = ComponentKey.From("MO2.Conflicts");
    public static readonly ComponentKey FlagsKey = ComponentKey.From("MO2.Flags");
    protected override IColumn<CompositeItemModel<EntityId>>[] CreateColumns(bool viewHierarchical) => [
        new TextColumn<CompositeItemModel<EntityId>, string>("Priority", item => item.Get<ValueComponent<string>>(PriorityTextKey).Value.Value, width: new GridLength(70),
            options: new TextColumnOptions<CompositeItemModel<EntityId>> { CompareAscending = (a, b) => a!.Get<ValueComponent<int>>(PriorityKey).Value.Value.CompareTo(b!.Get<ValueComponent<int>>(PriorityKey).Value.Value) }),
        viewHierarchical ? ITreeDataGridItemModel<CompositeItemModel<EntityId>, EntityId>.CreateExpanderColumn(ColumnCreator.Create<EntityId, SharedColumns.Name>(width: new GridLength(180))) : ColumnCreator.Create<EntityId, SharedColumns.Name>(width: new GridLength(180)),
        new TemplateColumn<CompositeItemModel<EntityId>>("Conflicts / status", new FuncDataTemplate<CompositeItemModel<EntityId>>((item, _) => {
            if (item is null) return new TextBlock();
            var text = string.Join("\n", new[] { item.Get<ValueComponent<string>>(ConflictsKey).Value.Value, item.Get<ValueComponent<string>>(FlagsKey).Value.Value }.Where(x => x.Length > 0));
            var label = new TextBlock { Text = text, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            ToolTip.SetTip(label, text); return label;
        }), width: new GridLength(115)),
        ColumnCreator.Create<EntityId, LoadoutColumns.EnabledState>(width: new GridLength(70)),
    ];
}

internal sealed class Mo2ModsView : ReactiveUserControl<ScenarioInstalledPage>
{
    public LoadoutView NativeView { get; } = new();
    private TabControl SubTabs => NativeView.FindControl<TabControl>("RulesTabControl")!;
    private TextBox SearchBox => NativeView.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!.FindControl<TextBox>("SearchTextBox")!;
    private Control SearchPanel => NativeView.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!.FindControl<Control>("SearchPanel")!;
    public Mo2ModsView()
    {
        var native = NativeView;
        Content = native;
        var header = native.FindControl<NexusMods.App.UI.Controls.PageHeader.PageHeader>("AllPageHeader")!;
        header.Title = "My Mods";
        header.Description = "Installed mods in the selected MO2 profile.";
        var empty = native.FindControl<EmptyState>("EmptyState")!;
        if (empty.Subtitle is StackPanel subtitle)
            subtitle.Children.OfType<TextBlock>().First().Text = "Install mods from your MO2 downloads folder.";
        native.FindControl<StandardButton>("ViewLibraryButton")!.Text = "Downloads";
        ToolTip.SetTip(native.FindControl<StandardButton>("ViewFilesButton")!, "View this mod’s files and conflicts in MO2");
        var group = native.FindControl<ItemsControl>("ContextControlGroup")!;
        var moves = new List<StandardButton>();
        foreach (var delta in new[] { -1, 1 }) {
            var button = new StandardButton { ShowLabel = false, ShowIcon = StandardButton.ShowIconOptions.Left,
                LeftIcon = delta < 0 ? IconValues.ArrowUp : IconValues.ArrowDown, Size = StandardButton.Sizes.Toolbar,
                Fill = StandardButton.Fills.None, Type = StandardButton.Types.Tertiary,
                Name = delta < 0 ? "MoveModEarlierButton" : "MoveModLaterButton" };
            var description = delta < 0 ? "Move earlier in MO2 priority" : "Move later in MO2 priority";
            ToolTip.SetTip(button, description);
            Avalonia.Automation.AutomationProperties.SetName(button, description);
            button.Click += async (_, _) => {
                if (ViewModel is { LiveProfile: { } profile } model && model.Adapter.SelectedModels.Count == 1)
                    await profile.MoveMod(model.Adapter.SelectedModels.Single().Key, delta);
            };
            group.Items.Add(button); moves.Add(button);
        }
        this.WhenActivated(disposables => {
            this.Bind(ViewModel, vm => vm.Mo2SearchText, view => view.SearchBox.Text).AddTo(disposables);
            this.Bind(ViewModel, vm => vm.Mo2SearchExpanded, view => view.SearchPanel.IsVisible).AddTo(disposables);
            this.OneWayBind(ViewModel, vm => vm, view => view.NativeView.ViewModel).AddTo(disposables);
            this.Bind(ViewModel, vm => vm.SelectedSubTab, view => view.SubTabs.SelectedIndex, value => (int)value, value => value == 1 ? LoadoutPageSubTabs.Rules : LoadoutPageSubTabs.Mods).AddTo(disposables);
            ViewModel!.Adapter.SelectedModels.ObserveCountChanged(notifyCurrentCount: true)
                .Subscribe(count => {
                    var selected = ViewModel.Adapter.SelectedModels.Select(x => x.Key).ToHashSet();
                    var mods = ViewModel.LiveProfile!.Mods.Where(x => selected.Contains(x.Id)).ToArray();
                    foreach (var button in moves) button.IsEnabled = count == 1 && mods.Length == 1 && (mods[0].State & 4) == 0;
                    native.FindControl<StandardButton>("ViewFilesButton")!.IsEnabled = count == 1;
                    native.FindControl<StandardButton>("DeleteButton")!.IsEnabled = mods.Any(x => (x.State & 4) == 0);
                }).AddTo(disposables);
        });
    }
}
