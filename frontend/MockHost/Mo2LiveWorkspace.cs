using System.Reactive;
using System.Reactive.Linq;
using Avalonia.ReactiveUI;
using NexusMods.App.UI.Controls.Spine;
using NexusMods.App.UI.Controls.TopBar;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusMods.App.UI;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Pages.Sorting;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceAttachments;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.Sdk.Settings;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class Mo2LiveWorkspace : IWorkspaceWindow
{
    private readonly ServiceProvider _database = ScenarioDatabase.Create();
    private readonly ServiceProvider _desktop;
    public NexusMods.Sdk.IOSInterop DesktopInterop => _desktop.GetRequiredService<NexusMods.Sdk.IOSInterop>();
    public WindowId WindowId { get; } = WindowId.NewId();
    public bool IsActive => true;
    public IWorkspaceController WorkspaceController { get; }
    public ReactiveCommand<Unit, bool> BringWindowToFront { get; } = ReactiveCommand.Create(() => true);
    private readonly PageData _downloadsPage;
    private readonly PageData _profilesPage;
    private readonly PageData _modsPage;
    private readonly PageData _gamesPage;
    private readonly PageData _gameLoadoutsPage;
    private readonly PageData _connectionsPage;
    private readonly Mo2HealthDetailsFactory _healthDetailsFactory;
    private readonly Mo2WorkspaceLayout _layout;
    public string? LayoutError => _layout.Error;
    public void SaveLayouts() => _layout.Save(WorkspaceController.AllWorkspaces);
    public PageData ConnectionsPage => _connectionsPage;
    private readonly WorkspaceId _homeWorkspace;
    private WorkspaceId _profileWorkspace;
    private readonly PageData _pluginsPage;
    private readonly PageData _healthPage;
    private readonly Dictionary<(string Endpoint, string Path), WorkspaceId> _profileWorkspaces = new();
    private readonly Dictionary<WorkspaceId, Mo2LoadoutMenu> _profileMenus = new();
    public ScenarioHomeMenu HomeMenu { get; }
    public Mo2LoadoutMenu ProfileMenu => _profileMenus[_profileWorkspace];
    public Mo2InstanceCatalog Catalog { get; }
    public IReadOnlyList<Mo2CatalogEntry> CatalogEntries { get; private set; } = [];
    private string? _catalogSnapshot;
    public event Action? CatalogChanged;
    public void RefreshCatalog()
    {
        var entries = Catalog.Read();
        var snapshot = System.Text.Json.JsonSerializer.Serialize(entries.Select(entry => new {
            entry.Registration, entry.Error, Game = entry.Instance?.Game, Selected = entry.Instance?.SelectedProfile,
            Profiles = entry.Instance?.Profiles.Select(profile => new {
                profile.Name, profile.Directory, Total = profile.ModEntries.Length, Enabled = profile.ModEntries.Count(x => x.Enabled)
            })
        }));
        CatalogEntries = entries;
        if (snapshot == _catalogSnapshot) return;
        _catalogSnapshot = snapshot; CatalogChanged?.Invoke();
    }
    public string GameName => Profile.GameName;
    public Mo2LiveProfile Profile { get; }
    private IEnumerable<object> ProfilePages => WorkspaceController.TryGetWorkspace(_profileWorkspace, out var workspace)
        ? workspace.Panels.SelectMany(x => x.Tabs).Select(x => (object)x.Contents.ViewModel) : [];
    public ScenarioInstalledPage? ModsPage => ProfilePages.OfType<ScenarioInstalledPage>().FirstOrDefault();
    public ScenarioLoadOrderPage? PluginsPage => ProfilePages.OfType<ScenarioLoadOrderPage>().FirstOrDefault();
    public Mo2LiveWorkspace(string endpoint)
    {
        Profile = new(endpoint);
        Catalog = new(endpoint);
        RefreshCatalog();
        Profile.Changed += RefreshCatalog;
        var services = new FixtureServices();
        var windows = new FixtureWindows { ActiveWindow = this };
        services.Add<IWindowManager>(windows);
        services.Add<ISettingsManager>(new MemorySettings(services));
        services.Add<ILoggerFactory>(NullLoggerFactory.Instance);
        services.Add<IWorkspaceAttachmentsFactoryManager>(new FixtureAttachments());
        _desktop = NexusMods.Backend.ServiceExtensions.AddRuntimeDependencies(
            NexusMods.Backend.ServiceExtensions.AddOSInterop(new ServiceCollection()
                .AddLogging().AddSingleton<NexusMods.Paths.IFileSystem>(NexusMods.Paths.FileSystem.Shared)
                .AddSingleton(services.GetRequiredService<ISettingsManager>()))).BuildServiceProvider();
        services.Add<NexusMods.Sdk.IOSInterop>(DesktopInterop);
        services.Add<IEnumerable<ILoadOrderDataProvider>>([new ScenarioOrderProvider()]);
        services.Add(_database.GetRequiredService<NexusMods.MnemonicDB.Abstractions.IConnection>());
        services.Add<IEnumerable<ILoadoutDataProvider>>([Profile]);
        var mods = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82101", "Mods", IconValues.Package,
            () => new ScenarioInstalledPage(services, windows, Profile, Profile.Order, openDownloads: OpenDownloads));
        _modsPage = mods.Data;
        var plugins = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82102", "Plugins", IconValues.Package,
            () => new ScenarioLoadOrderPage(services, Profile.Order) { LiveProfile = Profile });
        _pluginsPage = plugins.Data;
        var downloads = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82103", "Downloads", IconValues.LibraryOutline,
            () => new Mo2DownloadsPage(windows, Profile));
        _downloadsPage = downloads.Data;
        var profiles = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82104", "My Loadouts", IconValues.Package,
            () => new Mo2LoadoutsPage(windows, this, null));
        var gameLoadouts = new Mo2GameLoadoutsFactory(windows, this);
        _gameLoadoutsPage = gameLoadouts.Data;
        var games = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82105", "My Games", IconValues.GamepadOutline,
            () => new Mo2GamesPage(windows, this));
        _gamesPage = games.Data;
        var connections = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82106", "MO2 instances", IconValues.Folder,
            () => new Mo2ProfilesPage(windows, Catalog, Profile, ShowProfile));
        _connectionsPage = connections.Data;
        _profilesPage = profiles.Data;
        var health = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82108", "Health Check", IconValues.Cardiology,
            () => new Mo2HealthPage(windows, this));
        _healthPage = health.Data;
        _healthDetailsFactory = new Mo2HealthDetailsFactory(windows, this);
        _layout = new Mo2WorkspaceLayout([mods.Data, plugins.Data, downloads.Data, profiles.Data, games.Data, gameLoadouts.Data, connections.Data, health.Data], _healthDetailsFactory.Id);
        services.Add(new PageFactoryController([health, _healthDetailsFactory, mods, plugins, downloads, profiles, games, gameLoadouts, connections, new NewTabPageFactory(services)]));
        var controllerType = typeof(WorkspaceViewModel).Assembly.GetType("NexusMods.App.UI.WorkspaceSystem.WorkspaceController", true)!;
        WorkspaceController = (IWorkspaceController)Activator.CreateInstance(controllerType, this, services)!;
        var home = WorkspaceController.CreateWorkspace(new HomeContext(), games.Data);
        _homeWorkspace = home.Id;
        _layout.Restore(home, WorkspaceController);
        WorkspaceController.ChangeActiveWorkspace(home.Id);
        HomeMenu = new ScenarioHomeMenu(WorkspaceController, games.Data, profiles.Data);
        _profileWorkspace = CreateProfileWorkspace(new Mo2WorkspaceContext()).Id;
        WorkspaceController.ChangeActiveWorkspace(_profileWorkspace);
        Profile.Changed += () => {
            if (Profile.IsConnected && !Profile.SelectingProfile && Profile.ProfilePath.Length > 0)
                EnsureProfileWorkspace(WorkspaceController.ActiveWorkspace.Context is Mo2WorkspaceContext);
        };
    }
    private IWorkspaceViewModel CreateProfileWorkspace(Mo2WorkspaceContext context)
    {
        var workspace = WorkspaceController.CreateWorkspace(context, _modsPage);
        _profileMenus[workspace.Id] = new Mo2LoadoutMenu(WorkspaceController, workspace.Id, _modsPage, _pluginsPage, _downloadsPage, _healthPage);
        WorkspaceController.OpenPage(workspace.Id, _pluginsPage, new OpenPageBehavior.NewPanel(WorkspaceGridState.From(true,
            new PanelGridState(workspace.Panels.Single().Id, new Rect(0, 0, 0.5, 1)),
            new PanelGridState(PanelId.DefaultValue, new Rect(0.5, 0, 0.5, 1)))));
        return workspace;
    }
    private void EnsureProfileWorkspace(bool activate)
    {
        if (Profile.ProfilePath.Length == 0) return;
        var key = (Profile.Endpoint, Profile.ProfilePath);
        if (!_profileWorkspaces.TryGetValue(key, out var id)) {
            var context = new Mo2WorkspaceContext(key.Endpoint, key.ProfilePath);
            if (_profileWorkspaces.Count == 0 && WorkspaceController.TryGetWorkspace(_profileWorkspace, out var initial)) {
                initial.Context = context; id = initial.Id;
            } else id = CreateProfileWorkspace(context).Id;
            _profileWorkspaces.Add(key, id);
            if (WorkspaceController.TryGetWorkspace(id, out var restored)) _layout.Restore(restored, WorkspaceController);
        }
        _profileWorkspace = id;
        if (activate && WorkspaceController.ActiveWorkspaceId != id) WorkspaceController.ChangeActiveWorkspace(id);
    }
    private void OpenHomePage(PageData page, bool replace = false)
    {
        WorkspaceController.ChangeActiveWorkspace(_homeWorkspace);
        var panel = WorkspaceController.ActiveWorkspace.SelectedPanel ?? WorkspaceController.ActiveWorkspace.Panels.First();
        var existing = panel.Tabs.FirstOrDefault(x => x.Contents.PageData.FactoryId == page.FactoryId);
        if (existing is not null && !replace) {
            panel.SelectTab(existing.Id);
            // NMA restores selection from tab headers when a dormant panel activates.
            foreach (var tab in panel.Tabs) tab.Header.IsSelected = tab.Id == existing.Id;
        }
        else WorkspaceController.OpenPage(_homeWorkspace, page, existing is not null
            ? new OpenPageBehavior.ReplaceTab(panel.Id, existing.Id) : new OpenPageBehavior.NewTab(panel.Id));
    }
    public void OpenDownloads() => OpenHomePage(_downloadsPage);
    public void ShowHome() => WorkspaceController.ChangeActiveWorkspace(_homeWorkspace);
    public void OpenGames() => OpenHomePage(_gamesPage);
    public void OpenConnections() => OpenHomePage(_connectionsPage);
    public void OpenProfiles() => OpenLoadouts(null);
    public void OpenLoadouts(string? game) => OpenHomePage(game is null ? _profilesPage : _gameLoadoutsPage with { Context = new Mo2GamePageContext(_gameLoadoutsPage.FactoryId, game) }, true);
    public void OpenHealthDetails(Mo2HealthDetailsContext context, NavigationInformation info)
    {
        var page = new PageData { FactoryId = _healthDetailsFactory.Id, Context = context };
        WorkspaceController.OpenPage(_profileWorkspace, page, WorkspaceController.GetOpenPageBehavior(page, info));
    }
    public void ShowProfile()
    {
        EnsureProfileWorkspace(false);
        WorkspaceController.ChangeActiveWorkspace(_profileWorkspace);
    }
    public Window CreateWindow()
    {
        var status = new TextBlock { Text = Profile.Status, Margin = new Thickness(16, 8), TextWrapping = TextWrapping.Wrap };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,232,*"), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var topBar = new TopBarView { ViewModel = new Mo2TopBar(this) };
        topBar.ViewModel.WhenAnyValue(x => x.ActiveWorkspaceSubtitle).Subscribe(text =>
            topBar.FindControl<TextBlock>("ActiveWorkspaceSubtitleTextBlock")!.IsVisible = !string.IsNullOrEmpty(text));
        topBar.FindControl<MenuItem>("ViewAppLogsMenuItem")!.Header = "View MO2 logs";
        var account = topBar.FindControl<NexusMods.App.UI.Controls.StandardButton>("LoginButton")!;
        account.Text = "Nexus account";
        ToolTip.SetTip(account, "Manage the selected instance’s Nexus account in MO2. Select a profile first.");
        Grid.SetColumn(topBar, 1); Grid.SetColumnSpan(topBar, 2); grid.Children.Add(topBar);
        var view = new WorkspaceView { ViewModel = WorkspaceController.ActiveWorkspace, Margin = new Thickness(12, 0) };
        Grid.SetRow(view, 1); Grid.SetColumn(view, 2); grid.Children.Add(view);
        var spine = new Spine { ViewModel = new Mo2Spine(this) };
        Grid.SetRowSpan(spine, 2); grid.Children.Add(spine);
        var profileSidebar = new NexusMods.App.UI.LeftMenu.Loadout.LoadoutLeftMenuView { ViewModel = ProfileMenu };
        // MO2 has no separate collection/deployment authority.
        foreach (var name in new[] { "NewCollection", "ApplyControlViewHost" })
            profileSidebar.FindControl<Control>(name)!.IsVisible = false;
        var launchPanel = new Mo2LaunchPanel(Profile);
        Grid.SetRow(launchPanel, 1);
        ((Grid)profileSidebar.Content!).Children.Add(launchPanel);
        var homeSidebar = new NexusMods.App.UI.LeftMenu.Home.HomeLeftMenuView { ViewModel = HomeMenu };
        var sidebar = new ContentControl { Content = homeSidebar };
        Grid.SetColumn(sidebar, 1); Grid.SetRow(sidebar, 1); grid.Children.Add(sidebar);
        WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace).Subscribe(workspace => { view.ViewModel = workspace; profileSidebar.ViewModel = ProfileMenu; sidebar.Content = workspace.Context is Mo2WorkspaceContext ? profileSidebar : homeSidebar; });
        Profile.Changed += () => { view.IsEnabled = !Profile.Launching && !Profile.ManagingMod && !Profile.SelectingProfile; };
        Grid.SetRow(status, 2); Grid.SetColumnSpan(status, 3); grid.Children.Add(status);
        var window = new Window { Title = "Mod Organizer — Live MO2 profile", Width = 1440, Height = 900,
            Background = (IBrush)Application.Current!.FindResource("SurfaceBaseBrush")!, Content = grid };
        void UpdateStatus() => status.Text = Profile.Status + (LayoutError is { } error ? " · " + error : "");
        Profile.Changed += UpdateStatus;
        UpdateStatus();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) => { await Profile.Refresh(); RefreshCatalog(); SaveLayouts(); UpdateStatus(); };
        window.Opened += async (_, _) => { await Profile.Refresh(); timer.Start(); };
        window.Closing += (_, _) => SaveLayouts();
        window.Closed += (_, _) => { timer.Stop(); SaveLayouts(); };
        return window;
    }
    public void Dispose() { _desktop.Dispose(); Task.Run(async () => await _database.DisposeAsync()).GetAwaiter().GetResult(); }
}

internal sealed record Mo2WorkspaceContext(string Endpoint = "", string ProfilePath = "") : IWorkspaceContext
{
    public bool IsValid(IServiceProvider services) => true;
}
