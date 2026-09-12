using System.Reactive;
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
    public WindowId WindowId { get; } = WindowId.NewId();
    public bool IsActive => true;
    public IWorkspaceController WorkspaceController { get; }
    public ReactiveCommand<Unit, bool> BringWindowToFront { get; } = ReactiveCommand.Create(() => true);
    private readonly PageData _downloadsPage;
    private readonly PageData _profilesPage;
    private readonly PageData _modsPage;
    public Mo2InstanceCatalog Catalog { get; }
    public Mo2LiveProfile Profile { get; }
    public ScenarioInstalledPage? ModsPage { get; private set; }
    public ScenarioLoadOrderPage? PluginsPage { get; private set; }
    public Mo2LiveWorkspace(string endpoint)
    {
        Profile = new(endpoint);
        Catalog = new(endpoint);
        var services = new FixtureServices();
        var windows = new FixtureWindows { ActiveWindow = this };
        services.Add<IWindowManager>(windows);
        services.Add<ISettingsManager>(new MemorySettings(services));
        services.Add<ILoggerFactory>(NullLoggerFactory.Instance);
        services.Add<IWorkspaceAttachmentsFactoryManager>(new FixtureAttachments());
        services.Add<NexusMods.Sdk.IOSInterop>(new ScenarioOSInterop());
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
            () => new Mo2ProfilesPage(windows, Catalog, Profile, ShowProfile));
        _profilesPage = profiles.Data;
        services.Add(new PageFactoryController([mods, plugins, downloads, profiles, new NewTabPageFactory(services)]));
        var controllerType = typeof(WorkspaceViewModel).Assembly.GetType("NexusMods.App.UI.WorkspaceSystem.WorkspaceController", true)!;
        WorkspaceController = (IWorkspaceController)Activator.CreateInstance(controllerType, this, services)!;
        var workspace = WorkspaceController.CreateWorkspace(new HomeContext(), mods.Data);
        WorkspaceController.ChangeActiveWorkspace(workspace.Id);
        WorkspaceController.OpenPage(workspace.Id, plugins.Data, new OpenPageBehavior.NewPanel(WorkspaceGridState.From(true, new PanelGridState(workspace.Panels.Single().Id, new Rect(0, 0, 0.5, 1)), new PanelGridState(PanelId.DefaultValue, new Rect(0.5, 0, 0.5, 1)))));
    }
    public void OpenDownloads() => WorkspaceController.OpenPage(WorkspaceController.ActiveWorkspaceId, _downloadsPage,
        new OpenPageBehavior.NewTab(WorkspaceController.ActiveWorkspace.Panels.OrderBy(x => x.LogicalBounds.X).First().Id));
    public void OpenProfiles() => WorkspaceController.OpenPage(WorkspaceController.ActiveWorkspaceId, _profilesPage,
        new OpenPageBehavior.NewTab(WorkspaceController.ActiveWorkspace.Panels.OrderBy(x => x.LogicalBounds.X).First().Id));
    public void ShowProfile() => WorkspaceController.OpenPage(WorkspaceController.ActiveWorkspaceId, _modsPage,
        new OpenPageBehavior.NewTab(WorkspaceController.ActiveWorkspace.Panels.OrderBy(x => x.LogicalBounds.X).First().Id));
    public Window CreateWindow()
    {
        var title = new TextBlock { Text = Profile.CollectionName.Value, FontSize = 20, Margin = new Thickness(16, 12) };
        var status = new TextBlock { Text = Profile.Status, Margin = new Thickness(16, 8), TextWrapping = TextWrapping.Wrap };
        var refresh = new Button { Content = "Refresh from MO2", Margin = new Thickness(12) };
        refresh.Click += async (_, _) => await Profile.Refresh();
        var downloads = new Button { Content = "Downloads", Margin = new Thickness(12) };
        downloads.Click += (_, _) => OpenDownloads();
        var profiles = new Button { Content = "My Loadouts", Margin = new Thickness(12) };
        profiles.Click += (_, _) => OpenProfiles();
        var header = new DockPanel();
        DockPanel.SetDock(profiles, Dock.Right); header.Children.Add(profiles);
        DockPanel.SetDock(downloads, Dock.Right); header.Children.Add(downloads);
        DockPanel.SetDock(refresh, Dock.Right); header.Children.Add(refresh); header.Children.Add(title);
        var grid = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
        grid.Children.Add(header);
        var view = new WorkspaceView { ViewModel = WorkspaceController.ActiveWorkspace, Margin = new Thickness(12, 0) };
        Grid.SetRow(view, 1); grid.Children.Add(view);
        Grid.SetRow(status, 2); grid.Children.Add(status);
        var window = new Window { Title = "Mod Organizer — Live MO2 profile", Width = 1440, Height = 900,
            Background = (IBrush)Application.Current!.FindResource("SurfaceBaseBrush")!, Content = grid };
        Profile.Changed += () => { title.Text = Profile.CollectionName.Value; status.Text = Profile.Status; };
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) => await Profile.Refresh();
        window.Opened += async (_, _) => { await Profile.Refresh(); timer.Start(); };
        window.Closed += (_, _) => timer.Stop();
        return window;
    }
    public void Dispose() => Task.Run(async () => await _database.DisposeAsync()).GetAwaiter().GetResult();
}
