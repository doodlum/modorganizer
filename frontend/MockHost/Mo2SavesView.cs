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
internal sealed record Mo2Save(string Name, string File);
internal interface IMo2SavesPage : IPageViewModelInterface { }
internal sealed class Mo2SavesPage : APageViewModel<IMo2SavesPage>, IMo2SavesPage
{
    public Mo2LiveProfile Profile { get; }
    public Mo2SavesPage(IWindowManager windows, Mo2LiveProfile profile) : base(windows)
    { Profile = profile; TabTitle = "Saves"; TabIcon = IconValues.Save; }
}
internal sealed class Mo2SavesView : ReactiveUserControl<Mo2SavesPage>
{
    private readonly TreeDataGrid _table = new() { Name = "SavesTable", ShowColumnHeaders = true };
    private readonly TextBox _search = new() { Watermark = "Search saves", MinWidth = 60 };
    private readonly TextBlock _status = new() { Name = "SavesStatus", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _details;
    private readonly Button _repair;
    private readonly Button _delete;
    private Mo2Save[] _saves = [];
    private Mo2ProfileTarget? _target;
    private bool _reading;
    private string? _error;
    private string? _actionError;
    private string? _selectedFile;
    private readonly Func<Mo2ProfileTarget, Task<Mo2Save[]>>? _read;
    private bool _active;
    private long _activation;
    public Mo2SavesView() : this(null) { }
    internal Mo2SavesView(Func<Mo2ProfileTarget, Task<Mo2Save[]>>? read)
    {
        _read = read;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        var header = new PageHeader { Title = "Saves", Description = "Saved games available to the selected MO2 profile.", Icon = new AvaloniaSvg("avares://MockHost/Assets/saves-3d.svg") };
        root.Children.Add(header);
        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto"), Margin = new Thickness(0,8) };
        bar.Children.Add(_search);
        var refresh = Mo2ModRow.IconButton("mdi-refresh", "Refresh saves", async () => { _actionError = null; await Refresh(); });
        _details = Mo2ModRow.IconButton("mdi-information-outline", "View selected save details", async () => await RunAction("details"));
        _repair = Mo2ModRow.IconButton("mdi-wrench-outline", "Fix enabled mods for selected save", async () => await RunAction("repair"));
        _delete = Mo2ModRow.IconButton("mdi-trash-can-outline", "Delete selected save…", async () => await RunAction("delete"));
        Grid.SetColumn(_details,1); bar.Children.Add(_details);
        Grid.SetColumn(_repair,2); bar.Children.Add(_repair); Grid.SetColumn(_delete,3); bar.Children.Add(_delete);
        Grid.SetColumn(refresh,4); bar.Children.Add(refresh);
        Grid.SetRow(bar,1); root.Children.Add(bar); Grid.SetRow(_status,2); root.Children.Add(_status); Grid.SetRow(_table,3); root.Children.Add(_table);
        Content = root;
        _columns = new Mo2ColumnToggle("saves", Render);
        // Search in the row beneath, magnifier on the header line, as Mods does.
        // MO2's savesTab is its list and nothing else — no buttons at all — and the
        // three actions it does offer are on the menu it builds for a save row:
        // details, fixing the mods a save wants, and deleting it. They were drawn
        // here as well; the row menu is where MO2 has them.
        Mo2PanelChrome.Apply(this, root, header, Mo2PanelChrome.SearchAction(bar, "Search saves"),
            _columns.Action);
        _table.Classes.Add("MainListsStyling");
        _search.TextChanged += (_,_) => Render();
        _table.DoubleTapped += async (_,_) => await RunAction("details");
        // MO2's own saves menu (savestab.cpp, onContextMenu), which is the only place
        // its Saves tab offers anything: fixing the mods a save wants, deleting it,
        // and showing it in a file manager. The header line keeps the same actions;
        // this is where MO2 users reach for them.
        Mo2RowMenu.Attach<Mo2Save>(_table, save => [
            Mo2EntryMenu.Action("Fix enabled mods...", () => RunAction("repair"), Ready),
            Mo2EntryMenu.Action("Delete 1 save(s)", () => RunAction("delete"), Ready),
            // Opened on this desktop: MO2's own explores the folder inside its prefix.
            Mo2EntryMenu.Action("Open in Explorer...", () => { OpenSave(save); return Task.CompletedTask; },
                ViewModel?.Profile.SavesDirectory is { Length: > 0 }),
        ]);
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            _active = true; ++_activation;
            var connected = model.Profile.IsConnected;
            void Changed() {
                var edge = connected != model.Profile.IsConnected; connected = model.Profile.IsConnected;
                if (_target != model.Profile.CurrentTarget || edge) _ = Refresh();
                UpdateActions();
            }
            model.Profile.Changed += Changed; Disposable.Create(() => { _active = false; ++_activation; model.Profile.Changed -= Changed; UpdateActions(); }).DisposeWith(d); _ = Refresh();
        });
    }
    private async Task RunAction(string operation)
    {
        if (!_active || _reading || ViewModel is not { } model || _target is not { } target || _table.RowSelection?.SelectedItem is not Mo2Save save) return;
        var activation = _activation;
        _actionError = null;
        var error = await model.Profile.SaveAction(save, operation, target);
        await Refresh();
        if (_active && activation == _activation && target == model.Profile.CurrentTarget && error is not null) {
            _actionError = error; _status.Text = error;
        }
    }
    // What the header buttons are enabled by, which is also what the menu asks.
    private bool Ready => _active && !_reading && ViewModel?.Profile.CanChangeOriginalUi == true &&
        _target == ViewModel.Profile.CurrentTarget && _table.RowSelection?.SelectedItem is Mo2Save;
    private void UpdateActions() => _details.IsEnabled = _repair.IsEnabled = _delete.IsEnabled = Ready;

    // MO2 names a save relative to the folder it read it from, so the two are put
    // back together here and the file is shown in the desktop's own file manager.
    private void OpenSave(Mo2Save save)
    {
        if (ViewModel?.Profile is not { SavesDirectory: { Length: > 0 } folder } profile) return;
        var path = Path.Combine(folder, save.File.Replace('\\', '/'));
        if (!File.Exists(path)) { _status.Text = "MO2 no longer has " + save.File; return; }
        profile.RevealLocalFile?.Invoke(path);
    }
    internal async Task Refresh()
    {
        if (!_active || _reading || ViewModel is not { } model) return;
        var activation = _activation;
        _reading = true; _error = null;
        if (_target != model.Profile.CurrentTarget) { _actionError = null; _selectedFile = null; }
        _target = model.Profile.CurrentTarget;
        var target = _target.Value; var connected = model.Profile.IsConnected;
        _saves = []; Render();
        try {
            var saves = await (_read?.Invoke(target) ?? model.Profile.ReadSaves(target));
            if (_active && activation == _activation && target == model.Profile.CurrentTarget) {
                _saves = saves;
                if (_selectedFile is not null && !saves.Any(save => save.File == _selectedFile)) _selectedFile = null;
            }
        } catch (Exception error) { if (_active && activation == _activation && target == model.Profile.CurrentTarget) _error = error.Message; }
        finally {
            _reading = false;
            if (_active) {
                if (activation != _activation || target != model.Profile.CurrentTarget || connected != model.Profile.IsConnected) await Refresh();
                else Render();
            }
        }
    }
    private Mo2ColumnToggle? _columns;
    private void Render()
    {
        var rows = _saves.Where(x => (x.Name + " " + x.File).Contains(_search.Text ?? "",StringComparison.OrdinalIgnoreCase)).ToArray();
        var source = new FlatTreeDataGridSource<Mo2Save>(rows);
        // MO2's own two columns for this tab (mainwindow.ui, savegameList): the save's
        // name and the file it is stored in, rather than one column carrying both.
        static TemplateColumn<Mo2Save> Column(string header, Func<Mo2Save, string> text, GridLength width) =>
            new(header, new FuncDataTemplate<Mo2Save>((row, _) => {
                var label = new TextBlock { Text = row is null ? "" : text(row), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = Mo2TableRow.CellMargin };
                if (row is not null) ToolTip.SetTip(label, row.Name + "\n" + row.File);
                return label;
            }), width: width);
        source.Columns.Add(Column("Name", x => x.Name, new GridLength(1, GridUnitType.Star)));
        source.Columns.Add(Column("File", x => x.File, new GridLength(1, GridUnitType.Star)));
        source.RowSelection!.SelectionChanged += (_,_) => {
            if (ReferenceEquals(_table.Source, source)) _selectedFile = source.RowSelection.SelectedItem?.File;
            UpdateActions();
        };
        _columns?.Apply(source.Columns);
        var old = _table.Source; _table.Source = source; (old as IDisposable)?.Dispose();
        if (_selectedFile is { } file) {
            var index = Array.FindIndex(rows, row => row.File == file);
            if (index >= 0) source.RowSelection.Select(new IndexPath(index));
        }
        UpdateActions();
        _status.Text = _actionError ?? _error ?? (_reading ? "Reading MO2 saves…" : _saves.Length == 0 ? "No saves reported by MO2 for this profile." : $"{rows.Length} of {_saves.Length} saves");
    }
}
