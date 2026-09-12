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
    public PageData ConnectionsPage => _connectionsPage;
    private readonly WorkspaceId _homeWorkspace;
    private readonly WorkspaceId _profileWorkspace;
    public ScenarioHomeMenu HomeMenu { get; }
    public Mo2LoadoutMenu ProfileMenu { get; }
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
    public string GameName => Profile.NexusGame switch { "newvegas" => "Fallout: New Vegas", "skyrimspecialedition" => "Skyrim Special Edition", _ => "MO2 profile" };
    public Mo2LiveProfile Profile { get; }
    public ScenarioInstalledPage? ModsPage { get; private set; }
    public ScenarioLoadOrderPage? PluginsPage { get; private set; }
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
            () => ModsPage = new ScenarioInstalledPage(services, windows, Profile, Profile.Order));
        _modsPage = mods.Data;
        var plugins = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82102", "Plugins", IconValues.Package,
            () => PluginsPage = new ScenarioLoadOrderPage(services, Profile.Order) { LiveProfile = Profile });
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
        _healthDetailsFactory = new Mo2HealthDetailsFactory(windows);
        services.Add(new PageFactoryController([health, _healthDetailsFactory, mods, plugins, downloads, profiles, games, gameLoadouts, connections, new NewTabPageFactory(services)]));
        var controllerType = typeof(WorkspaceViewModel).Assembly.GetType("NexusMods.App.UI.WorkspaceSystem.WorkspaceController", true)!;
        WorkspaceController = (IWorkspaceController)Activator.CreateInstance(controllerType, this, services)!;
        var home = WorkspaceController.CreateWorkspace(new HomeContext(), games.Data);
        _homeWorkspace = home.Id;
        WorkspaceController.ChangeActiveWorkspace(home.Id);
        HomeMenu = new ScenarioHomeMenu(WorkspaceController, games.Data, profiles.Data);
        var workspace = WorkspaceController.CreateWorkspace(new Mo2WorkspaceContext(), mods.Data);
        _profileWorkspace = workspace.Id;
        ProfileMenu = new Mo2LoadoutMenu(WorkspaceController, workspace.Id, mods.Data, plugins.Data, downloads.Data, health.Data);
        WorkspaceController.ChangeActiveWorkspace(workspace.Id);
        WorkspaceController.OpenPage(workspace.Id, plugins.Data, new OpenPageBehavior.NewPanel(WorkspaceGridState.From(true, new PanelGridState(workspace.Panels.Single().Id, new Rect(0, 0, 0.5, 1)), new PanelGridState(PanelId.DefaultValue, new Rect(0.5, 0, 0.5, 1)))));
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
    public void OpenGames() => OpenHomePage(_gamesPage);
    public void OpenConnections() => OpenHomePage(_connectionsPage);
    public void OpenProfiles() => OpenLoadouts(null);
    public void OpenLoadouts(string? game) => OpenHomePage(game is null ? _profilesPage : _gameLoadoutsPage with { Context = new Mo2GamePageContext(_gameLoadoutsPage.FactoryId, game) }, true);
    public void OpenHealthDetails(NexusMods.Abstractions.Diagnostics.Diagnostic diagnostic, NavigationInformation info)
    {
        var page = new PageData { FactoryId = _healthDetailsFactory.Id, Context = new Mo2HealthDetailsContext(diagnostic) };
        WorkspaceController.OpenPage(_profileWorkspace, page, WorkspaceController.GetOpenPageBehavior(page, info));
    }
    public void ShowProfile()
    {
        WorkspaceController.ChangeActiveWorkspace(_profileWorkspace);
        var panel = WorkspaceController.ActiveWorkspace.Panels.OrderBy(x => x.LogicalBounds.X).First();
        var existing = panel.Tabs.FirstOrDefault(x => x.Contents.ViewModel is ScenarioInstalledPage { IsMo2Profile: true });
        if (existing is not null) {
            ModsPage = (ScenarioInstalledPage)existing.Contents.ViewModel;
            panel.SelectTab(existing.Id);
            foreach (var tab in panel.Tabs) tab.Header.IsSelected = tab.Id == existing.Id;
        } else {
            WorkspaceController.OpenPage(WorkspaceController.ActiveWorkspaceId, _modsPage, new OpenPageBehavior.NewTab(panel.Id));
        }
    }
    public Window CreateWindow()
    {
        var title = new TextBlock { Text = Profile.CollectionName.Value, FontSize = 20, Margin = new Thickness(16, 12) };
        var status = new TextBlock { Text = Profile.Status, Margin = new Thickness(16, 8), TextWrapping = TextWrapping.Wrap };
        var refresh = new Button { Content = "Refresh from MO2", Margin = new Thickness(12) };
        refresh.Click += async (_, _) => await Profile.Refresh();
        var downloads = new Button { Content = "Downloads", Margin = new Thickness(12) };
        downloads.Click += (_, _) => OpenDownloads();
        var profiles = new Button { Content = "Manage MO2 profiles…", Margin = new Thickness(12) };
        profiles.Click += async (_, _) => await Profile.ManageProfiles();
        var header = new DockPanel();
        DockPanel.SetDock(profiles, Dock.Right); header.Children.Add(profiles);
        DockPanel.SetDock(downloads, Dock.Right); header.Children.Add(downloads);
        DockPanel.SetDock(refresh, Dock.Right); header.Children.Add(refresh); header.Children.Add(title);
        var executable = new ComboBox { MinWidth = 180, Margin = new Thickness(16, 0, 8, 8) };
        var launch = new Button { Content = "Run through MO2", Margin = new Thickness(0, 0, 8, 8), IsEnabled = false };
        launch.Click += async (_, _) => { if (executable.SelectedItem is string name) await Profile.Launch(name); };
        var launchBar = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal };
        launchBar.Children.Add(executable); launchBar.Children.Add(launch);
        var top = new StackPanel(); top.Children.Add(header); top.Children.Add(launchBar);
        Profile.Changed += () => {
            if (!(executable.ItemsSource as IEnumerable<string> ?? []).SequenceEqual(Profile.Executables)) {
                var selected = executable.SelectedItem as string;
                executable.ItemsSource = Profile.Executables;
                executable.SelectedItem = Profile.Executables.Contains(selected!) ? selected : Profile.Executables.FirstOrDefault();
            }
            launchBar.IsEnabled = !Profile.Launching && !Profile.Installing && !Profile.SelectingProfile && !Profile.ManagingMod;
            launch.IsEnabled = Profile.Executables.Count > 0;
        };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,232,*"), RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto") };
        Grid.SetColumn(top, 2);
        var topBar = new TopBarView { ViewModel = new Mo2TopBar(this) };
        var account = topBar.FindControl<NexusMods.App.UI.Controls.StandardButton>("LoginButton")!;
        account.Text = "Nexus account";
        ToolTip.SetTip(account, "Manage the selected instance’s Nexus account in MO2. Select a profile first.");
        Grid.SetColumn(topBar, 1); Grid.SetColumnSpan(topBar, 2); grid.Children.Add(topBar);
        var view = new WorkspaceView { ViewModel = WorkspaceController.ActiveWorkspace, Margin = new Thickness(12, 0) };
        Grid.SetRow(top, 1); grid.Children.Add(top);
        Grid.SetRow(view, 2); Grid.SetColumn(view, 2); grid.Children.Add(view);
        var spine = new Spine { ViewModel = new Mo2Spine(this) };
        Grid.SetRowSpan(spine, 3); grid.Children.Add(spine);
        var profileSidebar = new NexusMods.App.UI.LeftMenu.Loadout.LoadoutLeftMenuView { ViewModel = ProfileMenu };
        // MO2 has no separate collection/deployment authority.
        foreach (var name in new[] { "NewCollection", "ApplyControlViewHost" })
            profileSidebar.FindControl<Control>(name)!.IsVisible = false;
        var homeSidebar = new NexusMods.App.UI.LeftMenu.Home.HomeLeftMenuView { ViewModel = HomeMenu };
        var sidebar = new ContentControl { Content = homeSidebar };
        Grid.SetColumn(sidebar, 1); Grid.SetRow(sidebar, 1); Grid.SetRowSpan(sidebar, 2); grid.Children.Add(sidebar);
        WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace).Subscribe(workspace => { view.ViewModel = workspace; top.IsVisible = workspace.Id == _profileWorkspace; sidebar.Content = workspace.Id == _profileWorkspace ? profileSidebar : homeSidebar; });
        Profile.Changed += () => { view.IsEnabled = !Profile.Launching && !Profile.ManagingMod; header.IsEnabled = !Profile.Launching && !Profile.ManagingMod; };
        Grid.SetRow(status, 3); Grid.SetColumnSpan(status, 3); grid.Children.Add(status);
        var window = new Window { Title = "Mod Organizer — Live MO2 profile", Width = 1440, Height = 900,
            Background = (IBrush)Application.Current!.FindResource("SurfaceBaseBrush")!, Content = grid };
        Profile.Changed += () => { title.Text = Profile.CollectionName.Value; status.Text = Profile.Status; };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) => { await Profile.Refresh(); RefreshCatalog(); };
        window.Opened += async (_, _) => { await Profile.Refresh(); timer.Start(); };
        window.Closed += (_, _) => timer.Stop();
        return window;
    }
    public void Dispose() { _desktop.Dispose(); Task.Run(async () => await _database.DisposeAsync()).GetAwaiter().GetResult(); }
}

internal sealed record Mo2WorkspaceContext : IWorkspaceContext
{
    public bool IsValid(IServiceProvider services) => true;
}
