using NexusMods.App.UI.Dialog;
using NexusMods.UI.Sdk.Dialog;
using NexusMods.App.UI.Pages.LibraryPage;
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
        if (args.FirstOrDefault() == "--register-mo2") {
            try {
                if (args.Length is < 2 or > 3) throw new ArgumentException("Expected MO2 instance directory and optional launcher path");
                var directory = Path.GetFullPath(args[1]);
                var launcher = args.Length == 3 ? Path.GetFullPath(args[2]) : null;
                if (launcher is not null && !File.Exists(launcher)) throw new FileNotFoundException("MO2 launcher not found");
                var catalog = new Mo2InstanceCatalog("");
                catalog.Add(directory);
                if (launcher is not null) catalog.SetLauncher(catalog.Read().Single(x => x.Registration.Directory == directory).Registration, launcher);
                Console.WriteLine("Registered MO2 instance: " + directory);
            } catch (Exception error) { Console.Error.WriteLine(error.Message); Environment.ExitCode = 1; }
            return;
        }
        if (args.FirstOrDefault() == "--mo2-import-nexus-key") {
            try {
                if (args.Length != 3) throw new ArgumentException("Expected MO2 bridge directory and key file path");
                var path = Path.GetFullPath(args[2]);
                if (!File.Exists(path)) throw new FileNotFoundException("Nexus key file not found");
                var hostPath = OperatingSystem.IsWindows() ? path : "Z:" + path;
                var result = new Mo2BridgeClient(args[1]).SendAsync("importNexusKey", new() { ["path"] = hostPath }).GetAwaiter().GetResult();
                Console.WriteLine(result.GetRawText());
            } catch (Exception error) {
                Console.Error.WriteLine(error.Message);
                Environment.ExitCode = 1;
            }
            return;
        }
        if (args.FirstOrDefault() == "--mo2-bridge-snapshot") {
            try {
                if (args.Length != 2) throw new ArgumentException("Expected MO2 bridge directory");
                Console.WriteLine(new Mo2BridgeClient(args[1]).SendAsync("snapshot").GetAwaiter().GetResult().GetRawText());
            } catch (Exception error) {
                Console.Error.WriteLine(error.Message);
                Environment.ExitCode = 1;
            }
            return;
        }
        if (args.FirstOrDefault() == "--inspect-mo2") { Mo2ProfileFiles.Inspect(args.Skip(1).ToArray()); return; }
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
        if (viewModel is ScenarioLoadOrderPage { LiveProfile: not null } plugins) return new Mo2PluginsView { ViewModel = plugins };
        if (viewModel is Mo2CreateProfileCard create) {
            var createView = new NexusMods.App.UI.Controls.LoadoutCard.CreateNewLoadoutCardView { ViewModel = create };
            var text = createView.FindControl<TextBlock>("CreateNewLoadoutTextBlock")!;
            text.Text = "Create new loadout\n" + create.InstanceLabel;
            text.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            ToolTip.SetTip(createView, "Create an MO2 profile in " + create.Registration.Directory);
            return createView;
        }
        if (viewModel is Mo2LoadoutCard card) {
            var cardView = new NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView { ViewModel = card };
            var rename = new NexusMods.App.UI.Controls.StandardButton { Name = "RenameMo2ProfileButton", Text = "Rename", ShowLabel = false, ShowIcon = NexusMods.App.UI.Controls.StandardButton.ShowIconOptions.Left, LeftIcon = NexusMods.UI.Sdk.Icons.IconValues.FolderEditOutline, Command = card.RenameProfileCommand };
            Avalonia.Automation.AutomationProperties.SetName(rename, "Rename profile");
            ToolTip.SetTip(rename, "Rename this profile in MO2. Select a different profile first if this one is active.");
            DockPanel.SetDock(rename, Dock.Right);
            cardView.FindControl<DockPanel>("ActionsDock")!.Children.Insert(1, rename);
            return cardView;
        }
        if (viewModel is Mo2ProfilesPage profiles) return new Mo2ProfilesView { ViewModel = profiles };
        if (viewModel is Mo2DownloadsPage downloads) return new Mo2DownloadsView { ViewModel = downloads };
        if (viewModel is ScenarioInstalledPage { IsMo2Profile: true } liveMods) return new Mo2ModsView { ViewModel = liveMods };
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
            if (Environment.GetEnvironmentVariable("MO2_FIXTURES") != "1") {
                var endpoint = Environment.GetEnvironmentVariable("MO2_BRIDGE_DIRECTORY") ?? "";
                var live = new Mo2LiveWorkspace(endpoint);
                var liveWindow = live.CreateWindow();
                desktop.MainWindow = liveWindow;
                if (endpoint.Length == 0) live.OpenGames();
                desktop.Exit += (_, _) => live.Dispose();
                if (Environment.GetEnvironmentVariable("MO2_SCREENSHOT") is { } liveScreenshot)
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        if (endpoint.Length > 0) await WaitFor(() => live.Profile.ProfilePath.Length > 0 && live.ModsPage?.Adapter.SourceCount.Value > 0 && live.PluginsPage?.Adapter.SourceCount.Value > 0, "Live MO2 tables did not connect");
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_CATALOG") == "1") {
                            if (endpoint.Length != 0 || live.Profile.Mods.Count != 0 || live.Profile.Order.Plugins.Count != 0)
                                throw new InvalidOperationException("Default startup must have no fixture or assumed active profile data");
                            if (!liveWindow.GetVisualDescendants().OfType<MyGamesView>().Any())
                                throw new InvalidOperationException("Default startup did not show native My Games");
                            await live.HomeMenu.LeftMenuItemMyLoadouts.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
                            await WaitFor(() => liveWindow.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>().Any(), "Native loadout cards missing");
                            var cardView = liveWindow.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>()
                                .Single(x => x.ViewModel is Mo2LoadoutCard card && card.Registration.Directory.EndsWith("/frontend/artifacts/mo2-fnv-host") && card.LoadoutName == "Frontend Test");
                            var original = ((Mo2LoadoutCard)cardView.ViewModel!).Profile;
                            var visitButton = cardView.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "CardOuterButton");
                            visitButton.Command!.Execute(visitButton.CommandParameter);
                            await WaitFor(() => live.Profile.ProfilePath.Length > 0 && Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath) == Path.GetFullPath(original.Directory)
                                && live.ModsPage?.Adapter.SourceCount.Value > 0 && live.PluginsPage?.Adapter.SourceCount.Value > 0, "Catalog selection did not connect both live panels", seconds: 110);
                            if (!live.Catalog.Read().Any(x => x.Instance?.Game == "Skyrim Special Edition")) throw new InvalidOperationException("Skyrim catalog entry missing");
                            Console.WriteLine("PASS: default startup has only the real MO2 catalog; selecting a registered profile connects both live panels; Skyrim profiles present");
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_REORDER_GUARDS") == "1") await VerifyReorderGuards(live);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PLUGIN_DETAILS") == "1") await VerifyPluginDetails(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_DESKTOP") == "1") {
                            if (live.DesktopInterop.GetType().Name != "LinuxInterop") throw new InvalidOperationException("Live desktop service is not NMA’s native Linux implementation");
                            var snapshot = await new Mo2BridgeClient(live.Profile.Endpoint).SendAsync("snapshot");
                            var folder = Mo2InstanceCatalog.LocalPath(snapshot.GetProperty("instance").GetProperty("downloadsPath").GetString()!);
                            var mounts = await live.DesktopInterop.GetFileSystemMounts();
                            if (mounts.Length == 0) throw new InvalidOperationException("Native desktop service returned no filesystem mounts");
                            live.DesktopInterop.OpenDirectory(NexusMods.Paths.FileSystem.Shared.FromUnsanitizedFullPath(folder));
                            Console.WriteLine("DESKTOP: native Linux service reports " + mounts.Length + " mounts; requested opening MO2 downloads folder " + folder);
                            await Task.Delay(2000);
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PROFILE_CARDS") == "1") await VerifyProfileCards(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_NATIVE_PANELS") == "1") await VerifyNativePanels(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_CROSS_GAME") is { } skyrimInstance)
                            await VerifyCrossGame(live, liveWindow, skyrimInstance);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_TRANSFERS") == "1") await VerifyTransfers(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_UNINSTALL") == "1") await VerifyUninstall(live);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_LAUNCH") is { } executable) {
                            if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test")) throw new InvalidOperationException("Launch check requires the isolated FNV profile");
                            await live.Profile.Launch(executable);
                            Console.WriteLine("LAUNCH RESULT: " + live.Profile.Status);
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_CONTROLS") == "1") await VerifyControls(live, endpoint, desktop.MainWindow!);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_LIVE") == "1") await VerifyLive(live, endpoint);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PROFILES") == "1") await VerifyProfiles(live);
                        if (Environment.GetEnvironmentVariable("MO2_SHOW_PROFILES") == "1") live.OpenProfiles();
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_DOWNLOADS") is { } downloadLink) {
                            if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test")) throw new InvalidOperationException("Download check requires the isolated FNV profile");
                            var before = live.Profile.Downloads.Select(x => x.Name).ToHashSet();
                            await live.Profile.DownloadNexus(downloadLink);
                            var deadline = DateTime.UtcNow.AddSeconds(60);
                            while (!live.Profile.Downloads.Any(x => !x.Partial && !before.Contains(x.Name))) {
                                if (DateTime.UtcNow > deadline) throw new InvalidOperationException("MO2 download did not finish: " + live.Profile.Status);
                                await Task.Delay(500); await live.Profile.Refresh();
                            }
                            live.OpenDownloads();
                            Console.WriteLine("PASS: Nexus file requested through MO2; completed archive appears in its download directory and frontend");
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_INSTALL_ARCHIVE") is { } installArchive) {
                            if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test")) throw new InvalidOperationException("Install check requires the isolated FNV profile");
                            await live.Profile.InstallArchive(installArchive);
                            Console.WriteLine("INSTALL: " + live.Profile.Status);
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_OPEN_MOD_DETAILS") is { } detailName) {
                            var mod = live.Profile.Mods.Single(x => x.Name == detailName);
                            await WaitFor(() => live.ModsPage!.Adapter.Source.Value.Items.Any(x => x.Key == mod.Id), "Selected mod row did not refresh");
                            live.ModsPage!.Adapter.SelectedModels.Clear();
                            live.ModsPage.Adapter.SelectedModels.Add(live.ModsPage.Adapter.Source.Value.Items.Single(x => x.Key == mod.Id));
                            liveWindow.GetVisualDescendants().OfType<Button>().Single(x => Equals(x.Content, "Details in MO2"))
                                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                            await WaitFor(() => live.Profile.ManagingMod, "Details action did not begin");
                            await WaitFor(() => !live.Profile.ManagingMod, "Close the native mod details dialog to finish verification", seconds: 180);
                            if (!live.Profile.Status.EndsWith("Connected to MO2")) throw new InvalidOperationException(live.Profile.Status);
                            Console.WriteLine("PASS: native Details in MO2 button opened and closed the original dialog for " + detailName);
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_ENABLE_FNV_DLCS") == "1") {
                            if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test")) throw new InvalidOperationException("DLC activation requires the isolated FNV profile");
                            var names = new[] { "TribalPack.esm", "MercenaryPack.esm", "ClassicPack.esm", "CaravanPack.esm", "DeadMoney.esm", "HonestHearts.esm", "OldWorldBlues.esm", "LonesomeRoad.esm", "GunRunnersArsenal.esm" };
                            live.PluginsPage!.Adapter.SelectedModels.Clear();
                            foreach (var name in names) {
                                var plugin = live.Profile.Order.Plugins.Single(x => x.DisplayName == name);
                                live.PluginsPage.Adapter.SelectedModels.Add(live.PluginsPage.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key)));
                            }
                            liveWindow.GetVisualDescendants().OfType<Button>().Single(x => Equals(x.Content, "Enable selected"))
                                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                            await WaitFor(() => names.All(name => live.Profile.Order.Plugins.Single(x => x.DisplayName == name).IsActive), "DLC activation did not reach MO2");
                            Console.WriteLine("PASS: native Enable selected button explicitly activates all nine FNV DLCs in the isolated profile");
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PLUGIN_DOWN") is { } pluginDown) {
                            if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test") || !pluginDown.StartsWith("MCM Example Menu")) throw new InvalidOperationException("In-game ordering check requires an isolated author example");
                            var plugin = live.Profile.Order.Plugins.Single(x => x.DisplayName == pluginDown);
                            var before = plugin.SortIndex;
                            live.PluginsPage!.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key))
                                .Get<SharedComponents.IndexComponent>(LoadOrderColumns.IndexColumn.IndexComponentKey).MoveDown.Execute(R3.Unit.Default);
                            await WaitFor(() => live.Profile.Order.Plugins.Single(x => x.DisplayName == pluginDown).SortIndex == before + 1, "Native plugin order command failed");
                            Console.WriteLine("PASS: native plugin row moved " + pluginDown + " from " + before + " to " + (before + 1));
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_ENABLE_MOD") is { } modName) {
                            if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test")) throw new InvalidOperationException("Activation check requires the isolated FNV profile");
                            var mod = live.Profile.Mods.Single(x => x.Name == modName);
                            if ((mod.State & 2) != 0) throw new InvalidOperationException("Activation check requires an initially disabled mod");
                            var row = live.ModsPage!.Adapter.Source.Value.Items.Single(x => x.Key == mod.Id);
                            row.Get<LoadoutComponents.EnabledStateToggle>(LoadoutColumns.EnabledState.EnabledStateToggleComponentKey).CommandToggle.Execute(R3.Unit.Default);
                            await WaitFor(() => (live.Profile.Mods.Single(x => x.Name == modName).State & 2) != 0 && live.Profile.Order.Plugins.Any(x => x.ModName == modName), "MO2 mod activation did not expose its plugin");
                            Console.WriteLine("PASS: native mod activation command enabled installed mod and exposed its ESP in the right panel");
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_MISSING_MASTER") == "1") {
                            const string name = "MO2 Missing Master Verification.esp";
                            if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test")) throw new InvalidOperationException("Diagnostic check requires isolated FNV");
                            await WaitFor(() => live.Profile.Order.Plugins.Any(x => x.DisplayName == name), "Diagnostic plugin missing");
                            await live.Profile.SetPluginsActive([name], true);
                            await WaitFor(() => live.Profile.Order.Plugins.Single(x => x.DisplayName == name).HasWarning, "MO2 did not mark the missing master");
                            var plugin = live.Profile.Order.Plugins.Single(x => x.DisplayName == name);
                            if (!plugin.Diagnostics.Contains("Missing Masters") || !plugin.Diagnostics.Contains("MO2 Diagnostic Absent Master.esm")) throw new InvalidOperationException("Missing-master diagnostic text was not preserved");
                            await WaitFor(() => live.PluginsPage!.Adapter.Source.Value.Items.Any(x => x.Key.Equals(plugin.Key)), "Diagnostic row missing");
                            var row = live.PluginsPage!.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key));
                            if (!row.Get<StringComponent>(LoadOrderColumns.DisplayNameColumn.DisplayNameComponentKey).Value.Value.StartsWith("⚠ ")) throw new InvalidOperationException("Native warning marker missing from row");
                            live.PluginsPage.Adapter.SelectedModels.Clear(); live.PluginsPage.Adapter.SelectedModels.Add(row);
                            await WaitFor(() => liveWindow.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Name == "Mo2PluginDiagnostics" && x.Text!.Contains("Missing Masters") && x.Text.Contains("MO2 Diagnostic Absent Master.esm")), "Missing-master details were not rendered");
                            Console.WriteLine("PASS: actual MO2 missing-master warning is marked in the plugin row and rendered in selected details");
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_DISABLE_MOD") is { } disabledModName) {
                            if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test")) throw new InvalidOperationException("Deactivation check requires the isolated FNV profile");
                            var mod = live.Profile.Mods.Single(x => x.Name == disabledModName);
                            if ((mod.State & 2) == 0) throw new InvalidOperationException("Deactivation check requires an initially enabled mod");
                            var row = live.ModsPage!.Adapter.Source.Value.Items.Single(x => x.Key == mod.Id);
                            row.Get<LoadoutComponents.EnabledStateToggle>(LoadoutColumns.EnabledState.EnabledStateToggleComponentKey).CommandToggle.Execute(R3.Unit.Default);
                            await WaitFor(() => (live.Profile.Mods.Single(x => x.Name == disabledModName).State & 2) == 0 && live.Profile.Order.Plugins.All(x => x.ModName != disabledModName), "MO2 mod deactivation did not remove its plugins");
                            Console.WriteLine("PASS: native mod deactivation command disabled the mod and removed its plugins from the right panel");
                        }
                        await Task.Delay(500);
                        foreach (var table in liveWindow.GetVisualDescendants().OfType<TreeDataGrid>())
                            Console.WriteLine($"TABLE: source={table.Source?.Items.Cast<object>().Count()} rows={table.Rows?.Count} bounds={table.Bounds} visualRows={table.GetVisualDescendants().Count(x => x is Avalonia.Controls.Primitives.TreeDataGridRow)}");
                        using var bitmap = new RenderTargetBitmap(new PixelSize((int)liveWindow.ClientSize.Width, (int)liveWindow.ClientSize.Height));
                        bitmap.Render(liveWindow);
                        bitmap.Save(liveScreenshot);
                        desktop.Shutdown();
                    }, TimeSpan.FromSeconds(4));
                base.OnFrameworkInitializationCompleted();
                return;
            }
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
            if (Environment.GetEnvironmentVariable("MO2_INTERACTION_REPORT") is { } interactionReport)
                ScenarioInteractionReport.Attach(window, scenario, interactionReport);
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
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_LIBRARY") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => { verification = VerifyLibrary(scenario, window); }, TimeSpan.FromSeconds(1));
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_DIALOGS") == "1")
                window.Opened += (_, _) => DispatcherTimer.RunOnce(() => { verification = VerifyDialogs(scenario, desktop); }, TimeSpan.FromSeconds(1));
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

    private static async Task VerifyProfiles(Mo2LiveWorkspace live)
    {
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Profile check requires the isolated FNV test instance");
        var entry = live.Catalog.Read().Single(x => x.Instance?.Profiles.Any(p => Path.GetFullPath(p.Directory) == Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath)) == true);
        var original = entry.Instance!.Profiles.Single(x => x.Name == "Frontend Test");
        if (!entry.Instance.Profiles.Any(x => x.Name == "Frontend Clone Test")) await live.Profile.ManageProfiles();
        var clone = Mo2ProfileFiles.Read(entry.Registration.Directory).Profiles.Single(x => x.Name == "Frontend Clone Test");
        if (!await live.Profile.SelectProfile(entry.Registration, clone)) throw new InvalidOperationException(live.Profile.Status);
        await WaitFor(() => live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count, "Cloned profile rows did not activate");
        var mod = live.Profile.Mods.Single(x => x.Name == "The Mod Configuration Menu");
        if ((mod.State & 2) != 0) {
            live.Profile.Toggle([NexusMods.Abstractions.Loadouts.LoadoutItemId.From(mod.Id)]);
            await WaitFor(() => (live.Profile.Mods.Single(x => x.Name == mod.Name).State & 2) == 0 && live.Profile.Order.Plugins.All(x => x.ModName != mod.Name), "Cloned profile mod/plugin state did not disable");
        }
        if (!await live.Profile.SelectProfile(entry.Registration, original)) throw new InvalidOperationException(live.Profile.Status);
        if ((live.Profile.Mods.Single(x => x.Name == mod.Name).State & 2) == 0 || !live.Profile.Order.Plugins.Any(x => x.ModName == mod.Name && x.IsActive)) throw new InvalidOperationException("Original profile mod/plugin state was not restored");
        if (!await live.Profile.SelectProfile(entry.Registration, clone)) throw new InvalidOperationException(live.Profile.Status);
        if ((live.Profile.Mods.Single(x => x.Name == mod.Name).State & 2) != 0 || live.Profile.Order.Plugins.Any(x => x.ModName == mod.Name)) throw new InvalidOperationException("Clone mod/plugin state was not retained");
        if (!await live.Profile.SelectProfile(entry.Registration, original)) throw new InvalidOperationException(live.Profile.Status);
        if (!live.Catalog.Read().Any(x => x.Instance?.Game == "Skyrim Special Edition")) throw new InvalidOperationException("Skyrim profiles missing from catalog");
        Console.WriteLine("PASS: native MO2 profile manager clone; profile switching; independent saved activation; original profile restored; Skyrim profiles discovered");
        live.OpenProfiles();
    }

    private static async Task VerifyUninstall(Mo2LiveWorkspace live)
    {
        const string name = "Frontend Uninstall Verification";
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Uninstall verification requires the isolated FNV profile");
        var root = Path.GetFullPath(Path.Combine(Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath), "..", ".."));
        var fixture = Path.Combine(root, "mods", name);
        var original = live.Profile.Mods.Where(x => x.Name != name).Select(x => (x.Name, x.State, x.Priority)).ToArray();
        var archives = live.Profile.Downloads.Select(x => (x.Name, x.Bytes)).ToArray();
        foreach (var answer in new[] { "No", "Yes" }) {
            var mod = live.Profile.Mods.Single(x => x.Name == name);
            var row = live.ModsPage!.Adapter.Source.Value.Items.Single(x => x.Key == mod.Id);
            File.WriteAllText(Path.Combine(root, "uninstall-test-answer.txt"), answer);
            row.Get<SharedComponents.UninstallItemAction>(NexusMods.App.UI.Pages.LoadoutPage.LoadoutColumns.EnabledState.UninstallItemComponentKey).CommandUninstallItem.Execute(R3.Unit.Default);
            await WaitFor(() => !File.Exists(Path.Combine(root, "uninstall-test-answer.txt")) && !live.Profile.ManagingMod, "MO2 uninstall confirmation did not finish", seconds: 30);
            var removed = answer == "Yes";
            if (Directory.Exists(fixture) == removed || live.Profile.Mods.Any(x => x.Name == name) == removed)
                throw new InvalidOperationException("Uninstall outcome disagrees with MO2 confirmation");
        }
        if (File.ReadAllLines(Path.Combine(root, "profiles", "Frontend Test", "modlist.txt")).Any(x => x.TrimStart('+', '-') == name))
            throw new InvalidOperationException("Removed mod remains in the MO2 profile");
        if (!original.SequenceEqual(live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority))) || !archives.SequenceEqual(live.Profile.Downloads.Select(x => (x.Name, x.Bytes))))
            throw new InvalidOperationException("Uninstall changed unrelated mods or archives");
        Console.WriteLine("PASS: frontend uninstall command uses MO2 confirmation; No preserves mod, Yes removes files and profile entry; unrelated mods and archives unchanged");
    }

    private static async Task VerifyTransfers(Mo2LiveWorkspace live, Window window)
    {
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test") || live.Profile.Downloads.Any(x => x.Partial))
            throw new InvalidOperationException("Transfer check requires the isolated FNV profile without existing partial downloads");
        var original = live.Profile.Downloads.Select(x => x.Name).Order().ToArray();
        live.OpenDownloads();
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2DownloadsView>().Any(), "Downloads view did not open");
        File.WriteAllText(Path.Combine(Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath), "..", "..", "frontend-transfer-test.url"), "http://127.0.0.1:18642/mo2-transfer-test.zip");
        await WaitFor(() => live.Profile.Downloads.Any(x => x.Name == "mo2-transfer-test.zip.unfinished" && x.Bytes > 262144), "Host test transfer did not start", seconds: 30);
        void Click(string text) => window.GetVisualDescendants().OfType<Mo2DownloadsView>().Single().GetVisualDescendants().OfType<Button>()
            .Single(x => Equals(x.Content, text)).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Click("Pause");
        await WaitFor(() => live.Profile.Downloads.Any(x => x.Name == "mo2-transfer-test.zip.unfinished" && x.Paused), "MO2 did not pause the transfer", seconds: 20);
        var paused = live.Profile.Downloads.Single(x => x.Name == "mo2-transfer-test.zip.unfinished");
        await Task.Delay(800); await live.Profile.Refresh();
        if (live.Profile.Downloads.Single(x => x.Name == paused.Name).Bytes != paused.Bytes) throw new InvalidOperationException("Paused transfer continued writing");
        Click("Resume");
        await WaitFor(() => live.Profile.Downloads.Any(x => x.Name == paused.Name && !x.Paused && x.Bytes > paused.Bytes), "MO2 did not resume the transfer", seconds: 20);
        Click("Cancel");
        await WaitFor(() => live.Profile.Downloads.All(x => x.Name != paused.Name), "MO2 did not cancel the transfer", seconds: 20);
        if (!live.Profile.Downloads.Select(x => x.Name).Order().SequenceEqual(original)) throw new InvalidOperationException("Transfer controls changed other archives");
        Console.WriteLine("PASS: native frontend Pause, Resume and Cancel buttons route through MO2; paused bytes stop, resumed bytes grow, cancelled partial is removed; other archives unchanged");
    }

    private static async Task VerifyProfileCards(Mo2LiveWorkspace live, Window window)
    {
        const string target = "Frontend Card Action Verification";
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Profile card check requires the isolated FNV profile");
        var source = Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath);
        var root = Path.GetFullPath(Path.Combine(source, "..", ".."));
        var destination = Path.Combine(root, "profiles", target);
        if (Directory.Exists(destination)) throw new InvalidOperationException("Disposable profile already exists");
        var originals = Directory.GetDirectories(Path.Combine(root, "profiles")).SelectMany(directory =>
            new[] { "modlist.txt", "plugins.txt", "loadorder.txt" }.Select(file => Path.Combine(directory, file)))
            .Where(File.Exists).ToDictionary(path => path, File.ReadAllBytes);
        var backup = Path.Combine(root, "profile-card-test-before", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        foreach (var (path, bytes) in originals) {
            var saved = Path.Combine(backup, Path.GetRelativePath(Path.Combine(root, "profiles"), path));
            Directory.CreateDirectory(Path.GetDirectoryName(saved)!); File.WriteAllBytes(saved, bytes);
        }
        var originalProfile = live.Profile.ProfilePath;
        live.OpenLoadouts(live.CatalogEntries.Single(x => x.Registration.Directory == root).Instance!.Game);
        async Task ClickCard(string name, string buttonName, string operation, string answer)
        {
            await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>().Any(x =>
                x.ViewModel is Mo2LoadoutCard card && card.Registration.Directory == root && card.LoadoutName == name), "Profile card did not refresh");
            var card = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>().Single(x =>
                x.ViewModel is Mo2LoadoutCard model && model.Registration.Directory == root && model.LoadoutName == name);
            var button = card.GetVisualDescendants().OfType<Button>().Single(x => x.Name == buttonName);
            if (!button.Command!.CanExecute(button.CommandParameter)) throw new InvalidOperationException("Profile action is disabled");
            File.WriteAllText(Path.Combine(root, "profile-card-test.json"), System.Text.Json.JsonSerializer.Serialize(new { target, operation, answer }));
            button.Command.Execute(button.CommandParameter);
            await WaitFor(() => !File.Exists(Path.Combine(root, "profile-card-test.json")) && !live.Profile.SelectingProfile,
                "Native profile action did not finish: " + live.Profile.Status, seconds: 45);
            if (live.Profile.ProfilePath != originalProfile) throw new InvalidOperationException("Card action changed MO2’s active profile");
        }
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>().Any(x =>
            x.ViewModel is Mo2LoadoutCard card && card.Registration.Directory == root && card.LoadoutName == "Frontend Test"), "Active profile card missing");
        var activeCard = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>().Single(x =>
            x.ViewModel is Mo2LoadoutCard card && card.Registration.Directory == root && card.LoadoutName == "Frontend Test");
        var activeRename = activeCard.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.StandardButton>().Single(x => x.Name == "RenameMo2ProfileButton");
        if (activeRename.Command!.CanExecute(activeRename.CommandParameter)) throw new InvalidOperationException("Active profile rename must be disabled");
        if (Environment.GetEnvironmentVariable("MO2_VERIFY_CREATE_PROFILE") == "1") {
            async Task Create(string answer) {
                await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.CreateNewLoadoutCardView>().Any(x => x.ViewModel is Mo2CreateProfileCard card && card.Registration.Directory == root), "Native create card missing");
                var card = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.CreateNewLoadoutCardView>().Single(x => x.ViewModel is Mo2CreateProfileCard model && model.Registration.Directory == root);
                var button = card.FindControl<Button>("CreateNewLoadoutButton")!;
                File.WriteAllText(Path.Combine(root, "profile-card-test.json"), System.Text.Json.JsonSerializer.Serialize(new { target, operation = "create", answer }));
                button.Command!.Execute(button.CommandParameter);
                await WaitFor(() => !File.Exists(Path.Combine(root, "profile-card-test.json")) && !live.Profile.SelectingProfile, "Native profile creation did not finish", seconds: 45);
                if (live.Profile.ProfilePath != originalProfile) throw new InvalidOperationException("Create changed the active profile");
            }
            await Create("Cancel");
            if (Directory.Exists(destination)) throw new InvalidOperationException("Cancelled Create wrote a profile");
            await Create("Yes");
            await WaitFor(() => live.CatalogEntries.Where(x => x.Registration.Directory == root).Any(x => x.Instance!.Profiles.Any(p => p.Name == target)), "Created profile missing from catalog");
            if (!File.ReadAllLines(Path.Combine(destination, "modlist.txt")).Contains("-The Mod Configuration Menu")) throw new InvalidOperationException("Create did not produce MO2’s fresh disabled-mod state");
            await ClickCard(target, "DeleteButton", "remove", "Yes");
            await WaitFor(() => !Directory.Exists(destination), "Disposable profile was not removed");
            foreach (var (path, bytes) in originals)
                if (!bytes.SequenceEqual(File.ReadAllBytes(path))) throw new InvalidOperationException("Create changed an existing profile file");
            Console.WriteLine("PASS: native NMA Create card opens MO2; Cancel creates nothing; confirm creates a fresh profile and card without switching; native deletion cleans it up; existing profile bytes unchanged");
            return;
        }
        await ClickCard("Frontend Test", "CreateCopyButton", "copy", "");
        foreach (var file in new[] { "modlist.txt", "plugins.txt", "loadorder.txt" })
            if (!File.ReadAllBytes(Path.Combine(source, file)).SequenceEqual(File.ReadAllBytes(Path.Combine(destination, file))))
                throw new InvalidOperationException("Native profile copy did not preserve " + file);
        await ClickCard(target, "RenameMo2ProfileButton", "rename", "Cancel");
        if (!Directory.Exists(destination)) throw new InvalidOperationException("Cancelled rename moved the profile");
        await ClickCard(target, "RenameMo2ProfileButton", "rename", "Renamed");
        var renamed = destination + " Renamed";
        if (Directory.Exists(destination) || !Directory.Exists(renamed)) throw new InvalidOperationException("Native rename did not move the profile");
        foreach (var file in new[] { "modlist.txt", "plugins.txt", "loadorder.txt" })
            if (!File.ReadAllBytes(Path.Combine(source, file)).SequenceEqual(File.ReadAllBytes(Path.Combine(renamed, file))))
                throw new InvalidOperationException("Native rename changed " + file);
        await ClickCard(target + " Renamed", "RenameMo2ProfileButton", "rename", "Original");
        if (Directory.Exists(renamed) || !Directory.Exists(destination)) throw new InvalidOperationException("Native rename did not restore the profile name");
        await ClickCard(target, "DeleteButton", "remove", "No");
        if (!Directory.Exists(destination)) throw new InvalidOperationException("Cancelled removal deleted the profile");
        await ClickCard(target, "DeleteButton", "remove", "Yes");
        await WaitFor(() => !Directory.Exists(destination) && live.CatalogEntries.Where(x => x.Registration.Directory == root)
            .All(x => x.Instance!.Profiles.All(p => p.Name != target)), "Removed profile remains in catalog");
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>()
            .All(x => x.ViewModel is not Mo2LoadoutCard card || card.LoadoutName != target), "Removed profile card remains visible");
        foreach (var (path, bytes) in originals)
            if (!bytes.SequenceEqual(File.ReadAllBytes(path))) throw new InvalidOperationException("Profile action changed unrelated profile state: " + Path.GetRelativePath(root, path));
        Console.WriteLine("PASS: native Create Copy preserves mod/plugin files; Rename Cancel preserves name, Rename accepts and cards refresh with identical mod/plugin files; Delete No preserves profile; Delete Yes removes it; cards refresh without reopening; active and unrelated profiles unchanged");
        live.ShowProfile();
    }

    private static async Task VerifyReorderGuards(Mo2LiveWorkspace live)
    {
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Reorder check requires the isolated FNV profile");
        var profile = live.Profile;
        var client = new Mo2BridgeClient(profile.Endpoint);
        var original = profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        var movable = profile.Order.Plugins.Single(x => x.DisplayName == "The Mod Configuration Menu.esp");
        var fixedPlugin = profile.Order.Plugins.Single(x => x.DisplayName == "FalloutNV.esm");
        if (!movable.CanMove || fixedPlugin.CanMove || movable.SortIndex != original.Length - 1)
            throw new InvalidOperationException("Unexpected isolated test order");
        async Task<(string, int, bool)[]> HostOrder() => (await client.SendAsync("snapshot")).GetProperty("plugins").EnumerateArray()
            .Select(x => (x.GetProperty("name").GetString()!, x.GetProperty("priority").GetInt32(), x.GetProperty("state").GetInt32() == 2)).OrderBy(x => x.Item2).ToArray();
        await profile.Order.MoveItems(default, [fixedPlugin.Key], movable.Key, NexusMods.Abstractions.Games.TargetRelativePosition.AfterTarget);
        if (!original.SequenceEqual(await HostOrder())) throw new InvalidOperationException("Fixed-plugin drag altered the host order");
        var neighbour = profile.Order.Plugins[^2];
        try {
            await profile.Order.MoveItems(default, [movable.Key], neighbour.Key, NexusMods.Abstractions.Games.TargetRelativePosition.BeforeTarget);
            if ((await HostOrder()).Single(x => x.Item1 == movable.DisplayName).Item2 != movable.SortIndex - 1)
                throw new InvalidOperationException("Movable plugin drop path did not change native order");
            await profile.Order.MoveItems(default, [movable.Key], neighbour.Key, NexusMods.Abstractions.Games.TargetRelativePosition.AfterTarget);
            if (!original.SequenceEqual(await HostOrder())) throw new InvalidOperationException("Reverse move did not restore native order");
            var stale = profile.Order.Plugins.ToArray();
            await client.SendAsync("setPluginPriority", new() { ["profilePath"] = profile.ProfilePath, ["name"] = movable.DisplayName, ["priority"] = movable.SortIndex - 1 });
            var changed = await HostOrder();
            await profile.Order.ApplyOrder!(stale, CancellationToken.None);
            if (!profile.Status.Contains("MO2 plugin state changed") || !changed.SequenceEqual(await HostOrder()))
                throw new InvalidOperationException("Stale reorder overwrote a native host change");
        } finally {
            await client.SendAsync("setPluginPriority", new() { ["profilePath"] = profile.ProfilePath, ["name"] = movable.DisplayName, ["priority"] = movable.SortIndex });
            await profile.Refresh();
        }
        if (!original.SequenceEqual(await HostOrder())) throw new InvalidOperationException("Reorder verification did not restore original state");
        Console.WriteLine("PASS: shared plugin drop path rejects fixed plugins, moves and restores a movable plugin, rejects stale native state; original host order and activation restored");
    }

    private static async Task VerifyPluginDetails(Mo2LiveWorkspace live, Window window)
    {
        var snapshot = await new Mo2BridgeClient(live.Profile.Endpoint).SendAsync("snapshot");
        var expected = snapshot.GetProperty("plugins").EnumerateArray().Select(x => (
            x.GetProperty("name").GetString(), x.GetProperty("diagnostics").GetString(), x.GetProperty("modIndex").GetString(),
            x.GetProperty("canToggle").GetBoolean(), x.GetProperty("canMove").GetBoolean())).OrderBy(x => x.Item1).ToArray();
        if (!expected.SequenceEqual(live.Profile.Order.Plugins.Select(x => ((string?)x.DisplayName, (string?)x.Diagnostics, (string?)x.ModIndex, x.CanToggle, x.CanMove)).OrderBy(x => x.Item1)))
            throw new InvalidOperationException("Native plugin diagnostics disagree with MO2");
        var plugin = live.Profile.Order.Plugins.First(x => !x.CanToggle && !x.CanMove);
        await WaitFor(() => live.PluginsPage!.Adapter.Source.Value.Items.Any(x => x.Key.Equals(plugin.Key)), "Plugin rows did not refresh");
        live.PluginsPage!.Adapter.SelectedModels.Clear();
        live.PluginsPage.Adapter.SelectedModels.Add(live.PluginsPage.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key)));
        await WaitFor(() => window.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Name == "Mo2PluginDiagnostics" && x.Text!.Contains(plugin.Diagnostics)), "Native plugin diagnostics were not rendered");
        var row = live.PluginsPage.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key));
        var index = row.Get<SharedComponents.IndexComponent>(LoadOrderColumns.IndexColumn.IndexComponentKey);
        if (index.MoveUp.CanExecute() || index.MoveDown.CanExecute())
            throw new InvalidOperationException("Forced plugin movement controls are enabled");
        var view = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single();
        if (view.GetVisualDescendants().OfType<Button>().Any(x => Equals(x.Content, "Disable selected") && x.IsEffectivelyEnabled))
            throw new InvalidOperationException("Forced plugin activation controls are enabled");
        Console.WriteLine("PASS: " + expected.Length + " native plugin diagnostics, mod indices and restrictions match MO2; forced plugin controls disabled for " + plugin.DisplayName);
    }

    private static async Task VerifyNativePanels(Mo2LiveWorkspace live, Window window)
    {
        live.OpenGames();
        await WaitFor(() => window.GetVisualDescendants().OfType<MyGamesView>().Any(), "Native My Games missing");
        var workspace = live.WorkspaceController.ActiveWorkspace;
        var first = workspace.Panels.Single();
        var originalTabs = first.Tabs.Select(x => x.Id).ToHashSet();
        var top = window.GetVisualDescendants().OfType<TopBarView>().Single().ViewModel!;
        await top.NewTabCommand.Execute();
        await WaitFor(() => first.Tabs.Count == originalTabs.Count + 1, "Native top bar did not add a tab");
        first.CloseTab(first.Tabs.Single(x => !originalTabs.Contains(x.Id)).Id);
        for (var count = 2; count <= 4; count++) {
            await workspace.AddPanelButtonViewModels.First().AddPanelCommand.Execute();
            await WaitFor(() => workspace.Panels.Count == count, "Native Add Panel command failed");
        }
        foreach (var panel in workspace.Panels.Where(x => x.Id != first.Id).ToArray()) await panel.CloseCommand.Execute();
        await WaitFor(() => workspace.Panels.Count == 1, "Native Close Panel command failed");
        live.OpenProfiles();
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.MyLoadouts.MyLoadoutsView>().Any(), "Native My Loadouts missing");
        var countBefore = first.Tabs.Count;
        live.OpenProfiles();
        if (first.Tabs.Count != countBefore) throw new InvalidOperationException("Repeated navigation duplicated My Loadouts tab");
        Console.WriteLine("PASS: native top bar adds tabs; NMA commands split to 2/3/4 panels and close back to one; My Loadouts navigation reuses its tab");
        if (live.Profile.ProfilePath.Length > 0) live.ShowProfile();
    }

    private static async Task VerifyCrossGame(Mo2LiveWorkspace live, Window window, string skyrimInstance)
    {
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Cross-game check must start in the isolated FNV profile");
        var entries = live.Catalog.Read();
        var original = entries.Single(x => x.Registration.Endpoint == live.Profile.Endpoint);
        var originalProfile = original.Instance!.Profiles.Single(x => Path.GetFullPath(x.Directory) == Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath));
        var skyrim = entries.Single(x => x.Registration.Directory == Path.GetFullPath(skyrimInstance));
        if (skyrim.Instance?.Game != "Skyrim Special Edition") throw new InvalidOperationException("Choose a Skyrim instance for the cross-game check");
        var selected = skyrim.Instance.Profiles.Single(x => x.Name == "Default");
        var originalNames = live.Profile.Mods.Select(x => x.Name).Order().ToArray();
        async Task VisitGameProfile(Mo2CatalogEntry entry, Mo2ProfileSnapshot profile)
        {
            live.OpenGames();
            await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.GameWidget.GameWidget>().Any(), "My Games widgets missing");
            var gameView = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.GameWidget.GameWidget>()
                .Single(x => x.ViewModel!.Name == entry.Instance!.Game);
            var viewButton = gameView.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "ViewGameButton");
            viewButton.Command!.Execute(viewButton.CommandParameter);
            await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>().Any(x => x.ViewModel is Mo2LoadoutCard card && card.Registration.Directory == entry.Registration.Directory && card.Profile.Directory == profile.Directory), "Selected game’s loadout cards missing");
            var cardView = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>()
                .Single(x => x.ViewModel is Mo2LoadoutCard card && card.Registration.Directory == entry.Registration.Directory && card.Profile.Directory == profile.Directory);
            var visit = cardView.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "CardOuterButton");
            visit.Command!.Execute(visit.CommandParameter);
            await WaitFor(() => !live.Profile.SelectingProfile && Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath) == Path.GetFullPath(profile.Directory)
                && live.WorkspaceController.ActiveWorkspace.Context is Mo2WorkspaceContext, "Game card did not switch both panels: " + live.Profile.Status, seconds: 110);
        }
        try {
            await VisitGameProfile(skyrim, selected);
            if (live.WorkspaceController.ActiveWorkspace.Panels.SelectMany(x => x.Tabs).Count(x => x.Contents.ViewModel is ScenarioInstalledPage { IsMo2Profile: true }) != 1)
                throw new InvalidOperationException("Profile selection duplicated the mod-list tab");
            await WaitFor(() => live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count && live.PluginsPage!.Adapter.SourceCount.Value == live.Profile.Order.Plugins.Count, "Both panels did not change to Skyrim");
            var snapshot = await new Mo2BridgeClient(skyrim.Registration.Endpoint).SendAsync("snapshot");
            var mods = snapshot.GetProperty("mods").EnumerateArray().Select(x => (x.GetProperty("name").GetString(), x.GetProperty("state").GetInt32(), x.GetProperty("priority").GetInt32())).OrderBy(x => x.Item1).ToArray();
            if (!mods.SequenceEqual(live.Profile.Mods.Select(x => ((string?)x.Name, x.State, x.Priority)).OrderBy(x => x.Item1)))
                throw new InvalidOperationException("Skyrim mod panel disagrees with independent host snapshot");
            var details = snapshot.GetProperty("mods").EnumerateArray().Select(x => (
                x.GetProperty("name").GetString(), x.GetProperty("priorityText").GetString(),
                x.GetProperty("conflicts").GetString(), x.GetProperty("flags").GetString(), x.GetProperty("overwrite").GetBoolean())).OrderBy(x => x.Item1).ToArray();
            if (!details.SequenceEqual(live.Profile.Mods.Select(x => ((string?)x.Name, (string?)x.PriorityText, (string?)x.Conflicts, (string?)x.Flags, x.IsOverwrite)).OrderBy(x => x.Item1)))
                throw new InvalidOperationException("Native mod details disagree with MO2");
            var overwrite = live.Profile.Mods.Single(x => x.IsOverwrite);
            if ((overwrite.State & 4) == 0 || overwrite.PriorityText.Length != 0) throw new InvalidOperationException("Overwrite lost its native fixed-priority state");
            foreach (var mod in live.Profile.Mods) {
                var row = live.ModsPage!.Adapter.Source.Value.Items.Single(x => x.Key == mod.Id);
                if (row.Get<ValueComponent<string>>(Mo2ModsAdapter.ConflictsKey).Value.Value != mod.Conflicts)
                    throw new InvalidOperationException("Conflict column disagrees with MO2");
            }
            Console.WriteLine("PASS: Overwrite, native priority labels, conflict and status columns match MO2; " + live.Profile.Mods.Count(x => x.Conflicts.Length > 0) + " mods have native conflict messages");
            var plugins = snapshot.GetProperty("plugins").EnumerateArray().Select(x => (x.GetProperty("name").GetString(), x.GetProperty("state").GetInt32() == 2, x.GetProperty("priority").GetInt32())).OrderBy(x => x.Item1).ToArray();
            if (!plugins.SequenceEqual(live.Profile.Order.Plugins.Select(x => ((string?)x.DisplayName, x.IsActive, x.SortIndex)).OrderBy(x => x.Item1)))
                throw new InvalidOperationException("Skyrim plugin panel disagrees with independent host snapshot");
            await VerifyPluginDetails(live, window);
            var archives = snapshot.GetProperty("downloads").EnumerateArray().Where(x => !x.GetProperty("hidden").GetBoolean()).Select(x => x.GetProperty("path").GetString()).Order().ToArray();
            if (!archives.SequenceEqual(live.Profile.Downloads.Select(x => (string?)x.Path).Order()))
                throw new InvalidOperationException("Download folder context disagrees with the Skyrim host");
            if (!snapshot.GetProperty("executables").EnumerateArray().Select(x => x.GetString()).SequenceEqual(live.Profile.Executables))
                throw new InvalidOperationException("Executable picker disagrees with the Skyrim host");
            if (live.Profile.NexusGame != "skyrimspecialedition" || live.Profile.Executables.Contains("NVSE"))
                throw new InvalidOperationException("Cross-game selection retained FNV download or executable context");
            if (Environment.GetEnvironmentVariable("MO2_CROSS_GAME_SCREENSHOT") is { } capture) {
                await Task.Delay(500);
                using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
                bitmap.Render(window); bitmap.Save(capture);
            }
            Console.WriteLine($"PASS: Skyrim host connected; {mods.Length} mod states/priorities and {plugins.Length} plugin states/priorities match independent host snapshot; game context switched");
        } finally {
            await VisitGameProfile(original, originalProfile);
        }
        if (live.Profile.NexusGame != "newvegas" || !live.Profile.Mods.Select(x => x.Name).Order().SequenceEqual(originalNames))
            throw new InvalidOperationException("Original FNV connection was not restored");
        Console.WriteLine("PASS: FNV connection restored after cross-game selection");
    }

    private static async Task VerifyControls(Mo2LiveWorkspace live, string endpoint, Window window)
    {
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test", StringComparison.Ordinal))
            throw new InvalidOperationException("Control check requires the isolated FNV test profile");
        var mod = live.Profile.Mods.Single(x => x.Name == "The Mod Configuration Menu");
        var plugin = live.Profile.Order.Plugins.Single(x => x.ModName == mod.Name);
        var originalMods = live.Profile.Mods.OrderBy(x => x.Priority).Select(x => x.Name).ToArray();
        async Task AssertHost(bool active, int priority)
        {
            var snapshot = await new Mo2BridgeClient(endpoint).SendAsync("snapshot");
            if ((snapshot.GetProperty("plugins").EnumerateArray().Single(x => x.GetProperty("name").GetString() == plugin.DisplayName).GetProperty("state").GetInt32() == 2) != active)
                throw new InvalidOperationException("Host plugin activation disagrees");
            if (snapshot.GetProperty("mods").EnumerateArray().Single(x => x.GetProperty("name").GetString() == mod.Name).GetProperty("priority").GetInt32() != priority)
                throw new InvalidOperationException("Host mod priority disagrees");
        }
        try {
            live.PluginsPage!.Adapter.SelectedModels.Add(live.PluginsPage.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key)));
            window.GetVisualDescendants().OfType<Button>().Single(x => Equals(x.Content, "Disable selected"))
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await WaitFor(() => !live.Profile.Order.Plugins.Single(x => x.Key.Equals(plugin.Key)).IsActive, "Plugin disable button failed");
            if ((live.Profile.Mods.Single(x => x.Id == mod.Id).State & 2) == 0) throw new InvalidOperationException("Plugin disable also disabled its mod");
            await AssertHost(false, mod.Priority);
            live.ModsPage!.Adapter.SelectedModels.Add(live.ModsPage.Adapter.Source.Value.Items.Single(x => x.Key == mod.Id));
            window.GetVisualDescendants().OfType<Button>().Single(x => Equals(x.Content, "Move earlier"))
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await WaitFor(() => live.Profile.Mods.Single(x => x.Id == mod.Id).Priority == mod.Priority - 1, "Mod priority button failed: " + live.Profile.Status);
            await AssertHost(false, mod.Priority - 1);
        } finally {
            await live.Profile.SetPluginsActive([plugin.DisplayName], plugin.IsActive);
            var current = live.Profile.Mods.Single(x => x.Id == mod.Id);
            await live.Profile.MoveMod(mod.Id, mod.Priority - current.Priority);
        }
        await AssertHost(plugin.IsActive, mod.Priority);
        if (!live.Profile.Mods.OrderBy(x => x.Priority).Select(x => x.Name).SequenceEqual(originalMods)) throw new InvalidOperationException("Original mod order was not restored");
        Console.WriteLine("PASS: plugin disable and mod priority toolbar buttons update MO2; plugin activation independent of mod activation; original state restored");
    }

    private static async Task VerifyLive(Mo2LiveWorkspace live, string endpoint)
    {
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test", StringComparison.Ordinal))
            throw new InvalidOperationException("Live mutation check requires the isolated FNV test profile");
        var original = live.Profile.Order.Plugins.ToArray();
        var plugin = original.Single(x => x.DisplayName == "TribalPack.esm");
        var indexBefore = plugin.SortIndex;
        try {
            var row = live.PluginsPage!.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key));
            row.Get<SharedComponents.IndexComponent>(LoadOrderColumns.IndexColumn.IndexComponentKey).MoveDown.Execute(R3.Unit.Default);
            await WaitFor(() => live.Profile.Order.Plugins.Single(x => x.Key.Equals(plugin.Key)).SortIndex == indexBefore + 1, "Native row command did not update MO2 priority");
            var snapshot = await new Mo2BridgeClient(endpoint).SendAsync("snapshot");
            var actual = snapshot.GetProperty("plugins").EnumerateArray().Single(x => x.GetProperty("name").GetString() == plugin.DisplayName);
            if (actual.GetProperty("priority").GetInt32() != indexBefore + 1) throw new InvalidOperationException("Host disagrees with frontend order");
        } finally {
            await live.Profile.Order.ApplyOrder!(original, default);
        }
        if (!live.Profile.Order.Plugins.Select(x => x.DisplayName).SequenceEqual(original.Select(x => x.DisplayName)))
            throw new InvalidOperationException("Live check did not restore original plugin order");
        Console.WriteLine("PASS: live two-panel tables; native plugin row command changes MO2; independent host read agrees; original order restored");
    }

    private static async Task VerifyDialogs(ScenarioWorkspace scenario, IClassicDesktopStyleApplicationLifetime desktop)
    {
        await scenario.Data.Game.AddGameCommand.Execute();
        var card = scenario.Data.Section.Loadouts.Single();
        await card.VisitLoadoutCommand.Execute();
        var page = (ScenarioInstalledPage)scenario.WorkspaceController.ActiveWorkspace.SelectedTab.Contents.ViewModel;
        var menu = (ScenarioLoadoutMenu)scenario.GetActiveMenu();
        await menu.LeftMenuItemLibrary.NavigateCommand.Execute(NavigationInformation.From(OpenPageBehaviorType.NewPanel));
        var library = (ScenarioLibraryPage)scenario.WorkspaceController.ActiveWorkspace.SelectedTab.Contents.ViewModel;
        await WaitFor(() => library.Adapter.SourceCount.Value == 2, "Library did not activate");
        page.CommandRenameGroup.Execute(R3.Unit.Default);
        await WaitFor(() => desktop.Windows.OfType<DialogWindow>().Any(x => x.IsVisible), "Rename dialog did not open");
        var dialog = desktop.Windows.OfType<DialogWindow>().Single();
        ((IDialogStandardContentViewModel)dialog.ViewModel!.ContentViewModel!).InputText = "Cancelled name";
        dialog.ViewModel.ButtonPressCommand.Execute(ButtonDefinitionId.Cancel);
        await WaitFor(() => !desktop.Windows.OfType<DialogWindow>().Any(), "Cancel did not close dialog");
        if (card.InstalledMods.CollectionName.Value != "My Mods") throw new InvalidOperationException("Cancelled rename changed state");
        page.CommandRenameGroup.Execute(R3.Unit.Default);
        await WaitFor(() => desktop.Windows.OfType<DialogWindow>().Any(x => x.IsVisible), "Second rename dialog did not open");
        dialog = desktop.Windows.OfType<DialogWindow>().Single();
        ((IDialogStandardContentViewModel)dialog.ViewModel!.ContentViewModel!).InputText = "Mojave Essentials";
        await Task.Delay(200);
        var capture = Environment.GetEnvironmentVariable("MO2_DIALOG_SCREENSHOT");
        if (capture is not null) {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)dialog.ClientSize.Width, (int)dialog.ClientSize.Height));
            bitmap.Render(dialog); bitmap.Save(capture);
        }
        dialog.ViewModel.ButtonPressCommand.Execute(ButtonDefinitionId.Accept);
        await WaitFor(() => card.InstalledMods.CollectionName.Value == "Mojave Essentials", "Accepted rename did not update state");
        await WaitFor(() => menu.LeftMenuCollectionItems.Single().Text.Value.Value == "Mojave Essentials" && library.SelectedInstallationTarget?.Name == "Mojave Essentials", "Rename did not update sidebar and Library target");
        if (page.CollectionName.Value != "Mojave Essentials" || page.TabTitle != "Mojave Essentials") throw new InvalidOperationException("Rename did not update open page");
        Console.WriteLine("PASS: native modal rename; cancel preserves state; accept updates open page, sidebar and Library target");
    }

    private static async Task WaitFor(Func<bool> ready, string failure, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!ready()) {
            if (DateTime.UtcNow >= deadline) throw new InvalidOperationException(failure);
            await Task.Delay(50);
        }
    }

    private static async Task VerifyLibrary(ScenarioWorkspace scenario, Window window)
    {
        await scenario.Data.Game.AddGameCommand.Execute();
        var card = scenario.Data.Section.Loadouts.Single();
        await card.VisitLoadoutCommand.Execute();
        var installedPage = (ScenarioInstalledPage)scenario.WorkspaceController.ActiveWorkspace.SelectedTab.Contents.ViewModel;
        var menu = (ScenarioLoadoutMenu)scenario.GetActiveMenu();
        await menu.LeftMenuItemLibrary.NavigateCommand.Execute(NavigationInformation.From(OpenPageBehaviorType.NewPanel));
        await Task.Delay(200);
        var page = (ScenarioLibraryPage)scenario.WorkspaceController.ActiveWorkspace.SelectedTab.Contents.ViewModel;
        await WaitFor(() => page.Adapter.SourceCount.Value == 2, "Library fixture rows missing");
        var row = page.Adapter.Source.Value.Items.First();
        row.Get<LibraryComponents.InstallAction>(LibraryColumns.Actions.InstallComponentKey).CommandInstall.Execute(R3.Unit.Default);
        await Task.Delay(150);
        if (card.InstalledMods.Mods.Count != 5 || !card.PluginOrder.Plugins.Any(x => x.DisplayName == card.InstalledMods.Mods.Single(m => m.Id == row.Key).Plugin && x.IsActive))
            throw new InvalidOperationException("Library row install did not update mods and plugin order");
        var updated = page.Adapter.Source.Value.Items.Single(x => x.Key == row.Key);
        if (!updated.Get<LibraryComponents.InstallAction>(LibraryColumns.Actions.InstallComponentKey).IsInstalled.Value)
            throw new InvalidOperationException("Library installed status did not update");
        page.Adapter.SelectAll();
        page.InstallSelectedItemsCommand.Execute(R3.Unit.Default);
        await Task.Delay(100);
        if (card.InstalledMods.Mods.Count != 6) throw new InvalidOperationException("Batch install duplicated existing mod");
        await WaitFor(() => installedPage.ItemCount.Value == 6 && installedPage.Adapter.SourceCount.Value == 6, "Open Mods panel did not update after Library install");
        if (scenario.WorkspaceController.ActiveWorkspace.Panels.Count != 2) throw new InvalidOperationException("Library did not open in a second panel");
        page.Adapter.SelectAll();
        page.RemoveSelectedItemsCommand.Execute(R3.Unit.Default);
        await Task.Delay(100);
        if (!page.Adapter.IsSourceEmpty.Value || card.InstalledMods.Mods.Count != 6)
            throw new InvalidOperationException("Library deletion affected installed mods or failed to empty library");
        scenario.Library.Add("Reticle archive");
        await Task.Delay(100);
        if (page.Adapter.SourceCount.Value != 1) throw new InvalidOperationException("Library add did not update live page");
        if (!window.GetVisualDescendants().OfType<LibraryView>().Any()) throw new InvalidOperationException("Native Library view missing");
        Console.WriteLine("PASS: native Library navigation; row and batch install; installed status; duplicate prevention; library deletion preserves installed mods; live add; live Mods panel updates");
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
