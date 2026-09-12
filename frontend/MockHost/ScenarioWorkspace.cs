using System.Collections.Frozen;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reactive;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusMods.App.UI;
using NexusMods.App.UI.Dialog;
using NexusMods.App.UI.Dialog.Enums;
using NexusMods.App.UI.LeftMenu;
using NexusMods.App.UI.Pages.MyGames;
using NexusMods.App.UI.Pages.MyLoadouts;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceAttachments;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.Sdk.Settings;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;
using R3;

namespace Mo2.Frontend;

internal sealed class ScenarioWorkspace : IWorkspaceWindow
{
    public WindowId WindowId { get; } = WindowId.NewId();
    public bool IsActive => true;
    public IWorkspaceController WorkspaceController { get; }
    public ReactiveUI.ReactiveCommand<System.Reactive.Unit, bool> BringWindowToFront { get; } = ReactiveUI.ReactiveCommand.Create(() => true);
    public MemorySettings Settings { get; }
    public PageData SettingsPage { get; }
    public PageData PluginOrderPage { get; }
    public ScenarioPluginOrder PluginOrder { get; } = new();
    public ScenarioData Data { get; }
    public ScenarioHomeMenu HomeMenu { get; }
    public ScenarioWorkspace()
    {
        var services = new FixtureServices();
        var windows = new FixtureWindows { ActiveWindow = this };
        services.Add<IWindowManager>(windows);
        Settings = new MemorySettings(services);
        services.Add<ISettingsManager>(Settings);
        services.Add<ILoggerFactory>(NullLoggerFactory.Instance);
        services.Add<IWorkspaceAttachmentsFactoryManager>(new FixtureAttachments());
        PageData? loadoutsData = null;
        Data = new ScenarioData(() => {
            var controller = WorkspaceController!;
            var panel = controller.ActiveWorkspace.SelectedPanel;
            controller.OpenPage(controller.ActiveWorkspaceId, loadoutsData!, new OpenPageBehavior.ReplaceTab(panel.Id, panel.SelectedTab.Id));
        });
        var games = new FixturePageFactory("5f4a4e38-3b08-40d9-9ab3-d3a2a5f30001", "My Games", IconValues.GamepadOutline,
            () => new ScenarioGamesPage(windows, Data));
        var loadouts = new FixturePageFactory("5f4a4e38-3b08-40d9-9ab3-d3a2a5f30002", "My Loadouts", IconValues.Package,
            () => new ScenarioLoadoutsPage(windows, Data));
        loadoutsData = loadouts.Data;
        var settingsPage = ScenarioSettings.Register(services, Settings, windows);
        SettingsPage = settingsPage.Data;
        services.Add<NexusMods.Sdk.IOSInterop>(new ScenarioOSInterop());
        services.Add<IEnumerable<NexusMods.App.UI.Pages.Sorting.ILoadOrderDataProvider>>([new ScenarioOrderProvider()]);
        var orderPage = new FixturePageFactory("5f4a4e38-3b08-40d9-9ab3-d3a2a5f30004", "Plugin load order", IconValues.Package,
            () => new ScenarioLoadOrderPage(services, PluginOrder));
        PluginOrderPage = orderPage.Data;
        services.Add(new PageFactoryController([games, loadouts, settingsPage, orderPage, new NewTabPageFactory(services)]));
        // The controller is internal upstream. Instantiate its public constructor without forking
        // its implementation so panel geometry, tab navigation, drag/drop and history stay original.
        var controllerType = typeof(WorkspaceViewModel).Assembly.GetType("NexusMods.App.UI.WorkspaceSystem.WorkspaceController", throwOnError: true)!;
        WorkspaceController = (IWorkspaceController)Activator.CreateInstance(controllerType, this, services)!;
        var workspace = WorkspaceController.CreateWorkspace(new HomeContext(), games.Data);
        WorkspaceController.ChangeActiveWorkspace(workspace.Id);
        HomeMenu = new ScenarioHomeMenu(WorkspaceController, games.Data, loadouts.Data);
    }
}

internal sealed class FixtureServices : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = new();
    public void Add<T>(T service) where T : notnull => _services[typeof(T)] = service;
    private readonly Dictionary<Type, Func<object>> _factories = new();
    public void AddFactory<T>(Func<T> factory) where T : notnull => _factories[typeof(T)] = () => factory();
    public object? GetService(Type serviceType) => _factories.TryGetValue(serviceType, out var factory) ? factory() : _services.GetValueOrDefault(serviceType);
}

internal sealed record FixturePageContext(PageFactoryId FactoryId) : IPageFactoryContext;
internal sealed class FixturePageFactory(string id, string title, IconValue icon, Func<IPageViewModelInterface> create) : IPageFactory
{
    public PageFactoryId Id { get; } = PageFactoryId.From(Guid.Parse(id));
    public PageData Data => new() { FactoryId = Id, Context = new FixturePageContext(Id) };
    public DynamicData.Kernel.Optional<OpenPageBehaviorType> DefaultOpenPageBehavior => default;
    public Page Create(IPageFactoryContext context)
    {
        var vm = create();
        return new Page { ViewModel = vm, PageData = Data };
    }
    public IEnumerable<PageDiscoveryDetails?> GetDiscoveryDetails(IWorkspaceContext workspaceContext)
    {
        yield return new PageDiscoveryDetails { SectionName = "General", ItemName = title, Icon = icon, PageData = Data };
    }
}

internal sealed class FixtureAttachments : IWorkspaceAttachmentsFactoryManager
{
    public ILeftMenuViewModel? CreateLeftMenuFor(IWorkspaceContext context, WorkspaceId workspaceId, IWorkspaceController workspaceController) => null;
    public string CreateTitleFor(IWorkspaceContext context) => "Home";
    public string CreateSubtitleFor(IWorkspaceContext context) => "";
}

internal sealed class FixtureWindows : IWindowManager
{
    public IWorkspaceWindow ActiveWindow { get; set; } = null!;
    public ReadOnlyObservableCollection<WindowId> AllWindowIds => new(new ObservableCollection<WindowId> { ActiveWindow.WindowId });
    public bool TryGetWindow(WindowId id, [NotNullWhen(true)] out IWorkspaceWindow? window)
    { window = id == ActiveWindow.WindowId ? ActiveWindow : null; return window is not null; }
    public void RegisterWindow(IWorkspaceWindow window) => ActiveWindow = window;
    public void UnregisterWindow(IWorkspaceWindow window) { }
    public void SaveWindowState(IWorkspaceWindow window) { }
    public bool RestoreWindowState(IWorkspaceWindow window) => false;
    public Task<StandardDialogResult> ShowDialog(IDialog dialog, DialogWindowType windowType) => throw new NotSupportedException("Scenario dialogs are not wired yet.");
}

internal sealed class MemorySettings(IServiceProvider services) : ISettingsManager
{
    private readonly Dictionary<(Type, string?), object> _values = new();
    private readonly Dictionary<(Type, string?), object> _changes = new();
    public FrozenDictionary<Type, SettingsConfig> Configs { get; private set; } = FrozenDictionary<Type, SettingsConfig>.Empty;
    public void Register<T>() where T : class, ISettings, new()
    {
        var builder = new NexusMods.Backend.SettingsBuilder();
        T.Configure(builder);
        var config = builder.ToConfig(new SettingsRegistration(typeof(T), new T(), T.Configure));
        Configs = Configs.Values.Append(config).ToFrozenDictionary(x => x.Type);
    }
    public T GetDefault<T>() where T : class, ISettings, new() => Configs.TryGetValue(typeof(T), out var config) ? (T)config.DefaultValueFactory(services) : new();
    public T Get<T>(string? key = null) where T : class, ISettings, new() => TryGet<T>(out var value, key) ? value : GetDefault<T>();
    public bool TryGet<T>([NotNullWhen(true)] out T? value, string? key = null) where T : class, ISettings, new()
    { value = _values.GetValueOrDefault((typeof(T), key)) as T; return value is not null; }
    public void Set<T>(T value, string? key = null) where T : class, ISettings, new()
    { _values[(typeof(T), key)] = value; if (_changes.TryGetValue((typeof(T), key), out var changes)) ((R3.Subject<T>)changes).OnNext(value); }
    public T Update<T>(Func<T, T> updater, string? key = null) where T : class, ISettings, new()
    { var value = updater(Get<T>(key)); Set(value, key); return value; }
    public R3.Observable<T> GetChanges<T>(string? key = null, bool prependCurrent = false) where T : class, ISettings, new()
    {
        var lookup = (typeof(T), key);
        if (!_changes.TryGetValue(lookup, out var changes)) _changes[lookup] = changes = new R3.Subject<T>();
        var observable = (R3.Subject<T>)changes;
        return prependCurrent ? observable.Prepend(Get<T>(key)) : observable;
    }
}
