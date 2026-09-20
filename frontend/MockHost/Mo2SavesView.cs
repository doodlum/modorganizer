using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
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
    private bool _backgroundReading, _refreshRequested;
    private string? _error;
    private string? _actionError;
    private readonly HashSet<string> _selectedFiles = new(StringComparer.Ordinal);
    private readonly Func<Mo2ProfileTarget, Task<Mo2Save[]>>? _read;
    private bool _active;
    private long _activation;
    public Mo2SavesView() : this(null) { }
    internal Mo2SavesView(Func<Mo2ProfileTarget, Task<Mo2Save[]>>? read, TimeSpan? refreshInterval = null)
    {
        _read = read;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        var header = new PageHeader { Title = "Saves", Description = "Saved games available to the selected MO2 profile.", Icon = new AvaloniaSvg("avares://NexusModsApp/Assets/saves-3d.svg") };
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
        Mo2PanelChrome.Apply(this, root, header, Mo2PanelChrome.SearchAction(bar, "Search saves"));
        _table.Classes.Add("MainListsStyling");
        _search.TextChanged += (_,_) => Render();
        _table.DoubleTapped += async (_,_) => await RunAction("details");
        // SavesTab::eventFilter sends Delete to the native confirmation for the
        // selected saves. Bind it to the table, leaving search editing untouched.
        _table.KeyDown += async (_, args) => {
            if (args.Key != Key.Delete || !Ready) return;
            args.Handled = true;
            await RunAction("delete");
        };
        // MO2's own saves menu (savestab.cpp, onContextMenu), which is the only place
        // its Saves tab offers anything: fixing the mods a save wants, deleting it,
        // and showing it in a file manager. The header line keeps the same actions;
        // this is where MO2 users reach for them.
        Mo2RowMenu.Attach<Mo2Save>(_table, save => [
            Mo2EntryMenu.Action("Fix enabled mods...", () => RunAction("repair"), Ready && SelectedSaves.Length == 1),
            Mo2EntryMenu.Action($"Delete {SelectedSaves.Length} save(s)", () => RunAction("delete"), Ready),
            Mo2EntryMenu.Action("Open in Explorer...", () => OpenSave(save), Ready),
        ], availability: ReadMenuAvailability);
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            _active = true; ++_activation;
            if (_read is null) new Mo2SaveHover(this, _table, model.Profile).DisposeWith(d);
            var connected = model.Profile.IsConnected;
            void Changed() {
                var edge = connected != model.Profile.IsConnected; connected = model.Profile.IsConnected;
                if (_target != model.Profile.CurrentTarget || edge) _ = Refresh();
                UpdateActions();
            }
            // A game can create saves without changing mods, plugins or profiles.
            // Read through MO2 so local-save settings and Wine drive mappings stay
            // native. Coalesce ticks, pause during host actions, and stop on detach.
            var timer = new DispatcherTimer { Interval = refreshInterval ?? TimeSpan.FromSeconds(3) };
            timer.Tick += async (_, _) => {
                if (IsEffectivelyVisible && model.Profile.CanChangeOriginalUi) await Refresh(background: true);
            };
            model.Profile.Changed += Changed;
            Disposable.Create(() => {
                timer.Stop(); _active = false; _backgroundReading = false; _refreshRequested = false; ++_activation;
                model.Profile.Changed -= Changed; UpdateActions();
            }).DisposeWith(d);
            timer.Start(); _ = Refresh();
        });
    }
    private async Task<Mo2MenuEntry[]?> ReadMenuAvailability(Mo2Save? save)
    {
        // Injected save readers have no corresponding native files.
        if (_read is not null) return null;
        if (!Ready || ViewModel is not { } model || _target is not { } target) return [];
        var activation = _activation;
        var files = SelectedSaves.Select(row => row.File).ToArray();
        var entries = await model.Profile.ReadListMenu("readFileMenu", files, target, "saves");
        return Ready && activation == _activation && target == model.Profile.CurrentTarget &&
            files.SequenceEqual(SelectedSaves.Select(row => row.File)) ? entries : [];
    }

    internal async Task RunAction(string operation)
    {
        if (!Ready || ViewModel is not { } model || _target is not { } target) return;
        var saves = SelectedSaves;
        if (operation != "delete" && saves.Length != 1) return;
        var activation = _activation;
        _actionError = null;
        var error = await model.Profile.SaveAction(saves, operation, target);
        await Refresh();
        if (_active && activation == _activation && target == model.Profile.CurrentTarget && error is not null) {
            _actionError = error; _status.Text = error;
        }
    }
    // What the header buttons are enabled by, which is also what the menu asks.
    private bool Ready => _active && (!_reading || _backgroundReading) && ViewModel?.Profile.CanChangeOriginalUi == true &&
        _target == ViewModel.Profile.CurrentTarget && _table.RowSelection?.SelectedItem is Mo2Save;
    private Mo2Save[] SelectedSaves => _table.RowSelection?.SelectedItems.OfType<Mo2Save>().ToArray() ?? [];
    private void UpdateActions()
    {
        _details.IsEnabled = _repair.IsEnabled = Ready && SelectedSaves.Length == 1;
        _delete.IsEnabled = Ready;
    }

    // MO2 names a save relative to the folder it read it from, so the two are put
    // back together here and the file is shown in the desktop's own file manager.
    private async Task OpenSave(Mo2Save? save)
    {
        if (save is null || !Ready || ViewModel?.Profile is not { } profile || _target is not { } target) return;
        if (profile.SavesDirectory is not { Length: > 0 } folder) {
            // The owning host knows how C: and custom Wine drives are mapped.
            await profile.RunFileMenu("saves", SelectedSaves.Select(row => row.File).ToArray(), [["Open in Explorer..."]], target);
            return;
        }
        var path = Path.Combine(folder, save.File.Replace('\\', '/'));
        if (!File.Exists(path)) { _status.Text = "MO2 no longer has " + save.File; return; }
        profile.RevealLocalFile?.Invoke(path);
    }
    internal async Task Refresh(bool background = false)
    {
        if (!_active || ViewModel is not { } model) return;
        if (_reading) { if (!background) _refreshRequested = true; return; }
        var activation = _activation;
        var retainRows = background && _target == model.Profile.CurrentTarget;
        var changed = false;
        _reading = true; _backgroundReading = retainRows; _error = null;
        if (_target != model.Profile.CurrentTarget) { _actionError = null; _selectedFiles.Clear(); }
        _target = model.Profile.CurrentTarget;
        var target = _target.Value; var connected = model.Profile.IsConnected;
        if (!retainRows) { _saves = []; Render(); }
        else UpdateActions();
        try {
            var saves = await (_read?.Invoke(target) ?? model.Profile.ReadSaves(target));
            if (_active && activation == _activation && target == model.Profile.CurrentTarget) {
                changed = !_saves.SequenceEqual(saves);
                _saves = saves;
                _selectedFiles.IntersectWith(saves.Select(save => save.File));
            }
        } catch (Exception error) { if (_active && activation == _activation && target == model.Profile.CurrentTarget) _error = error.Message; }
        finally {
            _reading = false; _backgroundReading = false;
            if (_active) {
                if (_refreshRequested || activation != _activation || target != model.Profile.CurrentTarget || connected != model.Profile.IsConnected) {
                    _refreshRequested = false; await Refresh();
                }
                else if (!retainRows || changed) Render();
                else { UpdateActions(); UpdateStatus(); }
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
        TemplateColumn<Mo2Save> Column(string header, Func<Mo2Save, string> text, GridLength width) =>
            new(header, new FuncDataTemplate<Mo2Save>((row, _) => {
                if (row is null) return null;
                var label = new TextBlock { Text = text(row), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = Mo2TableRow.CellMargin };
                // Native hover already supplies the extension's complete save
                // information. A second text tooltip would cover that widget.
                if (_read is not null || ViewModel?.Profile.CanPreviewSaves != true)
                    ToolTip.SetTip(label, row.Name + "\n" + row.File);
                return label;
            }), width: width);
        source.Columns.Add(Column("Name", x => x.Name, new GridLength(1, GridUnitType.Star)));
        source.Columns.Add(Column("File", x => x.File, new GridLength(1, GridUnitType.Star)));
        source.RowSelection!.SingleSelect = false;
        _columns?.Apply(source.Columns);
        var old = _table.Source; _table.Source = source; (old as IDisposable)?.Dispose();
        for (var index = 0; index < rows.Length; index++)
            if (_selectedFiles.Contains(rows[index].File)) source.RowSelection.Select(new IndexPath(index));
        // Attach after restoring: intermediate selections must not erase the rest
        // of the saved set. Filtering keeps hidden identities, but actions only
        // operate on rows selected in the visible table.
        source.RowSelection.SelectionChanged += (_,_) => {
            if (ReferenceEquals(_table.Source, source)) {
                _selectedFiles.Clear();
                _selectedFiles.UnionWith(source.RowSelection.SelectedItems.OfType<Mo2Save>().Select(save => save.File));
            }
            UpdateActions();
        };
        UpdateActions();
        UpdateStatus();
    }
    private void UpdateStatus() => _status.Text = _actionError ?? _error ?? (_reading && !_backgroundReading
        ? "Reading MO2 saves…" : _saves.Length == 0 ? "No saves reported by MO2 for this profile."
        : $"{_table.Rows?.Count ?? 0} of {_saves.Length} saves");
}
