using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

internal sealed record Mo2OverwriteFile(string Path, long Bytes);
internal interface IMo2OverwritePage : IPageViewModelInterface { }
internal sealed class Mo2OverwritePage : APageViewModel<IMo2OverwritePage>, IMo2OverwritePage
{
    public Mo2LiveProfile Profile { get; }
    public Mo2OverwritePage(IWindowManager windows, Mo2LiveProfile profile) : base(windows)
    { Profile = profile; TabTitle = "Overwrite"; TabIcon = IconValues.Folder; }
}
internal sealed class Mo2OverwriteView : ReactiveUserControl<Mo2OverwritePage>
{
    private readonly TreeDataGrid _table = Mo2FolderPage.Table("OverwriteFiles");
    private readonly TextBlock _status = Mo2FolderPage.Status("OverwriteStatus");
    private readonly TextBox _search = Mo2FolderPage.Search("OverwriteSearch", "Search generated files");
    private readonly List<Button> _actions = [];
    private Mo2OverwriteFile[] _files = [];
    private Mo2ProfileTarget? _target;
    private bool _loading, _loaded;
    private string? _error, _selectedPath;
    private readonly Func<Mo2ProfileTarget, Task<Mo2OverwriteFile[]>>? _read;
    private bool _active;
    private long _activation;
    public Mo2OverwriteView() : this(null) { }
    internal Mo2OverwriteView(Func<Mo2ProfileTarget, Task<Mo2OverwriteFile[]>>? read)
    {
        _read = read;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        var header = new PageHeader { Title = "Overwrite", Icon = new AvaloniaSvg("avares://MockHost/Assets/Pictograms/overwrite.svg"),
            Description = "Generated files that take priority over installed mods." };
        root.Children.Add(header);
        // These were labelled buttons in a row of their own under the header, the
        // only page in the frontend that had one. They are the folder's actions, so
        // they sit on the header line as icons like every other page's do, and what
        // the row of text underneath used to explain is in their tooltips.
        var inspect = Mo2TableRow.IconButton("mdi-file-search-outline",
            "Open the Overwrite folder's files in MO2", async () => {
                if (ViewModel is { } model && _target == model.Profile.CurrentTarget &&
                    model.Profile.Mods.SingleOrDefault(x => x.IsOverwrite) is { } mod) {
                    await model.Profile.ShowModDetails(mod.Id); await Refresh();
                }
            });
        inspect.Name = "OpenOverwriteFiles";
        _actions.Add(inspect);
        var operations = new (string Icon, string Label, string Operation)[] {
            ("mdi-folder-plus-outline", "Create a mod from everything in Overwrite…", "create"),
            ("mdi-folder-move-outline", "Move everything in Overwrite into an existing mod…", "move"),
            ("mdi-folder-sync-outline", "Sync every file back to the mod it came from…", "sync"),
            ("mdi-delete-outline", "Delete everything in Overwrite…", "clear"),
        };
        var actions = new List<Control> { inspect };
        foreach (var (icon, label, operation) in operations) {
            var button = Mo2TableRow.IconButton(icon, label, async () => {
                if (ViewModel is not { } model || _target is not { } target) return;
                await model.Profile.OverwriteAction(operation, target); await Refresh();
            });
            button.Name = "Overwrite_" + operation; button.Tag = operation;
            _actions.Add(button); actions.Add(button);
        }
        var refresh = Mo2TableRow.IconButton("mdi-refresh", "Refresh Overwrite", async () => await Refresh());
        refresh.Name = "RefreshOverwrite";
        var filter = new StackPanel { Spacing = 8, Margin = new Thickness(0,4,0,8) }; filter.Children.Add(_search);
        Grid.SetRow(filter,1); root.Children.Add(filter);
        Grid.SetRow(_status,2); root.Children.Add(_status);
        Grid.SetRow(_table,3); root.Children.Add(_table);
        Content = root;
        _columns = new Mo2ColumnToggle("overwrite", Render);
        // Search beneath the header behind a magnifier, and a way to hide a column,
        // as the other two folder pages have.
        Mo2PanelChrome.Apply(this, root, header,
            [Mo2PanelChrome.SearchAction(filter, "Search generated files"), .. actions, refresh]);
        _search.TextChanged += (_,_) => Render();
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            _active = true; ++_activation;
            var wasConnected = model.Profile.IsConnected;
            void Changed() {
                var connectionChanged = wasConnected != model.Profile.IsConnected;
                wasConnected = model.Profile.IsConnected;
                if (_target != model.Profile.CurrentTarget || connectionChanged) _ = Refresh();
                UpdateActions();
            }
            model.Profile.Changed += Changed;
            Disposable.Create(() => { _active = false; ++_activation; model.Profile.Changed -= Changed; UpdateActions(); }).DisposeWith(d);
            _ = Refresh();
        });
    }
    internal async Task Refresh()
    {
        if (!_active || _loading || ViewModel is not { } model) return;
        var activation = _activation;
        _loading = true; _loaded = false; _error = null;
        if (_target != model.Profile.CurrentTarget) _selectedPath = null;
        _target = model.Profile.CurrentTarget; var target = _target;
        var wasConnected = model.Profile.IsConnected;
        _files = []; Render(); _status.Text = "Reading MO2 Overwrite…"; UpdateActions();
        try { var files = await (_read?.Invoke(target!.Value) ?? model.Profile.ReadOverwrite(target.Value)); if (_active && activation == _activation && target == model.Profile.CurrentTarget) { _files = files; _loaded = true; Render(); } }
        catch (Exception error) { if (_active && activation == _activation && target == model.Profile.CurrentTarget) _error = error.Message; }
        finally {
            _loading = false;
            if (_active) {
                if (activation != _activation || target != model.Profile.CurrentTarget || wasConnected != model.Profile.IsConnected) await Refresh();
                else { Render(); UpdateActions(); }
            }
        }
    }
    private void UpdateActions()
    {
        foreach (var button in _actions) button.IsEnabled = _active && _loaded && !_loading && ViewModel?.Profile.CanChangeOriginalUi == true && _target == ViewModel.Profile.CurrentTarget && (button.Tag is null || _files.Length > 0);
    }
    private Mo2ColumnToggle? _columns;
    private void Render()
    {
        var visible = _files.Where(x => x.Path.Contains(_search.Text ?? "",StringComparison.OrdinalIgnoreCase)).ToArray();
        var source = new FlatTreeDataGridSource<Mo2OverwriteFile>(visible);
        // The shared name cell and the shared way of writing a size: this page
        // counted in B and KB only, so a 40MB generated file read as 41277.5 KB
        // where the same file on External Files read as 40.31 MB.
        source.Columns.Add(Mo2FolderPage.NameColumn<Mo2OverwriteFile>(row => (row.Path, false, row.Path, false)));
        source.Columns.Add(Mo2FolderPage.SizeColumn<Mo2OverwriteFile>(x => Mo2FolderPage.SizeText(x.Bytes)));
        _columns?.Apply(source.Columns);
        source.RowSelection!.SelectionChanged += (_,_) => {
            if (source.RowSelection.SelectedItem is { } selected) _selectedPath = selected.Path;
        };
        var previous = _table.Source; _table.Source = source;
        if (previous is IDisposable disposable) disposable.Dispose();
        if (_selectedPath is { } path) {
            var index = Array.FindIndex(visible,x => x.Path == path);
            if (index >= 0) source.RowSelection.Select(new IndexPath(index));
        }
        _status.Text = _error ?? (_loading ? "Reading MO2 Overwrite…" : !_loaded ? "" : _files.Length == 0 ? "Overwrite is empty." : $"{visible.Length} of {_files.Length} files · Stored by MO2 for this game instance.");
    }
}
