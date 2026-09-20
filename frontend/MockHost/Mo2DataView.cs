using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

internal sealed record Mo2DataEntry(string Name, bool Directory, string[] Origins, string Archive, string Size = "", string Modified = "") : System.ComponentModel.INotifyPropertyChanged
{
    public bool IsLoading { get; internal init; }
    public string ParentPath { get; internal set; } = "";
    public string RelativePath => Mo2DataView.DataRelativePath(ParentPath, Name);
    public List<Mo2DataEntry> Children { get; internal set; } = [];
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    internal Action<Mo2DataEntry>? ExpansionChanged;
    private bool _expanded;
    public bool IsExpanded {
        get => _expanded;
        set {
            if (_expanded == value) return;
            _expanded = value;
            PropertyChanged?.Invoke(this, new(nameof(IsExpanded)));
            ExpansionChanged?.Invoke(this);
        }
    }
    // FileTreeModel::shouldShowFile/shouldShowFolder: conflicts mode excludes
    // hidden entries even when Show Hidden Files is checked.
    internal bool MatchesVisibility(bool conflictsOnly, bool fromArchives, bool hiddenFiles)
    {
        var hidden = Name.EndsWith(".mohidden", StringComparison.OrdinalIgnoreCase);
        if (hidden && (!hiddenFiles || conflictsOnly)) return false;
        return Directory || ((!conflictsOnly || Origins.Distinct().Count() > 1)
            && (fromArchives || Archive.Length == 0));
    }
    public string Source => Origins.FirstOrDefault() ?? "";
    // MO2's Data tree names these Type, Size and Date modified
    // (filetreemodel.cpp). A folder has no type or size of its own, and a file
    // served out of an archive has no file to read either from.
    public string Type => Directory ? "" : Path.GetExtension(Name).TrimStart('.').ToUpperInvariant();
    public string SizeText => Directory || !long.TryParse(Size, out var bytes) ? "" : Mo2FolderPage.SizeText(bytes);
    public string ModifiedText => !long.TryParse(Modified, out var stamp) ? ""
        : DateTimeOffset.FromUnixTimeSeconds(stamp).LocalDateTime.ToString("yyyy-MM-dd HH:mm");
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
    internal HashSet<string> ExpandedDirectories { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Mo2ProfileTarget? Target { get; set; }
    public Mo2DataPage(IWindowManager windows, Mo2LiveProfile profile) : base(windows)
    { Profile = profile; Target = profile.ProfilePath.Length > 0 ? profile.CurrentTarget : null; TabTitle = "Data"; TabIcon = DataIcon; }
}
internal sealed class Mo2DataView : ReactiveUserControl<Mo2DataPage>
{
    private readonly TreeDataGrid _table = Mo2FolderPage.Table("DataTable");
    private readonly TextBox _search;
    private readonly TextBlock _path = new() { Text = "Data", TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = Mo2FolderPage.Status("DataStatus");
    // MO2's own "Conflicts only" box, which this page used to draw twice: once
    // beside the path and once in the row of MO2's filters. The one MO2 has is the
    // one in that row, and this is it.
    private readonly CheckBox _conflicts;
    private Mo2DataEntry[] _entries = [];
    private readonly Dictionary<string, Mo2DataEntry[]> _children = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _loading = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failed = new(StringComparer.OrdinalIgnoreCase);
    private long _treeGeneration;
    private Vector? _refreshOffset;
    private long _filterVersion;
    private bool _indexing;
    private bool _autoExpandSearch = true;
    private int _indexedFolders;
    private bool NeedsTreeScan => !string.IsNullOrEmpty(_search.Text) || _qtFilter.Length > 0 ||
        _conflicts.IsChecked == true || !_fromArchives;
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
        Mo2TableRow.InstallRowStyles(_table);
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        var header = new PageHeader { Title = "Data", Description = "Browse merged game files and their winning source mods.", Icon = new AvaloniaSvg("avares://MockHost/Assets/data-3d.svg") };
        root.Children.Add(header);
        var searchField = Mo2QtWidgets.Filter("DataSearch", "Search files and source mods", text => {
            if (ViewModel is { } model) model.SearchText = text;
            FilterChanged();
        }, out _search);
        var search = new Mo2ToolbarSearch(this, "DataToolbarSearch", searchField, _search, "Search files and source mods");
        var location = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; location.Children.Add(_path);
        // MO2's own dataTab furniture: a Refresh, the three checkboxes that narrow what
        // the tree lists, and its filter field.
        var qtBar = Mo2QtWidgets.ActionBar("DataQtBar");
        var refresh = Mo2QtWidgets.Icon("DataRefreshButton", Mo2QtWidgets.DataRefreshTip, "mdi-refresh",
            async () => { _actionError = null; await Refresh(); });
        _conflicts = Mo2QtWidgets.Check("DataConflictsOnly", Mo2QtWidgets.ConflictsOnly, Mo2QtWidgets.ConflictsOnlyTip, false, _ => { });
        qtBar.Children.Add(_conflicts);
        qtBar.Children.Add(Mo2QtWidgets.Check("DataFromArchives", Mo2QtWidgets.FromArchives, Mo2QtWidgets.FromArchivesTip,
            _fromArchives, value => { _fromArchives = value; FilterChanged(); }));
        qtBar.Children.Add(Mo2QtWidgets.Check("DataHiddenFiles", Mo2QtWidgets.HiddenFiles, Mo2QtWidgets.HiddenFilesTip,
            _hiddenFiles, value => { _hiddenFiles = value; FilterChanged(); }));
        var qtFilter = Mo2QtWidgets.Filter("DataQtFilter", Mo2QtWidgets.DataFilterTip, text => { _qtFilter = text; FilterChanged(); }, out var filenameFilter);
        filenameFilter.Watermark = "Filter filenames";
        qtFilter.Margin = new Thickness(0,8,0,0);

        var tab = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        tab.Children.Add(qtBar);
        Grid.SetRow(_table,1); tab.Children.Add(_table);
        Grid.SetRow(qtFilter,2); tab.Children.Add(qtFilter);
        Grid.SetRow(location,1); root.Children.Add(location); Grid.SetRow(_status,2); root.Children.Add(_status);
        Grid.SetRow(tab,3); root.Children.Add(tab);
        Content = root;
        _columns = new Mo2ColumnToggle("data", Render, pinned: [Mo2FolderPage.NameHeader]);
        // Share the installed lists' inline search; the native filename-only
        // filter and visibility switches continue narrowing the tree independently.
        Mo2PanelChrome.Apply(this, root, header, Mo2ListToolbar.Pill("DataToolbarActions", refresh), search);
        _conflicts.IsCheckedChanged += (_,_) => { if (ViewModel is { } model) model.ConflictsOnly = _conflicts.IsChecked == true; FilterChanged(); };
        _table.DoubleTapped += async (_,_) => await ActivateSelection();
        // Qt's item activation is reachable from Return as well as a double
        // click. Keep the same native preference, preview and launch dispatch.
        _table.AddHandler(InputElement.KeyDownEvent, async (_, args) => {
            if (args.Key != Key.Enter || args.KeyModifiers != KeyModifiers.None || !DataReady ||
                _table.RowSelection?.SelectedItem is not Mo2DataEntry { Directory: false, IsLoading: false }) return;
            args.Handled = true;
            await ActivateSelection();
        }, RoutingStrategies.Tunnel);
        // MO2's own Data menu (filetree.cpp, FileTree::onContextMenu), cut to what MO2
        // will do for a file this frontend can name: open it, preview it through MO2's
        // own preview extensions, show the mod it came from, and hide or unhide it.
        // Program launch, VFS launch and tree export delegate to MO2 itself.
        // Expansion actions operate on this displayed tree.
        Mo2RowMenu.Attach<Mo2DataEntry>(_table, entry => [
            entry is { Directory: false, IsLoading: false }
                ? Mo2EntryMenu.Action("Preview", async () => await RunAction("preview"), DataReady) : null,
            // MO2 offers Open on a file as well as Preview, and hands it to the handlers
            // inside its prefix; this opens the same file with the handlers this desk
            // has, as a download's Open File already does.
            entry is { Directory: false, IsLoading: false } ? Mo2EntryMenu.Action(IsExecutable(entry) ? "Execute" : "Open",
                async () => { if (IsExecutable(entry)) await RunNativeMenu(entry, "Execute"); else await RunAction("open"); }, CanTouchFile(entry)) : null,
            entry is { Directory: false, IsLoading: false } ? Mo2EntryMenu.Action(IsExecutable(entry) ? "Execute with VFS" : "Open with VFS",
                async () => await RunNativeMenu(entry, "Execute with VFS", "Open with VFS"), CanTouchFile(entry)) : null,
            // MO2's own, triggered where MO2 built it: it adds the file to the
            // executables this instance keeps, which the Tools page then lists.
            entry is { Directory: false, IsLoading: false } ? Mo2EntryMenu.Action("Add as Executable",
                async () => await AddAsExecutable(entry), DataReady && IsExecutable(entry)) : null,
            new Separator(),
            entry is { Directory: false, IsLoading: false } ? Mo2EntryMenu.Action("Reveal in Explorer", async () => await RunAction("reveal"), CanTouchFile(entry)) : null,
            entry is { Directory: false, IsLoading: false } ? Mo2EntryMenu.Action("Open Mod Info", async () => await OpenModInfo(entry),
                DataReady && OriginMod(entry) is not null) : null,
            new Separator(),
            entry is not { Directory: false, IsLoading: false } ? null :
            entry is { Name: { } name } && name.EndsWith(".mohidden", StringComparison.OrdinalIgnoreCase)
                ? Mo2EntryMenu.Action("Un-Hide", async () => await RunAction("unhide"), CanTouchFile(entry))
                : Mo2EntryMenu.Action("Hide", async () => await RunAction("hide"), CanTouchFile(entry)),
            new Separator(),
            // MO2 puts Refresh on this menu as well as above the tree. The page already
            // had the action and only the button to reach it by, which is a difference
            // in what the menu offers rather than in what the page can do.
            Mo2EntryMenu.Action("Save Tree to Text File...", async () => await RunNativeMenu(entry is { IsLoading: false } ? entry : null, "Save Tree to Text File..."),
                DataReady),
            Mo2EntryMenu.Action("Refresh", async () => { _actionError = null; await Refresh(); }, DataReady),
            Mo2EntryMenu.Action("Expand All", async () => await SetAllExpanded(true), DataReady),
            Mo2EntryMenu.Action("Collapse All", async () => await SetAllExpanded(false), DataReady),
        ], allowEmpty: true, availability: ReadMenuAvailability);
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

            }
            model.Profile.Changed += Changed; Disposable.Create(() => { _active = false; ++_activation; model.Profile.Changed -= Changed; }).DisposeWith(d);
            Disposable.Create(() => model.Profile.HighlightDataSource(this, model.Profile.CurrentTarget, null)).DisposeWith(d); _ = Refresh();
        });
    }
    private async Task<Mo2MenuEntry[]?> ReadMenuAvailability(Mo2DataEntry? entry)
    {
        // Injected readers supply fixture trees with no corresponding native rows.
        if (_read is not null || entry is not { Directory: false, IsLoading: false }) return null;
        if (!DataReady || ViewModel is not { } model || _target is not { } target) return [];
        var activation = _activation;
        var entries = await model.Profile.ReadListMenu("readFileMenu", [entry.RelativePath], target, "data");
        return DataReady && activation == _activation && target == model.Profile.CurrentTarget ? entries : [];
    }

    internal async Task ActivateSelection()
    {
        if (!_reading && _table.RowSelection?.SelectedItem is Mo2DataEntry { Directory: true } row)
            row.IsExpanded = !row.IsExpanded;
        else if (DataReady && ViewModel is { } model && _target is { } target &&
                 _table.RowSelection?.SelectedItem is Mo2DataEntry { Directory: false, IsLoading: false } file) {
            await model.Profile.ActivateDataFile(file.RelativePath, target);
            await Refresh();
        }
    }
    // MO2's own Add as Executable, on the row MO2 has of that name. MO2 keeps the
    // executables, so it is asked to add one rather than told what to write.
    internal static string DataRelativePath(string directory, string name) =>
        directory.Length == 0 ? name : directory + "/" + name;

    internal static bool IsExecutable(Mo2DataEntry entry) =>
        Path.GetExtension(entry.Name).ToLowerInvariant() is ".exe" or ".cmd" or ".bat" or ".jar";

    private async Task RunNativeMenu(Mo2DataEntry? entry, params string[] captions)
    {
        if (!DataReady || ViewModel is not { } model || _target is not { } target || entry?.IsLoading == true) return;
        await model.Profile.RunFileMenu("data", entry is null ? [] : [entry.RelativePath], captions.Select(x => new[] { x }).ToArray(), target);
        await Refresh();
    }

    private async Task AddAsExecutable(Mo2DataEntry? entry)
    {
        if (!_active || _reading || ViewModel is not { } model || _target is not { } target || entry is not { Directory: false, IsLoading: false }) return;
        await model.Profile.RunFileMenu("data", [entry.RelativePath], [["Add as Executable"]], target);
        await Refresh();
    }

    private async Task RunAction(string operation)
    {
        if (!_active || _reading || ViewModel is not { } model || _target is not { } target || _table.RowSelection?.SelectedItem is not Mo2DataEntry { Directory: false, IsLoading: false } file) return;
        var directory = file.ParentPath;
        var activation = _activation;
        _actionError = null;
        var error = await model.Profile.PreviewDataFile(directory, file, target, operation);
        await Refresh();
        if (_active && activation == _activation && target == model.Profile.CurrentTarget) {
            _actionError = error;
            Render();
        }
    }
    private async Task LoadChildren(Mo2DataEntry folder)
    {
        if (!_active || _reading || ViewModel is not { } model || _target is not { } target ||
            target != model.Profile.CurrentTarget || _children.ContainsKey(folder.RelativePath) || _failed.Contains(folder.RelativePath) || !_loading.Add(folder.RelativePath)) return;
        var generation = _treeGeneration;
        var activation = _activation;
        try {
            // Finish the expander's own event before replacing its source, even
            // when an in-memory reader completes synchronously.
            await Task.Yield();
            if (!_active || activation != _activation || generation != _treeGeneration || target != model.Profile.CurrentTarget) return;
            var entries = await (_read?.Invoke(target, folder.RelativePath) ?? model.Profile.ReadDataDirectory(target, folder.RelativePath));
            if (_active && activation == _activation && generation == _treeGeneration && target == model.Profile.CurrentTarget)
                _children[folder.RelativePath] = entries;
        } catch (Exception error) {
            if (_active && activation == _activation && generation == _treeGeneration && target == model.Profile.CurrentTarget) {
                _failed.Add(folder.RelativePath);
                _actionError = "Could not read " + folder.RelativePath + ": " + error.Message;
            }
        } finally {
            if (generation == _treeGeneration) _loading.Remove(folder.RelativePath);
            if (_active && activation == _activation && generation == _treeGeneration && target == model.Profile.CurrentTarget) {
                Render();
                if (_children.TryGetValue(folder.RelativePath, out var loaded))
                    foreach (var child in loaded.Where(x => x.Directory && model.ExpandedDirectories.Contains(x.RelativePath)))
                        _ = LoadChildren(child);
            }
        }
    }

    internal async Task SetAllExpanded(bool expanded)
    {
        if (!_active || _reading || ViewModel is not { } model) return;
        var version = ++_filterVersion;
        _autoExpandSearch = expanded;
        if (!expanded) model.ExpandedDirectories.Clear();
        _indexing = expanded || NeedsTreeScan;
        _indexedFolders = 0;
        Render();
        if (_indexing) await ScanFilteredTree(version, expandAll: expanded);
    }

    private void FilterChanged()
    {
        _autoExpandSearch = true;
        var version = ++_filterVersion;
        _indexing = _active && !_reading && NeedsTreeScan;
        _indexedFolders = 0;
        Render();
        if (_indexing) _ = ScanFilteredTree(version);
    }

    private async Task ScanFilteredTree(long version, bool expandAll = false)
    {
        if (ViewModel is not { } model || _target is not { } target) return;
        var generation = _treeGeneration;
        var activation = _activation;
        bool Current() => _active && !_reading && version == _filterVersion &&
            generation == _treeGeneration && activation == _activation && target == model.Profile.CurrentTarget;
        try {
            // Coalesce typing before issuing bridge reads. A superseded scan stops
            // after its current read, so it cannot enqueue a whole stale traversal.
            await Task.Delay(150);
            if (!Current()) return;
            var queue = new Queue<(string Parent, Mo2DataEntry[] Entries)>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            queue.Enqueue((_directory, _entries));
            var painted = System.Diagnostics.Stopwatch.StartNew();
            while (queue.TryDequeue(out var branch) && Current()) {
                foreach (var folder in branch.Entries.Where(x => x.Directory)) {
                    if (!Current()) return;
                    if (!folder.MatchesVisibility(_conflicts.IsChecked == true, _fromArchives, _hiddenFiles)) continue;
                    var path = DataRelativePath(branch.Parent, folder.Name);
                    if (!visited.Add(path)) continue;
                    // A user may already have expanded this branch. Reuse its
                    // pending read instead of sending duplicate bridge requests.
                    while (_loading.Contains(path) && Current()) await Task.Delay(25);
                    if (!Current()) return;
                    if (!_children.TryGetValue(path, out var children)) {
                        if (_failed.Contains(path)) continue;
                        try {
                            children = await (_read?.Invoke(target, path) ?? model.Profile.ReadDataDirectory(target, path));
                            if (!Current()) return;
                            _children[path] = children;
                        } catch (Exception error) {
                            if (!Current()) return;
                            _failed.Add(path);
                            _actionError = "Search incomplete: could not read " + path + ": " + error.Message;
                            continue;
                        }
                    }
                    ++_indexedFolders;
                    queue.Enqueue((path, children));
                    if (painted.ElapsedMilliseconds >= 200) {
                        RenderCore(onlyIfChanged: true); painted.Restart();
                        await Task.Yield();
                    }
                }
            }
        } finally {
            if (Current()) {
                _indexing = false;
                RenderCore(onlyIfChanged: true);
                if (expandAll) {
                    // Expand only the folders retained by the current filters.
                    void Remember(IEnumerable<Mo2DataEntry> entries) {
                        foreach (var folder in entries.Where(x => x.Directory)) {
                            if (_children.ContainsKey(folder.RelativePath)) model.ExpandedDirectories.Add(folder.RelativePath);
                            Remember(folder.Children);
                        }
                    }
                    Remember(_table.Source!.Items.OfType<Mo2DataEntry>());
                    Render();
                }
            }
        }
    }

    private IEnumerable<Mo2DataEntry> ChildRows(Mo2DataEntry folder)
    {
        if (!_children.ContainsKey(folder.RelativePath)) {
            _ = LoadChildren(folder);
            // TreeDataGrid collapses an empty branch before it writes IsExpanded.
            // Keep a non-actionable row until the lazy read has a result.
            return [new(_failed.Contains(folder.RelativePath) ? "Could not load folder — collapse and expand to retry" : "Loading…", false, [], "") { IsLoading = true, ParentPath = folder.RelativePath }];
        }
        return folder.Children;
    }

    private void ExpansionChanged(Mo2DataEntry folder)
    {
        if (!_active || ViewModel is not { } model) return;
        if (folder.IsExpanded) {
            _failed.Remove(folder.RelativePath);
            model.ExpandedDirectories.Add(folder.RelativePath);
            _ = LoadChildren(folder);
        } else model.ExpandedDirectories.Remove(folder.RelativePath);
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
            ++_filterVersion; _indexing = false;
            _entries = []; _selection = null; _actionError = null; _error = null; Render();
            return;
        }
        var activation = _activation;
        if (_entries.Length > 0)
            _refreshOffset ??= _table.GetVisualDescendants().OfType<ScrollViewer>()
                .OrderByDescending(x => x.Extent.Height).FirstOrDefault()?.Offset;
        _reading = true; _error = null;
        ++_treeGeneration; ++_filterVersion; _indexing = false; _children.Clear(); _loading.Clear(); _failed.Clear();
        _target = model.Profile.CurrentTarget; var target = _target.Value; var connected = model.Profile.IsConnected;
        _revision = model.Profile.ContentRevision; var revision = _revision;
        _entries = []; Render();
        try {
            var entries = await (_read?.Invoke(target, _directory) ?? model.Profile.ReadDataDirectory(target, _directory));
            if (_active && activation == _activation && target == model.Profile.CurrentTarget) {
                _entries = entries;
                // Re-read only branches that were open. Closed branches remain lazy.
                foreach (var path in model.ExpandedDirectories.OrderBy(x => x.Count(c => c == '/')).ToArray()) {
                    var parent = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
                    var siblings = parent == _directory ? _entries : _children.GetValueOrDefault(parent);
                    if (siblings?.Any(x => x.Directory && DataRelativePath(parent, x.Name).Equals(path, StringComparison.OrdinalIgnoreCase)) != true) continue;
                    var children = await (_read?.Invoke(target, path) ?? model.Profile.ReadDataDirectory(target, path));
                    if (!_active || activation != _activation || target != model.Profile.CurrentTarget) break;
                    _children[path] = children;
                }
                if (_selection is { } selected &&
                    !_entries.Any(x => DataRelativePath(_directory, x.Name) == selected.Name && x.Directory == selected.Directory) &&
                    !_children.Any(pair => pair.Value.Any(x => DataRelativePath(pair.Key, x.Name) == selected.Name && x.Directory == selected.Directory)))
                    _selection = null;
            }
        } catch (Exception error) { if (_active && activation == _activation && target == model.Profile.CurrentTarget) _error = error.Message; }
        finally {
            _reading = false;
            if (_active) {
                if (activation != _activation || target != model.Profile.CurrentTarget || connected != model.Profile.IsConnected || revision != model.Profile.ContentRevision) await Refresh();
                else FilterChanged();
            }
        }
    }
    // What the header actions are enabled by, and what the row menu asks in turn.
    private bool DataReady => _active && !_reading && ViewModel?.Profile.IsConnected == true &&
        _target == ViewModel.Profile.CurrentTarget && ViewModel?.Profile.CanChangeOriginalUi == true;
    // A file MO2 can act on individually: one it lays down itself, rather than one it
    // reads out of an archive.
    private bool CanTouchFile(Mo2DataEntry? entry) => DataReady && entry is { Directory: false, Archive.Length: 0, IsLoading: false };

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

    private Mo2ColumnToggle? _columns;
    // The handler that keeps the columns fitting the panel. Held so a re-render can
    // replace it rather than adding another one for every table it builds.
    private EventHandler? _fitColumns;
    internal Action<double>? RenderMeasured { get; set; }
    private (Mo2DataEntry Entry, bool Expanded)[] _presentedRows = [];
    private void Render() => RenderCore(onlyIfChanged: false);
    private void UpdateStatus(int visibleCount)
    {
        _path.Text = "Data" + (_directory.Length > 0 ? "/" + _directory : ""); ToolTip.SetTip(_path, _path.Text);
        _status.Text = _actionError ?? _error ?? (_reading ? "Reading MO2 Data…" : _indexing ? $"Searching Data… {_indexedFolders} folders checked" : $"{visibleCount} visible entries");
    }
    private void RenderCore(bool onlyIfChanged)
    {
        var renderStarted = RenderMeasured is null ? 0 : System.Diagnostics.Stopwatch.GetTimestamp();
        using var timing = Mo2UiLatencyProbe.Measure("Data tree render");
        // MO2's own filters, over the page's existing search: only conflicting files,
        // whether files served from archives count, and whether hidden ones do. A
        // visible folder remains navigable; hidden folders follow MO2's hidden rule.
        List<Mo2DataEntry> Visible(Mo2DataEntry[] entries, string parent) {
            var result = new List<Mo2DataEntry>();
            foreach (var entry in entries) {
                entry.ParentPath = parent;
                entry.ExpansionChanged = null;
                entry.Children = _children.TryGetValue(entry.RelativePath, out var children) ? Visible(children, entry.RelativePath) : [];
                var searching = !string.IsNullOrEmpty(_search.Text) || _qtFilter.Length > 0;
                entry.IsExpanded = ViewModel?.ExpandedDirectories.Contains(entry.RelativePath) == true ||
                    (_autoExpandSearch && searching && entry.Children.Count > 0);
                entry.ExpansionChanged = ExpansionChanged;
                var matches = (entry.Name + " " + entry.Source).Contains(_search.Text ?? "", StringComparison.OrdinalIgnoreCase)
                    && (_qtFilter.Length == 0 || entry.Name.Contains(_qtFilter, StringComparison.OrdinalIgnoreCase));
                var prune = entry.Directory && (_conflicts.IsChecked == true || !_fromArchives);
                var hasDescendant = entry.Children.Count > 0;
                var visible = entry.Directory && prune ? hasDescendant : matches || (entry.Directory && hasDescendant);
                if (visible && entry.MatchesVisibility(_conflicts.IsChecked == true, _fromArchives, _hiddenFiles)) result.Add(entry);
            }
            return result;
        }
        var rows = Visible(_entries, _directory);
        // A scan often discovers no additional matches. Keep the existing source
        // (and its selection/realized controls) when the visible tree is unchanged.
        // Snapshot references and expansion values: Visible updates each entry's
        // child list in place, so comparing the old source's items is insufficient.
        IEnumerable<(Mo2DataEntry Entry, bool Expanded)> Flatten(IEnumerable<Mo2DataEntry> items) {
            foreach (var item in items) {
                yield return (item, item.IsExpanded);
                foreach (var child in Flatten(item.Children)) yield return child;
            }
        }
        var presented = Flatten(rows).ToArray();
        if (onlyIfChanged && _table.Source is not null && presented.Length == _presentedRows.Length &&
            presented.Zip(_presentedRows).All(pair => ReferenceEquals(pair.First.Entry, pair.Second.Entry) &&
                pair.First.Expanded == pair.Second.Expanded)) {
            UpdateStatus(_table.Rows?.Count ?? 0);
            return;
        }
        _presentedRows = presented;
        var source = new HierarchicalTreeDataGridSource<Mo2DataEntry>(rows);
        source.Columns.Add(new HierarchicalExpanderColumn<Mo2DataEntry>(
            Mo2FolderPage.NameColumn<Mo2DataEntry>(row => (row.Name, row.Directory, row.Details, false)),
            row => ChildRows(row), row => row.Directory, row => row.IsExpanded));
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
        source.Columns.Add(Column("Size", x => x.SizeText, new GridLength(Mo2FolderPage.SizeColumnWidth)));
        source.Columns.Add(Column("Date modified", x => x.ModifiedText, new GridLength(140)));
        // Dropped from the right as the panel narrows, so the name always has room:
        // in MO2's own panel layout this tree is half a window wide, and four fixed
        // columns left the name column — the one the tree is read by — at nothing but
        // its icon.
        (IColumn<Mo2DataEntry> Column, double Width, double Threshold)[] optional = [
            (source.Columns[1], 150, 380), (source.Columns[2], 70, 460),
            (source.Columns[3], Mo2FolderPage.SizeColumnWidth, 560), (source.Columns[4], 140, 700)];
        var fitMetadata = Mo2FolderPage.MetadataFitter(_table, source, optional);
        void FitColumns() {
            fitMetadata();
            // Clearing rows while a refresh is pending coerces the viewport to
            // zero. Restore only after the replacement source has been laid out.
            if (_active && !_reading && _refreshOffset is { } offset && ReferenceEquals(_table.Source, source)) {
                var scroll = _table.GetVisualDescendants().OfType<ScrollViewer>()
                    .OrderByDescending(x => x.Extent.Height).FirstOrDefault();
                if (scroll is not null) {
                    _refreshOffset = null;
                    scroll.Offset = offset;
                }
            }
        }
        _table.LayoutUpdated -= _fitColumns;
        _fitColumns = (_, _) => FitColumns();
        _table.LayoutUpdated += _fitColumns;
        FitColumns();
        source.RowSelection!.SelectionChanged += (_,_) => {
            if (_active && ReferenceEquals(_table.Source, source) && ViewModel is { } model && _target is { } target) {
                _selection = source.RowSelection.SelectedItem is { IsLoading: false } row ? (row.RelativePath, row.Directory) : null;
                model.Profile.HighlightDataSource(this, target, source.RowSelection.SelectedItem is { Directory: false } selected ? selected.Source : null);
            }

        };
        _columns?.Apply(source.Columns);
        var old = _table.Source; _table.Source = source; (old as IDisposable)?.Dispose();
        if (ViewModel is { } model) model.Profile.HighlightDataSource(this, model.Profile.CurrentTarget, null);
        IndexPath? Find(IReadOnlyList<Mo2DataEntry> entries, int[] parent) {
            for (var i = 0; i < entries.Count; i++) {
                int[] path = [.. parent, i];
                if (_selection is { } remembered && entries[i].RelativePath == remembered.Name && entries[i].Directory == remembered.Directory)
                    return new IndexPath(path);
                if (Find(entries[i].Children, path) is { } found) return found;
            }
            return null;
        }
        void ExpandRemembered(IReadOnlyList<Mo2DataEntry> entries, int[] parent) {
            for (var i = 0; i < entries.Count; i++) {
                int[] path = [.. parent, i];
                if (!entries[i].Directory || !entries[i].IsExpanded) continue;
                source.Expand(new IndexPath(path));
                ExpandRemembered(entries[i].Children, path);
            }
        }
        ExpandRemembered(rows, []);
        if (Find(rows, []) is { } selectedIndex) source.RowSelection.Select(selectedIndex);
        UpdateStatus(source.Rows.Count);
        if (renderStarted != 0) RenderMeasured?.Invoke(System.Diagnostics.Stopwatch.GetElapsedTime(renderStarted).TotalMilliseconds);
    }
}
