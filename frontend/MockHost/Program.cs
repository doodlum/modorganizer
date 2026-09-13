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
        if (viewModel is Mo2DiagnosticText diagnosticText) return new Mo2DiagnosticTextView { ViewModel = diagnosticText };
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
                if (endpoint.Length == 0) live.ShowHome();
                desktop.Exit += (_, _) => live.Dispose();
                if (Environment.GetEnvironmentVariable("MO2_SCREENSHOT") is { } liveScreenshot)
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        if (endpoint.Length > 0) await WaitFor(() => live.Profile.ProfilePath.Length > 0 && live.ModsPage?.Adapter.SourceCount.Value > 0 && live.PluginsPage?.Adapter.SourceCount.Value > 0, "Live MO2 tables did not connect");
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_DIALOG_QUEUE") == "1") await Mo2DialogQueueCheck.Run();
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_TOPBAR") == "1") await VerifyTopBar(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_LAYOUT_OPTIONS") is { } options) await VerifyLayoutOptions(live, liveWindow, options);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_LAYOUT_RESTORE") == "1") await VerifyLayoutRestore(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_HEALTH_RESTART") is { } healthRestart) await VerifyHealthRestart(live, liveWindow, healthRestart);
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
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_NATIVE_LAUNCH") == "1") await VerifyNativeLaunch(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_ACTION_GUARDS") == "1") await VerifyActionGuards(live);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PREVIEW") == "1") await VerifyPreview(live);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_EXTERNAL_PROFILE") == "1") await VerifyExternalProfile(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_ORIGINAL_UI") == "1") await VerifyOriginalUi(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_DOWNLOAD_CONTEXT") == "1") await VerifyDownloadContext(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PLUGIN_MULTI") == "1") await VerifyPluginMulti(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PLUGIN_DRAG") == "1") await VerifyPluginDrag(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PANEL_DRAG") == "1") await VerifyPanelDrag(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_TOPBAR") == "1" && live.Profile.IsConnected) await VerifyTopBar(live, liveWindow, openLogs: true);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PROFILE_WORKSPACES") == "1") await VerifyProfileWorkspaces(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PROFILE_NAVIGATION") == "1") await VerifyProfileNavigation(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_HEALTH_SKYRIM") == "1") {
                            var entry = live.CatalogEntries.Single(x => x.Registration.Directory == "/home/deck/Games/mod-organizer-2-skyrimspecialedition/modorganizer2");
                            var target = entry.Instance!.Profiles.Single(x => x.Name == "Default");
                            var spine = liveWindow.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
                            await spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance.Game + " — " + target.Name + " (" + entry.Registration.Directory + ")").Click.Execute();
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_HEALTH") == "1") await VerifyHealth(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_HEALTH_DETAILS") == "1") await VerifyHealthDetails(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_NATIVE_MODS") == "1") await VerifyNativeMods(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_SIDEBAR_LAUNCH") == "1") await VerifySidebarLaunch(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_REORDER_GUARDS") == "1") await VerifyReorderGuards(live);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PLUGIN_DETAILS") == "1") await VerifyPluginDetails(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_NEXUS_ACCOUNT") == "1") {
                            var button = liveWindow.GetVisualDescendants().OfType<TopBarView>().Single().FindControl<NexusMods.App.UI.Controls.StandardButton>("LoginButton")!;
                            button.Command!.Execute(button.CommandParameter);
                            await WaitFor(() => live.Profile.ManagingMod, "Nexus account button did not begin");
                            await WaitFor(() => !live.Profile.ManagingMod, "Close MO2 Nexus settings to finish verification", seconds: 180);
                            if (!live.Profile.Status.EndsWith("Connected to MO2")) throw new InvalidOperationException(live.Profile.Status);
                            Console.WriteLine("PASS: native account button opened MO2 settings on nexusTab and returned to the live profile");
                        }
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
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_HISTORY") == "1") await VerifyLiveHistory(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_NATIVE_PANELS") == "1") await VerifyNativePanels(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_CROSS_GAME") is { } skyrimInstance)
                            await VerifyCrossGame(live, liveWindow, skyrimInstance);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_TRANSFERS") == "1") await VerifyTransfers(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_UNINSTALL") == "1") await VerifyUninstall(live);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_LAUNCH") is { } executable) {
                            if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test")) throw new InvalidOperationException("Launch check requires the isolated FNV profile");
                            var launchPanel = liveWindow.GetVisualDescendants().OfType<Mo2LaunchPanel>().Single();
                            launchPanel.Executable.SelectedItem = executable;
                            if (launchPanel.Model.SelectedExecutable != executable || !launchPanel.Model.CanLaunch)
                                throw new InvalidOperationException("Native launch control cannot select this executable");
                            await launchPanel.Model.Command.Execute();
                            Console.WriteLine("LAUNCH RESULT: " + live.Profile.Status);
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_CONTROLS") == "1") await VerifyControls(live, live.Profile.Endpoint, desktop.MainWindow!);
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
                            var detailsButton = liveWindow.GetVisualDescendants().OfType<Mo2ModsView>().Single().NativeView.FindControl<NavigationControl>("ViewFilesButton")!;
                            detailsButton.Command!.Execute(NavigationInformation.From(NavigationInput.Default));
                            await WaitFor(() => live.Profile.ManagingMod, "Details action did not begin");
                            await WaitFor(() => !live.Profile.ManagingMod, "Close the native mod details dialog to finish verification", seconds: 180);
                            if (!live.Profile.Status.EndsWith("Connected to MO2")) throw new InvalidOperationException(live.Profile.Status);
                            Console.WriteLine("PASS: native View files button opened and closed the original dialog for " + detailName);
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

    private static async Task VerifyNativeLaunch(Mo2LiveWorkspace live, Window window)
    {
        const string root = "/home/deck/mo2/frontend/artifacts/";
        var report = root + "native-launch-callbacks.json";
        if (!live.Profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Native launch check requires isolated FNV");
        var executable = live.Profile.Executables.Single(x => x.Contains("Launcher", StringComparison.OrdinalIgnoreCase));
        var panel = window.GetVisualDescendants().OfType<Mo2LaunchPanel>().Single();
        foreach (var mode in new[] { "normal", "unlock" }) {
            if (File.Exists(report)) File.Delete(report);
            File.WriteAllText(root + "native-launch-mode.txt", mode);
            panel.Executable.SelectedItem = executable;
            if (!panel.Model.CanLaunch) throw new InvalidOperationException("Native PLAY action is unavailable");
            await panel.Model.Command.Execute();
            var launchStatus = live.Profile.Status;
            await WaitFor(() => File.Exists(report) && File.ReadAllText(report).Contains("closeRequested"), "Launcher fixture did not close its own window", seconds: 20);
            using var data = System.Text.Json.JsonDocument.Parse(File.ReadAllText(report));
            var result = data.RootElement;
            if (result.GetProperty("startedName").GetString() != "FalloutNVLauncher.exe" || result.GetProperty("finishedName").GetString() != "FalloutNVLauncher.exe" ||
                !result.GetProperty("mainSuppressed").GetBoolean() || !result.GetProperty("closeRequested").GetBoolean() || live.Profile.Launching || !panel.Model.CanLaunch)
                throw new InvalidOperationException("Native launch lost callback identity, visibility suppression or PLAY recovery");
            if (mode == "normal" && (result.GetProperty("exitCode").GetInt64() != 0 || !launchStatus.Contains("exited (code 0)")))
                throw new InvalidOperationException("Normal native launch did not report its successful exit: " + launchStatus);
            if (mode == "unlock" && (!result.GetProperty("unlocked").GetBoolean() || !launchStatus.Contains("MO2 stopped waiting")))
                throw new InvalidOperationException("Native Unlock was reported as a completed process: " + launchStatus);
            File.Copy(report, root + "native-launch-" + mode + ".json", overwrite: true);
            Console.WriteLine("PASS: native PLAY launcher " + mode + " preserves executable identity in both callbacks, main-window suppression and accurate completion/Unlock result");
            await Task.Delay(1500);
        }
    }

    private static async Task VerifyActionGuards(Mo2LiveWorkspace live)
    {
        const string executable = "frontend-nonexistent-executable";
        var profile = live.Profile;
        if (!profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test") || profile.Executables.Contains(executable))
            throw new InvalidOperationException("Action guard check requires isolated FNV and an unconfigured executable name");
        var client = new Mo2BridgeClient(profile.Endpoint);
        var before = (await client.SendAsync("snapshot")).GetRawText();
        await profile.Launch(executable);
        if (profile.Launching || !profile.Status.Contains("Choose an executable configured in MO2"))
            throw new InvalidOperationException("Launch did not reach native executable validation");
        await profile.Refresh();
        var entry = live.CatalogEntries.Single(x => x.Registration.Endpoint == profile.Endpoint);
        var target = entry.Instance!.Profiles.Single(x => x.Name == "Frontend Test");
        await profile.ManageProfile(entry.Registration, target, "rename");
        if (profile.SelectingProfile || !profile.Status.Contains("Select a different profile in MO2"))
            throw new InvalidOperationException("Profile-card action did not reach native active-profile validation");
        await profile.Refresh();
        if (!profile.IsConnected || (await client.SendAsync("snapshot")).GetRawText() != before)
            throw new InvalidOperationException("Native rejection changed MO2 state or prevented reconnect");
        Console.WriteLine("PASS: queued-action implementation reaches real MO2 launch/profile validation; unconfigured executable and active-profile rename are rejected before side effects; snapshot unchanged and connection recovers");
    }

    private static async Task VerifyPreview(Mo2LiveWorkspace live)
    {
        const string report = "/home/deck/mo2/frontend/artifacts/mo2-fnv-host/frontend-preview-result.json";
        if (!live.Profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test") || File.Exists(report))
            throw new InvalidOperationException("Preview check requires isolated FNV and no stale preview report");
        var mod = live.Profile.Mods.Single(x => x.Name == "The Mod Configuration Menu");
        await live.Profile.ShowModDetails(mod.Id);
        if (!File.Exists(report)) throw new InvalidOperationException("Original preview did not complete: " + live.Profile.Status);
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(report));
        var result = document.RootElement;
        if (result.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetString());
        if (!result.GetProperty("visible").GetBoolean() || result.GetProperty("suppressed").GetBoolean() || !result.GetProperty("mainSuppressed").GetBoolean() ||
            !result.GetProperty("captured").GetBoolean() || result.GetProperty("glWidgets").GetArrayLength() != 1 ||
            result.GetProperty("glWidgets")[0].GetString() != "DDSWidget" || !result.GetProperty("glValid")[0].GetBoolean())
            throw new InvalidOperationException("Original DDS preview was suppressed or lacked a valid native OpenGL widget");
        Console.WriteLine("PASS: frontend mod details opens original DDS Preview Plugin; native preview is visible with a valid OpenGL widget; main MO2 window stays suppressed; dialogs return normally");
    }

    private static async Task VerifyExternalProfile(Mo2LiveWorkspace live, Window window)
    {
        if (!live.Profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("External profile check requires isolated FNV Frontend Test");
        var profile = live.Profile;
        var client = new Mo2BridgeClient(profile.Endpoint);
        var controller = live.WorkspaceController;
        var originalWorkspace = controller.ActiveWorkspace;
        var originalPath = profile.ProfilePath;
        var original = await client.SendAsync("snapshot");
        string State(System.Text.Json.JsonElement snapshot) => snapshot.GetProperty("mods").GetRawText() + snapshot.GetProperty("plugins").GetRawText();
        TextBox Search() => window.GetVisualDescendants().OfType<Mo2ModsView>().Single().NativeView
            .FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!.FindControl<TextBox>("SearchTextBox")!;
        var originalSearch = Search().Text;
        Search().Text = "Configuration";
        async Task Follow(System.Text.Json.JsonElement expected) {
            var path = expected.GetProperty("profile").GetProperty("path").GetString()!;
            var mods = expected.GetProperty("mods").EnumerateArray().Select(x => (x.GetProperty("name").GetString(), x.GetProperty("state").GetInt32(), x.GetProperty("priority").GetInt32())).OrderBy(x => x.Item1).ToArray();
            var plugins = expected.GetProperty("plugins").EnumerateArray().Select(x => (x.GetProperty("name").GetString(), x.GetProperty("state").GetInt32() == 2, x.GetProperty("priority").GetInt32())).OrderBy(x => x.Item1).ToArray();
            await WaitFor(() => profile.ProfilePath == path && profile.IsConnected &&
                controller.ActiveWorkspace.Context is Mo2WorkspaceContext c && c.ProfilePath == path &&
                mods.SequenceEqual(profile.Mods.Select(x => ((string?)x.Name, x.State, x.Priority)).OrderBy(x => x.Item1)) &&
                plugins.SequenceEqual(profile.Order.Plugins.Select(x => ((string?)x.DisplayName, x.IsActive, x.SortIndex)).OrderBy(x => x.Item1)),
                "Periodic polling did not follow native MO2 profile/model changes", seconds: 30);
            var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
            var active = spine.LoadoutSpineItems.Where(x => x.IsActive).ToArray();
            if (active.Length != 1 || !active[0].Name.Contains(" — " + profile.CollectionName.Value + " ("))
                throw new InvalidOperationException("Spine selection does not follow native MO2 profile");
            var topbar = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.TopBar.TopBarView>().Single().ViewModel!;
            if (topbar.ActiveWorkspaceSubtitle != profile.CollectionName.Value ||
                !window.GetVisualDescendants().OfType<Mo2ModsView>().Any() || !window.GetVisualDescendants().OfType<Mo2PluginsView>().Any())
                throw new InvalidOperationException("Native profile caption or live panels did not follow MO2");
        }
        System.Text.Json.JsonElement? cloneBefore = null;
        try {
            // Uses MO2's existing profile selector, without a frontend SelectProfile/ShowProfile call.
            var clone = await client.SendAsync("selectProfile", new() { ["profilePath"] = originalPath, ["name"] = "Frontend Clone Test" });
            cloneBefore = clone;
            await Follow(clone);
            if (controller.ActiveWorkspace.Id == originalWorkspace.Id || !string.IsNullOrEmpty(Search().Text))
                throw new InvalidOperationException("External profile change reused the previous profile workspace/search");
            var clonePath = profile.ProfilePath;
            var plugin = clone.GetProperty("plugins").EnumerateArray().First(x => x.GetProperty("canToggle").GetBoolean());
            var pluginName = plugin.GetProperty("name").GetString()!;
            var enabled = plugin.GetProperty("state").GetInt32() == 2;
            await client.SendAsync("setPluginActive", new() { ["profilePath"] = clonePath, ["name"] = pluginName, ["enabled"] = !enabled });
            await Follow(await client.SendAsync("snapshot"));
            await client.SendAsync("setPluginActive", new() { ["profilePath"] = clonePath, ["name"] = pluginName, ["enabled"] = enabled });
            await Follow(await client.SendAsync("snapshot"));
            var mod = clone.GetProperty("mods").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "MCM Author Examples");
            var priority = mod.GetProperty("priority").GetInt32();
            if (priority <= 0) throw new InvalidOperationException("Expected a movable installed mod above priority zero");
            await client.SendAsync("setModPriority", new() { ["profilePath"] = clonePath, ["name"] = "MCM Author Examples", ["priority"] = priority - 1 });
            await Follow(await client.SendAsync("snapshot"));
            if (profile.Mods.Single(x => x.Name == "MCM Author Examples").Priority != priority - 1)
                throw new InvalidOperationException("Native mod priority did not change");
            await client.SendAsync("setModPriority", new() { ["profilePath"] = clonePath, ["name"] = "MCM Author Examples", ["priority"] = priority });
            await Follow(await client.SendAsync("snapshot"));
            if (State(await client.SendAsync("snapshot")) != State(clone)) throw new InvalidOperationException("Clone state did not restore");
            var returned = await client.SendAsync("selectProfile", new() { ["profilePath"] = clonePath, ["name"] = "Frontend Test" });
            await Follow(returned);
            if (controller.ActiveWorkspace.Id != originalWorkspace.Id || Search().Text != "Configuration" || State(returned) != State(original))
                throw new InvalidOperationException("External return did not restore original workspace/search/MO2 state");
            Console.WriteLine("PASS: independent native profile selector, mod priority and plugin activation changes are detected by periodic polling; workspace, spine, caption and live panels follow MO2; both profiles restored");
        } finally {
            var current = await client.SendAsync("snapshot");
            if (cloneBefore is { } saved && current.GetProperty("profile").GetProperty("name").GetString() == "Frontend Clone Test") {
                var savedMod = saved.GetProperty("mods").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "MCM Author Examples");
                if (current.GetProperty("mods").EnumerateArray().Single(x => x.GetProperty("name").GetString() == "MCM Author Examples").GetProperty("priority").GetInt32() != savedMod.GetProperty("priority").GetInt32())
                    await client.SendAsync("setModPriority", new() { ["profilePath"] = current.GetProperty("profile").GetProperty("path").GetString(), ["name"] = "MCM Author Examples", ["priority"] = savedMod.GetProperty("priority").GetInt32() });
                foreach (var plugin in saved.GetProperty("plugins").EnumerateArray().Where(x => x.GetProperty("canToggle").GetBoolean()))
                    await client.SendAsync("setPluginActive", new() { ["profilePath"] = current.GetProperty("profile").GetProperty("path").GetString(), ["name"] = plugin.GetProperty("name").GetString(), ["enabled"] = plugin.GetProperty("state").GetInt32() == 2 });
                await client.SendAsync("selectProfile", new() { ["profilePath"] = current.GetProperty("profile").GetProperty("path").GetString(), ["name"] = "Frontend Test" });
            }
            await profile.Refresh();
            live.ShowProfile();
            Search().Text = originalSearch;
        }
    }

    private static async Task VerifyOriginalUi(Mo2LiveWorkspace live, Window window)
    {
        var profile = live.Profile;
        if (!profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test") || profile.OriginalUiVisible != false)
            throw new InvalidOperationException("Original UI check requires the isolated FNV host starting hidden");
        var client = new Mo2BridgeClient(profile.Endpoint);
        async Task<string> State() {
            var snapshot = await client.SendAsync("snapshot");
            return snapshot.GetProperty("profile").GetRawText() + snapshot.GetProperty("mods").GetRawText() + snapshot.GetProperty("plugins").GetRawText();
        }
        var before = await State();
        live.OpenConnections();
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ProfilesView>().Any(), "MO2 instances did not render");
        var button = window.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "OriginalMo2Ui");
        try {
            if (!button.IsEnabled || !Equals(button.Content, "Show original MO2")) throw new InvalidOperationException("Original UI action is unavailable");
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await WaitFor(() => profile.OriginalUiVisible == true && !profile.ManagingMod && Equals(button.Content, "Hide original MO2"), "Original MO2 did not become visible");
            Console.WriteLine("ORIGINAL_UI_VISIBLE");
            await Task.Delay(15000);
            if (before != await State()) throw new InvalidOperationException("Showing original MO2 changed the profile state");
            button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await WaitFor(() => profile.OriginalUiVisible == false && !profile.ManagingMod && Equals(button.Content, "Show original MO2"), "Original MO2 did not hide");
            if (before != await State()) throw new InvalidOperationException("Hiding original MO2 changed the profile state");
            Console.WriteLine("PASS: original MO2 starts hidden; rendered instance-page button shows and hides the same host; profile, mods and plugins unchanged");
        } finally { if (profile.OriginalUiVisible == true) await profile.SetOriginalUiVisible(false); }
    }

    private static async Task VerifyDownloadContext(Mo2LiveWorkspace live, Window window)
    {
        const string missing = "/home/deck/mo2/frontend/artifacts/nonexistent-context-check.zip";
        var profile = live.Profile;
        if (!profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test") || File.Exists(missing))
            throw new InvalidOperationException("Download context check requires isolated FNV and a nonexistent archive path");
        var disconnected = new Mo2LiveProfile("");
        await disconnected.InstallArchive(missing);
        if (disconnected.CanUseDownloads || !disconnected.Status.Contains("connected MO2 profile"))
            throw new InvalidOperationException("Disconnected archive operation was not rejected");
        var entry = live.CatalogEntries.Single(x => x.Registration.Directory.EndsWith("/frontend/artifacts/mo2-fnv-host"));
        var original = entry.Instance!.Profiles.Single(x => x.Name == "Frontend Test");
        var clone = entry.Instance.Profiles.Single(x => x.Name == "Frontend Clone Test");
        var client = new Mo2BridgeClient(profile.Endpoint);
        async Task<string> State() {
            var snapshot = await client.SendAsync("snapshot");
            return snapshot.GetProperty("mods").GetRawText() + snapshot.GetProperty("plugins").GetRawText();
        }
        var before = await State();
        live.OpenDownloads();
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2DownloadsView>().Any(), "Downloads page did not render");
        var view = window.GetVisualDescendants().OfType<Mo2DownloadsView>().Single();
        var context = view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "DownloadProfileContext");
        if (context.Text != "Fallout: New Vegas · Frontend Test") throw new InvalidOperationException("Downloads does not identify its MO2 game and profile");
        var import = view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "InstallArchiveButton");
        var download = view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DownloadNexusButton");
        if (!import.IsEnabled || !download.IsEnabled) throw new InvalidOperationException("Connected Downloads actions are disabled");
        var started = false; var disabledDuringSwitch = false;
        void Changed() {
            started |= profile.Installing;
            disabledDuringSwitch |= profile.SelectingProfile && !import.IsEnabled && !download.IsEnabled;
        }
        profile.Changed += Changed;
        var oldTarget = profile.CurrentTarget;
        try {
            var cloneBefore = "";
            await view.ChooseArchive(async () => {
                if (!await profile.SelectProfile(entry.Registration, clone)) throw new InvalidOperationException("Clone profile did not connect");
                cloneBefore = await State();
                return missing;
            });
            if (started || !profile.IsConnected || !profile.Status.StartsWith("The MO2 profile changed.") || !disabledDuringSwitch ||
                context.Text != "Fallout: New Vegas · Frontend Clone Test" || await State() != cloneBefore)
                throw new InvalidOperationException("Archive picker continuation crossed its original profile boundary");
            await profile.ControlDownload(missing, "cancel", oldTarget);
            if (!profile.IsConnected || !profile.Status.StartsWith("The MO2 profile changed.") || await State() != cloneBefore)
                throw new InvalidOperationException("Stale download row acted on another profile");
            await profile.InstallArchive(missing, new Mo2ProfileTarget(oldTarget.Endpoint + "-different-instance", profile.ProfilePath));
            if (started || !profile.IsConnected || !profile.Status.StartsWith("The MO2 profile changed."))
                throw new InvalidOperationException("Archive target guard ignored instance identity");
            await view.ChooseArchive(() => Task.FromResult<string?>(missing));
            if (!started || !profile.Status.Contains("The mod archive does not exist") || import.IsEnabled || download.IsEnabled ||
                view.GetVisualDescendants().OfType<Button>().Any(x => Equals(x.Content, "Install") && x.IsEnabled))
                throw new InvalidOperationException("Current target did not reach native archive validation, or unavailable controls remained enabled");
            if (!view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Name == "DownloadsUnavailable") ||
                view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "No downloads yet") ||
                view.GetVisualDescendants().OfType<Button>().Any(x => Equals(x.Content, "Install")))
                throw new InvalidOperationException("Unavailable Downloads still presents stale archive rows or an empty-folder claim");
            await profile.Refresh();
            if (view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Name == "DownloadsUnavailable") ||
                view.GetVisualDescendants().OfType<Button>().Count(x => Equals(x.Content, "Install")) != profile.Downloads.Count)
                throw new InvalidOperationException("Fresh MO2 snapshot did not restore the download rows");
            if (!profile.IsConnected || !import.IsEnabled || !download.IsEnabled || await State() != cloneBefore)
                throw new InvalidOperationException("Downloads did not recover after refresh or changed clone state");
        } finally {
            profile.Changed -= Changed;
            await profile.SelectProfile(entry.Registration, original);
        }
        if (await State() != before) throw new InvalidOperationException("Download context check changed original MO2 state");
        Console.WriteLine("PASS: picker continuation and stale archive/download actions stay bound to their MO2 instance/profile; current target reaches native validation; disconnected/busy controls disable; both profiles unchanged");
    }

    private static async Task VerifyPanelDrag(Mo2LiveWorkspace live, Window window)
    {
        if (!live.Profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Panel pointer test requires the isolated FNV profile");
        var workspace = live.WorkspaceController.ActiveWorkspace;
        var originalPanels = workspace.Panels.ToDictionary(x => x.Id, x => x.LogicalBounds);
        var tabs = workspace.Panels.SelectMany(x => x.Tabs).Select(x => x.Id).ToHashSet();
        var original = live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        var mods = live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray();
        async Task Drag(string phase, bool horizontal, int delta) {
            await WaitFor(() => window.GetVisualDescendants().OfType<PanelResizerView>().Any(x => x.ViewModel!.IsHorizontal == horizontal), "Native divider did not render");
            await Task.Delay(300);
            var divider = window.GetVisualDescendants().OfType<PanelResizerView>().First(x => x.ViewModel!.IsHorizontal == horizontal);
            var before = divider.ViewModel!.LogicalStartPoint;
            var start = divider.PointToScreen(new Point(divider.Bounds.Width / 2, divider.Bounds.Height / 2));
            var end = new PixelPoint(start.X + (horizontal ? 0 : delta), start.Y + (horizontal ? delta : 0));
            object PointData(PixelPoint p) => new { X = p.X, Y = p.Y, Ctrl = false };
            window.Activate();
            File.WriteAllText("/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json", System.Text.Json.JsonSerializer.Serialize(new {
                Phase = phase, Points = Array.Empty<object>(), Drag = new { Start = PointData(start), End = PointData(end) }
            }));
            await Task.Delay(2500);
            await WaitFor(() => workspace.Resizers.Any(x => x.IsHorizontal == horizontal &&
                Math.Abs((horizontal ? x.LogicalStartPoint.Y - before.Y : x.LogicalStartPoint.X - before.X)) > .05), "Pointer did not resize panels: " + phase, seconds: 30);
            if (!tabs.IsSubsetOf(workspace.Panels.SelectMany(x => x.Tabs).Select(x => x.Id)))
                throw new InvalidOperationException("Resizing replaced MO2 tabs");
            var pluginTable = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single().GetVisualDescendants().OfType<TreeDataGrid>().Single();
            if (phase == "panel-height-expand") {
                pluginTable.RowSelection!.Clear();
                pluginTable.RowSelection.Select(new IndexPath(10));
                pluginTable.RowSelection.Select(new IndexPath(11));
                await WaitFor(() => live.PluginsPage!.Adapter.SelectedModels.Count == 2, "Short panel could not select plugins");
                await Task.Delay(300);
            }
            var viewport = pluginTable.GetVisualDescendants().OfType<ScrollViewer>().OrderByDescending(x => x.Extent.Height - x.Viewport.Height).First();
            if (viewport.Viewport.Height < 48) throw new InvalidOperationException("Resized plugin panel has no room for a complete row");
            var rectangles = workspace.Panels.Select(x => x.LogicalBounds).ToArray();
            if (Math.Abs(rectangles.Sum(x => x.Width * x.Height) - 1) > .001 || rectangles.Any(x => x.Width <= 0 || x.Height <= 0 || x.X < 0 || x.Y < 0 || x.Right > 1.001 || x.Bottom > 1.001))
                throw new InvalidOperationException("Resize left invalid panel coverage");
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
            bitmap.Render(window); bitmap.Save("/home/deck/mo2/frontend/artifacts/" + phase + ".png");
            Console.WriteLine("PASS: native pointer divider " + phase + " retains MO2 tabs and full panel coverage");
        }
        try {
            await Drag("panel-width-expand", false, 100);
            await Drag("panel-width-restore", false, -100);
            await workspace.AddPanelButtonViewModels.First(x => x.NewLayoutState.Any(p => p.Rect.Height < .99)).AddPanelCommand.Execute();
            await WaitFor(() => workspace.Panels.Count == 3, "Third panel did not open");
            await Drag("panel-height-expand", true, 80);
            var layout = workspace.Panels.ToDictionary(x => x.Id, x => x.LogicalBounds);
            var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
            foreach (var directory in new[] { "/home/deck/Games/mod-organizer-2-skyrimspecialedition/modorganizer2", "/home/deck/mo2/frontend/artifacts/mo2-fnv-host" }) {
                var entry = live.CatalogEntries.Single(x => x.Registration.Directory == directory);
                var profile = entry.Instance!.Profiles.Single(x => x.Name == (directory.EndsWith("mo2-fnv-host") ? "Frontend Test" : "Default"));
                await spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance.Game + " — " + profile.Name + " (" + directory + ")").Click.Execute();
                await WaitFor(() => Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath) == profile.Directory && !live.Profile.SelectingProfile, "Game switch did not connect", seconds: 110);
            }
            if (live.WorkspaceController.ActiveWorkspace.Id != workspace.Id || workspace.Panels.Count != layout.Count || workspace.Panels.Any(x => layout[x.Id] != x.LogicalBounds))
                throw new InvalidOperationException("Game switching lost the pointer-resized workspace");
            await Drag("panel-height-restore", true, -80);
            await live.Profile.Refresh();
            if (!original.SequenceEqual(live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))) || !mods.SequenceEqual(live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority))))
                throw new InvalidOperationException("Panel interaction changed MO2 state");
            Console.WriteLine("PASS: FNV/Skyrim switching preserves pointer-resized FNV workspace; original mod/plugin state unchanged");
        } finally {
            File.Delete("/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json");
            foreach (var panel in workspace.Panels.Where(x => !originalPanels.ContainsKey(x.Id)).ToArray())
                await panel.CloseCommand.Execute();
        }
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2PluginsView>().Single().Bounds.Height > 500, "Original two-panel view did not restore");
        if (workspace.Panels.Count != originalPanels.Count || workspace.Panels.Any(x =>
            Math.Abs(x.LogicalBounds.Width - originalPanels[x.Id].Width) > .005 || Math.Abs(x.LogicalBounds.Height - originalPanels[x.Id].Height) > .005))
            throw new InvalidOperationException("Original panel bounds did not restore");
    }

    private static async Task SelectPluginsWithPointer(TreeDataGrid table, ScenarioLoadOrderPage page, string[] names, string phase)
    {
        table.RowSelection!.Clear();
        for (var index = 0; index < names.Length; index++) {
            var name = names[index];
            var scroll = table.GetVisualDescendants().OfType<ScrollViewer>().OrderByDescending(x => x.Extent.Height - x.Viewport.Height).First();
            scroll.Offset = new Vector(0, scroll.Extent.Height);
            await WaitFor(() => table.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == name), "Selection row did not render");
            await Task.Delay(300);
            var text = table.GetVisualDescendants().OfType<TextBlock>().First(x => x.Text == name);
            var point = text.PointToScreen(new Point(12, text.Bounds.Height / 2));
            var key = page.LiveProfile!.Order.Plugins.Single(x => x.DisplayName == name).Key;
            File.WriteAllText("/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json", System.Text.Json.JsonSerializer.Serialize(new {
                Phase = phase + "-select-" + index, Points = new[] { new { X = point.X, Y = point.Y, Ctrl = index > 0 } }
            }));
            await WaitFor(() => page.Adapter.SelectedModels.Count == index + 1 && page.Adapter.SelectedModels.Any(x => x.Key.Equals(key)), "Pointer did not select " + name, seconds: 30);
        }
    }

    private static async Task VerifyPluginDrag(Mo2LiveWorkspace live, Window window)
    {
        var profile = live.Profile;
        if (!profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Plugin drag test requires the isolated FNV profile");
        var table = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single().GetVisualDescendants().OfType<TreeDataGrid>().Single();
        table.RowDragStarted += (_, e) => Console.WriteLine("DRAG START: " + e.Models.Count() + " rows");
        table.RowDrop += (_, e) => Console.WriteLine("DRAG DROP: " + e.Position);
        var original = profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        var mods = profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray();
        var names = original.Select(x => x.DisplayName).ToArray();
        var moving = profile.Order.Plugins.Where(x => x.DisplayName.StartsWith("MCM Example") && x.CanMove).Take(2).Select(x => x.DisplayName).ToArray();
        const string target = "The Mod Configuration Menu.esp";
        if (moving.Length != 2 || names[^1] != target || !names.Skip(names.Length - 4).Take(2).SequenceEqual(moving))
            throw new InvalidOperationException("Unexpected isolated plugin order");
        var client = new Mo2BridgeClient(profile.Endpoint);
        async Task<string[]> HostOrder() => (await client.SendAsync("snapshot")).GetProperty("plugins").EnumerateArray()
            .OrderBy(x => x.GetProperty("priority").GetInt32()).Select(x => x.GetProperty("name").GetString()!).ToArray();
        async Task Phase(string phase, string destination, bool after, string[] expected) {
            table.RowSelection!.Clear();
            var scroll = table.GetVisualDescendants().OfType<ScrollViewer>().OrderByDescending(x => x.Extent.Height - x.Viewport.Height).First();
            scroll.Offset = new Vector(0, scroll.Extent.Height);
            await WaitFor(() => moving.Append(destination).All(name => table.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == name)), "Drag rows did not render");
            await Task.Delay(300);
            window.Activate();
            TextBlock Text(string name) => table.GetVisualDescendants().OfType<TextBlock>().First(x => x.Text == name);
            object Point(PixelPoint point, bool ctrl = false) => new { X = point.X, Y = point.Y, Ctrl = ctrl };
            await SelectPluginsWithPointer(table, live.PluginsPage!, moving, phase);
            scroll.Offset = new Vector(0, scroll.Extent.Height);
            await Task.Delay(300);
            var start = Text(moving[0]).PointToScreen(new Point(12, Text(moving[0]).Bounds.Height / 2));
            var row = Text(destination).GetVisualAncestors().OfType<Avalonia.Controls.Primitives.TreeDataGridRow>().First();
            var end = row.PointToScreen(new Point(start.X - row.PointToScreen(default).X, row.Bounds.Height * (after ? 0.65 : 0.35)));
            File.WriteAllText("/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json", System.Text.Json.JsonSerializer.Serialize(new {
                Phase = phase, Points = Array.Empty<object>(), Drag = new { Start = Point(start), End = Point(end) }
            }));
            await Task.Delay(4000);
            using (var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height))) {
                bitmap.Render(window); bitmap.Save("/home/deck/mo2/frontend/artifacts/plugin-" + phase + ".png");
            }
            Console.WriteLine("DRAG STATE: selected=" + live.PluginsPage!.Adapter.SelectedModels.Count + " order=" + string.Join(",", profile.Order.Plugins.TakeLast(4).Select(x => x.DisplayName)));
            await WaitFor(() => expected.SequenceEqual(profile.Order.Plugins.Select(x => x.DisplayName)), "Pointer drag did not move both selected plugins: " + phase, seconds: 60);
            if (!expected.SequenceEqual(await HostOrder())) throw new InvalidOperationException("Host order disagrees with pointer drag");
            Console.WriteLine("PASS: actual two-plugin pointer drag " + phase + " matches independent MO2 host order");
        }
        try {
            await Phase("drag-down", target, true, names.Except(moving).Concat(moving).ToArray());
            await Phase("drag-up", names[^2], false, names);
        } finally {
            File.Delete("/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json");
            await profile.Refresh();
            if (!names.SequenceEqual(profile.Order.Plugins.Select(x => x.DisplayName)))
                await profile.Order.ApplyOrder!(names.Select(name => profile.Order.Plugins.Single(x => x.DisplayName == name)).ToArray(), CancellationToken.None);
            table.RowSelection!.Clear();
        }
        if (!original.SequenceEqual(profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))) ||
            !mods.SequenceEqual(profile.Mods.Select(x => (x.Name, x.State, x.Priority))) || !names.SequenceEqual(await HostOrder()))
            throw new InvalidOperationException("Plugin drag check did not preserve original MO2 state");
        Console.WriteLine("PASS: multi-row drag restored full plugin order and preserved mod/plugin activation");
    }

    private static async Task VerifyPluginMulti(Mo2LiveWorkspace live, Window window)
    {
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Plugin mouse test requires the isolated FNV profile");
        var wrapper = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single();
        var table = wrapper.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        var enable = wrapper.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "EnableSelectedPlugins");
        var disable = wrapper.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DisableSelectedPlugins");
        var original = live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.IsActive, x.SortIndex)).ToArray();
        var mods = live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray();
        var targets = live.Profile.Order.Plugins.Where(x => x.DisplayName.StartsWith("MCM Example") && x.CanToggle && x.IsActive).Take(2).Select(x => x.DisplayName).ToArray();
        if (targets.Length != 2) throw new InvalidOperationException("Two active MCM example plugins are required");
        async Task MousePhase(string phase, Button button) {
            window.Activate();
            await SelectPluginsWithPointer(table, live.PluginsPage!, targets, phase);
            var point = button.PointToScreen(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2));
            File.WriteAllText("/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json", System.Text.Json.JsonSerializer.Serialize(new {
                Phase = phase, Points = new[] { new { X = point.X, Y = point.Y, Ctrl = false } }
            }));
        }
        try {
            await MousePhase("disable", disable);
            await WaitFor(() => targets.All(name => !live.Profile.Order.Plugins.Single(x => x.DisplayName == name).IsActive), "Mouse multi-selection did not disable both ESPs", seconds: 60);
            if (!mods.SequenceEqual(live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority))))
                throw new InvalidOperationException("Disabling plugins changed mod activation");
            await MousePhase("enable", enable);
            await WaitFor(() => targets.All(name => live.Profile.Order.Plugins.Single(x => x.DisplayName == name).IsActive), "Mouse multi-selection did not re-enable both ESPs", seconds: 60);
            await WaitFor(() => !enable.IsEnabled && disable.IsEnabled && live.PluginsPage!.Adapter.SelectedModels.Count == 2,
                $"Selection or activation button availability disagrees with restored plugins: selected={live.PluginsPage!.Adapter.SelectedModels.Count}, enable={enable.IsEnabled}, disable={disable.IsEnabled}");
            table.RowSelection!.Clear();
            var fixedIndex = live.Profile.Order.Plugins.ToList().FindIndex(x => !x.CanToggle);
            table.RowSelection.Select(new IndexPath(fixedIndex));
            await WaitFor(() => !enable.IsEnabled && !disable.IsEnabled, "Fixed-only selection enables plugin activation");
            var targetIndex = live.Profile.Order.Plugins.ToList().FindIndex(x => x.DisplayName == targets[0]);
            table.RowSelection.Select(new IndexPath(targetIndex));
            await WaitFor(() => disable.IsEnabled, "Mixed selection did not allow its editable plugin");
            disable.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await WaitFor(() => !live.Profile.Order.Plugins.Single(x => x.DisplayName == targets[0]).IsActive, "Mixed selection did not disable the editable plugin");
            if (!live.Profile.Order.Plugins.Where(x => !x.CanToggle).All(x => original.Single(o => o.DisplayName == x.DisplayName).IsActive == x.IsActive))
                throw new InvalidOperationException("Mixed selection changed a fixed plugin");
        } finally {
            File.Delete("/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json");
            foreach (var state in original.Where(x => live.Profile.Order.Plugins.Single(p => p.DisplayName == x.DisplayName).IsActive != x.IsActive))
                await live.Profile.SetPluginsActive([state.DisplayName], state.IsActive);
            table.RowSelection!.Clear();
        }
        await live.Profile.Refresh();
        if (!original.SequenceEqual(live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.IsActive, x.SortIndex))) ||
            !mods.SequenceEqual(live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority)))) throw new InvalidOperationException("Plugin test did not restore original MO2 state");
        Console.WriteLine("PASS: real Ctrl-click multi-selection and activation buttons disable/enable two ESPs independently of mods; fixed and mixed selections respect restrictions; original state restored");
    }

    private static async Task VerifyTopBar(Mo2LiveWorkspace live, Window window, bool openLogs = false)
    {
        var view = window.GetVisualDescendants().OfType<TopBarView>().Single();
        var model = (Mo2TopBar)view.ViewModel!;
        if (typeof(TopBarDesignViewModel).IsAssignableFrom(model.GetType()) || model.Username is not null || model.Avatar is not null || model.IsLoggedIn)
            throw new InvalidOperationException("Live top bar still contains a demo identity");
        foreach (var command in new[] { model.ShowWelcomeMessageCommand, model.LogoutCommand, model.OpenNexusModsProfileCommand,
            model.OpenNexusModsPremiumCommand, model.OpenNexusModsAccountSettingsCommand, model.OpenForumsCommand })
            if (((System.Windows.Input.ICommand)command).CanExecute(System.Reactive.Unit.Default)) throw new InvalidOperationException("Unmapped top-bar action remains enabled");
        if (((System.Windows.Input.ICommand)model.ViewChangelogCommand).CanExecute(NavigationInformation.From(NavigationInput.Default)))
            throw new InvalidOperationException("Unmapped changelog remains enabled");
        var menu = view.FindControl<MenuItem>("ViewAppLogsMenuItem")!;
        await WaitFor(() => ReferenceEquals(menu.Command, model.ViewAppLogsCommand), "Native logs menu did not bind to the MO2 command");
        if (menu.Header?.ToString() != "View MO2 logs") throw new InvalidOperationException("Logs menu misidentifies its destination");
        if (!live.Profile.IsConnected) {
            if (((System.Windows.Input.ICommand)model.ViewAppLogsCommand).CanExecute(System.Reactive.Unit.Default) || model.LogsDirectory is not null)
                throw new InvalidOperationException("Disconnected top bar enables instance logs");
        } else {
            var root = live.CatalogEntries.Single(x => x.Registration.Endpoint == live.Profile.Endpoint).Registration.Directory;
            await WaitFor(() => model.LogsDirectory == Path.Combine(root, "logs") && ((System.Windows.Input.ICommand)model.ViewAppLogsCommand).CanExecute(System.Reactive.Unit.Default),
                "Logs menu did not follow the connected MO2 instance");
            if (openLogs) menu.Command!.Execute(System.Reactive.Unit.Default);
        }
        Console.WriteLine("PASS: live top bar has no demo account; unmapped actions disabled; native logs command follows MO2 connection; directory request=" + openLogs);
    }

    private static async Task VerifySidebarLaunch(Mo2LiveWorkspace live, Window window)
    {
        var panel = window.GetVisualDescendants().OfType<Mo2LaunchPanel>().Single();
        var native = panel.NativeButton.FindControl<Button>("LaunchButton")!;
        await WaitFor(() => ReferenceEquals(native.Command, panel.Model.Command), "Native PLAY button was not bound to MO2 launch command");
        var snapshot = await new Mo2BridgeClient(live.Profile.Endpoint).SendAsync("snapshot");
        var expected = snapshot.GetProperty("executables").EnumerateArray().Select(x => x.GetString()!).ToArray();
        if (!expected.SequenceEqual(panel.Executable.ItemsSource!.Cast<string>())) throw new InvalidOperationException("Sidebar executable choices disagree with MO2");
        var original = panel.Model.SelectedExecutable;
        foreach (var choice in expected) {
            panel.Executable.SelectedItem = choice;
            if (panel.Model.SelectedExecutable != choice || !panel.Model.CanLaunch)
                throw new InvalidOperationException("Sidebar could not select an MO2 executable");
        }
        panel.Executable.SelectedItem = null;
        if (panel.Model.CanLaunch || await panel.Model.Command.CanExecute.FirstAsync())
            throw new InvalidOperationException("PLAY must be disabled without a valid executable");
        panel.Executable.SelectedItem = original;
        if (!await panel.Model.Command.CanExecute.FirstAsync()) throw new InvalidOperationException("PLAY did not re-enable for a valid executable");
        live.OpenGames();
        await WaitFor(() => !window.GetVisualDescendants().OfType<Mo2LaunchPanel>().Any(), "Home unexpectedly contains profile launch controls");
        live.ShowProfile();
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2LaunchPanel>().Any(), "Profile sidebar did not restore launch controls");
        if (panel.Model.SelectedExecutable != original) throw new InvalidOperationException("Home navigation lost the executable selection");
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault()?.NativeView.FindControl<TextBlock>("ModsCount")?.Text == live.Profile.Mods.Count.ToString(),
            "Native mod count did not return after Home navigation");
        if (window.GetVisualDescendants().OfType<Button>().Any(x => Equals(x.Content, "Run through MO2") || Equals(x.Content, "Refresh from MO2")))
            throw new InvalidOperationException("Duplicate header controls remain");
        Console.WriteLine($"PASS: native sidebar PLAY command bound to MO2; {expected.Length} executable choices match host; invalid selection disables PLAY; Home hides controls and restores selection");
    }

    private static async Task VerifyNativeMods(Mo2LiveWorkspace live, Window window)
    {
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Native mods test requires the isolated FNV profile");
        var wrapper = window.GetVisualDescendants().OfType<Mo2ModsView>().Single();
        var native = wrapper.NativeView;
        var table = native.FindControl<TreeDataGrid>("TreeDataGrid")!;
        var search = native.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!;
        var searchBox = search.FindControl<TextBox>("SearchTextBox")!;
        var mod = live.Profile.Mods.Single(x => x.Name == "The Mod Configuration Menu");
        var modsBefore = live.Profile.Mods.OrderBy(x => x.Name).Select(x => (x.Name, x.Priority, x.State)).ToArray();
        var pluginsBefore = live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        search.FindControl<Button>("SearchButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        searchBox.Text = mod.DisplayName;
        await WaitFor(() => table.Rows!.Count == 1, "Native mod search did not filter to the selected mod");
        table.RowSelection!.Select(new IndexPath(0));
        await WaitFor(() => native.FindControl<ItemsControl>("ContextControlGroup")!.IsVisible, "Native selection toolbar did not appear");
        native.FindControl<Button>("DeselectItemsButton")!.Command!.Execute(R3.Unit.Default);
        await WaitFor(() => live.ModsPage!.SelectionCount.Value == 0, "Native deselect command failed");
        try {
            var toggle = native.GetVisualDescendants().OfType<ToggleSwitch>().Single();
            toggle.Command!.Execute(toggle.CommandParameter);
            await WaitFor(() => (live.Profile.Mods.Single(x => x.Id == mod.Id).State & 2) != (mod.State & 2), "Native mod toggle did not update MO2");
            toggle = native.GetVisualDescendants().OfType<ToggleSwitch>().Single();
            toggle.Command!.Execute(toggle.CommandParameter);
            await WaitFor(() => live.Profile.Mods.Single(x => x.Id == mod.Id).State == mod.State, "Native mod toggle did not restore MO2");
        } finally {
            if (live.Profile.Mods.Single(x => x.Id == mod.Id).State != mod.State) {
                live.Profile.Toggle([NexusMods.Abstractions.Loadouts.LoadoutItemId.From(mod.Id)]);
                await WaitFor(() => live.Profile.Mods.Single(x => x.Id == mod.Id).State == mod.State, "Could not restore native mod toggle test");
            }
        }
        searchBox.Text = live.Profile.Mods.Single(x => x.IsOverwrite).DisplayName;
        await WaitFor(() => table.Rows!.Count == 1 && native.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == "Overwrite"), "Overwrite search did not render");
        table.RowSelection!.Select(new IndexPath(0));
        await WaitFor(() => live.ModsPage!.SelectionCount.Value == 1, "Overwrite selection failed");
        if (native.FindControl<Button>("DeleteButton")!.IsEnabled || native.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "MoveModEarlierButton").IsEnabled ||
            !native.FindControl<Button>("ViewFilesButton")!.IsEnabled)
            throw new InvalidOperationException("Native toolbar does not respect Overwrite restrictions");
        search.FindControl<Button>("SearchClearButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await WaitFor(() => table.Rows!.Count == live.Profile.Mods.Count, "Native clear search did not restore all mods");
        var tabs = native.FindControl<TabControl>("RulesTabControl")!;
        tabs.SelectedItem = native.FindControl<TabItem>("RulesTabItem");
        await WaitFor(() => native.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Sorting.LoadOrderView>().Any(), "Native Rules tab did not render");
        var rules = native.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Sorting.LoadOrderView>().Single().ViewModel!;
        await WaitFor(() => rules.Adapter.SourceCount.Value == live.Profile.Order.Plugins.Count, "Native Rules tab did not load MO2 plugins");
        tabs.SelectedItem = native.FindControl<TabItem>("ModsTabItem");
        live.ModsPage!.CommandDeselectItems.Execute(R3.Unit.Default);
        await live.Profile.Refresh();
        if (!modsBefore.SequenceEqual(live.Profile.Mods.OrderBy(x => x.Name).Select(x => (x.Name, x.Priority, x.State))) ||
            !pluginsBefore.SequenceEqual(live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))))
            throw new InvalidOperationException("Native mods UI test did not restore MO2 state");
        var priorities = live.ModsPage.Adapter.Source.Value.Items.Select(x => x.Get<ValueComponent<int>>(Mo2ModsAdapter.PriorityKey).Value.Value).ToArray();
        if (!priorities.SequenceEqual(priorities.Order())) throw new InvalidOperationException("Default mod rows do not follow MO2 priority");
        Console.WriteLine("PASS: native mods search/clear, selection/deselect, activation, Overwrite restrictions and Rules tab use MO2 state; original state restored");
    }

    private static async Task VerifyLayoutOptions(Mo2LiveWorkspace live, Window window, string mode)
    {
        const string root = "/home/deck/mo2/frontend/artifacts/mo2-fnv-host";
        var entry = live.CatalogEntries.Single(x => x.Registration.Directory == root);
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        await spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance!.Game + " — Frontend Test (" + root + ")").Click.Execute();
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any(), "Options workspace did not connect", seconds: 110);
        var native = window.GetVisualDescendants().OfType<Mo2ModsView>().Single().NativeView;
        var search = native.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!;
        var tabs = native.FindControl<TabControl>("RulesTabControl")!;
        var panel = live.WorkspaceController.ActiveWorkspace.Panels.OrderBy(x => x.LogicalBounds.X).First();
        if (mode == "write") {
            search.FindControl<Button>("SearchButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            search.FindControl<TextBox>("SearchTextBox")!.Text = "Configuration";
            tabs.SelectedItem = native.FindControl<TabItem>("RulesTabItem");
            var selected = panel.SelectedTab;
            await window.GetVisualDescendants().OfType<TopBarView>().Single().ViewModel!.NewTabCommand.Execute();
            await WaitFor(() => panel.Tabs.Count == 2, "New Tab did not open");
            panel.SelectTab(selected.Id);
            foreach (var tab in panel.Tabs) tab.Header.IsSelected = tab.Id == selected.Id;
        }
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Sorting.LoadOrderView>().Any() &&
            live.ModsPage?.SelectedSubTab == NexusMods.App.UI.Pages.LoadoutPage.LoadoutPageSubTabs.Rules && live.ModsPage.Mo2SearchExpanded && live.ModsPage.Mo2SearchText == "Configuration",
            "Rules and expanded search did not restore");
        if (panel.Tabs.Count != 2 || !panel.Tabs.Any(t => t.Contents.PageData.FactoryId == NewTabPageFactory.StaticId) || panel.SelectedTab.Contents.ViewModel is not ScenarioInstalledPage)
            throw new InvalidOperationException("Extra New Tab or selected mods tab did not restore");
        live.SaveLayouts();
        if (live.LayoutError is { } error) throw new InvalidOperationException(error);
        Console.WriteLine("PASS: layout options " + mode + " preserves Rules, expanded search, extra native New Tab and selected mods tab");
    }

    private static async Task VerifyLayoutRestore(Mo2LiveWorkspace live, Window window)
    {
        if (live.LayoutError is { } error) throw new InvalidOperationException(error);
        if (live.Profile.ProfilePath.Length != 0 || !window.GetVisualDescendants().OfType<MyLoadoutsView>().Any())
            throw new InvalidOperationException("Restart did not restore Home without connecting a profile");
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        async Task Visit(string directory, string name, int panels, string search) {
            var entry = live.CatalogEntries.Single(x => x.Registration.Directory == directory);
            await spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance!.Game + " — " + name + " (" + directory + ")").Click.Execute();
            await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any(), "Restored mods view missing", seconds: 110);
            var native = window.GetVisualDescendants().OfType<Mo2ModsView>().Single().NativeView;
            await WaitFor(() => native.FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!.FindControl<TextBox>("SearchTextBox")!.Text == search,
                "Search did not survive restart");
            if (live.LayoutError is { } failure) throw new InvalidOperationException(failure);
            if (live.WorkspaceController.ActiveWorkspace.Panels.Count != panels || live.ProfileMenu.WorkspaceId != live.WorkspaceController.ActiveWorkspaceId)
                throw new InvalidOperationException("Restored panel count or contextual sidebar is wrong");
            if (panels == 3 && !window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticListView>().Any())
                throw new InvalidOperationException("Selected Health Check tab did not survive restart");
            var snapshot = await new Mo2BridgeClient(live.Profile.Endpoint).SendAsync("snapshot");
            if (snapshot.GetProperty("plugins").GetArrayLength() != live.Profile.Order.Plugins.Count)
                throw new InvalidOperationException("Restored workspace did not connect the real MO2 plugins");
        }
        await Visit("/home/deck/mo2/frontend/artifacts/mo2-fnv-host", "Frontend Test", 3, "Configuration");
        live.SaveLayouts();
        using var saved = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT")!));
        if (!saved.RootElement.GetProperty("Workspaces").EnumerateArray().Any(w => w.GetProperty("ProfilePath").GetString()!.Contains("skyrimspecialedition") &&
            w.GetProperty("Panels").EnumerateArray().Any(p => p.GetProperty("Tabs").EnumerateArray().Any(t => t.GetProperty("Search").GetString() == "SkyUI"))))
            throw new InvalidOperationException("Saving after restart discarded an unvisited profile's layout");
        await Visit("/home/deck/Games/mod-organizer-2-skyrimspecialedition/modorganizer2", "Default", 2, "SkyUI");
        await Visit("/home/deck/mo2/frontend/artifacts/mo2-fnv-host", "Frontend Test", 3, "Configuration");
        Console.WriteLine("PASS: fresh frontend restores Home, per-profile panel counts, selected Health Check tab and native mod searches; unvisited layouts survive saving; MO2 reconnects on selection");
    }

    private static async Task VerifyProfileWorkspaces(Mo2LiveWorkspace live, Window window)
    {
        var controller = live.WorkspaceController;
        var fnv = controller.ActiveWorkspace;
        var original = (Mods: live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray(),
            Plugins: live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray());
        TextBox Search() => window.GetVisualDescendants().OfType<Mo2ModsView>().Single().NativeView
            .FindControl<NexusMods.App.UI.Controls.Search.SearchControl>("SearchControl")!.FindControl<TextBox>("SearchTextBox")!;
        Search().Text = "Configuration";
        await live.ProfileMenu.LeftMenuItemHealthCheck.NavigateCommand.Execute(NavigationInformation.From(OpenPageBehaviorType.NewPanel));
        await WaitFor(() => fnv.Panels.Count == 3 && window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticListView>().Any(), "FNV third panel did not open");
        string Layout(IWorkspaceViewModel workspace) => string.Join(";", workspace.Panels.Select(p =>
            p.Id + ":" + p.LogicalBounds + ":" + p.SelectedTab.Id + ":" + string.Join(",", p.Tabs.Select(t => t.Id + "/" + t.Contents.PageData.FactoryId))));
        var fnvLayout = Layout(fnv);
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        async Task Visit(string directory, string name) {
            var entry = live.CatalogEntries.Single(x => x.Registration.Directory == directory);
            await spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance!.Game + " — " + name + " (" + directory + ")").Click.Execute();
            await WaitFor(() => controller.ActiveWorkspace.Context is Mo2WorkspaceContext c &&
                c.ProfilePath == live.Profile.ProfilePath && Mo2InstanceCatalog.LocalPath(c.ProfilePath) == entry.Instance!.Profiles.Single(x => x.Name == name).Directory &&
                window.GetVisualDescendants().OfType<Mo2ModsView>().Any(), "Profile workspace did not activate", seconds: 110);
        }
        const string skyrimRoot = "/home/deck/Games/mod-organizer-2-skyrimspecialedition/modorganizer2";
        const string fnvRoot = "/home/deck/mo2/frontend/artifacts/mo2-fnv-host";
        await Visit(skyrimRoot, "Default");
        var skyrim = controller.ActiveWorkspace;
        if (skyrim.Id == fnv.Id || skyrim.Panels.Count != 2 || !string.IsNullOrEmpty(Search().Text))
            throw new InvalidOperationException("Skyrim inherited FNV's workspace or search");
        Search().Text = "SkyUI";
        var skyrimLayout = Layout(skyrim);
        await Visit(fnvRoot, "Frontend Test");
        if (controller.ActiveWorkspace.Id != fnv.Id || Layout(fnv) != fnvLayout || Search().Text != "Configuration")
            throw new InvalidOperationException($"FNV restoration failed: workspace={controller.ActiveWorkspace.Id == fnv.Id}; layout={Layout(fnv) == fnvLayout}; search={Search().Text}; before={fnvLayout}; after={Layout(fnv)}");
        if (live.ProfileMenu.WorkspaceId != fnv.Id) throw new InvalidOperationException("Sidebar targets another profile workspace");
        await Visit(skyrimRoot, "Default");
        if (controller.ActiveWorkspace.Id != skyrim.Id || Layout(skyrim) != skyrimLayout || Search().Text != "SkyUI")
            throw new InvalidOperationException("Skyrim did not restore its independent layout and search");
        await Visit(fnvRoot, "Frontend Test");
        await Visit(fnvRoot, "Frontend Clone Test");
        if (controller.ActiveWorkspace.Id == fnv.Id || controller.ActiveWorkspace.Id == skyrim.Id || controller.ActiveWorkspace.Panels.Count != 2 || !string.IsNullOrEmpty(Search().Text))
            throw new InvalidOperationException("Two profiles in the same MO2 instance shared a workspace");
        await Visit(fnvRoot, "Frontend Test");
        if (controller.ActiveWorkspace.Id != fnv.Id || Layout(fnv) != fnvLayout || Search().Text != "Configuration")
            throw new InvalidOperationException("FNV layout did not survive a same-instance profile switch");
        await live.Profile.Refresh();
        if (!original.Mods.SequenceEqual(live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority))) ||
            !original.Plugins.SequenceEqual(live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))))
            throw new InvalidOperationException("Workspace switching changed MO2 mod/plugin state");
        live.SaveLayouts();
        if (live.LayoutError is { } layoutFailure) throw new InvalidOperationException(layoutFailure);
        Console.WriteLine("PASS: profile spine restores separate FNV/Skyrim workspace IDs, panel bounds, tabs, selected tabs and native searches; sidebar targets current workspace; MO2 state preserved");
    }

    private static async Task VerifyHealthRestart(Mo2LiveWorkspace live, Window window, string mode)
    {
        const string marker = "/home/deck/mo2/frontend/artifacts/mo2-fnv-host/plugins/data/frontend-health-restart.txt";
        const string title = "Frontend diagnostic restart verification";
        var layout = Environment.GetEnvironmentVariable("MO2_FRONTEND_LAYOUT");
        if (mode is not ("write" or "read") || layout is null || !Path.GetFullPath(layout).StartsWith("/home/deck/mo2/frontend/artifacts/") || !File.Exists(marker))
            throw new InvalidOperationException("Health restart requires an isolated layout and native diagnostic fixture");
        if (live.Profile.ProfilePath.Length != 0) throw new InvalidOperationException("Restart must begin disconnected at Home");
        var expected = mode == "write" ? "phase-one-live-report" : "phase-two-live-report";
        if (File.ReadAllText(marker).Trim() != expected) throw new InvalidOperationException("Native report marker has wrong phase");
        var entry = live.CatalogEntries.Single(x => x.Registration.Directory.EndsWith("/frontend/artifacts/mo2-fnv-host"));
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        await spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance!.Game + " — Frontend Test (" + entry.Registration.Directory + ")").Click.Execute();
        await WaitFor(() => live.Profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test") && !live.Profile.SelectingProfile, "FNV did not connect", seconds: 110);
        var plugins = live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        var mods = live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray();
        if (mode == "write") {
            await live.ProfileMenu.LeftMenuItemHealthCheck.NavigateCommand.Execute(NavigationInformation.From(OpenPageBehaviorType.NewTab));
            await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Diagnostics.DiagnosticEntryView>().Any(x => x.ViewModel?.Title == title), "Native fixture report not listed");
            var report = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Diagnostics.DiagnosticEntryView>().Single(x => x.ViewModel?.Title == title);
            report.FindControl<NavigationControl>("EntryButton")!.Command!.Execute(NavigationInformation.From(OpenPageBehaviorType.NewPanel));
        }
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticDetailsView>().Any(x => x.ViewModel is Mo2HealthDetails d && d.HasResult && d.MarkdownRendererViewModel.Contents.Contains(expected)), "Restored details did not read current native report", seconds: 30);
        var details = (Mo2HealthDetails)window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticDetailsView>().Single().ViewModel!;
        if (live.WorkspaceController.ActiveWorkspace.Panels.Count != 3 || details.TabTitle != title)
            throw new InvalidOperationException("Diagnostic panel identity or layout did not restore");
        if (mode == "read") {
            try {
                File.Delete(marker);
                await WaitFor(() => details.HasResult && details.MarkdownRendererViewModel.Contents.Contains("no longer reports"), "Restored details kept a resolved report", seconds: 30);
            } finally { File.WriteAllText(marker, expected); }
            await WaitFor(() => details.HasResult && details.MarkdownRendererViewModel.Contents.Contains(expected), "Restored details did not refresh recurring report", seconds: 30);
        }
        live.SaveLayouts();
        if (live.LayoutError is { } error) throw new InvalidOperationException(error);
        var saved = File.ReadAllText(layout);
        if (!saved.Contains(title) || saved.Contains("phase-one-live-report") || saved.Contains("phase-two-live-report"))
            throw new InvalidOperationException("Layout did not save diagnostic identity independently of report contents");
        await live.Profile.Refresh();
        if (!plugins.SequenceEqual(live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))) || !mods.SequenceEqual(live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority))))
            throw new InvalidOperationException("Diagnostic restart check changed MO2 state");
        Console.WriteLine("PASS: health restart " + mode + " uses native diagnostic text, three-panel context and identity-only persistence; MO2 state unchanged");
    }

    private static async Task VerifyHealthDetails(Mo2LiveWorkspace live, Window window)
    {
        const string marker = "/home/deck/mo2/frontend/artifacts/mo2-fnv-host/plugins/data/frontend-health-fixture-active";
        if (!live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/") || !File.Exists(marker))
            throw new InvalidOperationException("Health details check requires the isolated diagnostic fixture");
        await live.ProfileMenu.LeftMenuItemHealthCheck.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Diagnostics.DiagnosticEntryView>()
            .Any(x => x.ViewModel?.Title == "Frontend diagnostic verification"), "Diagnostic fixture did not render");
        var entry = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Diagnostics.DiagnosticEntryView>()
            .Single(x => x.ViewModel?.Title == "Frontend diagnostic verification");
        var button = entry.FindControl<NavigationControl>("EntryButton")!;
        button.Command!.Execute(NavigationInformation.From(OpenPageBehaviorType.NewPanel));
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticDetailsView>().Any(), "Details panel did not open");
        var details = (Mo2HealthDetails)window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticDetailsView>().Single().ViewModel!;
        await WaitFor(() => details.HasResult && details.MarkdownRendererViewModel.Contents.Contains("original MO2 diagnostic extension"), "Details did not read the live MO2 report");
        try {
            File.Delete(marker);
            await WaitFor(() => details.HasResult && details.MarkdownRendererViewModel.Contents.Contains("no longer reports"), "Open details retained a resolved diagnostic", seconds: 30);
        } finally { File.WriteAllText(marker, ""); }
        await WaitFor(() => details.HasResult && details.MarkdownRendererViewModel.Contents.Contains("original MO2 diagnostic extension"), "Open details did not restore the current report", seconds: 30);
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        foreach (var directory in new[] { "/home/deck/Games/mod-organizer-2-skyrimspecialedition/modorganizer2", "/home/deck/mo2/frontend/artifacts/mo2-fnv-host" }) {
            var catalog = live.CatalogEntries.Single(x => x.Registration.Directory == directory);
            var target = catalog.Instance!.Profiles.Single(x => x.Name == (directory.EndsWith("mo2-fnv-host") ? "Frontend Test" : "Default"));
            await spine.LoadoutSpineItems.Single(x => x.Name == catalog.Instance.Game + " — " + target.Name + " (" + directory + ")").Click.Execute();
            var source = directory.EndsWith("mo2-fnv-host");
            await WaitFor(() => Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath) == target.Directory &&
                (source ? details.HasResult && details.MarkdownRendererViewModel.Contents.Contains("original MO2 diagnostic extension") :
                    !details.HasResult && details.MarkdownRendererViewModel.Contents.Contains("another or disconnected MO2 profile")),
                "Diagnostic details did not respect its source profile", seconds: 110);
            if (window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticDetailsView>().Any(x => ReferenceEquals(x.ViewModel, details)) != source)
                throw new InvalidOperationException("Diagnostic panel did not follow its originating workspace");
        }
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2DiagnosticTextView>().Any(x => x.Text.Text == details.MarkdownRendererViewModel.Contents), "Native diagnostic body did not render its plain text");
        Console.WriteLine("PASS: visible native details panel clears resolved diagnostics, restores recurring reports, keeps FNV details in their workspace and refreshes on return through profile spine");
    }

    private static async Task VerifyHealth(Mo2LiveWorkspace live, Window window)
    {
        if (live.Profile.ProfilePath.Length == 0) {
            live.ShowProfile();
            await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any(), "Disconnected profile workspace did not render");
        }
        var original = (live.Profile.ProfilePath,
            Mods: live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray(),
            Plugins: live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray());
        await live.ProfileMenu.LeftMenuItemHealthCheck.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticListView>().Any(), "Native Health Check page missing");
        var view = window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticListView>().Single();
        var page = (Mo2HealthPage)view.ViewModel!;
        if (live.Profile.ProfilePath.Length == 0) {
            await WaitFor(() => page.DiagnosticEntries.Any(x => x.Title == "Health check unavailable"), "Disconnected health check did not report unavailability");
            if (page.HasResult || page.NumWarnings != 1 || view.FindControl<NexusMods.App.UI.Controls.EmptyState>("EmptyState")!.IsActive)
                throw new InvalidOperationException("Disconnected Health Check displayed success");
            Console.WriteLine("PASS: disconnected Health Check reports unavailability and never displays the green success state");
            return;
        }
        await WaitFor(() => page.HasResult, "MO2 Health Check did not return a successful result", seconds: 60);
        var expected = await live.Profile.ReadHealth();
        if (!expected.SequenceEqual(page.DiagnosticEntries.Select(x => (x.Title, x.Summary))))
            throw new InvalidOperationException("Native Health Check does not match MO2 Notifications");
        if (expected.Length > 0) {
            var first = page.DiagnosticEntries.First();
            var entryView = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Diagnostics.DiagnosticEntryView>().First();
            var button = entryView.FindControl<NavigationControl>("EntryButton")!;
            button.Command!.Execute(NavigationInformation.From(NavigationInput.Default));
            await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticDetailsView>().Any(), "Native health details page missing");
            var details = window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticDetailsView>().Single().ViewModel!;
            if (details.Severity != first.Severity || details.MarkdownRendererViewModel.Contents.Length == 0)
                throw new InvalidOperationException("Health details are empty");
            await live.ProfileMenu.LeftMenuItemHealthCheck.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
            await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticListView>().Any(), "Health Check did not return after details");
            view = window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticListView>().Single();
            page = (Mo2HealthPage)view.ViewModel!;
            await WaitFor(() => page.HasResult, "Health Check did not refresh after returning from details");
        }
        await live.Profile.Refresh();
        if (original.ProfilePath != live.Profile.ProfilePath || !original.Mods.SequenceEqual(live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority))) ||
            !original.Plugins.SequenceEqual(live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))))
            throw new InvalidOperationException("Health Check changed MO2 profile state");
        if (Environment.GetEnvironmentVariable("MO2_VERIFY_HEALTH_FIXTURE") == "1") {
            const string marker = "/home/deck/mo2/frontend/artifacts/mo2-fnv-host/plugins/data/frontend-health-fixture-active";
            if (!expected.Any(x => x.Title == "Frontend diagnostic verification") || !live.Profile.ProfilePath.Contains("/frontend/artifacts/mo2-fnv-host/"))
                throw new InvalidOperationException("Expected isolated diagnostic fixture was not reported");
            try {
                File.Delete(marker);
                await page.Refresh();
                if (!page.HasResult || page.DiagnosticEntries.Any(x => x.Title == "Frontend diagnostic verification"))
                    throw new InvalidOperationException("Resolved MO2 diagnostic did not clear");
            } finally { File.WriteAllText(marker, ""); }
            await page.Refresh();
            if (!page.DiagnosticEntries.Any(x => x.Title == "Frontend diagnostic verification"))
                throw new InvalidOperationException("MO2 diagnostic did not return after invalidation");
            Console.WriteLine("PASS: original MO2 diagnostic extension appears, clears and returns in native Health Check without changing profiles");
        }
        if (expected.Length > 0)
            await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Diagnostics.DiagnosticEntryView>().Count() == page.DiagnosticEntries.Length,
                "Diagnostic entries did not render after returning from details");
        if (Environment.GetEnvironmentVariable("MO2_VERIFY_HEALTH_SWITCH") == "1") {
            var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
            foreach (var directory in new[] { "/home/deck/Games/mod-organizer-2-skyrimspecialedition/modorganizer2", "/home/deck/mo2/frontend/artifacts/mo2-fnv-host" }) {
                var entry = live.CatalogEntries.Single(x => x.Registration.Directory == directory);
                var target = entry.Instance!.Profiles.Single(x => x.Name == (directory.EndsWith("mo2-fnv-host") ? "Frontend Test" : "Default"));
                await spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance.Game + " — " + target.Name + " (" + directory + ")").Click.Execute();
                await live.ProfileMenu.LeftMenuItemHealthCheck.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
                await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticListView>()
                    .Any(x => x.ViewModel is Mo2HealthPage { HasResult: true }), "Selected profile's Health Check did not refresh", seconds: 110);
                var selectedHealth = (Mo2HealthPage)window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticListView>().Single().ViewModel!;
                if (ReferenceEquals(selectedHealth, page) != directory.EndsWith("mo2-fnv-host"))
                    throw new InvalidOperationException("Health Check did not retain its per-profile page");
                var reports = await live.Profile.ReadHealth();
                if (!reports.SequenceEqual(selectedHealth.DiagnosticEntries.Select(x => (x.Title, x.Summary))))
                    throw new InvalidOperationException("Health Check retained diagnostics from the previous game");
            }
            Console.WriteLine("PASS: native Health Check belongs to each profile workspace and restores the FNV page on return");
        }
        Console.WriteLine($"PASS: native Health Check matches {expected.Length} MO2 notification reports; details verified={expected.Length > 0}; profile/mod/plugin state preserved");
    }

    private static async Task VerifyProfileNavigation(Mo2LiveWorkspace live, Window window)
    {
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        var expected = live.CatalogEntries.Sum(x => x.Instance?.Profiles.Length ?? 0);
        if (spine.LoadoutSpineItems.Count != expected) throw new InvalidOperationException("Spine must show one entry per MO2 profile");
        foreach (var item in spine.LoadoutSpineItems)
            if (item.Image.Size.Width != item.Image.Size.Height) throw new InvalidOperationException("Profile spine artwork must be square");
        foreach (var directory in new[] { "/home/deck/Games/mod-organizer-2-skyrimspecialedition/modorganizer2", "/home/deck/mo2/frontend/artifacts/mo2-fnv-host" }) {
            var entry = live.CatalogEntries.Single(x => x.Registration.Directory == directory);
            var target = entry.Instance!.Profiles.Single(x => x.Name == (directory.EndsWith("mo2-fnv-host") ? "Frontend Test" : "Default"));
            var item = spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance.Game + " — " + target.Name + " (" + directory + ")");
            await item.Click.Execute();
            await WaitFor(() => Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath) == target.Directory && item.IsActive,
                "Spine did not connect and select the requested profile", seconds: 110);
            if (!window.GetVisualDescendants().OfType<NexusMods.App.UI.LeftMenu.Loadout.LoadoutLeftMenuView>().Any() ||
                window.GetVisualDescendants().OfType<NexusMods.App.UI.LeftMenu.Home.HomeLeftMenuView>().Any())
                throw new InvalidOperationException("Profile workspace did not replace the Home sidebar");
            if (!window.GetVisualDescendants().OfType<Mo2ModsView>().Any() || !window.GetVisualDescendants().OfType<Mo2PluginsView>().Any())
                throw new InvalidOperationException("Profile spine did not open both live panels");
        }
        await spine.Home.Click.Execute();
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.LeftMenu.Home.HomeLeftMenuView>().Any() && window.GetVisualDescendants().OfType<MyGamesView>().Any(), "Home sidebar and game page were not restored");
        live.OpenProfiles();
        await WaitFor(() => window.GetVisualDescendants().OfType<MyLoadoutsView>().Any(), "My Loadouts did not render");
        var page = (Mo2LoadoutsPage)live.WorkspaceController.ActiveWorkspace.SelectedTab.Contents.ViewModel;
        foreach (var section in page.GameSectionViewModels)
            if (section.CardViewModels.First() is not Mo2CreateProfileCard) throw new InvalidOperationException("Create card must come first");
        live.ShowProfile();
        Console.WriteLine("PASS: square per-profile spine entries connect Skyrim and FNV directly, replace Home sidebar, restore Home navigation and show Create first");
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

    private static async Task VerifyLiveHistory(Mo2LiveWorkspace live, Window window)
    {
        var endpoint = live.Profile.Endpoint;
        var path = live.Profile.ProfilePath;
        var originalMods = live.Profile.Mods.Select(x => (x.Name, x.Priority, x.State)).OrderBy(x => x.Name).ToArray();
        var originalPlugins = live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        var games = live.CatalogEntries.Where(x => x.Instance is not null).Select(x => x.Instance!.Game).Distinct().ToArray();
        var fnv = games.Single(x => x.Contains("Vegas"));
        var skyrim = games.Single(x => x.Contains("Skyrim"));
        live.OpenLoadouts(fnv);
        var home = live.WorkspaceController.ActiveWorkspace;
        var panel = home.SelectedPanel;
        var tab = panel.SelectedTab;
        async Task AssertPage(string game) {
            await WaitFor(() => tab.Contents.PageData.Context is Mo2GamePageContext context && context.Game == game &&
                tab.Contents.ViewModel is Mo2LoadoutsPage page && page.GameSectionViewModels.Count > 0 &&
                page.GameSectionViewModels.All(x => x.HeadingText == game + " Loadouts") &&
                window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.MyLoadouts.MyLoadoutsView>().Any(x => ReferenceEquals(x.ViewModel, page)), "History restored an incorrect game page");
            if (live.Profile.Endpoint != endpoint || live.Profile.ProfilePath != path) throw new InvalidOperationException("Browsing game history changed the connected profile");
        }
        await AssertPage(fnv);
        live.OpenLoadouts(skyrim);
        if (panel.SelectedTab.Id != tab.Id) throw new InvalidOperationException("Game filter navigation did not reuse its tab");
        await AssertPage(skyrim);
        async Task ClickHistory(string name, string game) {
            var button = window.GetVisualDescendants().OfType<TopBarView>().Single().FindControl<NexusMods.App.UI.Controls.StandardButton>(name)!;
            await WaitFor(() => button.Command?.CanExecute(button.CommandParameter) == true, "Native history control is disabled");
            button.Command!.Execute(button.CommandParameter);
            await AssertPage(game);
        }
        await ClickHistory("GoBackInHistory", fnv);
        await ClickHistory("GoForwardInHistory", skyrim);
        live.OpenGames();
        panel.SelectTab(tab.Id);
        await AssertPage(skyrim);
        await ClickHistory("GoBackInHistory", fnv);
        live.ShowProfile();
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any() && window.GetVisualDescendants().OfType<Mo2PluginsView>().Any(), "Live panels were not restored after history navigation");
        await live.Profile.Refresh();
        if (!originalMods.SequenceEqual(live.Profile.Mods.Select(x => (x.Name, x.Priority, x.State)).OrderBy(x => x.Name)) ||
            !originalPlugins.SequenceEqual(live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)))) throw new InvalidOperationException("History navigation changed live mod/plugin state");
        Console.WriteLine("PASS: native Back/Forward restores FNV and Skyrim loadout filters, survives another tab, preserves MO2 connection and mod/plugin state, and restores both live panels");
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
            await WaitFor(() => live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count, "Native mod rows did not refresh after game switch");
            var priorities = live.ModsPage!.Adapter.Source.Value.Items.Select(x => x.Get<ValueComponent<int>>(Mo2ModsAdapter.PriorityKey).Value.Value).ToArray();
            if (!priorities.SequenceEqual(priorities.Order())) throw new InvalidOperationException("Mod rows lost MO2 priority order after switching games");
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_SIDEBAR_LAUNCH") == "1") await VerifySidebarLaunch(live, window);
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_TOPBAR") == "1") await VerifyTopBar(live, window, openLogs: true);
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
            window.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "MoveModEarlierButton")
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
