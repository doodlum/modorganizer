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
using Projektanker.Icons.Avalonia;
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
            Add(grid, new Spine { ViewModel = new ScenarioSpine(scenario) }, 0, 0, rowSpan: 2);
            var topbar = new ScenarioTopBar(scenario);
            Add(grid, new TopBarView { ViewModel = topbar }, 1, 0, columnSpan: 2);
            Add(grid, new HomeLeftMenuView { ViewModel = scenario.HomeMenu }, 1, 1);
            Add(grid, new WorkspaceView { ViewModel = scenario.WorkspaceController.ActiveWorkspace,
                Margin = new Thickness(0, 0, 12, 12) }, 2, 1);
            var window = new Window { Title = "Mod Organizer — Nexus frontend scenarios", Width = 1440, Height = 900,
                Background = (IBrush)this.FindResource("SurfaceBaseBrush")!, Content = grid };
            Add(grid, new DevelopmentBuildBannerView { ViewModel = new DevelopmentBuildBannerDesignViewModel() }, 0, 2, columnSpan: 3);
            desktop.MainWindow = window;
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_WORKSPACE") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => VerifyWorkspace(scenario), TimeSpan.FromSeconds(1));
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_SCENARIOS") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(async () => await VerifyScenarios(scenario), TimeSpan.FromSeconds(1));
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_SETTINGS") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(async () => await VerifySettings(scenario, topbar), TimeSpan.FromSeconds(1));
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_ORDER") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(async () => await VerifyOrder(scenario), TimeSpan.FromSeconds(1));
            var screenshot = Environment.GetEnvironmentVariable("MO2_SCREENSHOT");
            if (screenshot is not null)
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => {
                    using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
                    bitmap.Render(window);
                    bitmap.Save(screenshot);
                    desktop.Shutdown();
                }, TimeSpan.FromSeconds(3));
        }
        base.OnFrameworkInitializationCompleted();
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
