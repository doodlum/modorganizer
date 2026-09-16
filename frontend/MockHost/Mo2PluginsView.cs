using ObservableCollections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using NexusMods.App.UI.Controls;
using NexusMods.UI.Sdk.Icons;
using NexusMods.Abstractions.Games;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using NexusMods.App.UI.Pages.Sorting;
using ReactiveUI;
using R3;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

// Keep the upstream ordering editor and add MO2's independent ESP activation.
internal sealed class Mo2PluginsView : ReactiveUserControl<ScenarioLoadOrderPage>
{
    private ScenarioLoadOrderPage? _columnPage;
    private IColumn<CompositeItemModel<ISortItemKey>>? _pluginColumn;
    private readonly NexusMods.App.UI.Controls.Search.SearchControl _search = new() { Name = "PluginSearchControl", PageName = "Plugins", ButtonSize = StandardButton.Sizes.Toolbar };
    private TextBox SearchBox => _search.FindControl<TextBox>("SearchTextBox")!;
    private Control SearchPanel => _search.FindControl<Control>("SearchPanel")!;
    public Mo2PluginsView()
    {
        Mo2UiLatencyProbe.Count("Plugins views created");
        var layout = new DockPanel();
        var header = new NexusMods.App.UI.Controls.PageHeader.PageHeader {
            Name = "PluginsPageHeader", Title = "Plugins",
            Description = "Enable plugins and arrange their load order.",
            Icon = ScenarioLoadOrderPage.HeaderIcon
        };
        var details = new TextBlock { Name = "Mo2PluginDiagnostics", TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(12) };
        var detailsPanel = new DockPanel();
        var closeDetails = Mo2ModRow.IconButton("mdi-close", "Close plugin details", () => { if (ViewModel is { } model) model.ShowPluginDetails = false; });
        closeDetails.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        DockPanel.SetDock(closeDetails, Dock.Right); detailsPanel.Children.Add(closeDetails); detailsPanel.Children.Add(details);
        var detailScroll = new ScrollViewer { Content = detailsPanel, MaxHeight = 150, IsVisible = false };
        DockPanel.SetDock(detailScroll, Dock.Bottom); layout.Children.Add(detailScroll);
        DockPanel.SetDock(header, Dock.Top); layout.Children.Add(header);
        // This page drew a toolbar of its own above its list. It was not MO2's — MO2
        // puts none on a tab — and everything on it is offered by a widget this page
        // already draws or by the row menu MO2 itself builds: the search box by the
        // filter field MO2 puts under the list (espFilterEdit), Enable and Disable
        // for a selection by the row's own menu, which is how MO2 works one, and the
        // backups behind its overflow by MO2's own Restore and Save beside Sort.
        // Refreshing goes through MO2's list-options button on the mod pane, which is
        // MO2's own Refresh over the whole profile.
        TreeDataGrid? editorTable = null;

        var editor = new LoadOrderView { Margin = new Thickness(0), Padding = new Thickness(0) };
        editorTable = editor.FindControl<TreeDataGrid>("SortOrderTreeDataGrid");
        // The native load-order styling insets its rows presenter by 4px, which put
        // every plugin row 4px right of the same column on Mods. Applied as a style
        // because the presenter is a template part that does not exist yet.
        editor.Styles.Add(new Avalonia.Styling.Style(x =>
            Avalonia.Styling.Selectors.OfType<Avalonia.Controls.Primitives.TreeDataGridRowsPresenter>(x)) {
            Setters = { new Avalonia.Styling.Setter(Avalonia.Layout.Layoutable.MarginProperty, new Thickness(0)) },
        });
        var alert = editor.FindControl<Control>("LoadOrderAlert")!;
        var tableControl = editor.FindControl<TreeDataGrid>("SortOrderTreeDataGrid")!;
        tableControl.ShowColumnHeaders = false;
        Mo2TableRow.InstallRowStyles(tableControl);
        var mainGrid = editor.FindControl<Grid>("MainGrid")!;
        mainGrid.Margin = new Thickness(0);
        mainGrid.RowDefinitions = new RowDefinitions("Auto,*"); Grid.SetRow(tableControl, 1);
        // The same band as the mod list's headings, top and bottom: 8 above and 6
        // below here against 4 and 4 there put the two tables' first rows 4px apart
        // when the panes sit side by side.
        var headings = Mo2PluginRow.Columns(); headings.Margin = new Thickness(0,4,0,4); headings.Height = Mo2TableRow.HeadingBand;
        // MO2's own headers, from the same table the row's columns come from. The
        // flags column takes the glyph it holds as its heading, as the mod list's
        // glyph columns do — a word in a column one icon wide is drawn as "…".
        foreach (var (column, text) in Mo2PluginRow.Headers)
            Mo2ModRow.Add(headings, column == Mo2PluginRow.Flags
                ? Mo2TableRow.GlyphHeading("mdi-flag", text) : Mo2TableRow.Heading(text), column);
        mainGrid.Children.Add(headings);
        var pluginRail = editor.FindControl<Grid>("TrophyBarColumnGrid")!;
        Grid.SetRowSpan(pluginRail, 2);
        // The same rail width as Mods. Left to the native markup this column sized
        // itself from its own help button and came out 24px wider, so the two tables
        // started at the same place and ended in different ones.
        pluginRail.Width = Mo2TableRow.RailWidth;
        headings.LayoutUpdated += (_, _) => Mo2PluginRow.Fit(headings, this.GetVisualAncestors().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().FirstOrDefault()?.Bounds.Width ?? Bounds.Width);
        // The same rail My Mods draws, in place of the load-order view's own trophy
        // bar. Its scrolling is followed by the shared connection rather than a copy
        // of it kept here, which is how this page ended up keeping the table's native
        // scrollbar beside the rail.
        editor.FindControl<Control>("TrophyBarColumnGrid")!.IsVisible = true;
        editor.FindControl<Control>("TrophyBarDockPanel")!.IsVisible = false;
        var pluginRailGrid = Mo2ListRail.Create("PluginRailScrollBar", out var pluginScroll);
        Grid.SetRow(pluginRailGrid, 1);
        editor.FindControl<Grid>("TrophyBarColumnGrid")!.Children.Add(pluginRailGrid);
        Mo2ListRail.Connect(pluginScroll, tableControl);
        LayoutUpdated += (_, _) => {
            // The toolbar now sits inside the header line, so subtracting both double
            // counted it and starved the list down to a couple of rows. Measure the
            // whole header stack once instead.
            var chrome = header.GetVisualAncestors().OfType<StackPanel>().FirstOrDefault(x => x.Name == "PanelHeaderStack");
            var chromeHeight = chrome?.Bounds.Height > 0 ? chrome.Bounds.Height : header.DesiredSize.Height;
            var available = Bounds.Height - chromeHeight - alert.Bounds.Height - 112;
            detailScroll.MaxHeight = Math.Min(150, Math.Min(Bounds.Height * .25, Math.Max(0, available)));
            details.Margin = new Thickness(12, detailScroll.MaxHeight < 80 ? 4 : 12);
        };
        var chooseProfile = new TextBlock { Text = "Select a profile in My Loadouts to view its plugins.",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(24) };
        // MO2's own espTab furniture: Sort, Restore and Save across the top with the
        // active-plugin count beside them, and its filter field under the list.
        var qtSort = Mo2QtWidgets.Button("SortPluginsButton", "Sort", Mo2QtWidgets.SortTip, "mdi-sort-alphabetical-variant",
            async () => { if (ViewModel?.LiveProfile is { } live) await live.SortPlugins(live.CurrentTarget); });
        // Restore and Save go through MO2's own backup buttons, which own the retention
        // policy, the restore picker and the writes — the same route the order history
        // menu already takes.
        var qtRestore = Mo2QtWidgets.Button("RestorePluginsButton", "Restore", Mo2QtWidgets.RestoreTip, "mdi-backup-restore",
            async () => { if (ViewModel?.LiveProfile is { } live) await live.OrderBackup("plugins", "restore", live.CurrentTarget); });
        var qtSave = Mo2QtWidgets.Button("SavePluginsButton", "Save", Mo2QtWidgets.SaveTip, "mdi-content-save-outline",
            async () => { if (ViewModel?.LiveProfile is { } live) await live.OrderBackup("plugins", "backup", live.CurrentTarget); });
        var qtCount = Mo2QtWidgets.Counter("ActivePluginsCounter", out var activeCount);
        // MO2 greys its own three out — sortButton when the managed game has no
        // sorting or LOOT is already running, and the whole pane while one of its
        // dialogs is up — and shows why on the one it disables. These stood drawn
        // live whatever MO2 said, and pressing one then went nowhere: the action
        // behind each returns without a word when MO2 cannot take it. The mod
        // pane's pair of backup buttons already follow MO2 this way.
        ToolTip.SetShowOnDisabled(qtSort, true);
        ToolTip.SetShowOnDisabled(qtRestore, true);
        ToolTip.SetShowOnDisabled(qtSave, true);
        // The same band as the row of widgets above the mod list, and the same gap
        // under it: 25px tall with 8 below here against 24 and 6 there put this
        // table's headings 3px below the ones beside them.
        var qtBar = new StackPanel { Name = "PluginsQtBar", Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 6, Height = Mo2TableRow.ActionSize, Margin = new Thickness(0, 0, 0, 6) };
        qtBar.Children.Add(qtSort); qtBar.Children.Add(qtRestore); qtBar.Children.Add(qtSave); qtBar.Children.Add(qtCount);
        var qtFilterHost = Mo2QtWidgets.Filter("PluginsQtFilter", Mo2QtWidgets.PluginFilterTip,
            text => { if (ViewModel is { } model) model.Mo2SearchText = text; }, out _);
        qtFilterHost.Margin = new Thickness(0, 8, 0, 0);

        var body = new Grid(); body.Children.Add(editor); body.Children.Add(chooseProfile);
        var tab = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        tab.Children.Add(qtBar);
        Grid.SetRow(body, 1); tab.Children.Add(body);
        Grid.SetRow(qtFilterHost, 2); tab.Children.Add(qtFilterHost);
        layout.Children.Add(tab); Content = layout;
        // MO2 shows how many plugins are active, which is what the counter beside the
        // sort actions reads.
        void RefreshCount() {
            if (ViewModel?.LiveProfile is { } live)
                activeCount.Text = live.Order.Plugins.Count(x => x.IsActive).ToString();
        }
        tab.AttachedToVisualTree += (_, _) => RefreshCount();
        tab.LayoutUpdated += (_, _) => RefreshCount();
        // Same as Mods: the column chooser alone on the action row, and no toolbar
        // beside it.
        var pluginColumns = Mo2TableRow.ColumnsButton(Mo2PluginRow.OptionalColumns, Mo2PluginRow.HiddenColumns, headings);
        Mo2PanelChrome.ApplyDocked(this, layout, header, pluginColumns);
        Mo2PanelChrome.Apply(this, layout, header);
        this.WhenActivated(disposables => {
            ViewModel!.Adapter.ViewHierarchical.Value = false;
            var page = ViewModel!;
            var profile = page.LiveProfile!;
            if (!ReferenceEquals(_columnPage, page)) {
                _columnPage = page;
                _pluginColumn = new TemplateColumn<CompositeItemModel<ISortItemKey>>("Plugins", new FuncDataTemplate<CompositeItemModel<ISortItemKey>>((item, _) => {
                    if (item is null || profile.Order.FindPlugin(item.Key) is not { } plugin) return new TextBlock();
                    Mo2UiLatencyProbe.Row("Plugins", page.Adapter, item, plugin.DisplayName);
                    return new Mo2DeferredRow(() => Mo2PluginRow.Create(profile, plugin));
                }), width: new GridLength(1, GridUnitType.Star));
            }
            // Configure before the native editor observes the source, and leave
            // the existing column intact when this page body is reactivated.
            page.Adapter.Source.AsObservable().Subscribe(source => {
                if (source is not FlatTreeDataGridSource<CompositeItemModel<ISortItemKey>> table ||
                    (table.Columns.Count == 1 && ReferenceEquals(table.Columns[0], _pluginColumn))) return;
                table.Columns.Clear();
                table.Columns.Add(_pluginColumn!);
            }).DisposeWith(disposables);
            editor.ViewModel = ViewModel;
            // The search control is not drawn any more — MO2's filter field under
            // the list is what narrows this table — but it is still what the
            // adapter's own query runs through, so it stays wired and unparented.
            _search.Adapter = ViewModel.Adapter;
            this.Bind(ViewModel, vm => vm.Mo2SearchText, view => view.SearchBox.Text).DisposeWith(disposables);
            System.Reactive.Disposables.Disposable.Create(() => _search.Adapter = null).DisposeWith(disposables);
            Mo2SearchQuery.Attach(_search, profile, plugins: true).DisposeWith(disposables);
            Mo2RowHighlights.Attach(editor, tableControl, profile, plugins: true).DisposeWith(disposables);
            void UpdateSelection() {
                var selected = profile.Order.Plugins.Where(plugin => ViewModel.Adapter.SelectedModels.Any(row => row.Key.Equals(plugin.Key))).ToArray();
                // The mirror of what selecting mods does to this table.
                profile.HighlightPlugins(selected.Select(x => x.DisplayName));
                details.Text = string.Join("\n\n", selected.Select(x => $"{x.DisplayName} · {(x.IsActive ? "Enabled" : "Disabled")} · Mod index {(x.ModIndex.Length == 0 ? "—" : x.ModIndex)}\n{x.Diagnostics}"));
                if (selected.Length == 0) details.Text = "Select a plugin to view MO2’s diagnostics and mod index.";
                detailScroll.IsVisible = ViewModel.ShowPluginDetails && profile.ProfilePath.Length > 0 && selected.Length > 0;
            }
            void UpdateConnection() {
                var connected = profile.ProfilePath.Length > 0;
                qtSort.IsEnabled = profile.CanChangeOriginalUi && profile.CanSortPlugins;
                ToolTip.SetTip(qtSort, profile.CanSortPlugins ? Mo2QtWidgets.SortTip : profile.SortPluginsUnavailableReason);
                qtRestore.IsEnabled = qtSave.IsEnabled = profile.CanChangeOriginalUi;
                editor.IsVisible = connected;
                chooseProfile.IsVisible = !connected;
                UpdateSelection();
            }
            profile.Changed += UpdateConnection;
            System.Reactive.Disposables.Disposable.Create(() => profile.Changed -= UpdateConnection).DisposeWith(disposables);
            UpdateConnection();
            ViewModel!.Adapter.SelectedModels.ObserveChanged()
                .Subscribe(_ => UpdateSelection()).DisposeWith(disposables);
            this.WhenAnyValue(x => x.ViewModel!.ShowPluginDetails).Subscribe(_ => UpdateSelection()).DisposeWith(disposables);
            // A closed page leaves nothing marked behind it, as the Data page does.
            System.Reactive.Disposables.Disposable.Create(() => { profile.HighlightPlugins([]); editor.ViewModel = null; }).DisposeWith(disposables);
        });
    }
}
