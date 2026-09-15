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
    private readonly TreeDataGrid _table = new() { Name = "OverwriteFiles", ShowColumnHeaders = true };
    private readonly TextBlock _status = new() { Name = "OverwriteStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _search = new() { Name = "OverwriteSearch", Watermark = "Filter generated files" };
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
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        var header = new PageHeader { Title = "Overwrite", Icon = new AvaloniaSvg("avares://MockHost/Assets/Pictograms/overwrite.svg"),
            Description = "Generated files that take priority over installed mods." };
        root.Children.Add(header);
        var text = new TextBlock { Text = "Move these files into a mod to organise them. Actions below apply to the entire Overwrite folder.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12), Opacity = .75 };
        Grid.SetRow(text,1); root.Children.Add(text);
        var bar = new WrapPanel();
        var inspect = new Button { Content = "Open files…", Name = "OpenOverwriteFiles" };
        inspect.Click += async (_,_) => { if (ViewModel is { } model && _target == model.Profile.CurrentTarget && model.Profile.Mods.SingleOrDefault(x => x.IsOverwrite) is { } mod) { await model.Profile.ShowModDetails(mod.Id); await Refresh(); } };
        bar.Children.Add(inspect); _actions.Add(inspect);
        foreach (var (label, operation) in new[] { ("Create mod…","create"), ("Move to mod…","move"), ("Sync to mods…","sync"), ("Clear…","clear") }) {
            var button = new Button { Content = label, Name = "Overwrite_" + operation, Tag = operation };
            button.Click += async (_,_) => { if (ViewModel is not { } model || _target is not { } target) return; await model.Profile.OverwriteAction(operation,target); await Refresh(); };
            bar.Children.Add(button); _actions.Add(button);
        }
        // The header action is the shared icon button every other panel uses; the
        // labelled Refresh it replaced was the one 62px control on those header lines.
        var refresh = Mo2TableRow.IconButton("mdi-refresh", "Refresh Overwrite", async () => await Refresh());
        refresh.Name = "RefreshOverwrite";
        foreach (var button in bar.Children) button.Margin = new Thickness(0,0,8,8);
        Grid.SetRow(bar,2); root.Children.Add(bar);
        var filter = new StackPanel { Spacing = 8, Margin = new Thickness(0,4,0,8) }; filter.Children.Add(_search); filter.Children.Add(_status);
        Grid.SetRow(filter,3); root.Children.Add(filter); Grid.SetRow(_table,4); root.Children.Add(_table);
        _table.Classes.Add("MainListsStyling"); Content = root;
        Mo2PanelChrome.Apply(this, root, header, refresh);
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
    private void Render()
    {
        var visible = _files.Where(x => x.Path.Contains(_search.Text ?? "",StringComparison.OrdinalIgnoreCase)).ToArray();
        var source = new FlatTreeDataGridSource<Mo2OverwriteFile>(visible);
        source.Columns.Add(new TextColumn<Mo2OverwriteFile,string>("File",x => x.Path,width:new GridLength(1,GridUnitType.Star)));
        source.Columns.Add(new TextColumn<Mo2OverwriteFile,string>("Size",x => x.Bytes < 1024 ? $"{x.Bytes} B" : $"{x.Bytes / 1024d:0.#} KB",width:new GridLength(85)));
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
