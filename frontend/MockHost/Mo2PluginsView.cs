using ObservableCollections;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using NexusMods.App.UI.Controls;
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
        var toolbar = new Toolbar { Name = "PluginsToolbar", Margin = new Thickness(0,8,0,0) };
        toolbar.ItemsPanel = new FuncTemplate<Panel?>(() => new WrapPanel { Orientation = Avalonia.Layout.Orientation.Horizontal });
        var sort = new Button { Content = "Sort with LOOT…", Name = "SortPluginsWithLoot", IsEnabled = false };
        ToolTip.SetTip(sort, "Sort plugins using MO2’s original LOOT workflow");
        ToolTip.SetShowOnDisabled(sort, true);
        sort.Click += async (_, _) => { if (ViewModel?.LiveProfile is { } profile) await profile.SortPlugins(profile.CurrentTarget); };
        var primary = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4 };
        primary.Children.Add(sort);
        var history = Mo2ModRow.IconButton("mdi-dots-vertical", "Plugin actions", () => { });
        history.Name = "PluginsOverflowButton";
        var historyMenu = new MenuFlyout();
        historyMenu.Items.Add(Mo2EntryMenu.Action("Refresh plugins", () => ViewModel?.LiveProfile?.Refresh() ?? Task.CompletedTask));
        historyMenu.Items.Add(new Separator());
        Mo2OrderHistoryMenu.Add(historyMenu, () => ViewModel?.LiveProfile, "plugins");
        history.Flyout = historyMenu; primary.Children.Add(history);
        foreach (var button in primary.Children.OfType<Button>()) { button.Height = 24; button.MinHeight = 0; if (button != sort) button.Width = 24; }
        toolbar.Items.Add(new Border { Background = Avalonia.Media.Brush.Parse("#29292E"), CornerRadius = new CornerRadius(8), Padding = new Thickness(0), Margin = new Thickness(0,0,8,0), Child = primary });
        var search = _search;
        toolbar.Items.Add(search);

        var editor = new LoadOrderView { Margin = new Thickness(0), Padding = new Thickness(0) };
        var alert = editor.FindControl<Control>("LoadOrderAlert")!;
        var tableControl = editor.FindControl<TreeDataGrid>("SortOrderTreeDataGrid")!;
        tableControl.ShowColumnHeaders = false;
        Mo2TableRow.InstallRowStyles(tableControl);
        var mainGrid = editor.FindControl<Grid>("MainGrid")!;
        mainGrid.Margin = new Thickness(0);
        mainGrid.RowDefinitions = new RowDefinitions("Auto,*"); Grid.SetRow(tableControl, 1);
        var headings = Mo2PluginRow.Columns(); headings.Margin = new Thickness(0,8,0,6);
        foreach (var (text, column) in new[] { ("Status", 1), ("Plugin name", 2), ("Type", 3), ("Masters", 4), ("Actions", 5) })
            Mo2ModRow.Add(headings, new TextBlock { Text = text, Margin = new Thickness(3,0), FontWeight = Avalonia.Media.FontWeight.SemiBold, FontSize = 12 }, column);
        mainGrid.Children.Add(headings);
        Grid.SetRowSpan(editor.FindControl<Grid>("TrophyBarColumnGrid")!, 2);
        headings.LayoutUpdated += (_, _) => Mo2PluginRow.Fit(headings, this.GetVisualAncestors().OfType<NexusMods.App.UI.WorkspaceSystem.PanelView>().FirstOrDefault()?.Bounds.Width ?? Bounds.Width);
        // Keep NMA's help and winner indicator visible beside the plugin list,
        // including when the list is short enough not to need a scrollbar.
        editor.FindControl<Control>("TrophyBarColumnGrid")!.IsVisible = true;
        var rail = editor.FindControl<Grid>("TrophyBarGrid")!;
        editor.FindControl<Control>("TrophyGradientBorder")!.IsVisible = false;
        var pluginScroll = new Mo2ListScrollBar {
            Name = "PluginRailScrollBar", Orientation = Avalonia.Layout.Orientation.Vertical,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Width = 16,
            SmallChange = 40
        };
        Grid.SetRow(pluginScroll, 1); rail.Children.Add(pluginScroll);
        ScrollViewer? pluginViewer = null;
        var syncingScroll = false;
        void SyncScroll() {
            if (pluginViewer is null) return;
            syncingScroll = true;
            pluginScroll.Maximum = Math.Max(0, pluginViewer.Extent.Height - pluginViewer.Viewport.Height);
            pluginScroll.ViewportSize = pluginViewer.Viewport.Height;
            pluginScroll.LargeChange = pluginViewer.Viewport.Height;
            pluginScroll.Value = pluginViewer.Offset.Y;
            pluginScroll.IsEnabled = pluginScroll.Maximum > 0;
            syncingScroll = false;
        }
        void ViewerScrolled(object? sender, ScrollChangedEventArgs args) => SyncScroll();
        pluginScroll.PropertyChanged += (_, args) => {
            if (!syncingScroll && args.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && pluginViewer is not null)
                pluginViewer.Offset = new Vector(pluginViewer.Offset.X, pluginScroll.Value);
        };
        LayoutUpdated += (_, _) => {
            var viewer = pluginViewer?.GetVisualRoot() is not null ? pluginViewer :
                editor.FindControl<TreeDataGrid>("SortOrderTreeDataGrid")!.GetVisualDescendants().OfType<ScrollViewer>()
                    .FirstOrDefault(candidate => candidate.GetVisualDescendants().Any(control => control.GetType().Name == "TreeDataGridRowsPresenter"));
            if (viewer != pluginViewer) {
                if (pluginViewer is not null) pluginViewer.ScrollChanged -= ViewerScrolled;
                pluginViewer = viewer;
                if (viewer is not null) {
                    viewer.VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
                    viewer.ScrollChanged += ViewerScrolled;
                }
            }
            SyncScroll();
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
            void Highlight() => Mo2RowHighlights.Apply(tableControl,profile,plugins:true);
            EventHandler layout = (_,_) => Highlight(); editor.LayoutUpdated += layout;
            profile.HighlightsChanged += Highlight;
            System.Reactive.Disposables.Disposable.Create(() => { editor.LayoutUpdated -= layout; profile.HighlightsChanged -= Highlight; }).DisposeWith(disposables);
            void UpdateSelection() {
                var selected = profile.Order.Plugins.Where(plugin => ViewModel.Adapter.SelectedModels.Any(row => row.Key.Equals(plugin.Key))).ToArray();
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
            System.Reactive.Disposables.Disposable.Create(() => editor.ViewModel = null).DisposeWith(disposables);
        });
    }
}
