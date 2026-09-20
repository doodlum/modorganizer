using Avalonia.Styling;
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
using System.Reactive.Threading.Tasks;
using NexusMods.Games.FOMOD.UI;
using NexusMods.Sdk.Settings;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class Mo2LiveWorkspace : IWorkspaceWindow
{
    private readonly ServiceProvider _database = Database();
    private static ServiceProvider Database()
    {
        using var timing = Mo2StartupCheck.Phase("database");
        return ScenarioDatabase.Create();
    }
    private readonly ServiceProvider _desktop;
    public NexusMods.Sdk.IOSInterop DesktopInterop => _desktop.GetRequiredService<NexusMods.Sdk.IOSInterop>();
    private readonly Func<Task> _createCollection;
    private readonly Func<string,Task<bool>> _removeCollection;
    public WindowId WindowId { get; } = WindowId.NewId();
    public bool IsActive => true;
    public IWorkspaceController WorkspaceController { get; }
    public ReactiveCommand<Unit, bool> BringWindowToFront { get; } = ReactiveCommand.Create(() => true);
    private readonly PageData _downloadsPage;
    private readonly PageData _toolsPage;
    private readonly PageData _overwritePage;
    private readonly PageData _externalFilesPage;
    private readonly PageData _logsPage;
    private readonly PageData _archivesPage;
    private readonly PageData _dataPage;
    private readonly PageData _savesPage;
    private readonly PageData _profilesPage;
    private readonly PageData _modsPage;
    private readonly PageData _gamesPage;
    private readonly PageData _gameLoadoutsPage;
    private readonly PageData _settingsPage;
    private readonly PageData _componentsPage;
    private Mo2TopBar? _topBar;
    internal System.Reactive.Subjects.BehaviorSubject<string> NexusAccountStatus { get; } = new("Checking Nexus account…");
    private readonly Mo2HealthDetailsFactory _healthDetailsFactory;
    private readonly Mo2WorkspaceLayout _layout;
    public string? LayoutError => _layout.Error;
    public void SaveLayouts() => _layout.Save(WorkspaceController.AllWorkspaces);
    public PageData SettingsPage => _settingsPage;
    private readonly WorkspaceId _homeWorkspace;
    private WorkspaceId _profileWorkspace;
    private readonly PageData _pluginsPage;
    private readonly PageData _healthPage;
    private readonly Dictionary<(string Endpoint, string Path), WorkspaceId> _profileWorkspaces = new();
    private readonly Dictionary<WorkspaceId, Mo2LoadoutMenu> _profileMenus = new();
    public ScenarioHomeMenu HomeMenu { get; }
    // MO2's run row. A collapsed sidebar moves it into that sidebar's tool flyout, so
    // it is not in the window's tree until the flyout is opened; a check reads it
    // here instead of guessing which of the two places it is in.
    internal Mo2LaunchPanel? LaunchPanel { get; private set; }
    public Mo2LoadoutMenu ProfileMenu => _profileMenus[_profileWorkspace];
    public Mo2InstanceCatalog Catalog { get; }
    public IReadOnlyList<Mo2CatalogEntry> CatalogEntries { get; private set; } = [];
    private string? _catalogSnapshot;
    private bool _catalogReading, _catalogRefreshPending, _disposed;
    public event Action? CatalogChanged;
    public void RefreshCatalog()
    {
        if (_disposed) return;
        _catalogRefreshPending = true;
        if (!_catalogReading) _ = RefreshCatalogAsync();
    }
    private async Task RefreshCatalogAsync()
    {
        _catalogReading = true;
        try {
            do {
                _catalogRefreshPending = false;
                var entries = await Catalog.ReadAsync();
                if (!_disposed) ApplyCatalog(entries);
            } while (_catalogRefreshPending && !_disposed);
        } catch (Exception) {
            Console.Error.WriteLine("Could not refresh MO2 catalog; the next poll will retry.");
        } finally { _catalogReading = false; }
    }
    private void ApplyCatalog(IReadOnlyList<Mo2CatalogEntry> entries)
    {
        using var timing = Mo2UiLatencyProbe.Measure("Apply instance catalog");
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
    // MO2 keeps a mod as a folder under the instance mods directory, and the
    // profile sits at <instance>/profiles/<name>, so the instance is two up.
    private string ModsRoot()
    {
        var profile = Mo2InstanceCatalog.LocalPath(Profile.ProfilePath);
        var instance = Path.GetDirectoryName(Path.GetDirectoryName(profile));
        return Path.Combine(instance ?? profile, "mods");
    }

    public string GameName => Profile.GameName;
    public Mo2LiveProfile Profile { get; }
    private readonly Task _firstRead;
    private IEnumerable<object> ProfilePages => WorkspaceController.TryGetWorkspace(_profileWorkspace, out var workspace)
        ? workspace.Panels.SelectMany(x => x.Tabs).Select(x => (object)x.Contents.ViewModel) : [];
    public ScenarioInstalledPage? ModsPage => ProfilePages.OfType<ScenarioInstalledPage>().FirstOrDefault();
    public ScenarioLoadOrderPage? PluginsPage => ProfilePages.OfType<ScenarioLoadOrderPage>().FirstOrDefault();
    public Mo2LiveWorkspace(string endpoint)
    {
        using var construction = Mo2StartupCheck.Phase("workspace");
        Profile = new(endpoint);
        Catalog = new(endpoint);
        using (Mo2StartupCheck.Phase("catalog")) ApplyCatalog(Catalog.Read());
        Profile.Changed += RefreshCatalog;
        // MO2's Open in Explorer opens the folder in the prefix; the frontend opens it
        // on the desktop it is running on, which is what the interop here is for.
        Profile.OpenLocalFolder = folder =>
            DesktopInterop.OpenDirectory(NexusMods.Paths.FileSystem.Shared.FromUnsanitizedFullPath(folder));
        Profile.OpenLocalFile = file =>
            DesktopInterop.OpenFile(NexusMods.Paths.FileSystem.Shared.FromUnsanitizedFullPath(file));
        Profile.RevealLocalFile = file =>
            DesktopInterop.OpenFileInDirectory(NexusMods.Paths.FileSystem.Shared.FromUnsanitizedFullPath(file));
        var services = new FixtureServices();
        var windows = new FixtureWindows { ActiveWindow = this };
        Profile.ConfirmRemoval = mods => Mo2SeparatorDialog.ConfirmRemoval(windows, mods);
        Profile.ChooseOrderBackup = list => Mo2RestoreBackupDialog.Show(windows, Profile, list);
        Profile.ShowModInformation = id => Mo2ModInformationDialog.Show(windows, Profile, id);
        // NMA guided-installer window and the FOMOD engine behind it, in their own
        // provider: the installer resolves its view models from a scope, which this
        // shell own fixture container does not provide.
        var installerServices = new ServiceCollection().AddLogging()
            .AddGuidedInstallerUi().BuildServiceProvider();
        Profile.InstallLocally = archive => Mo2FomodInstaller.Install(installerServices, archive, ModsRoot());
        services.Add<IWindowManager>(windows);
        var memorySettings = new MemorySettings(services, new Mo2AlertPreferences(Mo2AlertPreferences.DefaultPath));
        services.Add<ISettingsManager>(memorySettings);
        services.Add<ILoggerFactory>(NullLoggerFactory.Instance);
        services.Add<IWorkspaceAttachmentsFactoryManager>(new FixtureAttachments());
        using (Mo2StartupCheck.Phase("desktop services"))
            _desktop = NexusMods.Backend.ServiceExtensions.AddRuntimeDependencies(
                NexusMods.Backend.ServiceExtensions.AddOSInterop(new ServiceCollection()
                    .AddLogging().AddSingleton<NexusMods.Paths.IFileSystem>(NexusMods.Paths.FileSystem.Shared)
                    .AddSingleton(services.GetRequiredService<ISettingsManager>()))).BuildServiceProvider();
        services.Add<NexusMods.Sdk.IOSInterop>(DesktopInterop);
        services.Add<IEnumerable<ILoadOrderDataProvider>>([new ScenarioOrderProvider(Profile)]);
        services.Add(_database.GetRequiredService<NexusMods.MnemonicDB.Abstractions.IConnection>());
        services.Add<IEnumerable<ILoadoutDataProvider>>([Profile]);
        var mods = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82101", "My Mods", IconValues.CollectionsOutline,
            () => new ScenarioInstalledPage(services, windows, Profile, Profile.Order, openDownloads: OpenDownloads) {
                CreateCollection = () => Mo2Collections.Create(windows, Profile), Instances = () => CatalogEntries,
                OpenFolder = folder => DesktopInterop.OpenDirectory(NexusMods.Paths.FileSystem.Shared.FromUnsanitizedFullPath(folder)) });
        var collections = new Mo2CollectionFactory(services, windows, Profile, OpenDownloads);
        _createCollection = () => Mo2Collections.Create(windows, Profile);
        _removeCollection = key => Mo2Collections.Remove(windows, Profile, key);
        _modsPage = mods.Data;
        var plugins = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82102", "Plugins", ScenarioLoadOrderPage.PluginIcon,
            () => new ScenarioLoadOrderPage(services, Profile.Order) { LiveProfile = Profile });
        _pluginsPage = plugins.Data;
        var downloads = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82103", "Downloads", IconValues.Download,
            () => new Mo2DownloadsPage(windows, Profile, services));
        _downloadsPage = downloads.Data;
        var profiles = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82104", "My Loadouts", IconValues.Package,
            () => new Mo2LoadoutsPage(windows, this, null));
        var gameLoadouts = new Mo2GameLoadoutsFactory(windows, this);
        _gameLoadoutsPage = gameLoadouts.Data;
        var games = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82105", "My Games", IconValues.GamepadOutline,
            () => new Mo2GamesPage(windows, this));
        _gamesPage = games.Data;
        var components = new FixturePageFactory("3f2b6c41-9d7a-45e2-9c0f-6d1f2a7c8b34", "Components", Mo2ComponentsPage.ComponentsIcon,
            () => new Mo2ComponentsPage(windows, services.GetRequiredService<ISettingsManager>()));
        _componentsPage = components.Data;
        // NMA's own settings page, the same one the fixtures use, rather than the
        // instance-management page the gear used to open. The frontend creates and
        // owns its MO2 instances now, so there is nothing left for the user to
        // register by hand and no Connections page to register it on.
        var settings = ScenarioSettings.Register(services, memorySettings, windows);
        _settingsPage = settings.Data;
        _profilesPage = profiles.Data;
        var health = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82108", "Health Check", IconValues.Cardiology,
            () => new Mo2HealthPage(windows, this));
        _healthPage = health.Data;
        var tools = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82110", "Tools", Mo2ToolsPage.ToolIcon,
            () => new Mo2ToolsPage(windows, Profile));
        _toolsPage = tools.Data;
        var overwrite = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82111", "Overwrite", IconValues.Folder, () => new Mo2OverwritePage(windows, Profile));
        _overwritePage = overwrite.Data;
        var externalFiles = new FixturePageFactory("18e27c99-33d0-4f49-9022-ae3671586d24", "External Files", IconValues.FolderEditOutline,
            () => new Mo2ExternalFilesPage(windows, Profile));
        _externalFilesPage = externalFiles.Data;
        var logs = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82112", "Logs", Mo2LogsPage.LogsIcon, () => new Mo2LogsPage(windows,Profile));
        _logsPage = logs.Data;
        var archives = new FixturePageFactory("bcde2778-955d-4b57-a14e-85a878b82113", "Archives", Mo2ArchivesPage.ArchiveIcon, () => new Mo2ArchivesPage(windows,Profile));
        _archivesPage = archives.Data;
        var data = new FixturePageFactory("5ef051ba-34cd-4674-bb41-18a49ff6b7d3", "Data", Mo2DataPage.DataIcon, () => new Mo2DataPage(windows, Profile));
        _dataPage = data.Data;
        var saves = new FixturePageFactory("c0896704-91ec-4246-8651-85998f4a15b1", "Saves", IconValues.Save, () => new Mo2SavesPage(windows, Profile));
        _savesPage = saves.Data;
        foreach (var factory in new[] { mods, plugins, downloads, health, tools, overwrite, externalFiles, logs, archives, data, saves }) factory.IsAvailable = context => context is Mo2WorkspaceContext;
        foreach (var factory in new[] { games, profiles, settings, components }) factory.IsAvailable = context => context is HomeContext;
        _healthDetailsFactory = new Mo2HealthDetailsFactory(windows, this);
        _layout = new Mo2WorkspaceLayout([mods.Data, plugins.Data, downloads.Data, profiles.Data, games.Data, gameLoadouts.Data, settings.Data, components.Data, health.Data, tools.Data, overwrite.Data, externalFiles.Data, logs.Data, archives.Data, data.Data, saves.Data], _healthDetailsFactory.Id);
        services.Add(new PageFactoryController([collections, saves, data, archives, logs, overwrite, externalFiles, tools, health, _healthDetailsFactory, mods, plugins, downloads, profiles, games, gameLoadouts, settings, components, new NewTabPageFactory(services)]));
        var controllerType = typeof(WorkspaceViewModel).Assembly.GetType("NexusMods.App.UI.WorkspaceSystem.WorkspaceController", true)!;
        WorkspaceController = (IWorkspaceController)Activator.CreateInstance(controllerType, this, services)!;
        void ReplaceCollection(string instance, string oldName, PageData replacement) {
            foreach (var workspace in WorkspaceController.AllWorkspaces.Where(x => x.Context is Mo2WorkspaceContext context && context.Endpoint == instance))
                foreach (var tab in workspace.Panels.SelectMany(x => x.Tabs)) {
                    if (tab is PanelTabViewModel nativeTab)
                        nativeTab.ReplaceHistoryPages(data => data.Context is Mo2CollectionContext history && history.Key == oldName ? replacement : data);
                    if (tab.Contents.PageData.Context is Mo2CollectionContext collection && collection.Key == oldName) {
                        tab.Contents.PageData = replacement;
                        if (tab.Contents.ViewModel is ScenarioInstalledPage page) page.CollectionKey = (replacement.Context as Mo2CollectionContext)?.Key;
                    }
                }
            _layout.ReplaceCollection(instance, oldName, replacement);
            SaveLayouts();
        }
        Profile.CollectionRenamed += (instance, oldName, newName) => ReplaceCollection(instance, oldName, Mo2CollectionFactory.Data(newName));
        Profile.CollectionRemoved += (instance, oldName) => ReplaceCollection(instance, oldName, _modsPage);
        // The connected profile's first read starts here, not when the window opens:
        // the request is a file the host picks up on its own, so it can be in flight
        // while the rest of startup builds. Waiting until Opened put MO2's answer
        // behind every page factory, the workspace restore and the first frame.
        _firstRead = FirstRead();
        async Task FirstRead()
        {
            using var timing = Mo2StartupCheck.Phase("first snapshot");
            await Profile.Refresh();
        }
        using var rest = Mo2StartupCheck.Phase("workspaces");
        var home = WorkspaceController.CreateWorkspace(new HomeContext(), games.Data);
        _homeWorkspace = home.Id;
        _layout.Restore(home, WorkspaceController);
        WorkspaceController.ChangeActiveWorkspace(home.Id);
        HomeMenu = new ScenarioHomeMenu(WorkspaceController, games.Data, profiles.Data, components.Data);
        _profileWorkspace = CreateProfileWorkspace(new Mo2WorkspaceContext()).Id;
        WorkspaceController.ChangeActiveWorkspace(_profileWorkspace);
        Profile.Changed += () => {
            if (Profile.IsConnected && !Profile.SelectingProfile && Profile.ProfilePath.Length > 0)
                EnsureProfileWorkspace(WorkspaceController.ActiveWorkspace.Context is Mo2WorkspaceContext);
        };
    }
    private PageData GameProfilesPage(Mo2WorkspaceContext context)
    {
        var game = CatalogEntries.FirstOrDefault(x => x.Registration.Endpoint == context.Endpoint)?.Instance?.Game;
        return _gameLoadoutsPage with { Context = new Mo2GamePageContext(_gameLoadoutsPage.FactoryId, game) };
    }
    private IWorkspaceViewModel CreateProfileWorkspace(Mo2WorkspaceContext context)
    {
        using var timing = Mo2UiLatencyProbe.Measure("Create profile workspace", always: true);
        var workspace = WorkspaceController.CreateWorkspace(context, _modsPage);
        _profileMenus[workspace.Id] = new Mo2LoadoutMenu(WorkspaceController, workspace.Id, _modsPage, _pluginsPage, _downloadsPage, _healthPage, GameProfilesPage(context), _toolsPage, _overwritePage, _externalFilesPage, _logsPage, _archivesPage, _dataPage, _savesPage, Profile, _createCollection, _removeCollection);
        WorkspaceController.OpenPage(workspace.Id, _pluginsPage, new OpenPageBehavior.NewPanel(WorkspaceGridState.From(true,
            new PanelGridState(workspace.Panels.Single().Id, new Rect(0, 0, 0.5, 1)),
            new PanelGridState(PanelId.DefaultValue, new Rect(0.5, 0, 0.5, 1)))));
        return workspace;
    }
    private void EnsureProfileWorkspace(bool activate)
    {
        using var timing = Mo2UiLatencyProbe.Measure("Ensure profile workspace", always: true);
        if (Profile.ProfilePath.Length == 0) return;
        var key = (Profile.Endpoint, Profile.ProfilePath);
        if (!_profileWorkspaces.TryGetValue(key, out var id)) {
            var context = new Mo2WorkspaceContext(key.Endpoint, key.ProfilePath);
            if (_profileWorkspaces.Count == 0 && WorkspaceController.TryGetWorkspace(_profileWorkspace, out var initial)) {
                initial.Context = context; id = initial.Id;
                _profileMenus[id] = new Mo2LoadoutMenu(WorkspaceController, id, _modsPage, _pluginsPage, _downloadsPage, _healthPage, GameProfilesPage(context), _toolsPage, _overwritePage, _externalFilesPage, _logsPage, _archivesPage, _dataPage, _savesPage, Profile, _createCollection, _removeCollection);
            } else id = CreateProfileWorkspace(context).Id;
            _profileWorkspaces.Add(key, id);
            if (WorkspaceController.TryGetWorkspace(id, out var restored)) {
                _layout.Restore(restored, WorkspaceController);
                foreach (var panel in restored.Panels.ToArray())
                    foreach (var tab in panel.Tabs.ToArray())
                        if (tab.Contents.ViewModel is Mo2GamesPage or Mo2LoadoutsPage { Game: null })
                            WorkspaceController.OpenPage(id, GameProfilesPage(context), new OpenPageBehavior.ReplaceTab(panel.Id,tab.Id));
            }
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
    public void OpenDownloads()
    {
        if (Profile.ProfilePath.Length == 0) {
            OpenHomePage(_downloadsPage);
            return;
        }
        ShowProfile();
        WorkspaceController.OpenPage(_profileWorkspace, _downloadsPage,
            WorkspaceController.GetOpenPageBehavior(_downloadsPage, NavigationInformation.From(NavigationInput.Default)));
    }
    public void OpenHealth() {
        ShowProfile();
        WorkspaceController.OpenPage(_profileWorkspace, _healthPage, WorkspaceController.GetOpenPageBehavior(_healthPage, NavigationInformation.From(NavigationInput.Default)));
    }
    public void ShowHome() => WorkspaceController.ChangeActiveWorkspace(_homeWorkspace);
    public void OpenGames() => OpenHomePage(_gamesPage);
    public void OpenSettings() => OpenHomePage(_settingsPage);
    public void OpenComponents() => OpenHomePage(_componentsPage);
    public void OpenProfiles() => OpenLoadouts(null);
    public void OpenLoadouts(string? game) => OpenHomePage(game is null ? _profilesPage : _gameLoadoutsPage with { Context = new Mo2GamePageContext(_gameLoadoutsPage.FactoryId, game) }, true);
    public void OpenHealthDetails(Mo2HealthDetailsContext context, NavigationInformation info)
    {
        var page = new PageData { FactoryId = _healthDetailsFactory.Id, Context = context };
        WorkspaceController.OpenPage(_profileWorkspace, page, WorkspaceController.GetOpenPageBehavior(page, info));
    }
    public void ShowProfile()
    {
        using var timing = Mo2UiLatencyProbe.Measure("Show profile workspace");
        EnsureProfileWorkspace(false);
        if (WorkspaceController.ActiveWorkspaceId != _profileWorkspace)
            WorkspaceController.ChangeActiveWorkspace(_profileWorkspace);
    }
    private Window? _window;
    internal Mo2TopBar? TopBar => _topBar;

    // Installing MO2 and building an instance takes long enough that the status bar
    // has to say what is happening, so this rides alongside the profile status.
    internal string? Notice { get; private set; }
    internal event Action? NoticeChanged;
    private bool _addingGame;

    // Opening a game means the same thing wherever it is opened from: connect to
    // that game's MO2 and leave the app in that game's own workspace, with the game
    // selected in the spine. Pages belong either to Home or to a game, so a game's
    // profiles are shown inside that game rather than on a Home page beside it.
    public async Task<bool> SelectGame(string game, bool openProfiles = false)
    {
        var wanted = Mo2SupportedGames.Find(game)?.Name ?? game;
        var available = CatalogEntries.Where(entry => entry.Instance is { Profiles.Length: > 0 } instance &&
            (Mo2SupportedGames.Find(instance.Game)?.Name ?? instance.Game) == wanted).ToArray();
        if (available.Length == 0) { OpenLoadouts(game); return false; }
        // Prefer a host that is already running over starting an older one merely
        // because it was registered first, as the spine does.
        var chosen = available.OrderByDescending(entry => Mo2HostStartup.IsRunning(entry.Registration))
            .ThenByDescending(entry => entry.Registration.Launcher is not null).First();
        var profile = chosen.Instance!.Profiles.FirstOrDefault(x => x.Name == chosen.Instance.SelectedProfile)
            ?? chosen.Instance.Profiles[0];

        var connected = Profile.IsConnected && Profile.CurrentTarget.Endpoint == chosen.Registration.Endpoint &&
            Mo2InstanceCatalog.LocalPath(Profile.ProfilePath) == Mo2InstanceCatalog.LocalPath(profile.Directory);
        if (!connected && !await Profile.SelectProfile(chosen.Registration, profile)) return false;
        ShowProfile();
        if (openProfiles)
            await ProfileMenu.ProfilesItem.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)).ToTask();
        return true;
    }

    // Takes a game that is on disk but unmanaged and gives it an MO2 instance the
    // frontend owns, installing MO2 itself first if this is the first game added.
    // Registering it is what puts it in the spine, as NMA does the moment a game is
    // added, and navigation follows so the game is where the click left off.
    public async Task AddGame(string game)
    {
        if (_addingGame) return;
        _addingGame = true;
        void Report(string? text) => Avalonia.Threading.Dispatcher.UIThread.Post(() => {
            Notice = text; NoticeChanged?.Invoke();
        });
        try {
            await Mo2OwnedInstances.Install(Report);
            Report($"Creating the {game} instance…");
            var instance = await Task.Run(() => Mo2OwnedInstances.CreateInstance(game, Report));
            Catalog.Add(instance);
            // The instance carries its own MO2, so the frontend can start the host
            // itself when a profile is selected rather than asking for a launcher.
            var registration = Catalog.Read().Single(x => x.Registration.Directory == instance).Registration;
            Catalog.SetLauncher(registration, Mo2OwnedInstances.InstanceExecutable(game));
            RefreshCatalog();
            Report(null);
            await SelectGame(game, openProfiles: true);
        } catch (Exception error) {
            Report($"Could not add {game}: {error.Message}");
        } finally { _addingGame = false; }
    }
    // NMA hosts login and welcome as in-window overlays; this is that layer.
    internal Mo2OverlayHost? Overlays { get; private set; }
    private bool _loggingIn;
    public async Task<Mo2LoginResult?> LoginToNexus(bool logout = false)
    {
        // Two separate reasons used to return the same silent null.
        if (_loggingIn) { NexusAccountStatus.OnNext("Nexus sign-in is already open. Finish or cancel it first."); return null; }
        if (_window is null) return null;
        _loggingIn = true;
        try { return await Mo2NexusLoginPopup.Show(_window, this, logout: logout); }
        finally { _loggingIn = false; }
    }
    // Diagnostics need a window wide enough for the responsive columns to appear;
    // MO2_WINDOW_SIZE=WxH overrides the normal launch size.
    // Takes named entries out of the menus the top bar's buttons carry, and then
    // tidies the rules between them: the original markup writes a separator as a
    // MenuItem headed "-", so removing the entries around one leaves a rule at the
    // top of a menu, at the bottom of it, or two in a row.
    // The two buttons in the top bar that carry a menu. Named rather than searched
    // for: this runs while the window is being built, where nothing is in a visual
    // tree yet and a walk of one finds nothing at all.
    private static readonly string[] MenuHosts = ["HelpButton", "AvatarMenuItemButton"];

    private static void RemoveMenuEntries(Control topBar, params string[] names)
    {
        foreach (var flyout in MenuHosts
                     .Select(host => topBar.FindControl<NexusMods.App.UI.Controls.StandardButton>(host)?.Flyout)
                     .OfType<MenuFlyout>()) {
            foreach (var entry in flyout.Items.OfType<MenuItem>().Where(x => x.Name is { } name && names.Contains(name)).ToArray())
                flyout.Items.Remove(entry);
            bool Rule(object? item) => item is MenuItem { Header: "-" };
            while (flyout.Items.Count > 0 && Rule(flyout.Items[0])) flyout.Items.RemoveAt(0);
            while (flyout.Items.Count > 0 && Rule(flyout.Items[^1])) flyout.Items.RemoveAt(flyout.Items.Count - 1);
            for (var at = flyout.Items.Count - 1; at > 0; at--)
                if (Rule(flyout.Items[at]) && Rule(flyout.Items[at - 1])) flyout.Items.RemoveAt(at);
        }
    }

    private static (double Width, double Height) WindowSize()
    {
        var value = Environment.GetEnvironmentVariable("MO2_WINDOW_SIZE");
        if (value?.Split('x') is [var w, var h] && double.TryParse(w, out var width) && double.TryParse(h, out var height))
            return (width, height);
        return (1280, 800);
    }

    public Window CreateWindow()
    {
        // MO2's statusBar, along the bottom of the window.
        var status = new TextBlock { Name = "Mo2StatusBar", Text = Profile.Status, Margin = new Thickness(16, 8), TextWrapping = TextWrapping.Wrap };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,232,*"), RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        var topBar = new TopBarView { ViewModel = _topBar = new Mo2TopBar(this) };
        topBar.ViewModel.WhenAnyValue(x => x.ActiveWorkspaceSubtitle).Subscribe(text =>
            topBar.FindControl<TextBlock>("ActiveWorkspaceSubtitleTextBlock")!.IsVisible = !string.IsNullOrEmpty(text));
        // The top bar's grid is top-aligned inside a taller row, which left the
        // title and the toggle sitting above the window controls beside them.
        topBar.FindControl<Grid>("MainGrid")!.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        // The title and subtitle stretch to the header's height, so their glyphs sit
        // at the top of the box while the icons beside them are centred. Centring the
        // text blocks themselves puts the glyphs on the icons' line.
        foreach (var name in new[] { "ActiveWorkspaceTitleTextBlock", "ActiveWorkspaceSubtitleTextBlock" })
            topBar.FindControl<TextBlock>(name)!.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        var appLogsItem = topBar.FindControl<MenuItem>("ViewAppLogsMenuItem")!;
        appLogsItem.Header = "View MO2 logs";
        // Revealing MO2's own window was only ever offered from the Connections
        // page. That page is gone, so the toggle moves in beside the other MO2 host
        // actions rather than disappearing with it.
        var originalUiItem = new MenuItem { Name = "OriginalMo2UiMenuItem" };
        originalUiItem.Click += async (_, _) => {
            if (!Profile.CanChangeOriginalUi) return;
            await Profile.SetOriginalUiVisible(Profile.OriginalUiVisible != true);
        };
        void SyncOriginalUi() {
            originalUiItem.Header = Profile.OriginalUiVisible == true ? "Hide original MO2" : "Show original MO2";
            originalUiItem.IsEnabled = Profile.CanChangeOriginalUi;
        }
        Profile.Changed += SyncOriginalUi;
        SyncOriginalUi();
        object? menuOwner = appLogsItem.Parent;
        switch (menuOwner) {
            case MenuItem owner: {
                var items = owner.Items.Cast<object?>().ToList();
                owner.Items.Insert(Math.Min(items.IndexOf(appLogsItem) + 1, items.Count), originalUiItem);
                break;
            }
            case MenuFlyout flyout: {
                var items = flyout.Items.Cast<object?>().ToList();
                flyout.Items.Insert(Math.Min(items.IndexOf(appLogsItem) + 1, items.Count), originalUiItem);
                break;
            }
        }
        topBar.FindControl<MenuItem>("OpenNexusModsAccountSettingsMenuItem")!.Header = "Connect MO2 instances…";
        // The entries this frontend has nothing behind. They were bound to commands
        // that answer "no" for good, so they were drawn greyed in the two top-bar
        // menus on every run, for every user — a Nexus profile page, a Premium
        // upsell, Nexus forums, a changelog for an application this is not, and a
        // welcome message that does not exist. A greyed entry reads as "not now";
        // these mean "never", and the difference is the whole of what a menu is for.
        // Taken out rather than greyed, which is the rule MO2's own widgets are held
        // to: a control that draws and does nothing is the fault, not the remedy.
        RemoveMenuEntries(topBar, "ViewChangelogMenuItem", "OpenForumsMenuItem", "ShowWelcomeMessageMenuItem",
            "OpenNexusModsProfileMenuItem", "OpenGetPremiumMenuItem");
        var presetMenu = new MenuFlyout();
        var preset = new MenuItem { Header = "Use MO2 panel layout" };
        var undoPreset = new MenuItem { Header = "Undo panel layout" };
        presetMenu.Items.Add(preset); presetMenu.Items.Add(undoPreset);
        preset.Click += (_,_) => _layout.UseMo2Layout(WorkspaceController.ActiveWorkspace, WorkspaceController, _modsPage, _pluginsPage, _dataPage, _archivesPage, _savesPage, _downloadsPage);
        undoPreset.Click += (_,_) => _layout.UndoPreset(WorkspaceController.ActiveWorkspace, WorkspaceController);
        presetMenu.Opening += (_,_) => {
            preset.IsEnabled = WorkspaceController.ActiveWorkspace.Context is Mo2WorkspaceContext;
            undoPreset.IsEnabled = _layout.CanUndo(WorkspaceController.ActiveWorkspace);
        };
        var presetButton = new NexusMods.App.UI.Controls.StandardButton {
            Size = NexusMods.App.UI.Controls.StandardButton.Sizes.Medium, Fill = NexusMods.App.UI.Controls.StandardButton.Fills.None,
            Name = "Mo2LayoutPresetButton", ShowLabel = false, ShowIcon = NexusMods.App.UI.Controls.StandardButton.ShowIconOptions.Left,
            LeftIcon = new ProjektankerIcon("mdi-dock-right"), Flyout = presetMenu
        };
        ToolTip.SetTip(presetButton, "Panel layouts");
        var navigationButtons = (StackPanel)topBar.FindControl<NexusMods.App.UI.Controls.StandardButton>("NewTabButton")!.Parent!;
        navigationButtons.Children.Add(presetButton);
        var account = topBar.FindControl<NexusMods.App.UI.Controls.StandardButton>("LoginButton")!;
        account.Text = "Nexus account";
        ToolTip.SetTip(account, "Log into Nexus Mods for all registered MO2 instances.");
        // Vortex's header carries a user profile icon rather than NMA's placeholder
        // avatar art; it fills once an account is connected.
        var avatarIcon = topBar.FindControl<UnifiedIcon>("AvatarUnifiedIcon")!;
        avatarIcon.Size = 24;
        void SyncAvatar() => avatarIcon.Value = new ProjektankerIcon(
            _topBar?.IsLoggedIn == true ? "mdi-account-circle" : "mdi-account-circle-outline");
        SyncAvatar();
        var accountStatusSubscription = NexusAccountStatus.Subscribe(text => {
            ToolTip.SetTip(account, text);
            ToolTip.SetTip(topBar.FindControl<Control>("AvatarMenuItemButton")!, text);
            SyncAvatar();
        });
        Grid.SetColumn(topBar, 1); Grid.SetColumnSpan(topBar, 2); grid.Children.Add(topBar);
        var workspaceHost = new Mo2RetainedViews<IWorkspaceViewModel>(
            model => new WorkspaceView { ViewModel = model, Margin = new Thickness(12, 0),
                PanelViewFactory = panel => new Mo2DeferredPanel(panel) },
            model => model.Context is Mo2WorkspaceContext && WorkspaceController.AllWorkspaces.Contains(model) &&
                model.Panels.Count > 0 && model.Panels.All(panel => panel.Tabs.All(tab =>
                    tab.Contents.ViewModel is ScenarioInstalledPage or ScenarioLoadOrderPage)),
            control => ((WorkspaceView)control).ViewModel = null);
        workspaceHost.Activate(WorkspaceController.ActiveWorkspace);
        // Dividers are created and replaced whenever panels change, so the resistance
        // is attached from the canvas's layout rather than once at construction.
        workspaceHost.LayoutUpdated += (_, _) => Mo2PanelPhysics.Watch(workspaceHost);
        Grid.SetRow(workspaceHost, 1); Grid.SetColumn(workspaceHost, 2); grid.Children.Add(workspaceHost);
        // The badge strip is hidden for every game icon from the application styles,
        // so the spine no longer carries its own copy of that rule.
        var spine = new Spine { ViewModel = new Mo2Spine(this) };
        Grid.SetRowSpan(spine, 2); grid.Children.Add(spine);
        // Vortex fills the home glyph while home is showing and outlines it otherwise,
        // and marks the button active. IconButtonViewModel.IsActive is not reactive,
        // so the rendered button is driven from the active workspace directly.
        void SyncHomeButton() {
            var host = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(spine)
                .OfType<NexusMods.App.UI.Controls.Spine.Buttons.Icon.IconButton>().FirstOrDefault();
            if (host is null) return;
            var button = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(host).OfType<Button>().FirstOrDefault();
            if (button is null) return;
            var active = WorkspaceController.ActiveWorkspace.Context is HomeContext;
            button.Classes.Set("Active", active); button.Classes.Set("Inactive", !active);
            foreach (var icon in Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(button).OfType<UnifiedIcon>())
                icon.Value = new ProjektankerIcon(active ? "mdi-home" : "mdi-home-outline");
        }
        WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace)
            .Subscribe(_ => Dispatcher.UIThread.Post(SyncHomeButton, DispatcherPriority.Loaded));
        var profileSidebar = new NexusMods.App.UI.LeftMenu.Loadout.LoadoutLeftMenuView { ViewModel = ProfileMenu };
        // Separators are rows in My Mods, not separate collection pages.
        foreach (var name in new[] { "ApplyControlViewHost", "CollectionItems", "NewCollection" })
            profileSidebar.FindControl<Control>(name)!.IsVisible = false;
        var installed = (StackPanel)profileSidebar.FindControl<Control>("LoadoutItem")!.Parent!;
        var pluginsItem = profileSidebar.FindControl<Control>("ExternalChangesItem")!;
        ((Panel)pluginsItem.Parent!).Children.Remove(pluginsItem);
        installed.Children.Insert(1, pluginsItem);
        var overwriteItem = new NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView { ViewModel = ProfileMenu.OverwriteItem };
        installed.Children.Insert(2, overwriteItem);
        var externalFilesItem = new NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView { ViewModel = ProfileMenu.ExternalFilesItem };
        installed.Children.Add(externalFilesItem);
        var archivesItem = new NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView { ViewModel = ProfileMenu.ArchivesItem };
        installed.Children.Insert(2, archivesItem);
        var dataItem = new NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView { ViewModel = ProfileMenu.DataItem };
        installed.Children.Insert(3, dataItem);
        var savesItem = new NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView { ViewModel = ProfileMenu.SavesItem };
        installed.Children.Insert(4, savesItem);
        // Named after the MO2 window actions they answer for — actionTool and
        // actionViewLog — so MO2_VERIFY_QT_ACTIONS can find them.
        var toolsItem = new NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView { ViewModel = ProfileMenu.ToolsItem, Name = "Mo2ToolsMenuItem" };
        ((StackPanel)profileSidebar.FindControl<Control>("HealthCheckItem")!.Parent!).Children.Add(toolsItem);
        var logsItem = new NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView { ViewModel = ProfileMenu.LogsItem, Name = "Mo2LogsMenuItem" };
        ((StackPanel)profileSidebar.FindControl<Control>("HealthCheckItem")!.Parent!).Children.Add(logsItem);
        var profilesItem = new NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView { ViewModel = ProfileMenu.ProfilesItem };
        ((StackPanel)profileSidebar.FindControl<Control>("LibraryItem")!.Parent!).Children.Insert(0, profilesItem);
        var launchPanel = new Mo2LaunchPanel(Profile);
        // Held so a check can read MO2's run row wherever it currently lives: a
        // collapsed sidebar moves the whole panel into its tool flyout, so it is not
        // in the window's tree until that flyout is open.
        LaunchPanel = launchPanel;
        Grid.SetRow(launchPanel, 1);
        ((Grid)profileSidebar.Content!).Children.Add(launchPanel);
        var homeSidebar = new NexusMods.App.UI.LeftMenu.Home.HomeLeftMenuView { ViewModel = HomeMenu };
        if (HomeMenu.LeftMenuItemComponents is { } componentsItem)
            homeSidebar.FindControl<StackPanel>("LeftMenuStack")!.Children.Add(
                new NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView { ViewModel = componentsItem });
        var sidebar = new ContentControl { Name = "Mo2Sidebar", Content = homeSidebar };
        Grid.SetColumn(sidebar, 1); Grid.SetRow(sidebar, 1); grid.Children.Add(sidebar);
        WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace).Subscribe(workspace => {
            using var timing = Mo2UiLatencyProbe.Measure("Activate workspace view", always: true);
            using (Mo2UiLatencyProbe.Measure("Replace workspace canvas", always: true)) workspaceHost.Activate(workspace);
            using (Mo2UiLatencyProbe.Measure("Retarget sidebar controls", always: true)) {
                SyncMenu();
                sidebar.Content = workspace.Context is Mo2WorkspaceContext ? profileSidebar : homeSidebar;
            }
        });
        // Native dialogs can own the host without owning frontend navigation.
        // Keep panels, tabs and selection responsive; commands guard their targets.
        void SyncMenu() {
            using var timing = Mo2UiLatencyProbe.Measure("Sync profile sidebar");
            profileSidebar.ViewModel = ProfileMenu;
            profilesItem.ViewModel = ProfileMenu.ProfilesItem;
            toolsItem.ViewModel = ProfileMenu.ToolsItem;
            overwriteItem.ViewModel = ProfileMenu.OverwriteItem; externalFilesItem.ViewModel = ProfileMenu.ExternalFilesItem;
            logsItem.ViewModel = ProfileMenu.LogsItem; archivesItem.ViewModel = ProfileMenu.ArchivesItem; dataItem.ViewModel = ProfileMenu.DataItem; savesItem.ViewModel = ProfileMenu.SavesItem;
        }
        Profile.Changed += SyncMenu;
        Grid.SetRow(status, 2); Grid.SetColumnSpan(status, 3); grid.Children.Add(status);
        Overlays = new Mo2OverlayHost(grid);
        // NMA hides the system chrome by extending the client area rather than
        // switching decorations off, which keeps a native drag strip the height of
        // the top bar. SystemDecorations.None has no such strip, so the window
        // could not be moved by dragging its top bar at all.
        var window = new Window { Title = "Nexus Mods", Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://NexusModsApp/Assets/AppIcon.png"))),
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome,
            ExtendClientAreaToDecorationsHint = true,
            ExtendClientAreaTitleBarHeightHint = 44,
            Width = WindowSize().Width, Height = WindowSize().Height,
            Background = (IBrush)Application.Current!.FindResource("SurfaceBaseBrush")!, Content = grid };
        _window = window;
        Mo2TabDragDrop.Attach(window, WorkspaceController);
        Mo2CollapsibleSidebar.Attach(grid, sidebar, profileSidebar, homeSidebar, launchPanel, Profile,
            (Panel)topBar.FindControl<TextBlock>("ActiveWorkspaceTitleTextBlock")!.Parent!);
        var notifications = new Mo2Notifications(this);
        // Vortex keeps notifications at the foot of the spine beside downloads,
        // not in the header with the account controls.
        var downloadBorder = spine.FindControl<Border>("DownloadBorder")!;
        var spineFoot = (Panel)downloadBorder.Parent!;
        notifications.Button.Name = "SpineNotificationsButton";
        notifications.Button.Margin = new Thickness(0, 0, 0, 8);
        notifications.Button.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        spineFoot.Children.Insert(spineFoot.Children.IndexOf(downloadBorder), notifications.Button);
        window.Closed += (_,_) => notifications.Dispose();
        Mo2ResponsiveHeaders.Attach(window);
        void UpdateStatus() => status.Text = (Notice ?? Profile.Status) + (LayoutError is { } error ? " · " + error : "");
        Profile.Changed += UpdateStatus;
        NoticeChanged += UpdateStatus;
        UpdateStatus();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) => { await Profile.RefreshIfChanged(); RefreshCatalog(); SaveLayouts(); UpdateStatus(); };
        // The first read was started at construction; this only waits for whichever
        // of the two finishes last before the periodic poll takes over.
        window.Opened += async (_, _) => { await _firstRead; await Profile.Refresh(); timer.Start(); };
        window.Closing += (_, _) => SaveLayouts();
        window.Closed += (_, _) => { workspaceHost.ClearRetained(); timer.Stop(); accountStatusSubscription.Dispose(); ((Mo2TopBar)topBar.ViewModel!).Dispose(); SaveLayouts(); };
        return window;
    }
    public void Dispose() { _disposed = true; Profile.Changed -= RefreshCatalog; _desktop.Dispose(); Task.Run(async () => await _database.DisposeAsync()).GetAwaiter().GetResult(); }
}

internal sealed record Mo2WorkspaceContext(string Endpoint = "", string ProfilePath = "") : IWorkspaceContext
{
    public bool IsValid(IServiceProvider services) => true;
}
