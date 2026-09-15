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
using Avalonia.Platform.Storage;
using Avalonia.LogicalTree;
using Avalonia.Input;
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
    private Mo2ProfileTarget? _dragTarget;
    private string[] _dragOrder = [];
    private EntityId[] _dragIds = [];
    public override void OnRowDragStarted(object? sender, TreeDataGridRowDragStartedEventArgs e)
    {
        _dragIds = e.Models.OfType<CompositeItemModel<EntityId>>().Select(x => x.Key).ToArray();
        _dragTarget = null;
        if (!_profile.CanChangeOriginalUi || _dragIds.Length == 0 || _dragIds.Any(id => _profile.FindMod(id)?.CanManage != true)) {
            e.AllowedEffects = DragDropEffects.None; return;
        }
        _dragTarget = _profile.CurrentTarget; _dragOrder = _profile.ModPriorityOrder;
        e.AllowedEffects = DragDropEffects.Move;
    }
    private bool CanDrop(TreeDataGridRowDragEventArgs e) => _dragTarget == _profile.CurrentTarget &&
        _profile.CanChangeOriginalUi && e.Inner.Data.Get("TreeDataGridDragInfo") is DragInfo info && ReferenceEquals(info.Source, Source.Value);
    public override void OnRowDragOver(object? sender, TreeDataGridRowDragEventArgs e)
    {
        if (!CanDrop(e)) { e.Position = TreeDataGridRowDropPosition.None; e.Inner.DragEffects = DragDropEffects.None; return; }
        e.Position = e.Inner.GetPosition(e.TargetRow).Y < e.TargetRow.Bounds.Height / 2 ? TreeDataGridRowDropPosition.Before : TreeDataGridRowDropPosition.After;
    }
    public override async void OnRowDrop(object? sender, TreeDataGridRowDragEventArgs e)
    {
        e.Handled = true;
        if (!CanDrop(e) || e.TargetRow.Model is not CompositeItemModel<EntityId> target || e.Position == TreeDataGridRowDropPosition.None) return;
        var ids = _dragIds; var context = _dragTarget!.Value; var baseline = _dragOrder;
        _dragTarget = null;
        await _profile.MoveModsRelative(ids, target.Key, e.Position == TreeDataGridRowDropPosition.After, context, baseline);
    }
    private readonly Mo2LiveProfile _profile;
    private int _filterChoice;
    private readonly HashSet<string> _collapsed = new(StringComparer.Ordinal);
    private readonly System.Reactive.Subjects.BehaviorSubject<Func<Mo2LiveMod,bool>> _filter = new(_ => true);
    public static readonly ComponentKey StateKey = ComponentKey.From("MO2.State");
    public int VisibleRowCount => Roots.Count;
    public void RefreshFilter() => SetFilter(_filterChoice);
    public void SetFilter(int choice) {
        _filterChoice = choice;
        var hidden = new HashSet<EntityId>();
        var collapsed = false;
        foreach (var mod in _profile.Mods.OrderBy(x => x.Priority)) {
            if (mod.IsSeparator) collapsed = _collapsed.Contains(mod.Name);
            else if (collapsed) hidden.Add(mod.Id);
        }
        _filter.OnNext(mod => mod.IsSeparator || (!hidden.Contains(mod.Id) && mod.DisplayName.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase) && mod.Version.Contains(_versionFilter, StringComparison.OrdinalIgnoreCase) && mod.Category.Contains(_categoryFilter, StringComparison.OrdinalIgnoreCase) && (_endorsementFilter == 0 || (mod.NexusId > 0 && ((mod.State & 0x10) != 0) == (_endorsementFilter == 1))) && (choice switch {
            1 => (mod.State & 2) != 0, 2 => (mod.State & 2) == 0 && (mod.State & 4) == 0,
            3 => mod.Conflicts.Length > 0, 4 => mod.Conflicts.Length == 0, _ => true
        })));
    }
    private Mo2ProfileTarget? _presentationTarget;
    protected override IObservable<IChangeSet<CompositeItemModel<EntityId>,EntityId>> GetRootsObservable(bool viewHierarchical)
        => _profile.ObserveFilteredMods(_filter, _presentationTarget ??= _profile.CurrentTarget);
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
    internal bool IsCollapsed(string name) => _collapsed.Contains(name);
    internal void ToggleSeparator(string name) { if (!_collapsed.Add(name)) _collapsed.Remove(name); RefreshFilter(); }
    internal void RenameSeparator(string oldName, string newName) { if (_collapsed.Remove(oldName)) _collapsed.Add(newName); }
    internal int SeparatorCount(Mo2LiveMod separator) => _profile.Mods.OrderBy(m => m.Priority).SkipWhile(m => m.Id != separator.Id).Skip(1).TakeWhile(m => !m.IsSeparator && !m.IsOverwrite).Count();
    public void SetColumnFilters(string name, string version, string category, int endorsed = 0) {
        _nameFilter = name; _versionFilter = version; _categoryFilter = category; _endorsementFilter = endorsed; RefreshFilter();
    }
    private string _nameFilter = "", _versionFilter = "", _categoryFilter = "";
    private int _endorsementFilter;
    protected override IColumn<CompositeItemModel<EntityId>>[] CreateColumns(bool viewHierarchical) => [
        new TemplateColumn<CompositeItemModel<EntityId>>("Mods", new FuncDataTemplate<CompositeItemModel<EntityId>>((item, _) => {
            if (item is null || _profile.FindMod(item.Key) is not { } mod) return new TextBlock();
            Mo2UiLatencyProbe.Row("Mods", this, item, mod.Name);
            return new Mo2DeferredRow(() => Mo2ModRow.Create(_profile, this, mod));
        }), width: new GridLength(1, GridUnitType.Star))
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
        Mo2UiLatencyProbe.Count("Mods views created");
        var native = NativeView;
        Content = native;
        native.FindControl<TabItem>("RulesTabItem")!.IsVisible = false;
        SubTabs.SelectedIndex = 0;
        // With Rules hidden there is one tab left, so the strip is just a label above
        // the table. Hiding the remaining tab header is the same mechanism that
        // already hides Rules; the selected content still renders below.
        native.FindControl<TabItem>("ModsTabItem")!.IsVisible = false;
        SubTabs.Margin = new Thickness(0);
        var header = native.FindControl<NexusMods.App.UI.Controls.PageHeader.PageHeader>("AllPageHeader")!;
        header.Title = "My Mods";
        header.Icon = IconValues.PictogramCollection3D;
        header.Description = "Installed mods in the selected MO2 profile.";
        var empty = native.FindControl<EmptyState>("EmptyState")!;
        if (empty.Subtitle is StackPanel subtitle)
            subtitle.Children.OfType<TextBlock>().First().Text = "Install mods from your MO2 downloads folder.";
        native.FindControl<StandardButton>("ViewLibraryButton")!.Text = "Downloads";
        ToolTip.SetTip(native.FindControl<StandardButton>("ViewFilesButton")!, "View this mod’s files and conflicts in MO2");
        // The same group Plugins shows, built from the native view's own buttons so
        // both pages draw one component rather than two that happen to look alike.
        var nativeGroup = native.FindControl<ItemsControl>("ContextControlGroup")!;
        var group = Mo2SelectionGroup.Adopt(nativeGroup, out var groupDeselect,
            () => native.FindControl<TreeDataGrid>("TreeDataGrid")?.RowSelection?.Clear());
        // The emptied original takes the shared group's place rather than lingering
        // beside it: left in the toolbar it was still an item, so the two pages'
        // toolbars held a different number of things.
        if (nativeGroup.Parent is Panel groupOwner) {
            var at = groupOwner.Children.IndexOf(nativeGroup);
            groupOwner.Children.Remove(nativeGroup);
            groupOwner.Children.Insert(Math.Max(0, at), group);
        } else if (nativeGroup.Parent is ItemsControl groupHost) {
            var at = groupHost.Items.IndexOf(nativeGroup);
            groupHost.Items.Remove(nativeGroup);
            groupHost.Items.Insert(Math.Max(0, at), group);
        }
        var separator = new MenuItem { Name = "CreateSeparatorMenuItem", Header = "Add separator…" };
        separator.Click += async (_,_) => {
            if (ViewModel is { LiveProfile: { } profile } model)
                await model.CreateSeparatorDialog(model.Adapter.SelectedModels.Count == 1 ? model.Adapter.SelectedModels.Single().Key : null);
        };
        var install = new MenuItem { Name = "AddModMenuItem", Header = "Add mod from archive…" };
        install.Click += async (_,_) => {
            if (ViewModel?.LiveProfile is not { } profile || TopLevel.GetTopLevel(this) is not { } window) return;
            var target = profile.CurrentTarget;
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = "Install mod archive", AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Mod archives") { Patterns = ["*.zip", "*.7z", "*.rar", "*.fomod"] }]
            });
            if (files.FirstOrDefault()?.TryGetLocalPath() is { } path) await profile.InstallArchive(path, target);
        };
        var menu = new MenuFlyout();
        menu.Items.Add(install); menu.Items.Add(separator); menu.Items.Add(new Separator());
        foreach (var (label, step) in new[] { ("Move earlier", -1), ("Move later", 1) }) {
            var move = new MenuItem { Header = label };
            move.Click += async (_, _) => {
                if (ViewModel is { } model && model.Adapter.SelectedModels.Count == 1)
                    await ((Mo2ModsAdapter)model.Adapter).Move(model.Adapter.SelectedModels.Single().Key, step);
            };
            menu.Items.Add(move);
        }

        var overflow = Mo2ModRow.IconButton("mdi-dots-vertical", "Mod actions", () => { });
        overflow.Name = "ModsOverflowButton";
        overflow.Flyout = menu;
        menu.Items.Add(new Separator());
        Mo2OrderHistoryMenu.Add(menu, () => ViewModel?.LiveProfile, "mods");
        var toolbar = native.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!.GetLogicalAncestors().OfType<Toolbar>().First();
        Mo2ListToolbar.Wrap(toolbar);
        Mo2ListToolbar.AddSearch(toolbar, native.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!);
        var primaryGroup = Mo2ListToolbar.Pill("ModPrimaryActions",
            Mo2ModRow.IconButton("mdi-plus-circle-outline", "Add mod from archive…",
                () => install.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent))),
            overflow);
        toolbar.Items.Insert(0, primaryGroup);
        // The native view already grows a selection group once rows are selected: a
        // deselect action labelled with the count, then the actions that make sense in
        // bulk. A second group of our own beside it meant two X buttons and the count
        // twice over. Its deselect goes through the adapter's own selection model,
        // which does not empty the models the toolbar counts, so it is re-pointed at
        // the grid's row selection — the model the adapter actually listens to.
        var deselect = native.FindControl<StandardButton>("DeselectItemsButton")!;
        deselect.Command = ReactiveUI.ReactiveCommand.Create(
            () => native.FindControl<TreeDataGrid>("TreeDataGrid")?.RowSelection?.Clear());
        native.FindControl<StandardButton>("ViewFilesButton")!.ShowLabel = false;
        native.FindControl<StandardButton>("DeleteButton")!.ShowLabel = false;
        var table = native.FindControl<TreeDataGrid>("TreeDataGrid")!;
        var listContainer = native.FindControl<EmptyState>("EmptyState")!;
        listContainer.Content = null;
        // The shared padding reaches this page's header through the native view's own
        // panel, but not its table, so the rows sat hard against the panel edge while
        // Plugins — whose whole page is inset — started 24px in. Both ends: the rail
        // beside the rows used to run into the right-hand padding the header line
        // respects, which ended this table 24px past where Plugins ends.
        // The native tab control held 24px of space under a toolbar that now lives on
        // the header line, which started this table 24px lower than Plugins starts its
        // own. Nothing sits in that space any more.
        var rulesTabs = native.FindControl<TabControl>("RulesTabControl")!;
        rulesTabs.Margin = new Thickness(0);
        // A tab control rules a line across the top of its content to tie the selected
        // tab to what it shows. This page draws no tab strip, so that line had nothing
        // to tie and read as a separator above the column headings — one Plugins has no
        // equivalent of, because it has no tab control of its own. The separator this
        // page does own is the one under its header, and the panel chrome draws that.
        // Set as a style because the content host is a template part, and with the
        // theme's own selector so it replaces that rule rather than racing it.
        rulesTabs.Styles.Add(new Avalonia.Styling.Style(x => Avalonia.Styling.Selectors.Name(
            Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Presenters.ContentPresenter>(
                Avalonia.Styling.Selectors.Template(Avalonia.Styling.Selectors.OfType<TabControl>(x))),
            "PART_SelectedContentHost")) {
            Setters = { new Avalonia.Styling.Setter(
                Avalonia.Controls.Presenters.ContentPresenter.BorderThicknessProperty, new Thickness(0)) },
        });
        // And the same rows presenter inset Plugins carries, so the first row of each
        // table sits the same distance under its column headings. Applied as a style
        // because the presenter is a template part that does not exist yet.
        native.Styles.Add(new Avalonia.Styling.Style(x =>
            Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRowsPresenter>(x)) {
            Setters = { new Avalonia.Styling.Setter(Avalonia.Layout.Layoutable.MarginProperty, new Thickness(0)) },
        });
        var list = new Grid { ColumnDefinitions = new ColumnDefinitions($"*,{Mo2TableRow.RailWidth}"), RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(Mo2PanelChrome.Padding, 0, Mo2PanelChrome.Padding, Mo2PanelChrome.Padding) };
        var columns = Mo2ModRow.Columns(); columns.Margin = new Thickness(0,8,0,6); columns.Height = Mo2TableRow.HeadingBand;
        void Heading(string label, int column) => Mo2ModRow.Add(columns, Mo2TableRow.Heading(label), column);
        Heading("Status", 1); Heading("Mod name", 2); Heading("Version", 3); Heading("Category", 4); Heading("Endorsed", 5); Heading("Actions", 6);
        columns.LayoutUpdated += (_, _) => Mo2ModRow.Fit(columns, this.GetVisualAncestors().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().FirstOrDefault()?.Bounds.Width ?? Bounds.Width);
        var help = new StandardButton { Name = "ModsHelpButton", ShowLabel = false, ShowIcon = StandardButton.ShowIconOptions.Left,
            LeftIcon = IconValues.HelpOutline, Type = StandardButton.Types.Tertiary, Fill = StandardButton.Fills.None, Size = StandardButton.Sizes.Medium,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        ToolTip.SetTip(help, "Mod priority help");
        help.Flyout = new Flyout { Content = new TextBlock { Text = "Drag mods to change their priority. Mods lower in the list win file conflicts. Select a mod to highlight its conflicts and linked plugins. Use separators to group mods; click the arrow to collapse a group, or double-click its name to rename it.", TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 280 } };
        Grid.SetColumn(help, 1); list.Children.Add(help);
        // Vortex moves the toolbar onto the header's right side. The shared chrome
        // gives this page the same separator and animated compaction as the others.
        // The whole Toolbar moves onto the header line, search included. Moving its
        // children individually fails: it is an ItemsControl and regenerates them.
        if (toolbar.Parent is Panel toolbarOwner) toolbarOwner.Children.Remove(toolbar);
        toolbar.Margin = new Thickness(0);
        var modColumns = Mo2TableRow.ColumnsButton(Mo2ModRow.OptionalColumns, Mo2ModRow.HiddenColumns, columns, Mo2ModColumnPreference.Save);
        Mo2ModColumnPreference.Load();
        Mo2PanelChrome.Apply(this, (Panel)header.Parent!, header, modColumns, toolbar);
        // The native table is pushed 8px below its own headings, which it no longer
        // draws — the column headings above it are this page's. Plugins has none, and
        // the two tables' first rows sat 8px apart because of it.
        table.Margin = new Thickness(0);
        list.Children.Add(columns); table.ShowColumnHeaders = false; Grid.SetRow(table, 1);
        Mo2TableRow.InstallRowStyles(table);
        var rail = Mo2ListRail.Create("ModsRailScrollBar", out var scroll);
        Grid.SetColumn(rail, 1); Grid.SetRow(rail, 1); list.Children.Add(table); list.Children.Add(rail);
        listContainer.Content = list;
        Mo2ListRail.Connect(scroll, table);
        table.AutoDragDropRows = true;
        table.CanUserSortColumns = false;
        table.RowDragStarted += (sender, args) => ViewModel?.Adapter.OnRowDragStarted(sender, args);
        table.RowDragOver += (sender, args) => ViewModel?.Adapter.OnRowDragOver(sender, args);
        table.RowDrop += (sender, args) => ViewModel?.Adapter.OnRowDrop(sender, args);


        this.WhenActivated(disposables => {
            void SeparatorRenamed(string endpoint, string oldName, string newName) {
                if (endpoint == ViewModel!.LiveProfile!.Endpoint) ((Mo2ModsAdapter)ViewModel.Adapter).RenameSeparator(oldName, newName);
            }
            ViewModel!.LiveProfile!.CollectionRenamed += SeparatorRenamed;
            System.Reactive.Disposables.Disposable.Create(() => ViewModel!.LiveProfile!.CollectionRenamed -= SeparatorRenamed).AddTo(disposables);
            this.Bind(ViewModel, vm => vm.Mo2SearchText, view => view.SearchBox.Text).AddTo(disposables);
            this.Bind(ViewModel, vm => vm.Mo2SearchExpanded, view => view.SearchPanel.IsVisible).AddTo(disposables);
            this.OneWayBind(ViewModel, vm => vm, view => view.NativeView.ViewModel).AddTo(disposables);
            Mo2SearchQuery.Attach(native.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!, ViewModel!.LiveProfile!, plugins: false).AddTo(disposables);
            Mo2RowHighlights.Attach(native, table, ViewModel!.LiveProfile!, plugins: false).AddTo(disposables);
            void RefreshMods() {
                var model = ViewModel!;
                ((Mo2ModsAdapter)model.Adapter).RefreshFilter();
                header.Title = "My Mods";
                model.SetCollectionTitle(header.Title);
                native.FindControl<Statusbar>("Statusbar")!.IsVisible = false;
                native.FindControl<MenuItem>("MenuItemRenameCollection")!.IsVisible = false;
                native.FindControl<MenuItem>("MenuItemDeleteCollection")!.IsVisible = false;
                header.IsVisible = true;
                native.FindControl<Control>("WritableCollectionPageHeader")!.IsVisible = false;
                // As long as the Plugins one, so the two headers wrap to the same
                // number of lines and their tables start at the same height when the
                // panels sit side by side. What this used to also say about separators
                // is in the priority help beside the column headings.
                header.Description = "Installed mods in priority order. Drag to reorder.";
                separator.IsEnabled = install.IsEnabled = model.LiveProfile!.CanChangeOriginalUi;
            }
            ViewModel!.LiveProfile!.Changed += RefreshMods;
            System.Reactive.Disposables.Disposable.Create(() => ViewModel!.LiveProfile!.Changed -= RefreshMods).AddTo(disposables);
            RefreshMods();
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshMods);
            ViewModel!.SelectedSubTab = LoadoutPageSubTabs.Mods;
            SubTabs.SelectedIndex = 0;
            void UpdateSelection() {
                    var count = ViewModel.Adapter.SelectedModels.Count;
                    Mo2SelectionGroup.Update(group, groupDeselect, count);
                    ViewModel.LiveProfile!.HighlightMods(ViewModel.Adapter.SelectedModels.Select(x => x.Key));
                    var selected = ViewModel.Adapter.SelectedModels.Select(x => x.Key).ToHashSet();
                    var mods = ViewModel.LiveProfile!.Mods.Where(x => selected.Contains(x.Id)).ToArray();
                    native.FindControl<StandardButton>("ViewFilesButton")!.IsEnabled = count == 1;
                    native.FindControl<StandardButton>("DeleteButton")!.IsEnabled = mods.Any(x => x.CanManage);
            }
            ViewModel!.Adapter.SelectedModels.ObserveChanged().Subscribe(_ => UpdateSelection()).AddTo(disposables);
            UpdateSelection();
        });
    }
}
