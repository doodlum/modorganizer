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

internal sealed record Mo2DataEntry(string Name, bool Directory, string[] Origins, string Archive, string Size = "", string Modified = "")
{
    public string Source => Origins.FirstOrDefault() ?? "";
    // MO2's Data tree names these Type, Size and Date modified
    // (filetreemodel.cpp). A folder has no type or size of its own, and a file
    // served out of an archive has no file to read either from.
    public string Type => Directory ? "" : Path.GetExtension(Name).TrimStart('.').ToUpperInvariant();
    public string SizeText => Directory || !long.TryParse(Size, out var bytes) ? "" : Bytes(bytes);
    public string ModifiedText => !long.TryParse(Modified, out var stamp) ? ""
        : DateTimeOffset.FromUnixTimeSeconds(stamp).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    private static string Bytes(long value) => value switch {
        < 1024 => value + " B",
        < 1024 * 1024 => (value / 1024d).ToString("0.#") + " KB",
        < 1024 * 1024 * 1024 => (value / (1024d * 1024)).ToString("0.#") + " MB",
        _ => (value / (1024d * 1024 * 1024)).ToString("0.#") + " GB",
    };
    public string Details => string.Join("\n", new[] { Name, Directory ? "Folder" : "Winning source: " + Source,
        Archive.Length > 0 ? "Archive: " + Archive : "", Origins.Length > 1 ? "Other sources: " + string.Join(", ", Origins.Skip(1)) : "" }.Where(x => x.Length > 0));
}
internal interface IMo2DataPage : IPageViewModelInterface { }
internal sealed class Mo2DataPage : APageViewModel<IMo2DataPage>, IMo2DataPage
{
    public static readonly IconValue DataIcon = new ProjektankerIcon("mdi-folder-file-outline");
    public Mo2LiveProfile Profile { get; }
    public string Directory { get; set; } = "";
    public string SearchText { get; set; } = "";
    public bool ConflictsOnly { get; set; }
    internal Mo2ProfileTarget? Target { get; set; }
    public Mo2DataPage(IWindowManager windows, Mo2LiveProfile profile) : base(windows)
    { Profile = profile; Target = profile.ProfilePath.Length > 0 ? profile.CurrentTarget : null; TabTitle = "Data"; TabIcon = DataIcon; }
}
internal sealed class Mo2DataView : ReactiveUserControl<Mo2DataPage>
{
    private readonly TreeDataGrid _table = Mo2FolderPage.Table("DataTable");
    private readonly TextBox _search = Mo2FolderPage.Search("DataSearch", "Search this folder");
    private readonly TextBlock _path = new() { Text = "Data", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = Mo2FolderPage.Status("DataStatus");
    // MO2's own "Conflicts only" box, which this page used to draw twice: once
    // beside the path and once in the row of MO2's filters. The one MO2 has is the
    // one in that row, and this is it.
    private readonly CheckBox _conflicts;
    private readonly Button _up;
    private readonly Button _open;
    private readonly Button _visibility;
    private readonly Button _reveal;
    private Mo2DataEntry[] _entries = [];
    private Mo2ProfileTarget? _target;
    private string _directory = "";
    private bool _reading;
    private long _revision = -1;
    private string? _error, _actionError;
    private (string Name, bool Directory)? _selection;
    private readonly Func<Mo2ProfileTarget, string, Task<Mo2DataEntry[]>>? _read;
    private bool _active;
    // What MO2's own dataTab checkboxes and filter field narrow the tree by.
    private bool _fromArchives = true, _hiddenFiles;
    private string _qtFilter = "";
    private long _activation;
    public Mo2DataView() : this(null) { }
    internal Mo2DataView(Func<Mo2ProfileTarget, string, Task<Mo2DataEntry[]>>? read)
    {
        _read = read;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        var header = new PageHeader { Title = "Data", Description = "Browse merged game files and their winning source mods.", Icon = new AvaloniaSvg("avares://MockHost/Assets/data-3d.svg") };
        root.Children.Add(header);
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,Auto,Auto"), Margin = new Thickness(0,8) };
        _up = Mo2ModRow.IconButton("mdi-arrow-up", "Parent folder", async () => await Navigate(_directory.Contains('/') ? _directory[.._directory.LastIndexOf('/')] : ""));
        _up.Name = "DataParentFolder";
        _open = Mo2ModRow.IconButton("mdi-folder-open-outline", "Open folder or preview file", async () => await OpenFolder());
        _reveal = Mo2ModRow.IconButton("mdi-folder-search-outline", "Open selected file’s source folder", async () => await RunAction("reveal"));
        _reveal.Name = "RevealDataFile";
        _visibility = Mo2ModRow.IconButton("mdi-eye-off-outline", "Hide or unhide selected file", async () => {
            if (_table.RowSelection?.SelectedItem is Mo2DataEntry { Directory: false } file)
                await RunAction(file.Name.EndsWith(".mohidden",StringComparison.OrdinalIgnoreCase) ? "unhide" : "hide");
        });
        var refresh = Mo2ModRow.IconButton("mdi-refresh", "Refresh Data", async () => { _actionError = null; await Refresh(); });
        toolbar.Children.Add(_up); Grid.SetColumn(_search,1); toolbar.Children.Add(_search); Grid.SetColumn(_open,2); toolbar.Children.Add(_open); Grid.SetColumn(_reveal,3); toolbar.Children.Add(_reveal); Grid.SetColumn(_visibility,4); toolbar.Children.Add(_visibility); Grid.SetColumn(refresh,5); toolbar.Children.Add(refresh);
        Grid.SetRow(toolbar,1); root.Children.Add(toolbar);
        var location = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; location.Children.Add(_path);
        // MO2's own dataTab furniture: a Refresh, the three checkboxes that narrow what
        // the tree lists, and its filter field.
        var qtBar = new StackPanel { Name = "DataQtBar", Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Margin = new Thickness(0,0,0,8) };
        qtBar.Children.Add(Mo2QtWidgets.Button("DataRefreshButton", "Refresh", Mo2QtWidgets.DataRefreshTip, "mdi-refresh",
            async () => { _actionError = null; await Refresh(); }));
        _conflicts = Mo2QtWidgets.Check("DataConflictsOnly", Mo2QtWidgets.ConflictsOnly, Mo2QtWidgets.ConflictsOnlyTip, false, _ => { });
        qtBar.Children.Add(_conflicts);
        qtBar.Children.Add(Mo2QtWidgets.Check("DataFromArchives", Mo2QtWidgets.FromArchives, Mo2QtWidgets.FromArchivesTip,
            _fromArchives, value => { _fromArchives = value; Render(); }));
        qtBar.Children.Add(Mo2QtWidgets.Check("DataHiddenFiles", Mo2QtWidgets.HiddenFiles, Mo2QtWidgets.HiddenFilesTip,
            _hiddenFiles, value => { _hiddenFiles = value; Render(); }));
        var qtFilter = Mo2QtWidgets.Filter("DataQtFilter", Mo2QtWidgets.DataFilterTip, text => { _qtFilter = text; Render(); }, out _);
        qtFilter.Margin = new Thickness(0,8,0,0);

        var tab = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        tab.Children.Add(qtBar);
        Grid.SetRow(_table,1); tab.Children.Add(_table);
        Grid.SetRow(qtFilter,2); tab.Children.Add(qtFilter);
        Grid.SetRow(location,2); root.Children.Add(location); Grid.SetRow(_status,3); root.Children.Add(_status);
        Grid.SetRow(tab,4); root.Children.Add(tab);
        Content = root;
        _columns = new Mo2ColumnToggle("data", Render);
        // Search in the row beneath, magnifier on the header line, as Mods does.
        // MO2's dataTab carries a Refresh, three tick boxes and a filter, and this
        // page draws all of them in the row of MO2's own widgets above the tree. The
        // rest of what used to be on this line is MO2's row menu — Open, Reveal in
        // Explorer and Hide are all on it — or a second Refresh beside the one MO2
        // has. What stays is the way back up a folder, which this page needs because
        // it navigates where MO2's tree expands in place, and the two that stand in
        // for something MO2 does from its list header.
        Mo2PanelChrome.Apply(this, root, header, _up, Mo2PanelChrome.SearchAction(toolbar, "Search this folder"));
        _search.TextChanged += (_,_) => { if (ViewModel is { } model) model.SearchText = _search.Text ?? ""; Render(); };
        _conflicts.IsCheckedChanged += (_,_) => { if (ViewModel is { } model) model.ConflictsOnly = _conflicts.IsChecked == true; Render(); };
        _table.DoubleTapped += async (_,_) => await OpenFolder();
        // MO2's own Data menu (filetree.cpp, FileTree::onContextMenu), cut to what MO2
        // will do for a file this frontend can name: open it, preview it through MO2's
        // own preview extensions, show the mod it came from, and hide or unhide it.
        // MO2's Add as Executable and Execute run a Windows program inside the prefix
        // and are not offered here; Save Tree to Text File writes from its own tree.
        Mo2RowMenu.Attach<Mo2DataEntry>(_table, entry => [
            entry is { Directory: true }
                ? Mo2EntryMenu.Action("Open", async () => await OpenFolder(), DataReady)
                : Mo2EntryMenu.Action("Preview", async () => await RunAction("preview"), DataReady),
            // MO2 offers Open on a file as well as Preview, and hands it to the handlers
            // inside its prefix; this opens the same file with the handlers this desk
            // has, as a download's Open File already does.
            entry is { Directory: false } ? Mo2EntryMenu.Action("Open", async () => await RunAction("open"), CanTouchFile(entry)) : null,
            // MO2's own, triggered where MO2 built it: it adds the file to the
            // executables this instance keeps, which the Tools page then lists.
            entry is { Directory: false } ? Mo2EntryMenu.Action("Add as Executable",
                async () => await AddAsExecutable(entry), CanTouchFile(entry)) : null,
            new Separator(),
            Mo2EntryMenu.Action("Reveal in Explorer", async () => await RunAction("reveal"), CanTouchFile(entry)),
            Mo2EntryMenu.Action("Open Mod Info", async () => await OpenModInfo(entry),
                DataReady && OriginMod(entry) is not null),
            new Separator(),
            entry is { Name: { } name } && name.EndsWith(".mohidden", StringComparison.OrdinalIgnoreCase)
                ? Mo2EntryMenu.Action("Un-Hide", async () => await RunAction("unhide"), CanTouchFile(entry))
                : Mo2EntryMenu.Action("Hide", async () => await RunAction("hide"), CanTouchFile(entry)),
            new Separator(),
            // MO2 puts Refresh on this menu as well as above the tree. The page already
            // had the action and only the button to reach it by, which is a difference
            // in what the menu offers rather than in what the page can do.
            Mo2EntryMenu.Action("Refresh", async () => { _actionError = null; await Refresh(); }, DataReady),
        ]);
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            _active = true; ++_activation;
            _directory = model.Directory;
            _search.Text = model.SearchText;
            _conflicts.IsChecked = model.ConflictsOnly;
            var connected = model.Profile.IsConnected;
            void Changed() {
                var edge = connected != model.Profile.IsConnected; connected = model.Profile.IsConnected;
                if (_target != model.Profile.CurrentTarget || edge || _revision != model.Profile.ContentRevision) _ = Refresh();
                UpdateActions();
            }
            model.Profile.Changed += Changed; Disposable.Create(() => { _active = false; ++_activation; model.Profile.Changed -= Changed; UpdateActions(); }).DisposeWith(d);
            Disposable.Create(() => model.Profile.HighlightDataSource(this, model.Profile.CurrentTarget, null)).DisposeWith(d); _ = Refresh();
        });
    }
    private async Task OpenFolder()
    {
        if (!_reading && _table.RowSelection?.SelectedItem is Mo2DataEntry { Directory: true } row)
            await Navigate((_directory.Length == 0 ? "" : _directory + "/") + row.Name);
        else await RunAction("preview");
    }
    // MO2's own Add as Executable, on the row MO2 has of that name. MO2 keeps the
    // executables, so it is asked to add one rather than told what to write.
    private async Task AddAsExecutable(Mo2DataEntry? entry)
    {
        if (!_active || _reading || ViewModel is not { } model || _target is not { } target || entry is not { Directory: false }) return;
        await model.Profile.RunFileMenu("data", [entry.Name], [["Add as Executable"]], target);
        await Refresh();
    }

    private async Task RunAction(string operation)
    {
        if (!_active || _reading || ViewModel is not { } model || _target is not { } target || _table.RowSelection?.SelectedItem is not Mo2DataEntry { Directory: false } file) return;
        var directory = _directory;
        var activation = _activation;
        _actionError = null;
        var error = await model.Profile.PreviewDataFile(directory, file, target, operation);
        await Refresh();
        if (_active && activation == _activation && target == model.Profile.CurrentTarget && directory == _directory) {
            _actionError = error;
            Render();
        }
    }
    // Reaching a folder without a pointer. MO2 lists a BSA's contents under the
    // folders it puts them in, so the Data root has no archive-served file at all
    // and the "from archives" box has nothing to take away there — a check that
    // only ever looked at the root could not exercise it.
    internal Task ShowFolder(string path) => Navigate(path);
    private async Task Navigate(string path)
    {
        if (!_active || _reading) return;
        _selection = null; _actionError = null;
        _directory = path; if (ViewModel is { } model) model.Directory = path;
        _search.Text = ""; await Refresh();
    }
    private async Task Refresh()
    {
        if (!_active || _reading || ViewModel is not { } model) return;
        if (model.Profile.ProfilePath.Length > 0) model.Target ??= model.Profile.CurrentTarget;
        if (model.Target != model.Profile.CurrentTarget) {
            // The shared host can change before this profile's view deactivates.
            // Hide stale rows but retain this tab's presentation state for return.
            _entries = []; _selection = null; _actionError = null; _error = null; Render();
            return;
        }
        var activation = _activation;
        _reading = true; _error = null;
        _target = model.Profile.CurrentTarget; var target = _target.Value; var connected = model.Profile.IsConnected;
        _revision = model.Profile.ContentRevision; var revision = _revision;
        _entries = []; Render();
        try {
            var entries = await (_read?.Invoke(target, _directory) ?? model.Profile.ReadDataDirectory(target, _directory));
            if (_active && activation == _activation && target == model.Profile.CurrentTarget) {
                _entries = entries;
                if (_selection is { } selected && !entries.Any(x => x.Name == selected.Name && x.Directory == selected.Directory)) _selection = null;
            }
        } catch (Exception error) { if (_active && activation == _activation && target == model.Profile.CurrentTarget) _error = error.Message; }
        finally {
            _reading = false;
            if (_active) {
                if (activation != _activation || target != model.Profile.CurrentTarget || connected != model.Profile.IsConnected || revision != model.Profile.ContentRevision) await Refresh();
                else Render();
            }
        }
    }
    // What the header actions are enabled by, and what the row menu asks in turn.
    private bool DataReady => _active && !_reading && ViewModel?.Profile.IsConnected == true &&
        _target == ViewModel.Profile.CurrentTarget && ViewModel?.Profile.CanChangeOriginalUi == true;
    // A file MO2 can act on individually: one it lays down itself, rather than one it
    // reads out of an archive.
    private bool CanTouchFile(Mo2DataEntry? entry) => DataReady && entry is { Directory: false, Archive.Length: 0 };

    // The mod a file came from, as MO2 reports its origins. "data" is the game's own
    // folder, which is not a mod MO2 has a page for.
    private Mo2LiveMod? OriginMod(Mo2DataEntry? entry) =>
        entry?.Origins.Select(origin => ViewModel?.Profile.Mods.FirstOrDefault(mod =>
            mod.Name.Equals(origin, StringComparison.OrdinalIgnoreCase) ||
            mod.DisplayName.Equals(origin, StringComparison.OrdinalIgnoreCase))).OfType<Mo2LiveMod>().FirstOrDefault();

    private async Task OpenModInfo(Mo2DataEntry? entry)
    {
        if (ViewModel is not { } model || OriginMod(entry) is not { } mod) return;
        await model.Profile.ShowModDetails(mod.Id);
    }

    private void UpdateActions()
    {
        var ready = _active && !_reading && ViewModel?.Profile.IsConnected == true && _target == ViewModel.Profile.CurrentTarget;
        _up.IsEnabled = ready && _directory.Length > 0;
        _visibility.IsEnabled = ready && ViewModel?.Profile.CanChangeOriginalUi == true && _table.RowSelection?.SelectedItem is Mo2DataEntry { Directory: false, Archive.Length: 0 };
        _reveal.IsEnabled = _visibility.IsEnabled;
        ToolTip.SetTip(_visibility, _table.RowSelection?.SelectedItem is Mo2DataEntry selected && selected.Name.EndsWith(".mohidden",StringComparison.OrdinalIgnoreCase) ? "Unhide selected file" : "Hide selected file");
        _open.IsEnabled = ready && ViewModel?.Profile.CanChangeOriginalUi == true && _table.RowSelection?.SelectedItem is Mo2DataEntry;
    }
    private Mo2ColumnToggle? _columns;
    // The handler that keeps the columns fitting the panel. Held so a re-render can
    // replace it rather than adding another one for every table it builds.
    private EventHandler? _fitColumns;
    private void Render()
    {
        // MO2's own filters, over the page's existing search: only conflicting files,
        // whether files served from archives count, and whether hidden ones do. A
        // folder is never filtered out by them — it is how the rest is reached.
        var rows = _entries.Where(x => (x.Name + " " + x.Source).Contains(_search.Text ?? "",StringComparison.OrdinalIgnoreCase)
            && (_qtFilter.Length == 0 || x.Name.Contains(_qtFilter, StringComparison.OrdinalIgnoreCase))
            && (_conflicts.IsChecked != true || x.Directory || x.Origins.Distinct().Count() > 1)
            && (_fromArchives || x.Directory || x.Archive.Length == 0)
            && (_hiddenFiles || x.Directory || !x.Name.EndsWith(".mohidden", StringComparison.OrdinalIgnoreCase))).ToArray();
        var source = new FlatTreeDataGridSource<Mo2DataEntry>(rows);
        // The same name cell the other two folder pages draw: the icon says whether
        // a row is a folder, where this page used to type an arrow in front of the
        // name and leave the rows half a character out of line with each other.
        source.Columns.Add(Mo2FolderPage.NameColumn<Mo2DataEntry>(row => (row.Name, row.Directory, row.Details, false)));
        // The rest of MO2's own Data columns (filetreemodel.cpp): Mod, Type, Size and
        // Date modified beside the name.
        // MinWidth 0 so a column the panel has no room for can be taken down to
        // nothing: left at the default the tree keeps enough for the heading, and a
        // "dropped" column still drew its title and a column of ellipses.
        TemplateColumn<Mo2DataEntry> Column(string header, Func<Mo2DataEntry, string> text, GridLength width) =>
            new(header, new FuncDataTemplate<Mo2DataEntry>((row, _) => {
                var label = new TextBlock { Text = row is null ? "" : text(row), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = Mo2TableRow.CellMargin };
                if (row is not null) ToolTip.SetTip(label, row.Details);
                return label;
            }), width: width, options: new Avalonia.Controls.Models.TreeDataGrid.TemplateColumnOptions<Mo2DataEntry> {
                MinWidth = new GridLength(0),
            });
        // The mod a file comes from is a fixed column, as MO2 draws it: sharing the
        // width evenly with the name left both halved, and every file in a folder of
        // long names was drawn as "Carava…".
        source.Columns.Add(Column("Mod", x => x.Source, new GridLength(150)));
        source.Columns.Add(Column("Type", x => x.Type, new GridLength(70)));
        source.Columns.Add(Column("Size", x => x.SizeText, new GridLength(90)));
        source.Columns.Add(Column("Date modified", x => x.ModifiedText, new GridLength(140)));
        // Dropped from the right as the panel narrows, so the name always has room:
        // in MO2's own panel layout this tree is half a window wide, and four fixed
        // columns left the name column — the one the tree is read by — at nothing but
        // its icon.
        (int Column, double Width, double Threshold)[] optional = [
            (1, 150, 380), (2, 70, 460), (3, 90, 560), (4, 140, 700)];
        void FitColumns() {
            var width = _table.Bounds.Width;
            if (width <= 0) return;
            foreach (var (column, natural, threshold) in optional)
                source.Columns.SetColumnWidth(column, new GridLength(width >= threshold ? natural : 0));
        }
        _table.LayoutUpdated -= _fitColumns;
        _fitColumns = (_, _) => FitColumns();
        _table.LayoutUpdated += _fitColumns;
        FitColumns();
        source.RowSelection!.SelectionChanged += (_,_) => {
            if (_active && ReferenceEquals(_table.Source, source) && ViewModel is { } model && _target is { } target) {
                _selection = source.RowSelection.SelectedItem is { } row ? (row.Name, row.Directory) : null;
                model.Profile.HighlightDataSource(this, target, source.RowSelection.SelectedItem is { Directory: false } selected ? selected.Source : null);
            }
            UpdateActions();
        };
        _columns?.Apply(source.Columns);
        var old = _table.Source; _table.Source = source; (old as IDisposable)?.Dispose();
        if (ViewModel is { } model) model.Profile.HighlightDataSource(this, model.Profile.CurrentTarget, null);
        if (_selection is { } remembered) {
            var index = Array.FindIndex(rows, x => x.Name == remembered.Name && x.Directory == remembered.Directory);
            if (index >= 0) source.RowSelection.Select(new IndexPath(index));
        }
        _path.Text = "Data" + (_directory.Length > 0 ? "/" + _directory : ""); ToolTip.SetTip(_path,_path.Text);
        _status.Text = _actionError ?? _error ?? (_reading ? "Reading MO2 Data…" : $"{rows.Length} of {_entries.Length} entries"); UpdateActions();
    }
}
