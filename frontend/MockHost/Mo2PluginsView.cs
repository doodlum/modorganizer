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
        var toolbar = Mo2ListToolbar.Create("PluginsToolbar");
        // An icon like every other header action. As a labelled button it was the one
        // control wide enough to cost this page its title: the actions could not fit
        // beside it, so the header gave way at ordinary panel widths.
        var sort = Mo2TableRow.IconButton("mdi-sort-alphabetical-variant", "Sort with LOOT…",
            async () => { if (ViewModel?.LiveProfile is { } profile) await profile.SortPlugins(profile.CurrentTarget); });
        sort.Name = "SortPluginsWithLoot"; sort.IsEnabled = false;
        ToolTip.SetTip(sort, "Sort plugins using MO2’s original LOOT workflow");
        ToolTip.SetShowOnDisabled(sort, true);

        var primaryActions = new List<Control> { sort };
        var history = Mo2ModRow.IconButton("mdi-dots-vertical", "Plugin actions", () => { });
        history.Name = "PluginsOverflowButton";
        var historyMenu = new MenuFlyout();
        historyMenu.Items.Add(Mo2EntryMenu.Action("Refresh plugins", () => ViewModel?.LiveProfile?.Refresh() ?? Task.CompletedTask));
        historyMenu.Items.Add(new Separator());
        Mo2OrderHistoryMenu.Add(historyMenu, () => ViewModel?.LiveProfile, "plugins");
        history.Flyout = historyMenu; primaryActions.Add(history);
        toolbar.Items.Add(Mo2ListToolbar.Pill("PluginPrimaryActions", primaryActions.ToArray()));
        var search = _search;
        Mo2ListToolbar.AddSearch(toolbar, search);
        TreeDataGrid? editorTable = null;
        // The same selection group My Mods gets from the original app's toolbar:
        // how many rows are selected, a way to clear them, and the actions that
        // apply to all of them. Selecting plugins used to say nothing at all, and
        // the only way out of a selection was to click a single row again.
        var enableSelected = new StandardButton {
            Name = "EnableSelectedPlugins", Text = "Enable", Type = StandardButton.Types.Tertiary,
            Size = StandardButton.Sizes.Toolbar, Fill = StandardButton.Fills.None,
            ShowIcon = StandardButton.ShowIconOptions.Left, LeftIcon = IconValues.CheckCircleOutline,
        };
        ToolTip.SetTip(enableSelected, "Enable every selected plugin");
        enableSelected.Click += async (_, _) => { if (ViewModel is { } model) await model.SetSelectedActive(true); };
        var disableSelected = new StandardButton {
            Name = "DisableSelectedPlugins", Text = "Disable", Type = StandardButton.Types.Tertiary,
            Size = StandardButton.Sizes.Toolbar, Fill = StandardButton.Fills.None,
            ShowIcon = StandardButton.ShowIconOptions.Left, LeftIcon = new ProjektankerIcon("mdi-close-circle-outline"),
        };
        ToolTip.SetTip(disableSelected, "Disable every selected plugin");
        disableSelected.Click += async (_, _) => { if (ViewModel is { } model) await model.SetSelectedActive(false); };
        var selectionGroup = Mo2SelectionGroup.Create(out var deselectPlugins,
            () => editorTable?.RowSelection?.Clear(), enableSelected, disableSelected);
        toolbar.Items.Add(selectionGroup);

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
        var headings = Mo2PluginRow.Columns(); headings.Margin = new Thickness(0,8,0,6); headings.Height = Mo2TableRow.HeadingBand;
        // MO2's own headers, from the same table the row's columns come from.
        foreach (var (column, text) in Mo2PluginRow.Headers)
            Mo2ModRow.Add(headings, Mo2TableRow.Heading(text), column);
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
        var body = new Grid(); body.Children.Add(editor); body.Children.Add(chooseProfile);
        layout.Children.Add(body); Content = layout;
        // Same as Mods: the toolbar rides on the header line rather than in a row
        // of its own, so both pages present one chrome.
        toolbar.Margin = new Thickness(0);
        var pluginColumns = Mo2TableRow.ColumnsButton(Mo2PluginRow.OptionalColumns, Mo2PluginRow.HiddenColumns, headings);
        Mo2PanelChrome.ApplyDocked(this, layout, header, pluginColumns, toolbar);
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
            search.Adapter = ViewModel.Adapter;
            this.Bind(ViewModel, vm => vm.Mo2SearchText, view => view.SearchBox.Text).DisposeWith(disposables);
            this.Bind(ViewModel, vm => vm.Mo2SearchExpanded, view => view.SearchPanel.IsVisible).DisposeWith(disposables);
            search.AttachKeyboardHandlers(this, disposables);
            System.Reactive.Disposables.Disposable.Create(() => search.Adapter = null).DisposeWith(disposables);
            Mo2SearchQuery.Attach(search, profile, plugins: true).DisposeWith(disposables);
            Mo2RowHighlights.Attach(editor, tableControl, profile, plugins: true).DisposeWith(disposables);
            void UpdateSelection() {
                var selected = profile.Order.Plugins.Where(plugin => ViewModel.Adapter.SelectedModels.Any(row => row.Key.Equals(plugin.Key))).ToArray();
                Mo2SelectionGroup.Update(selectionGroup, deselectPlugins, ViewModel.Adapter.SelectedModels.Count);
                // The mirror of what selecting mods does to this table.
                profile.HighlightPlugins(selected.Select(x => x.DisplayName));
                enableSelected.IsEnabled = profile.CanChangeOriginalUi && selected.Any(x => x.CanToggle && !x.IsActive);
                disableSelected.IsEnabled = profile.CanChangeOriginalUi && selected.Any(x => x.CanToggle && x.IsActive);
                details.Text = string.Join("\n\n", selected.Select(x => $"{x.DisplayName} · {(x.IsActive ? "Enabled" : "Disabled")} · Mod index {(x.ModIndex.Length == 0 ? "—" : x.ModIndex)}\n{x.Diagnostics}"));
                if (selected.Length == 0) details.Text = "Select a plugin to view MO2’s diagnostics and mod index.";
                detailScroll.IsVisible = ViewModel.ShowPluginDetails && profile.ProfilePath.Length > 0 && selected.Length > 0;
            }
            void UpdateConnection() {
                var connected = profile.ProfilePath.Length > 0;
                sort.IsEnabled = profile.CanChangeOriginalUi && profile.CanSortPlugins;
                ToolTip.SetTip(sort, profile.CanSortPlugins ? "Sort plugins using MO2’s original LOOT workflow" : profile.SortPluginsUnavailableReason);
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
