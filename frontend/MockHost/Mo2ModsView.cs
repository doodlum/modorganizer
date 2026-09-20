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
    internal static bool MatchesCategories(Mo2LiveMod mod, IEnumerable<string> chosen, bool all, IEnumerable<Mo2CategoryNode>? tree = null, IEnumerable<string>? excluded = null)
    {
        var categories = chosen.ToArray();
        var owned = mod.CategoryNames;
        HashSet<string> Names(string name) => tree is null
            ? new HashSet<string>([name], StringComparer.OrdinalIgnoreCase) : Mo2CategoryNode.Names(name, tree);
        var sets = categories.Select(x => (Names(x), false))
            .Concat((excluded ?? []).Select(x => (Names(x), true))).ToArray();
        return MatchesMembership(owned, sets, all);
    }
    private static bool MatchesMembership(string[] owned, (HashSet<string> Names, bool Inverted)[] sets, bool all) =>
        sets.Length == 0 || (all ? sets.All(set => owned.Any(set.Names.Contains) != set.Inverted)
            : sets.Any(set => owned.Any(set.Names.Contains) != set.Inverted));

    private ((int Type, int Id) Key, bool Inverted)[] _nativeCriteria = [];
    private IReadOnlyDictionary<(int Type, int Id), HashSet<string>>? _nativeMatches;
    internal void SetNativeCriteria(IEnumerable<(int Type, int Id)> include, IEnumerable<(int Type, int Id)> exclude,
        IReadOnlyDictionary<(int Type, int Id), HashSet<string>>? matches)
    {
        _categoryChoice.Clear(); _categoryExcluded.Clear();
        _nativeCriteria = include.Select(x => (x, false)).Concat(exclude.Select(x => (x, true))).ToArray();
        _nativeMatches = matches; RefreshFilter();
    }
    private bool NativeCriteriaMatch(Mo2LiveMod mod)
    {
        if (_nativeCriteria.Length == 0) return true;
        if (_nativeMatches is null || _nativeCriteria.Any(x => !_nativeMatches.ContainsKey(x.Key))) return false;
        bool Match(((int Type, int Id) Key, bool Inverted) criterion) => _nativeMatches[criterion.Key].Contains(mod.Name) != criterion.Inverted;
        return _matchAllCategories ? _nativeCriteria.All(Match) : _nativeCriteria.Any(Match);
    }

    public void RefreshFilter() => SetFilter(_filterChoice);
    public void SetFilter(int choice) {
        _filterChoice = choice;
        Seed();
        var hidden = new HashSet<EntityId>();
        var collapsed = false;
        foreach (var mod in _profile.Mods.OrderBy(x => x.Priority)) {
            if (mod.IsSeparator) collapsed = _collapsed.Contains(mod.Name);
            else if (collapsed) hidden.Add(mod.Id);
        }
        // Membership comes from MO2's grouping role; its display column contains
        // only the primary category, and category names may contain commas.
        var categorySets = _categoryChoice.Select(x => (Mo2CategoryNode.Names(x, _profile.CategoryTree), false))
            .Concat(_categoryExcluded.Select(x => (Mo2CategoryNode.Names(x, _profile.CategoryTree), true))).ToArray();
        bool InChosenCategories(Mo2LiveMod mod) => categorySets.Length == 0 || MatchesMembership(mod.CategoryNames, categorySets, _matchAllCategories);
        bool Wanted(Mo2LiveMod mod) => !hidden.Contains(mod.Id) && InChosenCategories(mod) && NativeCriteriaMatch(mod) &&
            mod.DisplayName.Contains(_nameFilter, StringComparison.OrdinalIgnoreCase) &&
            mod.Version.Contains(_versionFilter, StringComparison.OrdinalIgnoreCase) &&
            mod.Category.Contains(_categoryFilter, StringComparison.OrdinalIgnoreCase) &&
            (_endorsementFilter == 0 || (mod.NexusId > 0 && ((mod.State & 0x10) != 0) == (_endorsementFilter == 1))) &&
            choice switch {
                1 => (mod.State & 2) != 0, 2 => (mod.State & 2) == 0 && (mod.State & 4) == 0,
                3 => mod.Conflicts.Length > 0, 4 => mod.Conflicts.Length == 0, _ => true
            };
        var filterActive = categorySets.Length > 0 || _nativeCriteria.Length > 0 || choice != 0 ||
            _nameFilter.Length > 0 || _versionFilter.Length > 0 || _categoryFilter.Length > 0 || _endorsementFilter != 0;
        _filter.OnNext(mod => mod.IsSeparator
            // A separator marks a place in the priority order, so a grouping that
            // replaces that order stands them down, as MO2's own does.
            ? _grouping == 0 && (!filterActive || _separators switch {
                1 => true, 2 => false,
                // SeparatorFilter takes the same predicate path as ordinary mods
                // in ModListSortProxy::filterMatchesMod.
                _ => Wanted(mod) })
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
    internal void ToggleSeparator(string name) { if (!_collapsed.Add(name)) _collapsed.Remove(name); Remember(); RefreshFilter(); }

    // Seeded once the profile has answered, because what a first visit should show
    // depends on which separators exist. A profile switch reseeds: the collapsed set
    // belongs to the profile, as it does in MO2.
    private string _seeded = "";
    private void Seed()
    {
        var profile = _profile.ProfilePath;
        if (profile.Length == 0 || profile == _seeded) return;
        var separators = _profile.Mods.Where(x => x.IsSeparator).Select(x => x.Name).ToArray();
        if (separators.Length == 0 && _profile.Mods.Count == 0) return;
        _seeded = profile;
        _collapsed.Clear();
        foreach (var name in Mo2SeparatorState.Initial(profile, separators)) _collapsed.Add(name);
    }
    private void Remember()
    {
        if (_seeded.Length > 0) Mo2SeparatorState.Save(_seeded, _collapsed);
    }
    // MO2's Collapse all and Expand all, which act on every separator at once rather
    // than on the one whose chevron was pressed.
    internal void SetAllSeparators(bool collapsed)
    {
        _collapsed.Clear();
        if (collapsed) foreach (var mod in _profile.Mods.Where(x => x.IsSeparator)) _collapsed.Add(mod.Name);
        Remember();
        RefreshFilter();
    }
    // MO2's Collapse others: everything folded but the one the menu was opened on,
    // which is how a long list is read one section at a time. A row that is not a
    // separator folds everything except the section it sits in.
    internal void CollapseOthers(Mo2LiveMod row)
    {
        var ordered = _profile.Mods.OrderBy(x => x.Priority).ToArray();
        var keep = row.IsSeparator ? row.Name
            : ordered.TakeWhile(x => x.Id != row.Id).LastOrDefault(x => x.IsSeparator)?.Name;
        _collapsed.Clear();
        foreach (var mod in ordered.Where(x => x.IsSeparator && x.Name != keep)) _collapsed.Add(mod.Name);
        Remember();
        RefreshFilter();
    }
    internal void RenameSeparator(string oldName, string newName) { if (_collapsed.Remove(oldName)) { _collapsed.Add(newName); Remember(); } }
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
    private readonly HashSet<string> _categoryExcluded = new(StringComparer.OrdinalIgnoreCase);
    public void SetCategoryChoice(IEnumerable<string> categories, IEnumerable<string>? excluded = null) {
        _nativeCriteria = []; _nativeMatches = null;
        _categoryChoice.Clear(); _categoryExcluded.Clear();
        foreach (var category in excluded ?? []) _categoryExcluded.Add(category);
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
        // These collection controls never belong to an MO2 mod list. Remove them
        // before attachment, rather than waiting for the first profile refresh to
        // hide the upstream placeholder title and publishing toolbar. Upstream
        // bindings may still update their named controls, but cannot display them.
        foreach (var name in new[] { "WritableCollectionPageHeader", "Statusbar" }) {
            var unused = native.FindControl<Control>(name)!;
            if (unused.Parent is Panel collectionParent) collectionParent.Children.Remove(unused);
        }
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
        // The row of actions this page drew above its list is gone. Everything on it
        // that MO2 also offers, MO2 offers from its own widgets, and this page draws
        // those: Install mod... and Create separator from the list-options button
        // (MO2's listOptionsBtn, where its own global menu keeps them), the search
        // box from the filter field MO2 puts under the list (modFilterEdit), and
        // Move earlier/later from the row's own menu, which carries MO2's
        // Send to... Going with it: the selection group the original app grows when
        // rows are picked, which MO2 has no equivalent of — it works a selection
        // from the row menu, and so does this list.
        var toolbar = native.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!
            .GetLogicalAncestors().OfType<Toolbar>().First();
        if (toolbar.Parent is Panel owner) owner.Children.Remove(toolbar);
        else toolbar.IsVisible = false;
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
                Mo2ModRow.Name => Mo2TableRow.NameHeading(label),
                Mo2ModRow.Conflicts => Mo2TableRow.GlyphHeading("mdi-swap-vertical-bold", label),
                Mo2ModRow.Flags => Mo2TableRow.GlyphHeading("mdi-flag", label),
                Mo2ModRow.Content => Mo2TableRow.GlyphHeading("mdi-package-variant-closed", label),
                _ => Mo2TableRow.Heading(label),
            }, column);
        columns.LayoutUpdated += (_, _) => Mo2ModRow.Fit(columns, this.GetVisualAncestors().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().FirstOrDefault()?.Bounds.Width ?? Bounds.Width);
        // The shared chrome gives this page the same separator and animated
        // compaction as the others. Its action row holds the column chooser alone
        // now that the toolbar beside it is gone.
        // No column chooser. MO2 offers one on exactly one list — its downloads
        // header, which is the only view that sets Qt::CustomContextMenu on its
        // header (downloadlistview.cpp) — and its mod list has none at all: the
        // columns it starts with are set in code and then remembered from the
        // header's saved geometry. What columns this list draws still follows the
        // width it is given, as MO2's do.
        Mo2ModColumnPreference.Load();
        Mo2PanelChrome.Apply(this, (Panel)header.Parent!, header);
        // The native table is pushed 8px below its own headings, which it no longer
        // draws — the column headings above it are this page's. Plugins has none, and
        // the two tables' first rows sat 8px apart because of it.
        table.Margin = new Thickness(0);
        list.Children.Add(columns); table.ShowColumnHeaders = false; Grid.SetRow(table, 1);
        Mo2TableRow.InstallRowStyles(table);
        var rail = Mo2ListRail.Create("ModsRailScrollBar", out var scroll);
        Grid.SetColumn(rail, 1); Grid.SetRow(rail, 0); Grid.SetRowSpan(rail, 2); list.Children.Add(table); list.Children.Add(rail);
        // MO2's own mod pane (src/mainwindow.ui): a row of actions above the list, the
        // Filters group beside it, and the row of filters under it.
        Mo2ModsAdapter Model() => (Mo2ModsAdapter)ViewModel!.Adapter;
        Action? clearFilters = null;
        Action? updateFilterPresentation = null;
        Func<bool> hasActiveFilters = () => false;
        TextBox? filterField = null;
        var categoriesShown = false;
        Action? updateCategoryPane = null;
        var filterSummary = "";

        // Above the list: the buttons MO2 puts on the right of that line and its
        // count of active mods.
        var qtBar = Mo2QtWidgets.ActionBar("ModsQtBar");
        // MO2 heads this line with "Profile" and a box to change it in. Neither is
        // drawn here: this frontend has a page for the instances it is connected to
        // and the profiles in each, which is where a profile is chosen and where the
        // one in use is named. A second chooser on the mod pane offered the same
        // switch from a place that showed none of what it would switch to.
        var barActions = new List<Control>();
        // MO2's listOptionsBtn: what to do with the list as a whole, and which of its
        // columns to draw.
        var listOptions = Mo2QtWidgets.Icon("ModsListOptionsButton", "More mod actions", "mdi-dots-vertical", () => { });
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
            //
            // MO2's own global menu opens with Install mod... and Create separator,
            // which this page used to keep on a toolbar of its own above the list.
            // They are here now, which is where MO2 has them, and the toolbar is
            // gone.
            listOptionsMenu.Items.Add(new Separator());
            listOptionsMenu.Items.Add(install);
            listOptionsMenu.Items.Add(separator);
            listOptionsMenu.Items.Add(new Separator());
            Mo2OrderHistoryMenu.Add(listOptionsMenu, () => ViewModel?.LiveProfile, "mods");
            listOptionsMenu.Items.Add(new Separator());
            var categories = new MenuItem { Name = "ModsCategoriesMenuItem", Header = "Category filters",
                Icon = new CheckBox { IsChecked = categoriesShown, IsHitTestVisible = false, Focusable = false } };
            categories.Click += (_, _) => { categoriesShown = !categoriesShown; updateCategoryPane?.Invoke(); };
            listOptionsMenu.Items.Add(categories);
            var grouping = new MenuItem { Header = "Group by", Name = "ModsGroupingMenu" };
            for (var index = 0; index < Mo2QtWidgets.GroupModes.Length; index++) {
                var choice = index;
                var item = new MenuItem { Header = Mo2QtWidgets.GroupModes[index],
                    Icon = new CheckBox { IsChecked = Model().Grouping == index, IsHitTestVisible = false, Focusable = false } };
                item.Click += (_, _) => Model().SetGrouping(choice);
                grouping.Items.Add(item);
            }
            listOptionsMenu.Items.Add(grouping);
            var clear = new MenuItem { Header = "Clear all filters", Name = "ModsClearFiltersMenuItem", IsEnabled = hasActiveFilters() };
            clear.Click += (_, _) => clearFilters?.Invoke();
            listOptionsMenu.Items.Add(clear);
        };
        barActions.Add(listOptions);
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
        barActions.Add(openFolder);
        // MO2's restoreModsButton and saveModsButton: the backups of the mod order it
        // keeps beside the list, which this frontend already reaches through MO2's own
        // buttons.
        var restoreMods = Mo2QtWidgets.Icon("RestoreModsButton", Mo2QtWidgets.RestoreModsTip, "mdi-backup-restore",
            async () => { if (ViewModel?.LiveProfile is { ChooseOrderBackup: { } choose }) await choose("mods"); });
        var saveMods = Mo2QtWidgets.Icon("SaveModsButton", Mo2QtWidgets.SaveModsTip, "mdi-content-save-outline",
            async () => { if (ViewModel?.LiveProfile is { } live) await live.OrderBackup("mods", "backup", live.CurrentTarget); });
        barActions.Add(restoreMods); barActions.Add(saveMods);
        qtBar.Children.Add(Mo2ListToolbar.Pill("ModsToolbarActions", barActions.ToArray()));
        var activeMods = Mo2QtWidgets.Counter("ActiveModsCounter", out var activeModsValue);
        // MO2 rings the count in the same red it rings the list with while a filter is
        // on (ModListView::onModFilterActive), because a count of what is shown means
        // something different from a count of what there is. Kept at 2px whatever the
        // state so the bar does not move when a filter goes on and off.
        var countFrame = new Border { Name = "ModsCounterFrame", Child = activeMods, Margin = new Thickness(6,0,0,0),
            BorderThickness = new Thickness(2), BorderBrush = Brushes.Transparent, Padding = new Thickness(2,0),
            CornerRadius = new CornerRadius(3), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        qtBar.Children.Add(countFrame);

        var qtFilter = Mo2QtWidgets.Filter("ModsQtFilter", Mo2QtWidgets.ModFilterTip,
            text => { Model().SetColumnFilters(text, "", ""); updateFilterPresentation?.Invoke(); }, out filterField);
        qtBar.Children.Add(new Mo2ToolbarSearch(this, "ModsToolbarSearch", qtFilter, filterField!, "Search mods"));

        // MO2's category filter sits beside the list, not above it, so the pane is a
        // column of categories and the list rather than the list alone. Its own two
        // rows of controls — Clear and Edit, then how the ticks are combined and what
        // to do with separators — sit inside the group, where MO2 puts them.
        var nativeFilters = new Mo2NativeFilterCache((target, criteria) => ViewModel!.LiveProfile!.ReadModFilterMatches(target, criteria));
        var filterStatus = new TextBlock { Name = "ModsNativeFilterStatus", FontSize = Mo2Density.FontSize, TextWrapping = TextWrapping.Wrap, IsVisible = false };
        var retryFilters = Mo2TableRow.IconButton("mdi-refresh", "Retry reading MO2 filters", nativeFilters.Retry);
        retryFilters.Name = "ModsNativeFiltersRetry"; retryFilters.IsVisible = false;
        var filtersActive = false;
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
        filterActions.Children.Add(retryFilters);
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
        var categoriesGroup = Mo2QtWidgets.Categories("ModCategories", out var categoryItems, out _, filterActions, filterOptions, filterStatus);
        categoriesGroup.IsVisible = false;
        categoriesGroup.Width = 190;
        categoriesGroup.Margin = new Thickness(0,0,6,0);
        var chosenCategories = categoryItems.Selection;
        hasActiveFilters = () => categoryItems.Selection.Count + categoryItems.Excluded.Count > 0 || !string.IsNullOrEmpty(filterField?.Text);
        void ApplyCategories() {
            if (ViewModel?.LiveProfile is not { } live) return;
            var model = (Mo2ModsAdapter)ViewModel.Adapter;
            if (live.CategoryTree.Length == 0) model.SetCategoryChoice(categoryItems.IncludedNames, categoryItems.ExcludedNames);
            else model.SetNativeCriteria(categoryItems.Selection, categoryItems.Excluded, nativeFilters.ForPresentation(new(live.CurrentTarget, live.FilterRevision)));
            updateFilterPresentation?.Invoke();
        }
        Mo2ProfileTarget? categoryTarget = null;
        bool shouldReadFilters = false;
        void RefreshCategories() {
            if (!filtersActive || ViewModel?.LiveProfile is not { } live) return;
            if (categoryTarget != live.CurrentTarget) {
                categoryTarget = live.CurrentTarget; categoryItems.Reset();
            }
            shouldReadFilters = filtersActive && IsEffectivelyVisible && live.CanChangeOriginalUi &&
                (categoriesShown || categoryItems.Selection.Count + categoryItems.Excluded.Count > 0);
            var key = new Mo2FilterRequestKey(live.CurrentTarget, live.FilterRevision);
            nativeFilters.Request(key, live.CategoryTree.SelectMany(x => x.Walk()).ToArray(), shouldReadFilters && live.CategoryTree.Length > 0);
            // Do not construct/style unused category rows during list startup.
            // Selected criteria still refresh while hidden, and opening the pane
            // changes shouldReadFilters so LayoutUpdated populates it immediately.
            if (!categoriesShown && categoryItems.Selection.Count + categoryItems.Excluded.Count == 0) {
                ApplyCategories();
                return;
            }
            var currentMatches = nativeFilters.For(key);
            var shownMatches = nativeFilters.ForPresentation(key);
            var stale = shownMatches is not null && currentMatches is null;
            categoryItems.Refresh(live.Mods, live.CategoryTree, ApplyCategories, shownMatches,
                native: live.CategoryTree.Length > 0, ready: live.CategoryTree.Length == 0 || currentMatches is not null);
            filterStatus.Text = nativeFilters.Error is { } error
                ? (stale ? "Filter results are out of date. " : "") + error
                : nativeFilters.IsReading ? (stale ? "Updating MO2 filters…" : "Reading MO2 filters…") : "";
            filterStatus.IsVisible = filterStatus.Text.Length > 0;
            retryFilters.IsVisible = nativeFilters.Error is not null;
            ApplyCategories();
        }
        nativeFilters.Changed += RefreshCategories;
        // Clear empties every half of MO2's filter: the categories ticked here, the
        // text typed in the field below, and what the list is narrowed to.
        clearFilters = () => {
            chosenCategories.Clear(); categoryItems.Excluded.Clear();
            foreach (var box in categoryItems.Items.OfType<CheckBox>()) box.IsChecked = false;
            if (filterField is not null) filterField.Text = "";
            Model().SetColumnFilters("", "", "");
            ApplyCategories();
        };

        var modsTab = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"),
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            Margin = new Thickness(Mo2PanelChrome.Padding, 0, Mo2PanelChrome.Padding, Mo2PanelChrome.Padding) };
        Grid.SetColumnSpan(qtBar, 2); modsTab.Children.Add(qtBar);
        var categoryPane = new ScrollViewer {
            Name = "ModsCategoryPane", Content = categoriesGroup, IsVisible = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
        };
        Grid.SetRow(categoryPane, 2); modsTab.Children.Add(categoryPane);
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
        var listBody = new Grid();
        listBody.Children.Add(list); listBody.Children.Add(listFrame);
        // Only the result area belongs to EmptyState. Keeping the toolbar inside
        // it hid the focused search field as soon as a query returned no rows.
        var listParent = (Panel)listContainer.Parent!;
        var listParentRow = Grid.GetRow(listContainer);
        listParent.Children.Remove(listContainer);
        listContainer.Content = listBody;
        Grid.SetRow(listContainer, 2); Grid.SetColumn(listContainer, 1);
        modsTab.Children.Add(listContainer);
        var paneSize = default(Size);
        var toggleWas = false;
        bool? stackedFilters = null;
        void FitPane()
        {
            // Preserve a readable result column. Below this width, filters take a
            // bounded row above the results instead of disappearing or squeezing
            // their names down to a few characters.
            var stacked = Bounds.Width < 600;
            categoryPane.IsVisible = categoriesGroup.IsVisible = categoriesShown;
            if (stackedFilters != stacked) {
                stackedFilters = stacked;
                Grid.SetRow(categoryPane, stacked ? 1 : 2);
                Grid.SetColumnSpan(categoryPane, stacked ? 2 : 1);
                Grid.SetColumn(listContainer, stacked ? 0 : 1);
                Grid.SetColumnSpan(listContainer, stacked ? 2 : 1);
                categoryPane.Margin = stacked ? new Thickness(0, 0, 0, 6) : new Thickness(0, 0, 6, 0);
                categoriesGroup.Margin = default;
                categoriesGroup.Width = double.NaN;
                categoryPane.VerticalScrollBarVisibility = stacked ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
            }
            var width = stacked ? double.NaN : Math.Clamp(Math.Round(Bounds.Width * .3), 200, 260);
            if (!categoryPane.Width.Equals(width)) categoryPane.Width = width;
            // In short panels the entire filter group can scroll, including its
            // footer controls. The results retain more than half of the body.
            var height = stacked ? Math.Min(220, Math.Max(0, modsTab.Bounds.Height - qtBar.Bounds.Height - 6) * .45) : double.NaN;
            if (!categoryPane.Height.Equals(height)) categoryPane.Height = height;
            var groupHeight = stacked ? Math.Max(190, height) : double.NaN;
            if (!categoriesGroup.Height.Equals(groupHeight)) categoriesGroup.Height = groupHeight;
            Mo2ModRow.SideWidth = categoriesShown && !stacked ? width + 6 : 0;
        }
        updateCategoryPane = () => {
            FitPane();
            // Reopening a retained page must invalidate stale counts/controls
            // before the next layout exposes them as ready.
            RefreshCategories();
        };
        updateFilterPresentation = () => {
            // What the list is narrowed to, which MO2 spells out beside the filter
            // field and offers to clear only while there is something to clear. Both
            // are written only when they change, so neither asks for a pass of its own.
            var narrowed = categoryItems.Summary;
            if ((filterField?.Text ?? "").Length > 0)
                narrowed = narrowed.Length > 0 ? narrowed + " · \"" + filterField!.Text + "\"" : "\"" + filterField!.Text + "\"";
            if (filterSummary != narrowed) {
                filterSummary = narrowed;
                ToolTip.SetTip(listOptions, narrowed.Length > 0 ? "More mod actions · Filters: " + narrowed : "More mod actions");
            }
            // MO2's own two: #f00 while a filter is on, #337733 while the list is
            // grouped and not filtered, nothing otherwise — and the count is ringed
            // only by the filter, as MO2 rings only its QLCDNumber.
            var wantedFrame = narrowed.Length > 0 ? FilteredInk
                : Model().Grouping > 0 ? GroupedInk : Brushes.Transparent;
            if (!ReferenceEquals(listFrame.BorderBrush, wantedFrame)) listFrame.BorderBrush = wantedFrame;
            var wantedCount = narrowed.Length > 0 ? FilteredInk : (IBrush)Brushes.Transparent;
            if (!ReferenceEquals(countFrame.BorderBrush, wantedCount)) countFrame.BorderBrush = wantedCount;
        };
        modsTab.LayoutUpdated += (_, _) => {
            if (ViewModel?.LiveProfile is { } profile && shouldReadFilters != (filtersActive && IsEffectivelyVisible && profile.CanChangeOriginalUi &&
                    (categoriesShown || categoryItems.Selection.Count + categoryItems.Excluded.Count > 0))) RefreshCategories();
            // Only update when the available body or requested state changes;
            // avoid asking for another layout on every layout notification.
            var size = new Size(Bounds.Width, Math.Max(0, modsTab.Bounds.Height - qtBar.Bounds.Height));
            if (paneSize != size || toggleWas != categoriesShown || categoryPane.IsVisible != categoriesShown) {
                paneSize = size; toggleWas = categoriesShown;
                FitPane();
            }
            updateFilterPresentation?.Invoke();
        };
        Grid.SetRow(modsTab, listParentRow);
        listParent.Children.Add(modsTab);
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
            // Internal rows belong to TreeDataGrid. Treating their payload as an
            // archive replaced the table's Move effect with None and blocked drops.
            if (e.Data.Get("TreeDataGridDragInfo") is DragInfo || e.Data.Contains(Mo2TabDragDrop.Format)) return;
            e.DragEffects = ViewModel?.LiveProfile?.CanChangeOriginalUi == true && Archives(e.Data).Length > 0
                ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        });
        AddHandler(DragDrop.DropEvent, async (_, e) => {
            if (e.Data.Get("TreeDataGridDragInfo") is DragInfo || e.Data.Contains(Mo2TabDragDrop.Format)) return;
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
            filtersActive = true;
            System.Reactive.Disposables.Disposable.Create(() => { filtersActive = false; shouldReadFilters = false; nativeFilters.SetEnabled(false); }).AddTo(disposables);
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
                RefreshCategories();
                var model = ViewModel!;
                ((Mo2ModsAdapter)model.Adapter).RefreshFilter();
                header.Title = "My Mods";
                // This is a fixed page title, set by the page constructor. A native
                // refresh can finish while this view is being replaced; writing its
                // old tab IDs here would rename the new page in that same tab.
                native.FindControl<MenuItem>("MenuItemRenameCollection")!.IsVisible = false;
                native.FindControl<MenuItem>("MenuItemDeleteCollection")!.IsVisible = false;
                header.IsVisible = true;
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
            }
            ViewModel!.LiveProfile!.Changed += RefreshMods;
            System.Reactive.Disposables.Disposable.Create(() => ViewModel!.LiveProfile!.Changed -= RefreshMods).AddTo(disposables);
            RefreshMods();
            Avalonia.Threading.Dispatcher.UIThread.Post(RefreshMods);
            ViewModel!.SelectedSubTab = LoadoutPageSubTabs.Mods;
            SubTabs.SelectedIndex = 0;
            void UpdateSelection() {
                    // What the selection is for is the conflict and linked-plugin
                    // highlighting MO2 draws for it. The buttons that used to be
                    // enabled alongside went with the toolbar.
                    ViewModel.LiveProfile!.HighlightMods(ViewModel.Adapter.SelectedModels.Select(x => x.Key));
            }
            ViewModel!.Adapter.SelectedModels.ObserveChanged().Subscribe(_ => UpdateSelection()).AddTo(disposables);
            UpdateSelection();
        });
    }
}
