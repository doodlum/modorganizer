using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Controls.PageHeader;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using System.Reactive.Disposables;

namespace Mo2.Frontend;

internal interface IMo2ExternalFilesPage : IPageViewModelInterface { }
internal sealed class Mo2ExternalFilesPage : APageViewModel<IMo2ExternalFilesPage>, IMo2ExternalFilesPage
{
    public Mo2LiveProfile Profile { get; }
    public IWindowManager Windows { get; }
    public Mo2ExternalFilesPage(IWindowManager windows, Mo2LiveProfile profile) : base(windows)
    { Profile = profile; Windows = windows; TabTitle = "External Files"; TabIcon = IconValues.FolderEditOutline; }
}

internal sealed class Mo2ExternalFilesView : ReactiveUserControl<Mo2ExternalFilesPage>
{
    private readonly TreeDataGrid _table = new() { Name = "ExternalFilesTable", ShowColumnHeaders = true };
    private readonly TextBox _search = new() { Name = "ExternalFilesSearch", Watermark = "Search external files", MinWidth = 60 };
    private readonly TextBlock _status = new() { Name = "ExternalFilesStatus", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 8) };
    private readonly Button _reveal;
    private readonly Button _import;
    private readonly Button _cleanup, _restore;
    private (Mo2ExternalArchiveCopy Copy, Mo2ArchiveInstallResult Installed, Mo2ProfileTarget Target)? _verified;
    private string? _backup;
    private CancellationTokenSource? _importing;
    private Mo2ExternalScan? _scan;
    private Mo2ProfileTarget? _target;
    private CancellationTokenSource? _reading;
    private bool _active;
    private Mo2ExternalFileNode[] _nodes = [];
    private readonly Dictionary<string, bool> _expansion = new(StringComparer.Ordinal);
    private bool _searching;
    private string? _pendingSelection;
    private readonly Func<Mo2ProfileTarget, string, CancellationToken, Task<Mo2ExternalScan>>? _read;
    internal bool IsReading => _reading is not null;

    public Mo2ExternalFilesView() : this(null) { }

    internal Mo2ExternalFilesView(Func<Mo2ProfileTarget, string, CancellationToken, Task<Mo2ExternalScan>>? read)
    {
        _read = read;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*"), Margin = new Thickness(24) };
        var header = new PageHeader { Title = "External Files", Icon = IconValues.FolderEditOutline,
            Description = "Files in your game folder that aren't included in Steam's installed game and DLC manifests." };
        root.Children.Add(header);
        var toolbar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto,Auto,Auto"), Margin = new Thickness(0, 8) };
        _import = Mo2ModRow.IconButton("mdi-import", "Import copy as mod… Select files inside Data. Originals remain in the game folder and can still override the imported mod.", async () => await ImportCopy());
        _import.Name = "ImportExternalFiles"; _import.IsEnabled = false;
        _cleanup = Mo2ModRow.IconButton("mdi-folder-move-outline", "Move verified originals to backup… Enable the imported mod first.", async () => await Cleanup());
        _cleanup.Name = "CleanupExternalFiles"; _cleanup.IsEnabled = false;
        _restore = Mo2ModRow.IconButton("mdi-backup-restore", "Restore last external-files cleanup…", async () => await Restore());
        _restore.Name = "RestoreExternalFiles"; _restore.IsEnabled = false;
        _reveal = Mo2ModRow.IconButton("mdi-folder-open-outline", "Open source folder", async () => await Reveal());
        _reveal.Name = "RevealExternalFiles"; _reveal.IsEnabled = false;
        var refresh = Mo2ModRow.IconButton("mdi-refresh", "Scan game folder", async () => await Refresh());
        refresh.Name = "RefreshExternalFiles";
        toolbar.Children.Add(_search); Grid.SetColumn(_import, 1); toolbar.Children.Add(_import);
        Grid.SetColumn(_cleanup, 2); toolbar.Children.Add(_cleanup);
        Grid.SetColumn(_restore, 3); toolbar.Children.Add(_restore);
        Grid.SetColumn(_reveal, 4); toolbar.Children.Add(_reveal);
        Grid.SetColumn(refresh, 5); toolbar.Children.Add(refresh);
        Grid.SetRow(toolbar, 1); root.Children.Add(toolbar);
        Grid.SetRow(_status, 2); root.Children.Add(_status);
        Grid.SetRow(_table, 3); root.Children.Add(_table);
        // NMA styles its file trees Compact and reserves MainListsStyling for mod
        // lists; this page is a file tree, so it follows the original page.
        _table.Classes.Add("Compact"); Content = root;
        _columns = new Mo2ColumnToggle("external-files", () => Render());
        Mo2PanelChrome.Apply(this, root, header, _search, _import, _cleanup, _restore, _reveal, _columns.Action, refresh);
        _search.TextChanged += (_, _) => Render();
        this.WhenActivated(d => {
            if (ViewModel is not { } model) return;
            _active = true;
            void Changed() { if (_target != model.Profile.CurrentTarget) { _importing?.Cancel(); _ = Refresh(); } UpdateImport(); }
            model.Profile.Changed += Changed;
            Disposable.Create(() => {
                _active = false; _reading?.Cancel(); _importing?.Cancel(); model.Profile.Changed -= Changed;
                _scan = null; _verified = null; _backup = null; _pendingSelection = null; _reveal.IsEnabled = false; _status.Text = ""; Render();
            }).DisposeWith(d);
            _ = Refresh();
        });
    }

    internal async Task Refresh()
    {
        if (!_active || ViewModel is not { } model) return;
        _reading?.Cancel();
        using var request = new CancellationTokenSource(); _reading = request;
        var target = model.Profile.CurrentTarget;
        if (_target != target) { _expansion.Clear(); _nodes = []; _pendingSelection = null; _verified = null; _backup = null; }
        else _pendingSelection = (_table.RowSelection?.SelectedItem as Mo2ExternalFileNode)?.Path ?? _pendingSelection;
        _target = target;
        var game = model.Profile.NexusGame;
        _scan = null; _reveal.IsEnabled = false; Render();
        _status.Text = "Comparing game files with Steam's installed manifests…";
        try {
            var scan = await (_read?.Invoke(target, game, request.Token) ?? Task.Run(() => {
                var appId = game switch { "newvegas" => "22380", "skyrimspecialedition" => "489830",
                    _ => throw new NotSupportedException("Steam file detection is available for Fallout: New Vegas and Skyrim Special Edition.") };
                var instance = Path.GetFullPath(Path.Combine(target.Endpoint, "../../.."));
                return Mo2ExternalFiles.Scan(Mo2ProfileFiles.ReadGameDirectory(instance), appId, request.Token);
            }, request.Token));
            var backup = await Task.Run(() => Mo2ExternalBackup.Latest(scan.GameDirectory), request.Token);
            if (!request.IsCancellationRequested && _active && target == model.Profile.CurrentTarget) {
                _backup = backup;
                _scan = scan; _reveal.IsEnabled = true; Render(_pendingSelection); _pendingSelection = null;
            }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) {
            if (!request.IsCancellationRequested && _active && target == model.Profile.CurrentTarget) _status.Text = error.Message;
        } finally { if (ReferenceEquals(_reading, request)) _reading = null; }
    }

    private Mo2ColumnToggle? _columns;
    // Named here so a check can compare them with NMA's own column definitions
    // without having to build the page.
    internal static string[] ColumnHeaders => [
        SharedColumns.NameWithFileIcon.GetColumnHeader(),
        SharedColumns.ItemSizeOverGamePath.GetColumnHeader(),
        SharedColumns.FileCount.GetColumnHeader(),
    ];

    private void Render(string? restoreSelection = null)
    {
        var selected = restoreSelection ?? (_table.RowSelection?.SelectedItem as Mo2ExternalFileNode)?.Path;
        if (!_searching) foreach (var node in _nodes.SelectMany(node => node.DescendantsAndSelf()).Where(node => node.IsFolder))
            _expansion[node.Path] = node.IsExpanded;
        _searching = !string.IsNullOrEmpty(_search.Text);
        var files = (_scan?.Files ?? []).Where(x => x.Path.Contains(_search.Text ?? "", StringComparison.OrdinalIgnoreCase)).ToArray();
        _nodes = Mo2ExternalFileNode.Build(files, _search.Text ?? "", _expansion);
        var source = new HierarchicalTreeDataGridSource<Mo2ExternalFileNode>(_nodes);
        source.Columns.Add(new HierarchicalExpanderColumn<Mo2ExternalFileNode>(
            new TemplateColumn<Mo2ExternalFileNode>(SharedColumns.NameWithFileIcon.GetColumnHeader(), new FuncDataTemplate<Mo2ExternalFileNode>((node, _) => {
                if (node is null) return new Control();
                var cell = new Grid { ColumnDefinitions = new ColumnDefinitions("24,*"), MinWidth = 0 };
                cell.Children.Add(new UnifiedIcon { Value = new ProjektankerIcon(node.IsFolder ? "mdi-folder-outline" :
                    node.Kind == "External link" ? "mdi-file-link-outline" : "mdi-file-outline"), Size = 16, Opacity = .7 });
                var name = new TextBlock { Text = node.Name, TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                Grid.SetColumn(name, 1); cell.Children.Add(name); ToolTip.SetTip(cell, node.Path);
                return cell;
            }), width: new GridLength(1, GridUnitType.Star)),
            x => x.Children, x => x.IsFolder, x => x.IsExpanded));
        // The headers come from NMA's own column definitions rather than being typed
        // here, so they cannot drift from the app this page is meant to match. They
        // were written in capitals, which made this the one page in the frontend
        // whose table headers did not read like the rest.
        source.Columns.Add(new TextColumn<Mo2ExternalFileNode, string>(
            SharedColumns.ItemSizeOverGamePath.GetColumnHeader(), x => x.SizeText, new GridLength(90)));
        source.Columns.Add(new TextColumn<Mo2ExternalFileNode, string>(
            SharedColumns.FileCount.GetColumnHeader(), x => x.FileCountText, new GridLength(90)));
        _columns?.Apply(source.Columns);
        var previous = _table.Source; _table.Source = source; (previous as IDisposable)?.Dispose();
        // Recycled expander cells can retain their previous collapsed state
        // while a replacement source attaches. Search must expose every match.
        if (_searching) source.ExpandAll();
        IndexPath? Find(IReadOnlyList<Mo2ExternalFileNode> nodes, int[] parent) {
            for (var index = 0; index < nodes.Count; index++) {
                int[] path = [..parent, index];
                if (nodes[index].Path == selected) return new IndexPath(path);
                if (Find(nodes[index].Children, path) is { } found) return found;
            }
            return null;
        }
        if (selected is not null && Find(_nodes, []) is { } selectedIndex) source.RowSelection!.Select(selectedIndex);
        source.RowSelection!.SelectionChanged += (_, _) => UpdateImport();
        UpdateImport();
        if (_scan is { } scan) _status.Text = $"{files.Length} of {scan.Files.Length} external files · {scan.SteamFiles} Steam files across {scan.Depots} installed depots. File ownership only; original files aren't checked for modifications.";
    }

    private void UpdateImport()
    {
        _verified = ViewModel?.Profile.VerifiedExternalImport;
        var node = _table.RowSelection?.SelectedItem as Mo2ExternalFileNode;
        _import.IsEnabled = _active && _importing is null && _scan is not null &&
            ViewModel is { } model && _target == model.Profile.CurrentTarget && model.Profile.CanUseDownloads &&
            model.Profile.DownloadsDirectory is { Length: > 0 } && node is not null &&
            Mo2ExternalArchive.IsDataPath(node.Path) && node.Kind != "External link";
        var ready = _active && _importing is null && _scan is not null && ViewModel is { } page &&
            _target == page.Profile.CurrentTarget && page.Profile.CanUseDownloads;
        _cleanup.IsEnabled = ready && _verified is { } verified && verified.Target == _target &&
            ViewModel!.Profile.Mods.Any(mod => mod.Name == verified.Installed.ModName && (mod.State & 2) != 0);
        _restore.IsEnabled = ready && _backup is not null;
    }

    private async Task ImportCopy()
    {
        UpdateImport();
        if (!_import.IsEnabled || ViewModel is not { } model || _target is not { } target ||
            _table.RowSelection?.SelectedItem is not Mo2ExternalFileNode selected) return;
        var downloads = model.Profile.DownloadsDirectory!;
        var game = model.Profile.NexusGame;
        using var request = new CancellationTokenSource(); _importing = request; UpdateImport();
        _verified = null; model.Profile.VerifiedExternalImport = null;
        _status.Text = "Preparing a copy for MO2. Original files will remain in the game folder…";
        try {
            var archive = await Task.Run(() => {
                var appId = game switch { "newvegas" => "22380", "skyrimspecialedition" => "489830",
                    _ => throw new NotSupportedException("Steam file detection is unavailable for this game.") };
                var instance = Path.GetFullPath(Path.Combine(target.Endpoint, "../../.."));
                var fresh = Mo2ExternalFiles.Scan(Mo2ProfileFiles.ReadGameDirectory(instance), appId, request.Token);
                return Mo2ExternalArchive.CreateCopy(fresh, selected.Path, downloads, request.Token);
            }, request.Token);
            if (!_active || request.IsCancellationRequested || target != model.Profile.CurrentTarget) return;
            var installed = await model.Profile.InstallArchive(archive.Archive, target);
            if (!_active || request.IsCancellationRequested || target != model.Profile.CurrentTarget) return;
            var outcome = model.Profile.Status;
            var changed = await Task.Run(() => Mo2ExternalArchive.ChangedOriginals(archive, request.Token), request.Token);
            var installedChanges = installed?.ModDirectory is { } directory
                ? await Task.Run(() => Mo2ExternalArchive.ChangedInstalledFiles(archive, directory, request.Token), request.Token) : null;
            if (_active && !request.IsCancellationRequested && target == model.Profile.CurrentTarget &&
                installed is not null && changed.Length == 0 && installedChanges?.Length == 0)
                _verified = model.Profile.VerifiedExternalImport = (archive, installed, target);
            if (_active && !request.IsCancellationRequested && target == model.Profile.CurrentTarget)
                _status.Text = outcome + ". Archive kept in Downloads; originals remain in the game folder." +
                    (changed.Length > 0 ? $" {changed.Length} original files changed or became unavailable after copying. Rescan before moving originals." : "") +
                    (installed is null ? "" : installedChanges is null ? " Installed-file verification is unavailable from this host." :
                        installedChanges.Length == 0 ? $" Verified {archive.Files.Length} installed files against the archive copy." :
                        $" {installedChanges.Length} installed files are missing or differ from the copy. Keep the originals until reviewed.");
        } catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) { if (_active && target == model.Profile.CurrentTarget) _status.Text = error.Message; }
        finally { if (ReferenceEquals(_importing, request)) _importing = null; UpdateImport(); }
    }

    private async Task Cleanup()
    {
        UpdateImport();
        if (!_cleanup.IsEnabled || _verified is not { } verified || ViewModel is not { } model) return;
        using var request = new CancellationTokenSource(); _importing = request; UpdateImport();
        try {
            var text = $"Move {verified.Copy.Files.Length} original files out of the game folder? The enabled mod {verified.Installed.ModName} contains verified copies.\n\nThis changes the physical game folder for every profile. Restore last cleanup can put the originals back without overwriting existing files.\n\n" +
                string.Join("\n", verified.Copy.Files.Take(10).Select(file => file.Path)) +
                (verified.Copy.Files.Length > 10 ? $"\n…and {verified.Copy.Files.Length - 10} more files." : "");
            if (!await Mo2ExternalCleanupDialog.Confirm(model.Windows, "Move originals to backup?", text, "Move to backup")) return;
            if (!_active || request.IsCancellationRequested || model.Profile.CurrentTarget != verified.Target) return;
            var game = model.Profile.NexusGame;
            await model.Profile.RunExternalFileAction(verified.Target, async () => {
                if (!model.Profile.Mods.Any(mod => mod.Name == verified.Installed.ModName && (mod.State & 2) != 0))
                    throw new InvalidOperationException("Enable the imported mod before moving its originals.");
                _backup = await Task.Run(() => {
                    var fresh = Mo2ExternalFiles.Scan(verified.Copy.GameDirectory, game == "newvegas" ? "22380" : "489830", request.Token);
                    return Mo2ExternalBackup.Move(verified.Copy, verified.Installed.ModDirectory!, fresh, request.Token);
                }, request.Token);
            });
            _verified = null; model.Profile.VerifiedExternalImport = null;
            if (_active && model.Profile.CurrentTarget == verified.Target) { await Refresh(); _status.Text = "Originals moved to backup. Restore last cleanup is available."; }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) { if (_active && model.Profile.CurrentTarget == verified.Target) _status.Text = error.Message; }
        finally { if (ReferenceEquals(_importing, request)) _importing = null; UpdateImport(); }
    }

    private async Task Restore()
    {
        UpdateImport();
        if (!_restore.IsEnabled || _backup is not { } backup || _scan is not { } scan || ViewModel is not { } model || _target is not { } target) return;
        using var request = new CancellationTokenSource(); _importing = request; UpdateImport();
        try {
            if (!await Mo2ExternalCleanupDialog.Confirm(model.Windows, "Restore last cleanup?",
                "Restore the backed-up originals to this game's folder for all profiles? Existing game files will not be overwritten. Installed mods keep their current enabled states.", "Restore")) return;
            if (!_active || request.IsCancellationRequested || model.Profile.CurrentTarget != target) return;
            var count = 0;
            await model.Profile.RunExternalFileAction(target, async () => {
                count = await Task.Run(() => Mo2ExternalBackup.Restore(scan.GameDirectory, backup, request.Token), request.Token);
            });
            if (_active && model.Profile.CurrentTarget == target) { await Refresh(); _status.Text = $"Restored {count} original files."; }
        } catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception error) { if (_active && model.Profile.CurrentTarget == target) _status.Text = error.Message; }
        finally { if (ReferenceEquals(_importing, request)) _importing = null; UpdateImport(); }
    }

    private async Task Reveal()
    {
        if (!_active || _scan is not { } scan || ViewModel is not { } model || _target != model.Profile.CurrentTarget) return;
        try {
            var path = _table.RowSelection?.SelectedItem is Mo2ExternalFileNode node
                ? node.IsFolder ? Path.Combine(scan.GameDirectory, node.Path)
                    : Path.GetDirectoryName(Path.Combine(scan.GameDirectory, node.Path))! : scan.GameDirectory;
            var start = new System.Diagnostics.ProcessStartInfo(OperatingSystem.IsWindows() ? "explorer.exe" : "xdg-open") { UseShellExecute = false };
            start.ArgumentList.Add(path); await Mo2DesktopLauncher.StartAsync(start);
        } catch (Exception error) { _status.Text = error.Message; }
    }
}
