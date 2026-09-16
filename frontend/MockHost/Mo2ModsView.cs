using ObservableCollections;
using DynamicData;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using Avalonia.Platform.Storage;
using Avalonia.LogicalTree;
using Avalonia.Input;
using Avalonia.Media;
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
        CustomSortComparer.Value = Comparer<CompositeItemModel<EntityId>>.Create(Compare);
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
    // The order the list is actually in, for the checks that drive the grouping box:
    // the rows themselves are the adapter's own, and a check cannot reach them.
    public string[] VisibleOrder => Roots.Select(x => x.Key.ToString()).ToArray();
    public void RefreshFilter() => SetFilter(_filterChoice);
    public void SetFilter(int choice) {
        _filterChoice = choice;
        var hidden = new HashSet<EntityId>();
        var collapsed = false;
        foreach (var mod in _profile.Mods.OrderBy(x => x.Priority)) {
            if (mod.IsSeparator) collapsed = _collapsed.Contains(mod.Name);
            else if (collapsed) hidden.Add(mod.Id);
        }
        // MO2 lets a mod carry several categories; the column it reports them in lists
        // them together, so a mod's categories are what that text separates.
        bool InChosenCategories(Mo2LiveMod mod) {
            if (_categoryChoice.Count == 0) return true;
            var owned = mod.Category.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return _matchAllCategories
                ? _categoryChoice.All(chosen => owned.Contains(chosen, StringComparer.OrdinalIgnoreCase))
                : owned.Any(_categoryChoice.Contains);
        }
        bool Wanted(Mo2LiveMod mod) => !hidden.Contains(mod.Id) && InChosenCategories(mod) &&
            mod.DisplayName.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase) &&
            mod.Version.Contains(_versionFilter, StringComparison.OrdinalIgnoreCase) &&
            mod.Category.Contains(_categoryFilter, StringComparison.OrdinalIgnoreCase) &&
            (_endorsementFilter == 0 || (mod.NexusId > 0 && ((mod.State & 0x10) != 0) == (_endorsementFilter == 1))) &&
            choice switch {
                1 => (mod.State & 2) != 0, 2 => (mod.State & 2) == 0 && (mod.State & 4) == 0,
                3 => mod.Conflicts.Length > 0, 4 => mod.Conflicts.Length == 0, _ => true
            };
        _filter.OnNext(mod => mod.IsSeparator
            // A separator marks a place in the priority order, so a grouping that
            // replaces that order stands them down, as MO2's own does.
            ? _grouping == 0 && _separators switch {
                1 => true, 2 => false,
                _ => mod.DisplayName.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase) }
            : Wanted(mod));
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
    // MO2's Collapse all and Expand all, which act on every separator at once rather
    // than on the one whose chevron was pressed.
    internal void SetAllSeparators(bool collapsed)
    {
        _collapsed.Clear();
        if (collapsed) foreach (var mod in _profile.Mods.Where(x => x.IsSeparator)) _collapsed.Add(mod.Name);
        RefreshFilter();
    }
    internal void RenameSeparator(string oldName, string newName) { if (_collapsed.Remove(oldName)) _collapsed.Add(newName); }
    internal int SeparatorCount(Mo2LiveMod separator) => _profile.Mods.OrderBy(m => m.Priority).SkipWhile(m => m.Id != separator.Id).Skip(1).TakeWhile(m => !m.IsSeparator && !m.IsOverwrite).Count();
    public void SetColumnFilters(string name, string version, string category, int endorsed = 0) {
        _nameFilter = name; _versionFilter = version; _categoryFilter = category; _endorsementFilter = endorsed; RefreshFilter();
    }

    // MO2's groupCombo: the mod list ordered by installation priority, by category or
    // by Nexus ID. MO2 draws a parent row per group; this list is flat, so a grouping
    // orders the rows into their groups and stands the separators down — they belong
    // to the priority order, which a grouping replaces.
    private int _grouping;
    public int Grouping => _grouping;
    public void SetGrouping(int choice) {
        if (_grouping == choice) return;
        _grouping = choice;
        CustomSortComparer.Value = Comparer<CompositeItemModel<EntityId>>.Create(Compare);
        RefreshFilter();
    }
    private string GroupOf(CompositeItemModel<EntityId> item) {
        var mod = _profile.FindMod(item.Key);
        return mod is null ? "" : _grouping == 1 ? mod.Category : mod.NexusId > 0 ? mod.NexusId.ToString("D9") : "";
    }
    private int Compare(CompositeItemModel<EntityId> a, CompositeItemModel<EntityId> b)
    {
        if (_grouping != 0) {
            var group = string.Compare(GroupOf(a), GroupOf(b), StringComparison.OrdinalIgnoreCase);
            if (group != 0) return group;
        }
        return a.Get<ValueComponent<int>>(PriorityKey).Value.Value.CompareTo(b.Get<ValueComponent<int>>(PriorityKey).Value.Value);
    }

    // MO2's filtersAnd/filtersOr: whether a mod has to carry every ticked category or
    // only one of them. Its own default is And.
    private bool _matchAllCategories = true;
    public void SetCategoryMatchAll(bool all) { _matchAllCategories = all; RefreshFilter(); }

    // MO2's filtersSeparators: separators subject to the filter, always shown, or
    // never shown.
    private int _separators;
    public void SetSeparatorMode(int mode) { _separators = mode; RefreshFilter(); }
    private string _nameFilter = "", _versionFilter = "", _categoryFilter = "";
    private int _endorsementFilter;
    // The categories ticked in MO2's filter list. Empty means no category filter at
    // all rather than "no category matches", which would empty the list.
    private readonly HashSet<string> _categoryChoice = new(StringComparer.OrdinalIgnoreCase);
    public void SetCategoryChoice(IEnumerable<string> categories) {
        _categoryChoice.Clear();
        foreach (var category in categories) _categoryChoice.Add(category);
        RefreshFilter();
    }
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
    // The archives MO2 installs from, which is what it accepts on a drop.
    internal static bool IsArchive(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".7z" or ".rar" or ".fomod";

    // MO2's own two, out of ModListView::onModFilterActive: #f00 around a filtered
    // list and its count, #337733 around a grouped one.
    internal static readonly IBrush FilteredInk = new SolidColorBrush(Color.FromRgb(0xff, 0x00, 0x00));
    internal static readonly IBrush GroupedInk = new SolidColorBrush(Color.FromRgb(0x33, 0x77, 0x33));

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
        // The pane around the list carries the page's padding, not the list alone:
        // MO2's widgets above and below the list line up with it, and a list that was
        // the only thing inset left the profile box and the counter beside it running
        // into the panel's edges.
        var list = new Grid { ColumnDefinitions = new ColumnDefinitions($"*,{Mo2TableRow.RailWidth}"), RowDefinitions = new RowDefinitions("Auto,*") };
        var columns = Mo2ModRow.Columns(); columns.Margin = new Thickness(0,4,0,4); columns.Height = Mo2TableRow.HeadingBand;
        // MO2's own headers, taken from the same table the row's columns come from so
        // the two cannot describe different lists. The three glyph columns take the
        // glyph they hold as their heading, as MO2's own do — their names do not fit
        // in a column one icon wide and were drawn as "C…" and "Fl…".
        foreach (var (column, label) in Mo2ModRow.Headers)
            Mo2ModRow.Add(columns, column switch {
                Mo2ModRow.Conflicts => Mo2TableRow.GlyphHeading("mdi-swap-vertical-bold", label),
                Mo2ModRow.Flags => Mo2TableRow.GlyphHeading("mdi-flag", label),
                Mo2ModRow.Content => Mo2TableRow.GlyphHeading("mdi-package-variant-closed", label),
                _ => Mo2TableRow.Heading(label),
            }, column);
        columns.LayoutUpdated += (_, _) => Mo2ModRow.Fit(columns, this.GetVisualAncestors().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().FirstOrDefault()?.Bounds.Width ?? Bounds.Width);
        var help = new StandardButton { Name = "ModsHelpButton", ShowLabel = false, ShowIcon = StandardButton.ShowIconOptions.Left,
            LeftIcon = IconValues.HelpOutline, Type = StandardButton.Types.Tertiary, Fill = StandardButton.Fills.None, Size = StandardButton.Sizes.Medium,
            // Kept inside the heading band it shares. Left to its own height it made
            // that row taller than the headings in it, which is what started this
            // table's first row 3px below the one on Plugins.
            Height = Mo2TableRow.ActionSize, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
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
        // MO2's own mod pane (src/mainwindow.ui): a row of actions above the list, the
        // Filters group beside it, and the row of filters under it.
        Mo2ModsAdapter Model() => (Mo2ModsAdapter)ViewModel!.Adapter;
        Action? clearFilters = null;
        Border? categoriesRef = null;
        TextBox? filterField = null;
        ToggleButton? categoriesToggle = null;

        // Above the list: the profile box, then the buttons MO2 puts on the right of
        // that line and its count of active mods.
        var qtBar = new Grid { Name = "ModsQtBar", ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"),
            Margin = new Thickness(0,0,0,6) };
        var profileLabel = Mo2QtWidgets.Caption("ModsProfileLabel", Mo2QtWidgets.ProfileLabel);
        profileLabel.Margin = new Thickness(0,0,6,0);
        qtBar.Children.Add(profileLabel);
        // Filling the box from the profile that is open must not read as the user
        // picking one, or the frontend reselects the profile it is already on every
        // time the list is refreshed.
        var switching = false;
        ComboBox? profileBoxRef = null;
        var profileBox = Mo2QtWidgets.Choice("ModsProfileBox", Mo2QtWidgets.ProfileTip, [], 0, async choice => {
            if (switching || choice < 0 || ViewModel?.LiveProfile is not { } live) return;
            if (profileBoxRef?.SelectedItem is not string name) return;
            var entry = ViewModel.Instances().FirstOrDefault(x => x.Registration.Endpoint == live.Endpoint);
            if (entry?.Instance is null || name == live.CollectionName.Value) return;
            if (entry.Instance.Profiles.FirstOrDefault(x => x.Name == name) is { } wanted)
                await live.SelectProfile(entry.Registration, wanted);
        });
        profileBoxRef = profileBox;
        profileBox.MinWidth = 120;
        Grid.SetColumn(profileBox, 1); qtBar.Children.Add(profileBox);
        var barActions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 2,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        Grid.SetColumn(barActions, 3); qtBar.Children.Add(barActions);
        // MO2's listOptionsBtn: what to do with the list as a whole, and which of its
        // columns to draw.
        var listOptions = Mo2QtWidgets.Icon("ModsListOptionsButton", Mo2QtWidgets.ListOptionsTip, "mdi-cog-outline", () => { });
        var listOptionsMenu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        listOptions.Flyout = listOptionsMenu;
        listOptionsMenu.Opening += (_, _) => {
            listOptionsMenu.Items.Clear();
            var refresh = new MenuItem { Header = "Refresh" };
            refresh.Click += async (_, _) => { if (ViewModel?.LiveProfile is { } live) await live.Refresh(); };
            listOptionsMenu.Items.Add(refresh);
            foreach (var (label, enable) in new[] { ("Enable all", true), ("Disable all", false) }) {
                var item = new MenuItem { Header = label, IsEnabled = ViewModel?.LiveProfile?.CanChangeOriginalUi == true };
                var wanted = enable;
                item.Click += (_, _) => {
                    if (ViewModel?.LiveProfile is not { } live) return;
                    live.Toggle(live.Mods.Where(x => !x.IsSeparator && !x.IsOverwrite && x.CanManage && ((x.State & 2) != 0) != wanted)
                        .Select(x => NexusMods.Abstractions.Loadouts.LoadoutItemId.From(x.Id)).ToArray());
                };
                listOptionsMenu.Items.Add(item);
            }
            // This menu used to end in a tick per optional column. The page already
            // carries a column chooser on its header line, over the same list of
            // columns and the same set of hidden ones, writing the same preference
            // file — one chooser for one list, drawn twice, and MO2 puts none here
            // at all: its listOptionsBtn carries the mod list's global actions
            // (ModListGlobalContextMenu) and its columns are chosen from the list's
            // own header, as this frontend's Downloads does.
        };
        barActions.Children.Add(listOptions);
        // MO2's openFolderMenu, over the folders this frontend knows the way to.
        var openFolder = Mo2QtWidgets.Icon("ModsOpenFolderButton", Mo2QtWidgets.OpenFolderTip, "mdi-folder-open-outline", () => { });
        var folderMenu = new MenuFlyout { Placement = PlacementMode.BottomEdgeAlignedRight };
        openFolder.Flyout = folderMenu;
        folderMenu.Opening += (_, _) => {
            folderMenu.Items.Clear();
            var live = ViewModel?.LiveProfile;
            var entry = live is null ? null : ViewModel!.Instances().FirstOrDefault(x => x.Registration.Endpoint == live.Endpoint);
            foreach (var (label, path) in new[] {
                ("Open Profile folder", live is null ? null : Mo2InstanceCatalog.LocalPath(live.ProfilePath)),
                ("Open Mods folder", entry?.Instance is null ? null : Mo2InstanceCatalog.LocalPath(entry.Instance.ModsDirectory)),
                ("Open Downloads folder", live?.DownloadsDirectory is { } downloads ? Mo2InstanceCatalog.LocalPath(downloads) : null),
                ("Open Instance folder", entry?.Registration.Directory),
                ("Open Logs folder", live?.LogsDirectory is { } logs ? Mo2InstanceCatalog.LocalPath(logs) : null),
            }) {
                var item = new MenuItem { Header = label, IsEnabled = path is { Length: > 0 } && Directory.Exists(path) };
                var folder = path;
                item.Click += (_, _) => { if (folder is { Length: > 0 }) ViewModel?.OpenFolder(folder); };
                folderMenu.Items.Add(item);
            }
        };
        barActions.Children.Add(openFolder);
        // MO2's restoreModsButton and saveModsButton: the backups of the mod order it
        // keeps beside the list, which this frontend already reaches through MO2's own
        // buttons.
        var restoreMods = Mo2QtWidgets.Icon("RestoreModsButton", Mo2QtWidgets.RestoreModsTip, "mdi-backup-restore",
            async () => { if (ViewModel?.LiveProfile is { } live) await live.OrderBackup("mods", "restore", live.CurrentTarget); });
        var saveMods = Mo2QtWidgets.Icon("SaveModsButton", Mo2QtWidgets.SaveModsTip, "mdi-content-save-outline",
            async () => { if (ViewModel?.LiveProfile is { } live) await live.OrderBackup("mods", "backup", live.CurrentTarget); });
        barActions.Children.Add(restoreMods); barActions.Children.Add(saveMods);
        var activeMods = Mo2QtWidgets.Counter("ActiveModsCounter", out var activeModsValue);
        // MO2 rings the count in the same red it rings the list with while a filter is
        // on (ModListView::onModFilterActive), because a count of what is shown means
        // something different from a count of what there is. Kept at 2px whatever the
        // state so the bar does not move when a filter goes on and off.
        var countFrame = new Border { Name = "ModsCounterFrame", Child = activeMods, Margin = new Thickness(6,0,0,0),
            BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent, Padding = new Thickness(2,0),
            CornerRadius = new CornerRadius(3), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        barActions.Children.Add(countFrame);

        // Under the list: MO2's displayCategoriesBtn, what the list is filtered to,
        // the grouping box and the filter field.
        var filterBar = new Grid { Name = "ModsFilterBar", ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto"),
            Margin = new Thickness(0,6,0,0) };
        categoriesToggle = Mo2QtWidgets.Toggle("ModsDisplayCategoriesButton", Mo2QtWidgets.DisplayCategoriesTip,
            "mdi-filter-variant", true, on => { if (categoriesRef is not null) categoriesRef.IsVisible = on && Bounds.Width >= 420; });
        filterBar.Children.Add(categoriesToggle);
        var filterLabel = Mo2QtWidgets.Caption("ModsFilterLabel", Mo2QtWidgets.FilterLabel);
        filterLabel.Margin = new Thickness(6,0,4,0);
        Grid.SetColumn(filterLabel, 1); filterBar.Children.Add(filterLabel);
        var currentCategory = Mo2QtWidgets.Caption("ModsCurrentCategoryLabel", "");
        currentCategory.Opacity = .85;
        // In the row's stretching column, and trimmed: a long list of ticked
        // categories used to take whatever width it wanted and push the grouping box
        // and the filter field off the end of the pane.
        currentCategory.TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis;
        Grid.SetColumn(currentCategory, 2); filterBar.Children.Add(currentCategory);
        var clearAll = Mo2QtWidgets.Button("ModsClearFiltersButton", Mo2QtWidgets.ClearAllFilters, Mo2QtWidgets.ClearAllFilters,
            "mdi-filter-remove-outline", () => clearFilters?.Invoke());
        clearAll.IsVisible = false;
        Grid.SetColumn(clearAll, 3); filterBar.Children.Add(clearAll);
        var groupBox = Mo2QtWidgets.Choice("ModsGroupBox", "Group the mod list.", Mo2QtWidgets.GroupModes, 0,
            choice => Model().SetGrouping(choice));
        groupBox.Margin = new Thickness(6,0); groupBox.MinWidth = 96;
        Grid.SetColumn(groupBox, 4); filterBar.Children.Add(groupBox);
        var qtFilter = Mo2QtWidgets.Filter("ModsQtFilter", Mo2QtWidgets.ModFilterTip,
            text => Model().SetColumnFilters(text, "", ""), out filterField);
        qtFilter.MinWidth = 140;
        Grid.SetColumn(qtFilter, 5); filterBar.Children.Add(qtFilter);
        // MO2 draws these six on one line because its mod pane is the width of a
        // window. Here the pane is one panel of a workspace beside another, and with
        // the filter list showing the column under the list came out 216px against
        // the 305px the six of them ask for — so the stretching column between them
        // went to nothing and MO2's readout of what the list is narrowed to was
        // drawn 0px wide. Nothing had caught it: a control with no width is skipped
        // by the fit checks, which look for one that leaves its panel rather than
        // one squeezed out of it.
        //
        // Narrow, they take two lines rather than one of them taking none. The
        // order is still MO2's, read left to right and then down.
        void Reflow()
        {
            var room = filterBar.Bounds.Width;
            if (room <= 0) return;
            // What the row asks for on one line, measured rather than assumed, so
            // this follows the theme's own metrics instead of a number typed here.
            var wanted = new Control[] { categoriesToggle!, filterLabel, groupBox, qtFilter }
                .Sum(x => x.DesiredSize.Width) + 56;
            var stacked = room < wanted;
            if (stacked == filterBar.RowDefinitions.Count > 1) return;
            filterBar.RowDefinitions = new RowDefinitions(stacked ? "Auto,Auto" : "Auto");
            filterBar.ColumnDefinitions = new ColumnDefinitions(stacked ? "Auto,Auto,*,Auto" : "Auto,Auto,*,Auto,Auto,Auto");
            Grid.SetRow(groupBox, stacked ? 1 : 0); Grid.SetRow(qtFilter, stacked ? 1 : 0);
            Grid.SetColumn(groupBox, stacked ? 0 : 4); Grid.SetColumnSpan(groupBox, stacked ? 2 : 1);
            Grid.SetColumn(qtFilter, stacked ? 2 : 5); Grid.SetColumnSpan(qtFilter, stacked ? 2 : 1);
            groupBox.Margin = stacked ? new Thickness(0,4,6,0) : new Thickness(6,0);
            qtFilter.Margin = stacked ? new Thickness(0,4,0,0) : default;
        }
        filterBar.SizeChanged += (_,_) => Reflow();
        filterBar.AttachedToVisualTree += (_,_) => Reflow();

        // MO2's category filter sits beside the list, not above it, so the pane is a
        // column of categories and the list rather than the list alone. Its own two
        // rows of controls — Clear and Edit, then how the ticks are combined and what
        // to do with separators — sit inside the group, where MO2 puts them.
        var filterActions = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4,
            Margin = new Thickness(0,4,0,0) };
        filterActions.Children.Add(Mo2QtWidgets.Button("ModsFiltersClear", Mo2QtWidgets.FiltersClear, Mo2QtWidgets.FiltersClear,
            "mdi-filter-remove-outline", () => clearFilters?.Invoke()));
        // Edit... opens MO2's own category editor. The categories belong to MO2 —
        // this list only shows which ones the profile's mods carry — so the button
        // hands over to MO2 and the list is re-read when its dialog closes. It used
        // to hide the filter list instead, which is what the button beside the mod
        // list does.
        filterActions.Children.Add(Mo2QtWidgets.Button("ModsFiltersEdit", Mo2QtWidgets.FiltersEdit, Mo2QtWidgets.FiltersEditAction,
            "mdi-filter-cog-outline", async () => { if (ViewModel?.LiveProfile is { } live) await live.EditCategories(); }));
        // MO2 gives this row the group's full width and lets the separators box take
        // what the two radios leave (stretch 1,1,2). At the width a group beside the
        // list can have here, that box is left about 80px and its caption is drawn as
        // "Filte", so it takes the line under the radios instead.
        var filterOptions = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"), Margin = new Thickness(0,4,0,0) };
        var andRadio = Mo2QtWidgets.Radio("ModsFiltersAnd", Mo2QtWidgets.FiltersAnd, Mo2QtWidgets.FiltersAndTip,
            "ModCategoryMatch", true, on => { if (on) Model().SetCategoryMatchAll(true); });
        var orRadio = Mo2QtWidgets.Radio("ModsFiltersOr", Mo2QtWidgets.FiltersOr, Mo2QtWidgets.FiltersOrTip,
            "ModCategoryMatch", false, on => { if (on) Model().SetCategoryMatchAll(false); });
        orRadio.Margin = new Thickness(4,0,4,0);
        Grid.SetColumn(orRadio, 1);
        filterOptions.Children.Add(andRadio); filterOptions.Children.Add(orRadio);
        var separatorMode = Mo2QtWidgets.Choice("ModsFiltersSeparators", Mo2QtWidgets.SeparatorsTip, Mo2QtWidgets.SeparatorModes, 0,
            choice => Model().SetSeparatorMode(choice));
        separatorMode.MinWidth = 0;
        separatorMode.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        separatorMode.Margin = new Thickness(0,4,0,0);
        Grid.SetRow(separatorMode, 1); Grid.SetColumnSpan(separatorMode, 3);
        filterOptions.Children.Add(separatorMode);
        var categoriesGroup = Mo2QtWidgets.Categories("ModCategories", out var categoryItems, out _, filterActions, filterOptions);
        categoriesGroup.Width = 190;
        categoriesGroup.Margin = new Thickness(0,0,6,0);
        var chosenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void ApplyCategories() => ((Mo2ModsAdapter)ViewModel!.Adapter).SetCategoryChoice(chosenCategories);
        // Rebuilt from whatever categories the profile's mods actually carry, because
        // MO2 reports a mod's category as text rather than from a fixed list.
        void RefreshCategories() {
            if (ViewModel?.LiveProfile is not { } live) return;
            var counts = live.Mods.Where(x => !x.IsSeparator && x.Category.Length > 0)
                .GroupBy(x => x.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToArray();
            var shown = categoryItems.Items.OfType<CheckBox>().Select(x => (string)x.Tag!).ToArray();
            if (shown.SequenceEqual(counts.Select(x => x.Key), StringComparer.OrdinalIgnoreCase)) return;
            categoryItems.Items.Clear();
            foreach (var group in counts) {
                var category = group.Key;
                categoryItems.Items.Add(Mo2QtWidgets.Category(category, group.Count(), chosenCategories.Contains(category),
                    on => { if (on) chosenCategories.Add(category); else chosenCategories.Remove(category); ApplyCategories(); }));
            }
        }
        categoriesRef = categoriesGroup;
        // Clear empties every half of MO2's filter: the categories ticked here, the
        // text typed in the field below, and what the list is narrowed to.
        clearFilters = () => {
            chosenCategories.Clear();
            foreach (var box in categoryItems.Items.OfType<CheckBox>()) box.IsChecked = false;
            if (filterField is not null) filterField.Text = "";
            Model().SetColumnFilters("", "", "");
            ApplyCategories();
        };

        var modsTab = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(Mo2PanelChrome.Padding, 0, Mo2PanelChrome.Padding, Mo2PanelChrome.Padding) };
        Grid.SetColumnSpan(qtBar, 2); modsTab.Children.Add(qtBar);
        Grid.SetRow(categoriesGroup,1); modsTab.Children.Add(categoriesGroup);
        // MO2 rings the mod list itself while it is showing less than all of it: red
        // for a filter, green for a grouping, nothing otherwise. Without it a filter
        // left on looks exactly like mods that have gone missing, which is the whole
        // reason MO2 draws it. Its own is a 2px ridge; Avalonia has no ridge, so this
        // is 2px solid in MO2's two colours, and it is 2px when clear as well so the
        // list does not shift as the border comes and goes.
        // Drawn over the list rather than around it. Wrapping it moved the table 2px
        // down and right, and the mod and plugin tables are meant to start on the
        // same line — the row-padding check caught them 2px apart. Qt insets its own
        // viewport inside the frame, so a ridge costs it nothing either.
        var listFrame = new Border { Name = "ModsFilterFrame", BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent, CornerRadius = new CornerRadius(3), IsHitTestVisible = false };
        Grid.SetRow(list,1); Grid.SetColumn(list,1); modsTab.Children.Add(list);
        Grid.SetRow(listFrame,1); Grid.SetColumn(listFrame,1); modsTab.Children.Add(listFrame);
        Grid.SetRow(filterBar,2); Grid.SetColumn(filterBar,1); modsTab.Children.Add(filterBar);
        // The pane's own width, as of the last pass that changed it. Everything here
        // runs on every layout pass, and anything it writes asks for another one: the
        // filter group's width was recomputed from the pane each time, and between
        // that and the column fitting it depends on, the passes never stopped —
        // which starved the background work that builds the rows, so the lists drew
        // two mods and no plugins at all.
        var paneWidth = double.NaN;
        var toggleWas = true;
        // Whether the filter list should be beside the list right now: its button
        // says whether it is wanted, and the pane has to be wide enough to hold it.
        bool WantsCategories() => categoriesToggle.IsChecked == true && Bounds.Width >= 420;
        void FitPane()
        {
            // MO2 hides the category filter when there is no room for it beside the
            // list, and its own button decides whether it is wanted at all. Set at 620
            // this never showed in a half-width panel, which is the default layout — so
            // the filter was only ever there on a maximised window.
            if (categoriesGroup.IsVisible != WantsCategories()) categoriesGroup.IsVisible = WantsCategories();
            // MO2 puts this group behind a splitter, so it grows with the pane. A
            // fixed 176 left its separators box drawn as "Fi" on every pane width.
            var wanted = Math.Clamp(Math.Round(Bounds.Width * .3), 200, 260);
            if (Math.Abs(categoriesGroup.Width - wanted) > .5) categoriesGroup.Width = wanted;
            // Tell the columns how much of the pane the filter is using, so they fit
            // into what is left rather than into the whole panel.
            Mo2ModRow.SideWidth = categoriesGroup.IsVisible ? categoriesGroup.Width + 6 : 0;
        }
        categoriesToggle.IsCheckedChanged += (_, _) => FitPane();
        modsTab.LayoutUpdated += (_, _) => {
            RefreshCategories();
            // Only when the pane's width has actually changed. Everything in FitPane
            // writes something that asks for another layout pass, so running it on
            // every pass never let the passes stop — which starved the background
            // work that builds the rows, and the lists drew two mods and no plugins.
            // And whenever what is drawn disagrees with what should be. The pane's
            // width and the button were the only two things that brought FitPane
            // back, so a run that hid the filter list while the pane was briefly
            // narrow — the sidebar opening, a page still being laid out — left it
            // hidden for good once the width settled at a value already recorded.
            // Nothing is written while the two agree, so this still asks for no
            // layout pass of its own.
            if (double.IsNaN(paneWidth) || Math.Abs(paneWidth - Bounds.Width) > .5 ||
                toggleWas != (categoriesToggle.IsChecked == true) || categoriesGroup.IsVisible != WantsCategories()) {
                paneWidth = Bounds.Width; toggleWas = categoriesToggle.IsChecked == true;
                FitPane();
            }
            // What the list is narrowed to, which MO2 spells out beside the filter
            // field and offers to clear only while there is something to clear. Both
            // are written only when they change, so neither asks for a pass of its own.
            var narrowed = chosenCategories.Count > 0 ? string.Join(", ", chosenCategories.OrderBy(x => x)) : "";
            if ((filterField?.Text ?? "").Length > 0)
                narrowed = narrowed.Length > 0 ? narrowed + " · \"" + filterField!.Text + "\"" : "\"" + filterField!.Text + "\"";
            if (currentCategory.Text != narrowed) currentCategory.Text = narrowed;
            if (clearAll.IsVisible != narrowed.Length > 0) clearAll.IsVisible = narrowed.Length > 0;
            // MO2's own two: #f00 while a filter is on, #337733 while the list is
            // grouped and not filtered, nothing otherwise — and the count is ringed
            // only by the filter, as MO2 rings only its QLCDNumber.
            var wantedFrame = narrowed.Length > 0 ? FilteredInk
                : groupBox.SelectedIndex > 0 ? GroupedInk : Brushes.Transparent;
            if (!ReferenceEquals(listFrame.BorderBrush, wantedFrame)) listFrame.BorderBrush = wantedFrame;
            var wantedCount = narrowed.Length > 0 ? FilteredInk : (IBrush)Brushes.Transparent;
            if (!ReferenceEquals(countFrame.BorderBrush, wantedCount)) countFrame.BorderBrush = wantedCount;
        };
        listContainer.Content = modsTab;
        Mo2ListRail.Connect(scroll, table);
        table.AutoDragDropRows = true;
        table.CanUserSortColumns = false;
        table.RowDragStarted += (sender, args) => ViewModel?.Adapter.OnRowDragStarted(sender, args);
        table.RowDragOver += (sender, args) => ViewModel?.Adapter.OnRowDragOver(sender, args);
        table.RowDrop += (sender, args) => ViewModel?.Adapter.OnRowDrop(sender, args);
        // MO2 installs an archive dropped onto its mod list, which is how most mods
        // arrive: a file manager, a browser download, an archive on the desktop.
        DragDrop.SetAllowDrop(this, true);
        static string[] Archives(Avalonia.Input.IDataObject data) =>
            (data.GetFiles() ?? []).Select(x => x.TryGetLocalPath() ?? "")
                .Where(x => x.Length > 0 && Mo2ModsView.IsArchive(x)).ToArray();
        AddHandler(DragDrop.DragOverEvent, (_, e) => {
            e.DragEffects = ViewModel?.LiveProfile?.CanChangeOriginalUi == true && Archives(e.Data).Length > 0
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        });
        AddHandler(DragDrop.DropEvent, async (_, e) => {
            e.Handled = true;
            if (ViewModel?.LiveProfile is not { } live || !live.CanChangeOriginalUi) return;
            var target = live.CurrentTarget;
            foreach (var archive in Archives(e.Data)) await live.InstallArchive(archive, target);
        });

        // What MO2's own list does with a mod without going through a menu: opening a
        // mod is a double-click, the space bar switches it on and off, and Delete
        // takes it out — through the same dialog its menu entry uses, so the
        // confirmation is the same one.
        Mo2LiveMod[] Selected() {
            var keys = ViewModel?.Adapter.SelectedModels.Select(x => x.Key).ToHashSet() ?? [];
            return ViewModel?.LiveProfile?.Mods.Where(x => keys.Contains(x.Id)).ToArray() ?? [];
        }
        table.DoubleTapped += async (_, e) => {
            if (ViewModel?.LiveProfile is not { } live || Selected() is not [var mod]) return;
            e.Handled = true;
            if (mod.IsSeparator) await ViewModel.RenameSeparatorDialog(mod.Name);
            else await live.ShowModDetails(mod.Id);
        };
        table.KeyDown += (_, e) => {
            if (ViewModel?.LiveProfile is not { } live || Selected() is not { Length: > 0 } chosen) return;
            if (e.Key == Avalonia.Input.Key.Space) {
                e.Handled = true;
                live.Toggle(chosen.Where(x => !x.IsSeparator && x.CanManage)
                    .Select(x => NexusMods.Abstractions.Loadouts.LoadoutItemId.From(x.Id)).ToArray());
            } else if (e.Key == Avalonia.Input.Key.Delete) {
                e.Handled = true;
                live.Remove(chosen.Where(x => x.CanManage)
                    .Select(x => NexusMods.Abstractions.Loadouts.LoadoutItemId.From(x.Id)).ToArray());
            }
        };


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
                // MO2's own counter beside the list: how many mods the profile has
                // switched on.
                var live = model.LiveProfile!;
                // The same bits the row's own enable box is drawn from: MO2 counts a
                // mod it keeps switched on as active too, and counting only the bit a
                // profile sets reported one active mod out of twelve.
                activeModsValue.Text = live.Mods.Count(x => !x.IsSeparator && !x.IsOverwrite && (x.State & 6) != 0).ToString();
                restoreMods.IsEnabled = saveMods.IsEnabled = live.CanChangeOriginalUi;
                // And its profile box, over the profiles of the instance this profile
                // belongs to. Refilled only when the instance's profiles change, so a
                // refresh does not close a list the user has open.
                var entry = model.Instances().FirstOrDefault(x => x.Registration.Endpoint == live.Endpoint);
                var names = entry?.Instance?.Profiles.Select(x => x.Name).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
                switching = true;
                try {
                    if (!profileBox.Items.OfType<string>().SequenceEqual(names)) {
                        profileBox.Items.Clear();
                        foreach (var name in names) profileBox.Items.Add(name);
                    }
                    var current = live.CollectionName.Value;
                    if ((string?)profileBox.SelectedItem != current)
                        profileBox.SelectedIndex = Array.IndexOf(names, current);
                    profileBox.IsEnabled = names.Length > 1 && live.CanChangeOriginalUi;
                } finally { switching = false; }
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
