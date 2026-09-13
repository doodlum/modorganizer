using System.Text.Json;
using Avalonia;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.App.UI.Pages.LoadoutPage;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// Presentation only: MO2 profile paths identify layouts, never create/select profiles.
internal sealed class Mo2WorkspaceLayout
{
    internal sealed record Tab(string Factory, string? Game, string Search, bool SearchExpanded, int SubTab,
        string? DiagnosticTitle, string? DiagnosticEndpoint, string? DiagnosticProfile);
    internal sealed record Panel(double X, double Y, double Width, double Height, bool Active, int Selected, Tab[] Tabs);
    internal sealed record Workspace(string Endpoint, string ProfilePath, Panel[] Panels);
    internal sealed record Layout(int Version, Workspace[] Workspaces);
    private readonly string? _path;
    private readonly Dictionary<string, PageData> _pages;
    private readonly PageFactoryId _detailsFactory;
    private readonly Dictionary<(string Endpoint, string Path), Workspace> _saved = new();
    private bool _canSave = true;
    private string? _lastWritten;
    public string? Error { get; private set; }

    public Mo2WorkspaceLayout(IEnumerable<PageData> pages, PageFactoryId detailsFactory)
    {
        _pages = pages.ToDictionary(x => x.FactoryId.ToString()); _detailsFactory = detailsFactory;
        _path = Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT");
        if (_path is null && Environment.GetEnvironmentVariable("MO2_SCREENSHOT") is null)
            _path = Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mo2-nexus-frontend", "workspace-layout.json");
        if (_path is null || !File.Exists(_path)) return;
        try {
            var json = File.ReadAllText(_path);
            var data = JsonSerializer.Deserialize<Layout>(json) ?? throw new InvalidDataException("Empty layout file");
            if (data.Version != 2 || data.Workspaces is null) throw new InvalidDataException("Unsupported layout format");
            foreach (var workspace in data.Workspaces) {
                Validate(workspace);
                _saved.Add((workspace.Endpoint, workspace.ProfilePath), workspace);
            }
            _lastWritten = json;
        } catch (Exception error) {
            _saved.Clear(); _canSave = false;
            Error = "Could not restore panel layouts: " + error.Message;
        }
    }
    private void Validate(Workspace workspace)
    {
        if (workspace.Endpoint is null || workspace.ProfilePath is null || workspace.Panels is null || workspace.Panels.Length is < 1 or > 4)
            throw new InvalidDataException("Invalid workspace layout");
        if ((workspace.Endpoint.Length == 0) != (workspace.ProfilePath.Length == 0)) throw new InvalidDataException("Incomplete profile identity");
        var area = 0.0;
        for (var i = 0; i < workspace.Panels.Length; i++) {
            var p = workspace.Panels[i];
            if (!double.IsFinite(p.X + p.Y + p.Width + p.Height) || p.X < 0 || p.Y < 0 || p.Width <= 0 || p.Height <= 0 || p.X + p.Width > 1.001 || p.Y + p.Height > 1.001 || p.Tabs is null || p.Tabs.Length == 0 || p.Selected < 0 || p.Selected >= p.Tabs.Length)
                throw new InvalidDataException("Invalid panel bounds or tabs");
            area += p.Width * p.Height;
            foreach (var q in workspace.Panels.Take(i))
                if (Math.Min(p.X + p.Width, q.X + q.Width) - Math.Max(p.X, q.X) > .001 && Math.Min(p.Y + p.Height, q.Y + q.Height) - Math.Max(p.Y, q.Y) > .001)
                    throw new InvalidDataException("Overlapping panels");
            foreach (var tab in p.Tabs) {
                if (tab.Search is null || !Enum.IsDefined(typeof(LoadoutPageSubTabs), tab.SubTab)) throw new InvalidDataException("Invalid tab state");
                if (tab.Factory == _detailsFactory.ToString()) {
                    if (tab.DiagnosticTitle is null || tab.DiagnosticEndpoint is null || tab.DiagnosticProfile is null)
                        throw new InvalidDataException("Missing diagnostic profile context");
                } else if (!_pages.ContainsKey(tab.Factory) && tab.Factory != NewTabPageFactory.StaticId.ToString())
                    throw new InvalidDataException("Unsupported saved page");
            }
        }
        if (Math.Abs(area - 1) > .005 || workspace.Panels.Count(p => p.Active) > 1) throw new InvalidDataException("Invalid panel grid");
    }
    private static (string Endpoint, string Path) Key(IWorkspaceViewModel workspace) => workspace.Context is Mo2WorkspaceContext c ? (c.Endpoint, c.ProfilePath) : ("", "");
    public void Restore(IWorkspaceViewModel workspace, IWorkspaceController controller)
    {
        if (!_saved.TryGetValue(Key(workspace), out var saved)) return;
        var original = workspace.ToData();
        try {
            var data = new WorkspaceData { Id = workspace.Id, Context = workspace.Context, Panels = saved.Panels.Select(panel => {
                var tabs = panel.Tabs.Select(tab => {
                    PageData page;
                    if (tab.Factory == NewTabPageFactory.StaticId.ToString()) page = controller.GetDefaultPageData(workspace.Id);
                    else if (tab.Factory == _detailsFactory.ToString()) page = new PageData { FactoryId = _detailsFactory,
                        Context = new Mo2HealthDetailsContext(new Diagnostic { Id = new DiagnosticId("MO2", 1), Title = tab.DiagnosticTitle!, Severity = DiagnosticSeverity.Warning,
                            Summary = DiagnosticMessage.From(""), Details = DiagnosticMessage.From(""), DataReferences = [] }, tab.DiagnosticEndpoint!, tab.DiagnosticProfile!) };
                    else {
                        page = _pages[tab.Factory];
                        if (page.Context is Mo2GamePageContext context) page = page with { Context = context with { Game = tab.Game } };
                    }
                    return new TabData { Id = PanelTabId.NewId(), PageData = page };
                }).ToArray();
                return new PanelData { LogicalBounds = new Rect(panel.X, panel.Y, panel.Width, panel.Height), Tabs = tabs, SelectedTabId = tabs[panel.Selected].Id };
            }).ToArray() };
            workspace.FromData(data);
            var panels = workspace.Panels.ToArray();
            if (panels.Length != saved.Panels.Length) throw new InvalidDataException("Could not recreate all panels");
            for (var i = 0; i < panels.Length; i++) {
                var panel = panels[i]; var state = saved.Panels[i];
                if (panel.Tabs.Count != state.Tabs.Length) throw new InvalidDataException("Could not recreate all tabs");
                for (var j = 0; j < panel.Tabs.Count; j++) {
                    if (panel.Tabs[j].Contents.ViewModel is ScenarioInstalledPage mods) {
                        mods.Mo2SearchText = state.Tabs[j].Search; mods.Mo2SearchExpanded = state.Tabs[j].SearchExpanded;
                        mods.SelectedSubTab = LoadoutPageSubTabs.Mods;
                    }
                    panel.Tabs[j].Header.IsSelected = j == state.Selected;
                }
                panel.IsSelected = state.Active;
            }
        } catch (Exception error) {
            workspace.FromData(original); _canSave = false;
            Error = "Could not restore panel layouts: " + error.Message;
        }
    }
    public void Save(IEnumerable<IWorkspaceViewModel> workspaces)
    {
        if (_path is null || !_canSave) return;
        try {
            foreach (var workspace in workspaces) {
                if (workspace.Context is Mo2WorkspaceContext { ProfilePath.Length: 0 }) continue;
                var key = Key(workspace);
                var saved = new Workspace(key.Endpoint, key.Path, workspace.Panels.Select(panel => {
                    var tabs = panel.Tabs.Select(tab => {
                        var page = tab.Contents.PageData;
                        var mods = tab.Contents.ViewModel as ScenarioInstalledPage;
                        var details = page.Context as Mo2HealthDetailsContext;
                        return new Tab(page.FactoryId.ToString(), (page.Context as Mo2GamePageContext)?.Game, mods?.Mo2SearchText ?? "", mods?.Mo2SearchExpanded ?? false,
                            (int)(mods?.SelectedSubTab ?? LoadoutPageSubTabs.Mods), details?.Diagnostic.Title, details?.Endpoint, details?.ProfilePath);
                    }).ToArray();
                    return new Panel(panel.LogicalBounds.X, panel.LogicalBounds.Y, panel.LogicalBounds.Width, panel.LogicalBounds.Height, panel.IsSelected,
                        panel.Tabs.ToList().FindIndex(t => t.Id == panel.SelectedTab.Id), tabs);
                }).ToArray());
                Validate(saved); _saved[key] = saved;
            }
            var json = JsonSerializer.Serialize(new Layout(2, _saved.Values.OrderBy(x => x.Endpoint).ThenBy(x => x.ProfilePath).ToArray()), new JsonSerializerOptions { WriteIndented = true });
            if (json == _lastWritten) return;
            var path = Path.GetFullPath(_path);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temporary, json); File.Move(temporary, path, overwrite: true); }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            _lastWritten = json; Error = null;
        } catch (Exception error) { Error = "Could not save panel layouts: " + error.Message; }
    }
}
