using Avalonia.VisualTree;
using NexusMods.App.UI.Pages.LoadoutPage;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages.Sorting;
using System.Reactive.Linq;
using NexusMods.App.UI.Controls.Settings.SettingEntries;
using NexusMods.App.UI.Pages.Settings;
using NexusMods.App.UI.Settings;
using NexusMods.App.UI.Controls.Navigation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using NexusMods.App.UI;
using NexusMods.App.UI.Controls.Spine;
using NexusMods.App.UI.Controls.DevelopmentBuildBanner;
using NexusMods.App.UI.Controls.TopBar;
using NexusMods.App.UI.LeftMenu.Home;
using NexusMods.App.UI.Pages.MyGames;
using NexusMods.App.UI.Pages.MyLoadouts;
using NexusMods.UI.Sdk;
using NexusMods.App.UI.WorkspaceSystem;
using IconProvider = Projektanker.Icons.Avalonia.IconProvider;
using Projektanker.Icons.Avalonia.MaterialDesign;
using ReactiveUI;
using Splat;

namespace Mo2.Frontend;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        IconProvider.Current.Register<MaterialDesignIconProvider>();
        AppBuilder.Configure<MockApp>().UsePlatformDetect()
            .With(new X11PlatformOptions { UseDBusMenu = false })
            .With(new SkiaOptions { UseOpacitySaveLayer = true })
            .UseReactiveUI().LogToTrace().StartWithClassicDesktopLifetime(args);
    }
}

// Resolve the same interface-based views as upstream without constructing its backend services.
internal sealed class FixtureViewLocator : IViewLocator
{
    public IViewFor? ResolveView<T>(T? viewModel, string? contract = null)
    {
        if (viewModel is not IViewModel vm) return null;
        var viewType = typeof(MyGamesView).Assembly.GetTypes().FirstOrDefault(type =>
            !type.IsAbstract && typeof(IViewFor<>).MakeGenericType(vm.ViewModelInterface).IsAssignableFrom(type)
            && type.GetConstructor(Type.EmptyTypes) is not null);
        if (viewType is null) throw new InvalidOperationException($"No upstream view for {vm.ViewModelInterface}");
        var view = (IViewFor)Activator.CreateInstance(viewType)!;
        if (view is IViewContract vc && contract is not null) vc.ViewContract = contract;
        return view;
    }
}

public partial class MockApp : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Locator.CurrentMutable.UnregisterCurrent(typeof(IViewLocator));
        Locator.CurrentMutable.RegisterConstant<IViewLocator>(new FixtureViewLocator());
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("72,232,*"),
                RowDefinitions = new RowDefinitions("Auto,*,48") };
            var scenario = new ScenarioWorkspace();
            desktop.Exit += (_, _) => scenario.Dispose();
            Add(grid, new Spine { ViewModel = new ScenarioSpine(scenario) }, 0, 0, rowSpan: 2);
            var topbar = new ScenarioTopBar(scenario);
            Add(grid, new TopBarView { ViewModel = topbar }, 1, 0, columnSpan: 2);
            var sidebar = new ViewModelViewHost { ViewModel = scenario.HomeMenu };
            Add(grid, sidebar, 1, 1);
            var workspaceView = new WorkspaceView { ViewModel = scenario.WorkspaceController.ActiveWorkspace,
                Margin = new Thickness(0, 0, 12, 12) };
            Add(grid, workspaceView, 2, 1);
            scenario.WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace).Subscribe(workspace => {
                workspaceView.ViewModel = workspace;
                sidebar.ViewModel = scenario.GetActiveMenu();
            });
            scenario.MenuChanged += () => sidebar.ViewModel = scenario.GetActiveMenu();
            var window = new Window { Title = "Mod Organizer — Nexus frontend scenarios", Width = 1440, Height = 900,
                Background = (IBrush)this.FindResource("SurfaceBaseBrush")!, Content = grid };
            Add(grid, new DevelopmentBuildBannerView { ViewModel = new DevelopmentBuildBannerDesignViewModel() }, 0, 2, columnSpan: 3);
            desktop.MainWindow = window;
            Task verification = Task.CompletedTask;
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_WORKSPACE") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => VerifyWorkspace(scenario), TimeSpan.FromSeconds(1));
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_SCENARIOS") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => { verification = VerifyScenarios(scenario); }, TimeSpan.FromSeconds(1));
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_SETTINGS") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => { verification = VerifySettings(scenario, topbar); }, TimeSpan.FromSeconds(1));
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_ORDER") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => { verification = VerifyOrder(scenario); }, TimeSpan.FromSeconds(1));
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_INSTALLED") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => { verification = VerifyInstalled(scenario, window); }, TimeSpan.FromSeconds(1));
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_CONTEXTS") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => { verification = VerifyContexts(scenario, window); }, TimeSpan.FromSeconds(1));
            var screenshot = Environment.GetEnvironmentVariable("MO2_SCREENSHOT");
            if (screenshot is not null)
                window.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                    await verification;
                    await Task.Delay(150);
                    using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
                    bitmap.Render(window);
                    bitmap.Save(screenshot);
                    desktop.Shutdown();
                }, TimeSpan.FromSeconds(3));
        }
        base.OnFrameworkInitializationCompleted();
    }

    private static async Task VerifyContexts(ScenarioWorkspace scenario, Window window)
    {
        var controller = scenario.WorkspaceController;
        var home = controller.ActiveWorkspace;
        home.SelectedPanel.AddDefaultTab();
        var homeTab = home.SelectedTab.Id;
        await scenario.Data.Game.AddGameCommand.Execute();
        var first = scenario.Data.Section.Loadouts.Single();
        await first.VisitLoadoutCommand.Execute();
        var firstWorkspace = controller.ActiveWorkspace;
        controller.AddPanel(firstWorkspace.Id, firstWorkspace.AddPanelButtonViewModels.First().NewLayoutState,
            new AddPanelBehavior(new AddPanelBehavior.WithDefaultTab()));
        firstWorkspace.SelectedPanel.AddDefaultTab();
        var firstTab = firstWorkspace.SelectedTab.Id;
        var firstPanels = firstWorkspace.Panels.Select(x => x.Id).ToArray();
        await first.CloneLoadoutCommand.Execute();
        var second = scenario.Data.Section.Loadouts.Last();
        await second.VisitLoadoutCommand.Execute();
        var secondWorkspace = controller.ActiveWorkspace;
        if (secondWorkspace.Id == firstWorkspace.Id || secondWorkspace.Panels.Count != 1)
            throw new InvalidOperationException("Loadout workspace was shared");
        scenario.GoHome();
        if (controller.ActiveWorkspace.Id != home.Id || home.SelectedTab.Id != homeTab)
            throw new InvalidOperationException("Home tab state was lost");
        await first.VisitLoadoutCommand.Execute();
        await Task.Delay(100);
        if (controller.ActiveWorkspace.Id != firstWorkspace.Id || firstWorkspace.SelectedTab.Id != firstTab ||
            !firstWorkspace.Panels.Select(x => x.Id).SequenceEqual(firstPanels))
            throw new InvalidOperationException("Loadout panel or tab state was lost");
        if (!window.GetVisualDescendants().OfType<NexusMods.App.UI.LeftMenu.Loadout.LoadoutLeftMenuView>().Any())
            throw new InvalidOperationException("Loadout sidebar did not render");
        await first.DeleteLoadoutCommand.Execute();
        if (controller.AllWorkspaces.Any(x => x.Id == firstWorkspace.Id) || controller.ActiveWorkspace.Id != home.Id)
            throw new InvalidOperationException("Deleted loadout left its workspace active");
        await second.VisitLoadoutCommand.Execute();
        await Task.Delay(100);
        var menu = (ScenarioLoadoutMenu)scenario.GetActiveMenu();
        await menu.LeftMenuCollectionItems.Single().NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
        await Task.Delay(100);
        if (controller.ActiveWorkspace.SelectedTab.Contents.ViewModel is not ScenarioInstalledPage { IsCollection: true } collection || collection.ItemCount.Value != 4)
            throw new InvalidOperationException("My Mods sidebar navigation failed");
        Console.WriteLine("PASS: independent Home/loadout workspaces; panel/tab restoration; native loadout sidebar; deleted workspace cleanup");
    }

    private static async Task VerifyInstalled(ScenarioWorkspace scenario, Window window)
    {
        var controller = scenario.WorkspaceController;
        var panel = controller.ActiveWorkspace.SelectedPanel;
        controller.OpenPage(controller.ActiveWorkspaceId, scenario.InstalledPage,
            new OpenPageBehavior.ReplaceTab(panel.Id, panel.SelectedTab.Id));
        await Task.Delay(200);
        var page = (ScenarioInstalledPage)controller.ActiveWorkspace.SelectedTab.Contents.ViewModel;
        if (page.ItemCount.Value != 4 || page.Adapter.SourceCount.Value != 4)
            throw new InvalidOperationException("Installed mod rows missing");
        var row = page.Adapter.Source.Value.Items.First();
        row.Get<LoadoutComponents.EnabledStateToggle>(LoadoutColumns.EnabledState.EnabledStateToggleComponentKey).CommandToggle.Execute(R3.Unit.Default);
        await Task.Delay(100);
        if (scenario.InstalledMods.Mods.Single(x => x.Id == row.Key).Enabled)
            throw new InvalidOperationException("Native enabled toggle failed");
        if (scenario.PluginOrder.Plugins.Single(x => x.DisplayName == scenario.InstalledMods.Mods.Single(m => m.Id == row.Key).Plugin).IsActive)
            throw new InvalidOperationException("Disabled mod still has active plugin");
        var view = window.GetVisualDescendants().OfType<LoadoutView>().Single();
        var tabs = view.FindControl<TabControl>("RulesTabControl")!;
        tabs.SelectedIndex = 1;
        await Task.Delay(150);
        if (!view.GetVisualDescendants().OfType<LoadOrderView>().Any())
            throw new InvalidOperationException("Rules subtab did not render original editor");
        tabs.SelectedIndex = 0;
        await Task.Delay(100);
        page.Adapter.SelectAll();
        if (page.SelectionCount.Value != 4) throw new InvalidOperationException("Mod multi-selection failed");
        page.CommandRemoveItem.Execute(R3.Unit.Default);
        await Task.Delay(100);
        if (page.ItemCount.Value != 0 || !page.Adapter.IsSourceEmpty.Value) throw new InvalidOperationException("Uninstall did not show empty state");
        await scenario.Data.Game.AddGameCommand.Execute();
        var card = scenario.Data.Section.Loadouts.Single();
        await card.VisitLoadoutCommand.Execute();
        await Task.Delay(100);
        var loadout = (ScenarioInstalledPage)controller.ActiveWorkspace.SelectedTab.Contents.ViewModel;
        if (loadout.ItemCount.Value != 4) throw new InvalidOperationException("Loadout visit did not use independent fixture data");
        var mod = card.InstalledMods.Mods.First();
        card.InstalledMods.Toggle([NexusMods.Abstractions.Loadouts.LoadoutItemId.From(mod.Id)]);
        await card.CloneLoadoutCommand.Execute();
        var clone = scenario.Data.Section.Loadouts.Last();
        if (clone.InstalledMods.Mods.Single(x => x.Id == mod.Id).Enabled) throw new InvalidOperationException("Clone lost enabled state");
        clone.InstalledMods.Toggle([NexusMods.Abstractions.Loadouts.LoadoutItemId.From(mod.Id)]);
        if (card.InstalledMods.Mods.Single(x => x.Id == mod.Id).Enabled) throw new InvalidOperationException("Clone mutated source loadout");
        var finalView = window.GetVisualDescendants().OfType<LoadoutView>().Single();
        finalView.FindControl<TabControl>("RulesTabControl")!.SelectedIndex = Environment.GetEnvironmentVariable("MO2_INSTALLED_CAPTURE_MODS") == "1" ? 0 : 1;
        Console.WriteLine("PASS: original Mods/Rules tabs; enable toggle updates plugin; multi-select/uninstall/empty state; card visit; independent cloned loadout data");
    }

    private static async Task VerifyOrder(ScenarioWorkspace scenario)
    {
        var controller = scenario.WorkspaceController;
        var panel = controller.ActiveWorkspace.SelectedPanel;
        controller.OpenPage(controller.ActiveWorkspaceId, scenario.PluginOrderPage,
            new OpenPageBehavior.ReplaceTab(panel.Id, panel.SelectedTab.Id));
        await Task.Delay(200);
        var page = (ScenarioLoadOrderPage)controller.ActiveWorkspace.SelectedTab.Contents.ViewModel;
        var order = scenario.PluginOrder;
        var lighting = order.Plugins.Single(x => x.DisplayName == "Desert Lighting.esp");
        var row = page.Adapter.Source.Value.Items.Single(x => x.Key.Equals(lighting.Key));
        var index = row.Get<SharedComponents.IndexComponent>(LoadOrderColumns.IndexColumn.IndexComponentKey);
        index.MoveDown.Execute(R3.Unit.Default);
        await Task.Delay(150);
        row = page.Adapter.Source.Value.Items.Single(x => x.Key.Equals(lighting.Key));
        index = row.Get<SharedComponents.IndexComponent>(LoadOrderColumns.IndexColumn.IndexComponentKey);
        if (index.SortIndex.Value != 4) throw new InvalidOperationException("Displayed plugin index did not update");
        if (!page.Adapter.Source.Value.Items.Select(x => x).ToArray()[4].Key.Equals(lighting.Key))
            throw new InvalidOperationException("Displayed rows did not reorder");
        if (lighting.SortIndex != 4) throw new InvalidOperationException("Plugin move failed");
        index.MoveDown.Execute(R3.Unit.Default);
        await Task.Delay(100);
        if (lighting.SortIndex != 4) throw new InvalidOperationException("Dependent plugin moved ahead of master");
        await page.SwitchSortDirectionCommand.Execute();
        if (page.IsAscending) throw new InvalidOperationException("Sort direction failed");
        await page.SwitchSortDirectionCommand.Execute();
        if (!page.IsAscending) throw new InvalidOperationException("Ascending restoration failed");
        Console.WriteLine("PASS: original load-order page activation; native row command; displayed row/index updates; master constraint; sort direction toggle");
    }

    private static async Task VerifySettings(ScenarioWorkspace scenario, ScenarioTopBar topbar)
    {
        await topbar.OpenSettingsCommand.Execute(NavigationInformation.From(NavigationInput.Default));
        await Task.Delay(150); // allow the actual settings view and nested controls to activate
        var workspace = scenario.WorkspaceController.ActiveWorkspace;
        if (workspace.SelectedTab.Contents.ViewModel is not SettingsPageViewModel settings)
            throw new InvalidOperationException("Settings navigation failed");
        var entry = settings.SettingEntries.Single(entry => entry.Config.Options.DisplayName == "Bring app window to front");
        var toggle = (SettingToggleViewModel)entry.InteractionControlViewModel;
        var original = scenario.Settings.Get<BehaviorSettings>().BringWindowToFront;
        toggle.BooleanContainer.CurrentValue = !original;
        if (!settings.HasAnyValueChanged.Value || scenario.Settings.Get<BehaviorSettings>().BringWindowToFront != original)
            throw new InvalidOperationException("Settings draft leaked into saved state");
        settings.CancelCommand.Execute(R3.Unit.Default);
        if (toggle.BooleanContainer.CurrentValue != original || settings.HasAnyValueChanged.Value)
            throw new InvalidOperationException("Discard failed");
        toggle.BooleanContainer.CurrentValue = !original;
        settings.SaveCommand.Execute(R3.Unit.Default);
        if (scenario.Settings.Get<BehaviorSettings>().BringWindowToFront == original || settings.HasAnyValueChanged.Value)
            throw new InvalidOperationException("Settings save failed");
        await topbar.OpenSettingsCommand.Execute(NavigationInformation.From(OpenPageBehaviorType.NewTab));
        await Task.Delay(100);
        var second = (SettingsPageViewModel)workspace.SelectedTab.Contents.ViewModel;
        var secondToggle = (SettingToggleViewModel)second.SettingEntries.Single(entry => entry.Config.Options.DisplayName == "Bring app window to front").InteractionControlViewModel;
        if (secondToggle.BooleanContainer.CurrentValue == original) throw new InvalidOperationException("Reopened settings lost saved values");
        Console.WriteLine($"PASS: Settings toolbar navigation; {settings.SettingEntries.Count} upstream settings; edit/discard/save; values survive reopening in another tab");
    }

    private static async Task VerifyScenarios(ScenarioWorkspace scenario)
    {
        var data = scenario.Data;
        await data.Game.AddGameCommand.Execute();
        if (data.Section.Loadouts.Count != 1 || data.Sections.Count != 1) throw new InvalidOperationException("Add game did not create its loadout");
        var first = data.Section.Loadouts.Single();
        await first.CloneLoadoutCommand.Execute();
        if (data.Section.Loadouts.Count != 2 || first.IsLastLoadout) throw new InvalidOperationException("Clone loadout failed");
        await data.Section.Loadouts.Last().DeleteLoadoutCommand.Execute();
        if (data.Section.Loadouts.Count != 1 || !first.IsLastLoadout) throw new InvalidOperationException("Delete loadout failed");
        await data.Game.RemoveAllLoadoutsCommand.Execute();
        if (data.Sections.Count != 0 || data.Section.Loadouts.Count != 0) throw new InvalidOperationException("Remove game failed");
        await data.Game.AddGameCommand.Execute();
        await data.Section.Loadouts.Single().CloneLoadoutCommand.Execute();
        await scenario.HomeMenu.LeftMenuItemMyLoadouts.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
        var workspace = scenario.WorkspaceController.ActiveWorkspace;
        if (workspace.SelectedTab.Contents.ViewModel is not ScenarioLoadoutsPage page || page.GameSectionViewModels.Count != 1)
            throw new InvalidOperationException("Sidebar loadout navigation failed");
        await workspace.SelectedTab.GoBackInHistoryCommand.Execute();
        if (workspace.SelectedTab.Contents.ViewModel is not ScenarioGamesPage) throw new InvalidOperationException("History back failed");
        await workspace.SelectedTab.GoForwardInHistoryCommand.Execute();
        if (workspace.SelectedTab.Contents.ViewModel is not ScenarioLoadoutsPage) throw new InvalidOperationException("History forward failed");
        Console.WriteLine("PASS: fake game add/remove; loadout create/clone/delete; sidebar navigation; back/forward history");
    }

    private static void VerifyWorkspace(ScenarioWorkspace scenario)
    {
        var controller = scenario.WorkspaceController;
        var workspace = controller.ActiveWorkspace;
        var first = workspace.Panels.Single();
        var original = first.SelectedTab.Id;
        first.AddDefaultTab();
        if (first.Tabs.Count != 2) throw new InvalidOperationException("Add tab failed");
        first.SelectTab(original);
        if (first.SelectedTab.Id != original) throw new InvalidOperationException("Select tab failed");
        first.CloseTab(first.Tabs.Single(t => t.Id != original).Id);
        if (first.Tabs.Count != 1) throw new InvalidOperationException("Close tab failed");
        for (var count = 2; count <= 4; count++)
        {
            var layout = workspace.AddPanelButtonViewModels.First().NewLayoutState;
            controller.AddPanel(workspace.Id, layout, new AddPanelBehavior(new AddPanelBehavior.WithDefaultTab()));
            if (workspace.Panels.Count != count) throw new InvalidOperationException($"Expected {count} panels");
        }
        controller.ClosePanel(workspace.Id, workspace.Panels.Last().Id);
        if (workspace.Panels.Count != 3) throw new InvalidOperationException("Close panel failed");
        controller.AddPanel(workspace.Id, workspace.AddPanelButtonViewModels.First().NewLayoutState,
            new AddPanelBehavior(new AddPanelBehavior.WithDefaultTab()));
        Console.WriteLine("PASS: add/select/close tabs; split to 2/3/4 panels; close and restore panel");
    }

    private static void Add(Grid grid, Control control, int column, int row, int columnSpan = 1, int rowSpan = 1)
    {
        Grid.SetColumn(control, column); Grid.SetRow(control, row);
        Grid.SetColumnSpan(control, columnSpan); Grid.SetRowSpan(control, rowSpan);
        grid.Children.Add(control);
    }
}
