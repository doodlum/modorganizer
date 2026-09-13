using ObservableCollections;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
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
        _profile = services.GetRequiredService<IEnumerable<ILoadoutDataProvider>>().OfType<Mo2LiveProfile>().Single();
        ViewHierarchical.Value = false;
        CustomSortComparer.Value = Comparer<CompositeItemModel<EntityId>>.Create((a, b) =>
            a.Get<ValueComponent<int>>(PriorityKey).Value.Value.CompareTo(b.Get<ValueComponent<int>>(PriorityKey).Value.Value));
    }
    private readonly Mo2LiveProfile _profile;
    private readonly System.Reactive.Subjects.BehaviorSubject<Func<Mo2LiveMod,bool>> _filter = new(_ => true);
    public static readonly ComponentKey StateKey = ComponentKey.From("MO2.State");
    public void SetFilter(int choice) => _filter.OnNext(mod => choice switch {
        1 => (mod.State & 2) != 0, 2 => (mod.State & 2) == 0 && (mod.State & 4) == 0,
        3 => mod.Conflicts.Length > 0, 4 => mod.Conflicts.Length == 0, _ => true
    });
    protected override IObservable<IChangeSet<CompositeItemModel<EntityId>,EntityId>> GetRootsObservable(bool viewHierarchical)
        => _profile.ObserveFilteredMods(_filter);
    public async Task Move(EntityId id, int value, bool absolute = false)
    {
        var target = _profile.CurrentTarget;
        await _profile.MoveMod(id, value, absolute);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            if (target != _profile.CurrentTarget) return;
            var index = Roots.ToList().FindIndex(x => x.Key == id);
            if (index >= 0 && Source.Value.Selection is Avalonia.Controls.Selection.TreeDataGridRowSelectionModel<CompositeItemModel<EntityId>> selection) {
                selection.Clear(); selection.Select(new IndexPath(index));
            }
        });
    }
    public static readonly ComponentKey PriorityKey = ComponentKey.From("MO2.Priority");
    public static readonly ComponentKey PriorityTextKey = ComponentKey.From("MO2.PriorityText");
    public static readonly ComponentKey ConflictsKey = ComponentKey.From("MO2.Conflicts");
    public static readonly ComponentKey FlagsKey = ComponentKey.From("MO2.Flags");
    protected override IColumn<CompositeItemModel<EntityId>>[] CreateColumns(bool viewHierarchical) => [
        new TextColumn<CompositeItemModel<EntityId>, string>("Priority", item => item.Get<ValueComponent<string>>(PriorityTextKey).Value.Value, width: new GridLength(76),
            options: new TextColumnOptions<CompositeItemModel<EntityId>> { CompareAscending = (a, b) => a!.Get<ValueComponent<int>>(PriorityKey).Value.Value.CompareTo(b!.Get<ValueComponent<int>>(PriorityKey).Value.Value) }),
        new TemplateColumn<CompositeItemModel<EntityId>>("Mod", new FuncDataTemplate<CompositeItemModel<EntityId>>((item, _) => {
            if (item is null) return new TextBlock();
            var conflicts = item.Get<ValueComponent<string>>(ConflictsKey).Value.Value;
            var flags = item.Get<ValueComponent<string>>(FlagsKey).Value.Value;
            var label = new TextBlock { Text = item.Get<NameComponent>(SharedColumns.Name.NameComponentKey).Value.Value,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(6,0) };
            if (_profile.FindMod(item.Key)?.IsSeparator == true)
                label.FontWeight = Avalonia.Media.FontWeight.Bold;
            var border = new Border { Child = label };
            ToolTip.SetTip(border, label.Text + "\n" + string.Join("\n", new[] { conflicts, flags }.Where(x => x.Length > 0)));
            var mod = _profile.FindMod(item.Key);
            var thumbnail = mod?.Name.StartsWith("DLC:",StringComparison.OrdinalIgnoreCase) == true ? Mo2GameArt.Thumbnail(_profile.GameName) : Mo2GameArt.ModThumbnail(_profile.NexusGame,mod?.NexusId ?? 0);
            if (thumbnail is not null) {
                var row = new DockPanel();
                var art = new Border { Width = 46, Height = 26, Margin = new Thickness(4,0), CornerRadius = new CornerRadius(4), ClipToBounds = true,
                    Child = new Image { Source = thumbnail, Stretch = Avalonia.Media.Stretch.Uniform } };
                DockPanel.SetDock(art,Dock.Left); row.Children.Add(art); row.Children.Add(border); return row;
            }
            return border;
        }), width: new GridLength(1, GridUnitType.Star)),
        new TemplateColumn<CompositeItemModel<EntityId>>("", new FuncDataTemplate<CompositeItemModel<EntityId>>((item, _) => {
            var text = item?.Get<ValueComponent<string>>(ConflictsKey).Value.Value ?? "";
            var label = new TextBlock { Text = text.Length > 0 ? "⚡" : "", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            ToolTip.SetTip(label, text); return label;
        }), width: new GridLength(26)),
        ColumnCreator.Create<EntityId, LoadoutColumns.EnabledState>(width: new GridLength(65)),
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
        native.FindControl<TabItem>("RulesTabItem")!.IsVisible = false;
        SubTabs.SelectedIndex = 0;
        var header = native.FindControl<NexusMods.App.UI.Controls.PageHeader.PageHeader>("AllPageHeader")!;
        header.Title = "Mods";
        header.Icon = IconValues.PictogramCollection3D;
        header.Description = "Installed mods in the selected MO2 profile.";
        var empty = native.FindControl<EmptyState>("EmptyState")!;
        if (empty.Subtitle is StackPanel subtitle)
            subtitle.Children.OfType<TextBlock>().First().Text = "Install mods from your MO2 downloads folder.";
        native.FindControl<StandardButton>("ViewLibraryButton")!.Text = "Downloads";
        ToolTip.SetTip(native.FindControl<StandardButton>("ViewFilesButton")!, "View this mod’s files and conflicts in MO2");
        var group = native.FindControl<ItemsControl>("ContextControlGroup")!;
        var filters = new ComboBox { Name = "ModStateFilter", ItemsSource = new[] { "All mods", "Enabled", "Disabled", "Conflicts", "No conflicts" }, SelectedIndex = 0, MinWidth = 120 };
        var priority = new TextBox { Name = "ModPriorityInput", Width = 70, Watermark = "Priority" };
        var setPriority = new Button { Name = "SetModPriorityButton", Content = "Move" };
        ToolTip.SetTip(priority, "MO2 mod priority (0 is first)");
        var filterRow = new WrapPanel { Margin = new Thickness(24,4,24,8) };
        filters.Margin = priority.Margin = setPriority.Margin = new Thickness(0,0,8,4);
        var separator = new Button { Name = "CreateSeparatorButton", Content = "Create separator…", Margin = new Thickness(0,0,8,4) };
        separator.Click += async (_,_) => {
            if (ViewModel is { LiveProfile: { } profile } model)
                await profile.CreateSeparator(model.Adapter.SelectedModels.Count == 1 ? model.Adapter.SelectedModels.Single().Key : null);
        };
        ToolTip.SetTip(separator,"Create an MO2 separator above the selected mod, or at the end of the list");
        filterRow.Children.Add(separator);
        filterRow.Children.Add(filters); filterRow.Children.Add(priority); filterRow.Children.Add(setPriority);
        var modsGrid = (Grid)native.FindControl<TabItem>("ModsTabItem")!.Content!;
        modsGrid.RowDefinitions.Insert(2,new RowDefinition(GridLength.Auto));
        foreach (var child in modsGrid.Children.Where(x => Grid.GetRow(x) >= 2).ToArray()) Grid.SetRow(child,Grid.GetRow(child)+1);
        Grid.SetRow(filterRow,2); modsGrid.Children.Add(filterRow);
        filters.SelectionChanged += (_,_) => (ViewModel?.Adapter as Mo2ModsAdapter)?.SetFilter(filters.SelectedIndex);
        setPriority.Click += async (_,_) => {
            if (ViewModel is { LiveProfile: { } profile } model && model.Adapter.SelectedModels.Count == 1 && int.TryParse(priority.Text, out var value))
                await ((Mo2ModsAdapter)model.Adapter).Move(model.Adapter.SelectedModels.Single().Key, value, absolute: true);
        };
        this.WhenActivated(disposables => {
            this.Bind(ViewModel, vm => vm.Mo2SearchText, view => view.SearchBox.Text).AddTo(disposables);
            this.Bind(ViewModel, vm => vm.Mo2SearchExpanded, view => view.SearchPanel.IsVisible).AddTo(disposables);
            this.OneWayBind(ViewModel, vm => vm, view => view.NativeView.ViewModel).AddTo(disposables);
            void Highlight() => Mo2RowHighlights.Apply(native, ViewModel!.LiveProfile!, plugins: false);
            EventHandler layout = (_,_) => Highlight();
            native.LayoutUpdated += layout;
            ViewModel!.LiveProfile!.HighlightsChanged += Highlight;
            System.Reactive.Disposables.Disposable.Create(() => { native.LayoutUpdated -= layout; ViewModel!.LiveProfile!.HighlightsChanged -= Highlight; }).AddTo(disposables);
            ViewModel!.SelectedSubTab = LoadoutPageSubTabs.Mods;
            SubTabs.SelectedIndex = 0;
            void UpdateSelection() {
                    var count = ViewModel.Adapter.SelectedModels.Count;
                    ViewModel.LiveProfile!.HighlightMods(ViewModel.Adapter.SelectedModels.Select(x => x.Key));
                    var selected = ViewModel.Adapter.SelectedModels.Select(x => x.Key).ToHashSet();
                    var mods = ViewModel.LiveProfile!.Mods.Where(x => selected.Contains(x.Id)).ToArray();
                    setPriority.IsEnabled = count == 1 && mods.Length == 1 && mods[0].CanManage;
                    if (count == 1 && mods.Length == 1) priority.Text = Math.Max(0,mods[0].Priority).ToString();
                    native.FindControl<StandardButton>("ViewFilesButton")!.IsEnabled = count == 1;
                    native.FindControl<StandardButton>("DeleteButton")!.IsEnabled = mods.Any(x => x.CanManage);
            }
            ViewModel!.Adapter.SelectedModels.ObserveChanged().Subscribe(_ => UpdateSelection()).AddTo(disposables);
            UpdateSelection();
        });
    }
}
