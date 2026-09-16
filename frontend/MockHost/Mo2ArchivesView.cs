using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

internal sealed record Mo2Archive(string Name,string Mod,bool Active,bool CanToggle);
internal interface IMo2ArchivesPage : IPageViewModelInterface { }
internal sealed class Mo2ArchivesPage : APageViewModel<IMo2ArchivesPage>, IMo2ArchivesPage
{
    public static readonly IconValue ArchiveIcon = new ProjektankerIcon("mdi-archive-outline");
    public Mo2LiveProfile Profile { get; }
    public string SearchText { get; set; } = "";
    public Mo2ArchivesPage(IWindowManager windows,Mo2LiveProfile profile) : base(windows)
    { Profile = profile; TabTitle = "Archives"; TabIcon = ArchiveIcon; }
}
internal sealed class Mo2ArchivesView : ReactiveUserControl<Mo2ArchivesPage>
{
    private readonly TreeDataGrid _table = new() { Name = "ArchivesTable", ShowColumnHeaders = true };
    private readonly TextBox _filter = new() { Name = "ArchivesFilter", Watermark = "Search archives", MinWidth = 60 };
    private readonly TextBlock _status = new() { Name = "ArchivesStatus", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _browse;
    private readonly Button _extract;
    private Mo2Archive[] _archives = [];
    private Mo2ProfileTarget? _target;
    private bool _reading;
    private long _revision = -1;
    private string? _error;
    private Mo2Archive? _selection;
    private bool _showModColumn;
    private readonly Func<Mo2ProfileTarget, Task<Mo2Archive[]>>? _read;
    private bool _active;
    private long _activation;
    public Mo2ArchivesView() : this(null) { }
    internal Mo2ArchivesView(Func<Mo2ProfileTarget, Task<Mo2Archive[]>>? read)
    {
        _read = read;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        _browse = Mo2ModRow.IconButton("mdi-folder-search-outline", "Browse selected archive", async () => {
            if (ViewModel is not { } model || _target is not { } target || _table.RowSelection?.SelectedItem is not Mo2Archive archive) return;
            await model.Profile.BrowseArchive(archive.Name,target); await Refresh();
        });
        _extract = Mo2ModRow.IconButton("mdi-archive-arrow-down-outline", "Extract selected archive", async () => {
            if (ViewModel is not { } model || _target is not { } target || _table.RowSelection?.SelectedItem is not Mo2Archive archive) return;
            await model.Profile.ExtractArchive(archive,target); await Refresh();
        });
        var header = new PageHeader { Title = "Archives", Description = "Browse BSA and BA2 archives in this profile.", Icon = new AvaloniaSvg("avares://MockHost/Assets/archives-3d.svg") };
        root.Children.Add(header);
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto"), Margin = new Thickness(0,8,0,8) };
        _browse.Name = "BrowseArchive"; _extract.Name = "ExtractArchive";
        _browse.IsEnabled = _extract.IsEnabled = false;
        _filter.Margin = new Thickness(0,0,8,0);
        bar.Children.Add(_filter); Grid.SetColumn(_browse,1); bar.Children.Add(_browse);
        Grid.SetColumn(_extract,2); bar.Children.Add(_extract);
        var refresh = Mo2ModRow.IconButton("mdi-refresh", "Refresh archives", async () => await Refresh());
        refresh.Name = "RefreshArchives"; Grid.SetColumn(refresh,3); bar.Children.Add(refresh);
        // MO2's own bsaTab note above the list, which says what the list below is.
        var note = Mo2QtWidgets.Note("ManagedArchiveLabel", Mo2QtWidgets.ArchivesNote);
        var archiveTab = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        archiveTab.Children.Add(note);
        Grid.SetRow(_table,1); archiveTab.Children.Add(_table);
        Grid.SetRow(bar,1); root.Children.Add(bar); Grid.SetRow(_status,2); root.Children.Add(_status); Grid.SetRow(archiveTab,3); root.Children.Add(archiveTab);
        _table.Classes.Add("MainListsStyling"); Content = root;
        // MO2's own archive menu (mainwindow.cpp, on_bsaList_customContextMenuRequested),
        // which carries one entry and only that one.
        //
        // Browse was here too, on the grounds that MO2 opens an archive when its row is
        // double-clicked. It does not: bsaList has no activation handler at all, only
        // this menu and its tick. Looking inside an archive is MO2's Data preview,
        // which the Browse action above this list reaches — a control of this page's
        // own, where MO2's Archives tab has none. The menu is MO2's, entry for entry.
        Mo2RowMenu.Attach<Mo2Archive>(_table, archive => [
            Mo2EntryMenu.Action("Extract...", async () => {
                if (ViewModel is not { } model || _target is not { } target) return;
                await model.Profile.ExtractArchive(archive!, target); await Refresh();
            }, ArchiveReady),
        ]);
        _columns = new Mo2ColumnToggle("archives", Render);
        // The filter stays in its own row under the separator and the header line
        // carries a magnifier, matching Mods and Plugins. A search box on the header
        // line is what made this page's header the widest and the first to run out.
        Mo2PanelChrome.Apply(this, root, header, Mo2PanelChrome.SearchAction(bar, "Search archives"),
            _browse, _extract, _columns.Action, refresh);
        ToolTip.SetTip(_status, "Loading follows the game’s archive and plugin rules.");
        LayoutUpdated += (_,_) => {
            var showModColumn = Bounds.Width >= 520;
            if (_showModColumn == showModColumn) return;
            _showModColumn = showModColumn; Render();
        };
        _filter.TextChanged += (_,_) => {
            if (ViewModel is { } model) model.SearchText = _filter.Text ?? "";
            Render();
        };
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            _active = true; ++_activation;
            _filter.Text = model.SearchText;
            var wasConnected = model.Profile.IsConnected;
            void Changed() {
                var connectionChanged = wasConnected != model.Profile.IsConnected;
                wasConnected = model.Profile.IsConnected;
                if (_target != model.Profile.CurrentTarget || connectionChanged || _revision != model.Profile.ContentRevision) _ = Refresh();
                UpdateActions();
            }
            model.Profile.Changed += Changed;
            Disposable.Create(() => { _active = false; ++_activation; model.Profile.Changed -= Changed; UpdateActions(); }).DisposeWith(d);
            _ = Refresh();
        });
    }
    internal async Task Refresh()
    {
        if (!_active || _reading || ViewModel is not { } model) return;
        var activation = _activation;
        _reading = true; _error = null;
        if (_target != model.Profile.CurrentTarget) _selection = null;
        _target = model.Profile.CurrentTarget; var target = _target.Value;
        var wasConnected = model.Profile.IsConnected;
        _revision = model.Profile.ContentRevision; var revision = _revision;
        _archives = []; Render(); _status.Text = "Reading MO2 archives…";
        try {
            var archives = await (_read?.Invoke(target) ?? model.Profile.ReadArchives(target));
            if (!_active || activation != _activation || target != model.Profile.CurrentTarget) return;
            _archives = archives;
            if (_selection is { } remembered && !archives.Any(x => x.Name == remembered.Name && x.Mod == remembered.Mod))
                _selection = null;
            Render();
        } catch (Exception error) { if (_active && activation == _activation && target == model.Profile.CurrentTarget) _error = error.Message; }
        finally {
            _reading = false;
            if (_active) {
                if (activation != _activation || target != model.Profile.CurrentTarget || wasConnected != model.Profile.IsConnected || revision != model.Profile.ContentRevision) await Refresh();
                else Render();
            }
        }
    }
    // What the two header actions are enabled by, which is also what the row menu asks.
    private bool ArchiveReady => _active && !_reading && ViewModel?.Profile.CanChangeOriginalUi == true &&
        _target == ViewModel.Profile.CurrentTarget && _table.RowSelection?.SelectedItem is Mo2Archive;
    private void UpdateActions() => _extract.IsEnabled = _browse.IsEnabled = ArchiveReady;
    private Mo2ColumnToggle? _columns;
    private void Render()
    {
        var rows = _archives.Where(x => (x.Name + " " + x.Mod).Contains(_filter.Text ?? "",StringComparison.OrdinalIgnoreCase)).ToArray();
        var source = new FlatTreeDataGridSource<Mo2Archive>(rows);
        // MO2 draws this tab as a single checked column (mainwindow.ui, bsaList has
        // columnCount 1), where the check is the archive's managed state rather than a
        // "Loaded" word in a column of its own: "archives not checked here are not
        // managed by MO and ignore installation order".
        source.Columns.Add(new TemplateColumn<Mo2Archive>("Archive", new FuncDataTemplate<Mo2Archive>((archive,_) => {
            var cell = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var check = new CheckBox { Name = "ArchiveManagedBox", IsChecked = archive?.Active == true, IsEnabled = archive?.CanToggle == true,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, MinWidth = 0, Padding = new Thickness(0) };
            // MO2's own tick: it decides whether MO2 manages the archive, and MO2
            // writes its profile's archive list the moment the tick changes. Without
            // this the box moved and nothing else did, and the next refresh put it
            // back — a control that looked like MO2's and did nothing.
            if (archive is not null) check.Click += async (_, e) => {
                e.Handled = true;
                if (ViewModel is not { } model) return;
                var wanted = check.IsChecked == true;
                check.IsEnabled = false;
                var updated = await model.Profile.SetArchiveManaged(archive, wanted, model.Profile.CurrentTarget);
                if (updated is null) { check.IsChecked = archive.Active; check.IsEnabled = archive.CanToggle; return; }
                _archives = updated; Render();
            };
            var label = new TextBlock { Text = archive?.Name, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            cell.Children.Add(check); cell.Children.Add(label);
            if (archive is not null) ToolTip.SetTip(cell, $"{archive.Name}\nMod: {archive.Mod}\nManaged by MO2: {(archive.Active ? "Yes" : "No")}");
            return cell;
        }),width:new GridLength(1,GridUnitType.Star)));
        source.RowSelection!.SelectionChanged += (_,_) => {
            if (ReferenceEquals(_table.Source, source)) _selection = source.RowSelection.SelectedItem;
            UpdateActions();
        };
        _columns?.Apply(source.Columns);
        var previous = _table.Source; _table.Source = source;
        if (previous is IDisposable disposable) disposable.Dispose();
        if (_selection is { } remembered) {
            var index = Array.FindIndex(rows,x => x.Name == remembered.Name && x.Mod == remembered.Mod);
            if (index >= 0) source.RowSelection.Select(new IndexPath(index));
        }
        _status.Text = _error ?? (_reading ? "Reading MO2 archives…" : _archives.Length == 0 ? "No archives reported by MO2." : $"{rows.Length} of {_archives.Length} archives");
        UpdateActions();
    }
}
