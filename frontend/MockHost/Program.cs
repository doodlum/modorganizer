using NexusMods.App.UI.Dialog;
using NexusMods.UI.Sdk.Dialog;
using NexusMods.App.UI.Pages.LibraryPage;
using Avalonia.VisualTree;
using Avalonia.LogicalTree;
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
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);
    private const uint ParentProcess = 0xFFFFFFFF;

    public static void Main(string[] args)
    {
        // A windowed application gets no console of its own, which is what keeps an
        // ordinary launch from flashing one up. The check and tool commands still
        // have to print somewhere, so when there are arguments this attaches to the
        // console that started it. Done before anything writes, because Console
        // binds its streams on first use.
        if (args.Length > 0 && OperatingSystem.IsWindows()) {
            try { AttachConsole(ParentProcess); } catch (DllNotFoundException) { }
        }
        if (args.FirstOrDefault() == "--check-category-membership") { Mo2CategoryMembershipCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-host-startup-lease") { Mo2HostStartupLeaseCheck.Run().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--hold-host-startup-lease") { Mo2HostStartupLeaseCheck.Hold(args[1]).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-original-actions") { if (args.Length != 2) throw new ArgumentException("Expected bridge directory"); Mo2OriginalActionCheck.Run(args[1]).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-bridge-latency") { if (args.Length != 2) throw new ArgumentException("Expected bridge directory"); Mo2BridgeLatencyCheck.Run(args[1]).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-modlist-live") { if (args.Length != 2) throw new ArgumentException("Expected bridge directory"); Mo2ModlistLiveCheck.Run(args[1]).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-modlist-register") { Mo2ModlistRegisterCheck.Run(args.ElementAtOrDefault(1)).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-mod-info") { if (args.Length < 2) throw new ArgumentException("Expected bridge directory"); Mo2ModInfoPanelCheck.Run(args[1], args.ElementAtOrDefault(2)).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-mod-menu-run") { if (args.Length < 2) throw new ArgumentException("Expected bridge directory"); Mo2ModMenuRunCheck.Run(args[1], args.ElementAtOrDefault(2)).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-mod-backup") { if (args.Length != 2) throw new ArgumentException("Expected bridge directory"); Mo2ModBackupCheck.Run(args[1]).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-mod-menu") { if (args.Length < 2) throw new ArgumentException("Expected bridge directory"); Mo2ModMenuAuditCheck.Run(args[1], args.ElementAtOrDefault(2)).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-mod-columns") { Mo2ModColumnDefaultCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-mod-flags") { Mo2ModFlagsCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-separator-state") { Mo2SeparatorStateCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-modlist-sidebar") { Mo2ModlistSidebarCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-modlist-install") { Mo2ModlistInstallCheck.Run(args.ElementAtOrDefault(1)).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-nexus-credential") { Mo2NexusCredentialCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-wabbajack-tool") { Mo2WabbajackToolCheck.Run().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-modlists") { Mo2ModlistCatalogCheck.Run(args.ElementAtOrDefault(1)).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-fomod-scripts") { Mo2FomodScriptCheck.Run(args.ElementAtOrDefault(1)).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-data-filters") { Mo2DataFiltersCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-row-padding") { Mo2RowPaddingCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-view-locator") { Mo2ViewLocatorCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-sorted-roots") { Mo2SortedRootsCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-download-identities") { Mo2DownloadIdentityCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-download-status") { Mo2DownloadStatusCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-external-columns") { Mo2ExternalColumnsCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-external-files") { Mo2ExternalFilesCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-game-icons") { Mo2GameIconCheck.Run(args); return; }
        if (args.FirstOrDefault() == "--fetch-game-icons") { Mo2GameIconLibrary.Fetch(args).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--add-game") {
            var game = args.Skip(1).FirstOrDefault() ?? throw new ArgumentException("Expected a game name");
            Mo2OwnedInstances.Install(Console.WriteLine).GetAwaiter().GetResult();
            var instance = Mo2OwnedInstances.CreateInstance(game, Console.WriteLine);
            var catalog = new Mo2InstanceCatalog("");
            catalog.Add(instance);
            catalog.SetLauncher(catalog.Read().Single(x => x.Registration.Directory == instance).Registration,
                Mo2OwnedInstances.InstanceExecutable(game));
            Console.WriteLine("Instance: " + instance);
            return;
        }
        if (args.FirstOrDefault() == "--scan-external-files") {
            if (args.Length != 3) throw new ArgumentException("Expected game folder and Steam app ID");
            var scan = Mo2ExternalFiles.Scan(args[1], args[2]);
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(scan)); return;
        }
        if (args.FirstOrDefault() == "--check-notifications") { Mo2NotificationStateCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-desktop-launcher") { Mo2DesktopLauncherCheck.Run().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-search-query") { Mo2SearchQueryCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-catalog-read") { Mo2CatalogReadCheck.Run().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-bridge-errors") { Mo2BridgeErrorCheck.Run().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-shared-nexus-logout") { Mo2SharedNexusLogoutCheck.Run().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-account-monitor") { Mo2AccountMonitorCheck.Run().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-account-status-live") { if (args.Length != 2) throw new ArgumentException("Expected bridge directory"); Mo2AccountMonitorCheck.Live(args[1]).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-shared-nexus-live") { if (args.Length != 3) throw new ArgumentException("Expected bridge directory and key-file path"); Mo2SharedNexusLoginCheck.Live(args[1],args[2]).GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-shared-nexus-login") { Mo2SharedNexusLoginCheck.Run().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-nexus-sso-connection") { Mo2NexusSsoCheck.CheckConnection().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-nexus-sso") { Mo2NexusSsoCheck.Run().GetAwaiter().GetResult(); return; }
        if (args.FirstOrDefault() == "--check-collections") { Mo2CollectionsCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-mod-order") { Mo2ModOrderCheck.Run(); return; }
        if (args.FirstOrDefault() == "--check-nxm") { Mo2NxmCheck.Run(); return; }
        if (args.FirstOrDefault() == "--nxm") {
            try {
                if (args.Length != 2) throw new ArgumentException("Expected one NXM file link");
                Mo2NxmRouter.Forward(args[1]).GetAwaiter().GetResult();
                Console.WriteLine("NXM link handed to MO2");
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
        Mo2StartupCheck.ProcessStarted();
        IconProvider.Current.Register<MaterialDesignIconProvider>();
        var builder = AppBuilder.Configure<MockApp>().UsePlatformDetect()
            .With(new X11PlatformOptions { UseDBusMenu = false, WmClass = "mo2-nexus-frontend" })
            .With(new SkiaOptions { UseOpacitySaveLayer = true })
            .UseReactiveUI().LogToTrace();
        // Lets the Avalonia DevTools MCP attach to this process. Opt-in, because the
        // diagnostics listener has no place in a normal user session.
#if !DEBUG
        if (Environment.GetEnvironmentVariable("MO2_DEVELOPER_TOOLS") == "1") builder = builder.WithDeveloperTools();
#endif
        builder.StartWithClassicDesktopLifetime(args);
    }
}

// Resolve the same interface-based views as upstream without constructing its backend services.
internal sealed class FixtureViewLocator : IViewLocator
{
    private static readonly Lazy<Type[]> NativeViewTypes = new(() => typeof(MyGamesView).Assembly.GetTypes()
        .Where(type => !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) is not null).ToArray());
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, Type> NativeViews = new();

    internal static Type ResolveNativeViewType(Type modelInterface) => NativeViews.GetOrAdd(modelInterface, key => {
        var required = typeof(IViewFor<>).MakeGenericType(key);
        return NativeViewTypes.Value.FirstOrDefault(required.IsAssignableFrom)
            ?? throw new InvalidOperationException($"No upstream view for {key}");
    });

    public IViewFor? ResolveView<T>(T? viewModel, string? contract = null)
    {
        if (viewModel is Mo2GamesPage games) {
            // My Games keeps NMA's own onboarding presentation: its "Add games to
            // get started" header, its "No games found" empty state, and its second
            // section, which Mo2GamesPage now fills from MO2's bundled game plugins.
            var gamesView = new MyGamesView { ViewModel = games };
            // Home's pages sit in a workspace panel like every other page, so they
            // take the same chrome. Leaving them on NMA's own header treatment made
            // Home the one place where a page had no separator and no compaction.
            Mo2PanelChrome.Adopt(gamesView);
            return gamesView;
        }
        if (viewModel is Mo2LoadoutsPage loadouts) {
            var loadoutsView = new MyLoadoutsView { ViewModel = loadouts };
            var profileHeader = loadoutsView.GetLogicalDescendants().OfType<NexusMods.App.UI.Controls.PageHeader.PageHeader>().Single();
            profileHeader.Title = loadouts.GameScoped ? "Profiles" : "My Loadouts";
            profileHeader.Description = !loadouts.GameScoped ? "Your MO2 profiles across all games." : loadouts.Game is null ? "Manage profiles for this game." : "Profiles for " + loadouts.Game + ".";
            var emptyLoadouts = loadoutsView.FindControl<EmptyState>("MyLoadoutsEmptyState")!;
            emptyLoadouts.Header = "No MO2 profiles found";
            emptyLoadouts.Subtitle = "Add an MO2 instance from Settings to see its profiles here.";
            // This branch returns before the generic adoption at the end of the
            // locator, which is why Profiles was the one game page still without it.
            Mo2PanelChrome.Adopt(loadoutsView);
            return loadoutsView;
        }
        if (viewModel is ScenarioLoadOrderPage { LiveProfile: not null } plugins) return Mo2DeferredView<ScenarioLoadOrderPage>.For(plugins, "Plugins", model => new Mo2PluginsView { ViewModel = model }, reuseBody: true);
        if (viewModel is Mo2CreateProfileCard create) {
            var createView = new NexusMods.App.UI.Controls.LoadoutCard.CreateNewLoadoutCardView { ViewModel = create };
            var text = createView.FindControl<TextBlock>("CreateNewLoadoutTextBlock")!;
            text.Text = "Create profile" + (create.InstanceLabel.Length > 0 ? "\n" + create.InstanceLabel : "");
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
            // The badge draws a filled strip across the top right corner of the game
            // icon. MO2 profiles are not numbered or applied loadouts, so it carries
            // nothing and only covers the artwork. Hidden on every badge the card
            // actually realises: an application style did not reach this view, and
            // hiding the named control alone left the strip on screen.
            void HideBadges() {
                foreach (var badge in cardView.GetVisualDescendants()
                             .OfType<NexusMods.App.UI.Controls.LoadoutBadge.LoadoutBadge>())
                    badge.IsVisible = false;
            }
            cardView.AttachedToVisualTree += (_, _) => HideBadges();
            cardView.LayoutUpdated += (_, _) => HideBadges();
            return cardView;
        }
        // The login flow builds and drives its own copy of the native overlay view.
        if (viewModel is Mo2NexusLoginPopupModel { View: not null } login) return login.View;
        if (viewModel is Mo2DiagnosticText diagnosticText) return new Mo2DiagnosticTextView { ViewModel = diagnosticText };
        if (viewModel is Mo2LogsPage logs) return new Mo2LogsView { ViewModel = logs };
        if (viewModel is Mo2SavesPage saves) return new Mo2SavesView { ViewModel = saves };
        if (viewModel is Mo2DataPage data) return new Mo2DataView { ViewModel = data };
        if (viewModel is Mo2ArchivesPage archives) return new Mo2ArchivesView { ViewModel = archives };
        if (viewModel is Mo2ExternalFilesPage externalFiles) return new Mo2ExternalFilesView { ViewModel = externalFiles };
        if (viewModel is Mo2OverwritePage overwrite) return new Mo2OverwriteView { ViewModel = overwrite };
        if (viewModel is Mo2ToolsPage tools) return new Mo2ToolsView { ViewModel = tools };
        if (viewModel is Mo2ComponentsPage components) return new Mo2ComponentsView { ViewModel = components };
        if (viewModel is Mo2ModlistsPage modlists) return new Mo2ModlistsView { ViewModel = modlists };
        if (viewModel is Mo2ModInfoPage modInfo) return new Mo2ModInfoView { ViewModel = modInfo };
        if (viewModel is Mo2DownloadsPage downloads) return new Mo2DownloadsView { ViewModel = downloads };
        if (viewModel is ScenarioInstalledPage { IsMo2Profile: true } liveMods) return Mo2DeferredView<ScenarioInstalledPage>.For(liveMods, "My Mods", model => new Mo2ModsView { ViewModel = model }, reuseBody: true);
        if (viewModel is not IViewModel vm) return null;
        var viewType = ResolveNativeViewType(vm.ViewModelInterface);
        var view = (IViewFor)Activator.CreateInstance(viewType)!;
        // Profiles and Health Check are drawn by native NMA views with no frontend
        // wrapper to set their chrome up, which left them as the only game pages
        // without the shared padding and header separator.
        if (viewModel is Mo2HealthPage or Mo2LoadoutsPage { GameScoped: true } && view is Control native)
            Mo2PanelChrome.Adopt(native);
        if (view is IViewContract vc && contract is not null) vc.ViewContract = contract;
        if (view is NexusMods.App.UI.Controls.Spine.Buttons.Icon.IconButton homeIcon && contract == "Home")
            homeIcon.AttachedToVisualTree += (_,_) => Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                foreach (var icon in homeIcon.GetVisualDescendants().OfType<NexusMods.UI.Sdk.Icons.UnifiedIcon>()) icon.Value = new NexusMods.UI.Sdk.Icons.ProjektankerIcon("mdi-home");
                foreach (var border in homeIcon.GetVisualDescendants().OfType<Border>().Where(x => x.Name == "IconButtonInnerBorder")) border.CornerRadius = new CornerRadius(8);
            });
        return view;
    }
}

public partial class MockApp : Application
{
    public override void Initialize()
    {
        if (Environment.GetEnvironmentVariable("MO2_UI_TEMPLATE_TIMING") == "1") Mo2TemplateTiming.Install();
        AvaloniaXamlLoader.Load(this);
        // After the theme, so the sizes a Qt list is read at replace the card-sized
        // ones the borrowed theme draws.
        Mo2Density.Install(Styles);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        Locator.CurrentMutable.UnregisterCurrent(typeof(IViewLocator));
        Locator.CurrentMutable.RegisterConstant<IViewLocator>(new FixtureViewLocator());
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Environment.GetEnvironmentVariable("MO2_FIXTURES") != "1") {
                var endpoint = Environment.GetEnvironmentVariable("MO2_BRIDGE_DIRECTORY") ?? "";
                var live = new Mo2LiveWorkspace(endpoint);
                Window liveWindow;
                using (Mo2StartupCheck.Phase("window")) liveWindow = live.CreateWindow();
                liveWindow.Opened += (_, _) => Mo2StartupCheck.WindowOpened(liveWindow, live);
                // The eight keys MO2 gives its own actions, which this window had none of.
                Mo2QtShortcuts.Install(liveWindow, live);
                desktop.MainWindow = liveWindow;
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SAVE_HOVER") is { Length: > 0 } hoverPhase)
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2SaveHoverCheck.Run(live, liveWindow, hoverPhase); }
                        catch (Exception error) { Console.WriteLine("FAIL save hover UI: " + error); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_INSTALL_MOD") is { Length: > 0 } archivePath) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2InstallModCheck.Run(live, liveWindow, archivePath); }
                        catch (Exception error) { Console.WriteLine("FAIL install mod: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_MOD_INFO") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2ModInfoCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL mod information: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_TAB_CLOSE") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        using var turn = await Mo2CheckTurn.Take();
                        try { await Mo2TabCloseCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL tab close: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_LIST_BUTTONS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2ListButtonsCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL list buttons: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SELECT_GAME") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        using var turn = await Mo2CheckTurn.Take();
                        try { await Mo2SelectGameCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL select game: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SETTINGS_PAGE") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        using var turn = await Mo2CheckTurn.Take();
                        try { await Mo2SettingsPageCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL settings page: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_ADD_GAME") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        using var turn = await Mo2CheckTurn.Take();
                        try { await Mo2AddGameCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL add game: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_GAME_WIDGET") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        using var turn = await Mo2CheckTurn.Take();
                        try { await Mo2GameWidgetCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL game widget: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_PLUGIN_RENDER") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2PluginRenderCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL plugin render: " + error); }
                    };
#if DEBUG
                if (Environment.GetEnvironmentVariable("MO2_DEVELOPER_TOOLS") == "1") liveWindow.AttachDevTools();
#endif
                if (endpoint.Length == 0) live.ShowHome();
                liveWindow.Opened += (_, _) => Mo2WelcomeOverlay.ShowIfFirstRun(live);
                desktop.Exit += (_, _) => live.Dispose();
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_FILE_PAGE_LIFECYCLE") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2FilePageLifecycleCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL file page lifecycle: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SAVE_DELETE_CANCEL") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2SaveDeleteCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL save delete cancellation: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SAVES_POLLING") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2SavesPollingCheck.Run(live, liveWindow, Environment.GetEnvironmentVariable("MO2_SAVES_POLL_PROBE")); }
                        catch (Exception error) { Console.WriteLine("FAIL Saves polling: " + error); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SAVE_DELETE_OUTCOME") is { Length: > 0 } saveDeleteSpec)
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2SaveDeleteOutcomeCheck.Run(live, liveWindow, saveDeleteSpec); }
                        catch (Exception error) { Console.WriteLine("FAIL save delete outcome: " + error); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SEARCH_FOCUS") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2SearchFocusCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL search focus: " + error); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SIDEBAR_TOGGLE") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2SidebarToggleCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL sidebar toggle: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_KEYBOARD_SEARCH") is { } keyboardSearchPath) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2KeyboardSearchCheck.Run(liveWindow, keyboardSearchPath); }
                        catch (Exception error) { Console.WriteLine("FAIL desktop keyboard search: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SEARCH_INPUT") == "1")
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2SearchInputCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL search input: " + error.Message); }
                    }, TimeSpan.FromSeconds(3));
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_EXTERNAL_FILES_LIFECYCLE") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        using var turn = await Mo2CheckTurn.Take();
                        try { await Mo2ExternalFilesLifecycleCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL External Files lifecycle: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_TOOLS_LIFECYCLE") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2ToolsLifecycleCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL Tools lifecycle: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_HEALTH_LIFECYCLE") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2HealthLifecycleCheck.Run(live); }
                        catch (Exception error) { Console.WriteLine("FAIL Health lifecycle: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_NOTIFICATION_FIT") is { Length: > 0 } notificationFit) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2NotificationFitCheck.Run(live, notificationFit); }
                        catch (Exception error) { Console.WriteLine("FAIL notification fit: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_TOGGLE_LIFECYCLE") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2ToggleLifecycleCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL toggle lifecycle: " + error); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_PAGE_REUSE") == "1") {
                    // Deferred bodies are built from a dispatcher post, and this ran the
                    // moment the window opened — while the workspace was still building
                    // its first rows, which is seconds of saturated dispatcher. The post
                    // it was waiting five seconds for could not run in them, so it
                    // reported that a deferred page never loaded when nothing had asked
                    // it to yet. Taking a turn puts it after the app has settled.
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { using var turn = await Mo2CheckTurn.Take(); await Mo2PageReuseCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL page reuse lifecycle: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_LAYOUT_PRESET") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2LayoutPresetCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL layout preset: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_WORKSPACE_CACHE") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2RetainedViewsCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL workspace cache: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_PROFILE_BUFFER") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try {
                            await Mo2ProfileBufferCheck.Run(live, liveWindow);
                            if (Environment.GetEnvironmentVariable("MO2_VERIFY_WORKSPACE_CACHE") == "1")
                                await Mo2RetainedViewsCheck.Live(live, liveWindow);
                        }
                        catch (Exception error) { Console.WriteLine("FAIL profile buffer: " + error); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_FILTERED_ROWS") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2FilteredRowsCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL filtered rows: " + error); }
                    };
                // Registered, so the screenshot path waits for it. It settles the
                // panels before measuring them, which takes longer than the shutdown
                // timer allowed: the run was cut off before it printed anything and
                // read as a check that had stopped saying anything.
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_PAIRED_PANELS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2DeferredPresentationCheck.CheckPairedWorkspace(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL paired panels: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                // Registered for the same reason: it runs the tab-restoration check as
                // its second stage, which walks four window widths and both tabs.
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DEFERRED_PANELS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try {
                            await Mo2DeferredPresentationCheck.CheckRowLifecycle(liveWindow);
                            await Mo2TabRestoreCheck.Run(live, liveWindow);
                            await Mo2SortedRootsUiCheck.Run(live, liveWindow);
                            Console.WriteLine("PASS deferred panels: repeated tab/size changes and live selection/sorting");
                        } catch (Exception error) { Console.WriteLine("FAIL deferred panels: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SORTED_ROOTS_UI") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2SortedRootsUiCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL sorted roots UI: " + error); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SEPARATOR_INTERACTIONS") == "1") {
                    // This one makes a separator in MO2 and takes it away again, so the
                    // run has to last until it has. Ungated, the screenshot path shut
                    // the app down through it and left the fixture behind — and the
                    // next run then refused to start, correctly, because a fixture it
                    // did not make was sitting there.
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2SeparatorInteractionCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL separator interactions: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_ORDER_PROVIDER") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2OrderProviderCheck.Run(); }
                        catch (Exception error) { Console.WriteLine("FAIL plugin provider: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_UI_LATENCY_REPORT") is { Length: > 0 } latencyReport)
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2UiLatencyProbe.Run(live, liveWindow, latencyReport); }
                        catch (Exception error) { Console.WriteLine("UI latency probe failed: " + error.GetType().Name); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_TEMPLATE_PREPARATION") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2TemplatePreparationCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL template preparation: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_ALERT_LIFECYCLE") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2AlertLifecycleCheck.Run(liveWindow); await Mo2AlertLifecycleCheck.Live(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL help lifecycle: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_ICON_ALIASES") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2IconAliasesCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL icon aliases: " + error); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_MOD_POINTER_DRAG") is { Length: > 0 } dragPhasePath) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2ModPointerDragCheck.Run(live, liveWindow, dragPhasePath); }
                        catch (Exception error) { Console.WriteLine("FAIL mod pointer drag: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_PLUGIN_POINTER_DRAG") is { Length: > 0 } pluginDragPhasePath) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2PluginPointerDragCheck.Run(live, liveWindow, pluginDragPhasePath); }
                        catch (Exception error) { Console.WriteLine("FAIL plugin pointer drag: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_ENTRY_MENUS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2EntryMenuCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL lazy entry menus: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(3));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_PLUGIN_ROW") == "1")
                    liveWindow.Opened += (_,_) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2PluginRowCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL plugin row: " + error.Message); }
                    }, TimeSpan.FromSeconds(3));
                // Registered, and this one had to be: unregistered it was shut down
                // part-way through, which skipped the finally that takes its separator
                // fixture back out of MO2. The fixture then stayed in the live profile
                // and wedged every later run. A check that changes the host has to be
                // waited for, or it changes the host and does not change it back.
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_INSTALLED_INTERACTIONS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_,_) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2InstalledInteractionCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL installed interactions: " + error.Message + "\n" + error.StackTrace); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(3));
                }
                // Registered, like every other check started from the window opening.
                // Without a turn the screenshot path does not wait for it, and its
                // three-second timer put it on the wrong side of that: it was shut
                // down part-way on every run and printed nothing at all, which
                // verify.sh reported as NO VERDICT for a check that was never given
                // the chance to have one.
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_TAB_POINTER") is { } tabPointerPath) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2TabPointerCheck.Run(live, liveWindow, tabPointerPath); }
                        catch (Exception error) { Console.WriteLine("FAIL tab pointer: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_TAB_RESTORE") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_,_) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2TabRestoreCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL tab restoration: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(3));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_TRADITIONAL_UI") == "1")
                    liveWindow.Opened += (_,_) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2TraditionalUiCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL traditional UI: " + error.Message); }
                    }, TimeSpan.FromSeconds(3));
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_BROWSER_SSO") == "1")
                    liveWindow.Opened += (_,_) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2LoginPopupCheck.Live(liveWindow, live); }
                        catch { Console.WriteLine("FAIL browser SSO verification; authorization or native account confirmation did not complete"); }
                    }, TimeSpan.FromSeconds(2));
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SHARED_LOGOUT") == "1")
                    liveWindow.Opened += (_,_) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2SharedNexusLogoutCheck.Live(live, desktop); }
                        catch { Console.WriteLine("FAIL shared logout UI verification; inspect native account state before retrying"); }
                    }, TimeSpan.FromSeconds(2));
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_OVERWRITE_TOOLBAR") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try {
                            using var turn = await Mo2CheckTurn.Take();
                            await WaitFor(() => live.Profile.IsConnected, "MO2 profile connected");
                            var view = new Mo2OverwriteView(_ => Task.FromResult<Mo2OverwriteFile[]>([
                                new("generated/config.ini", 120), new("output.log", 300)])) {
                                ViewModel = new Mo2OverwritePage(new FixtureWindows { ActiveWindow = live }, live.Profile)
                            };
                            var checkWindow = new Window { Width = 650, Height = 650, Content = view, ShowInTaskbar = false };
                            checkWindow.Show();
                            try { await Mo2ToolbarSearchCheck.Run(checkWindow, view); }
                            finally { checkWindow.Close(); }
                        } catch (Exception error) { Console.WriteLine("FAIL Overwrite toolbar: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_OVERWRITE_REFRESH") == "1")
                    liveWindow.Opened += (_,_) => DispatcherTimer.RunOnce(async () => {
                        try {
                            await WaitFor(() => live.Profile.IsConnected && live.ModsPage?.Adapter.SourceCount.Value > 0, "MO2 mod list did not connect");
                            await VerifyNativeMods(live, liveWindow);
                            await VerifyOverwriteRefresh(live, liveWindow);
                        } catch (Exception error) { Console.WriteLine("FAIL Overwrite verification: " + error.Message + "\n" + error.StackTrace); }
                    }, TimeSpan.FromSeconds(2));
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_COLLECTION_RENAME") == "1")
                    liveWindow.Opened += (_,_) => DispatcherTimer.RunOnce(async () => {
                        try { await VerifyLiveCollectionRename(live, desktop); }
                        catch (Exception error) { Console.WriteLine("FAIL collection rename verification: " + error.Message); }
                    }, TimeSpan.FromSeconds(2));
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_LOGIN_POPUP") == "1")
                    liveWindow.Opened += (_,_) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2LoginPopupCheck.Run(liveWindow, live); }
                        catch (Exception error) { Console.WriteLine("FAIL login popup verification: " + error.Message); }
                    }, TimeSpan.FromSeconds(1));
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_LOGIN_BUTTON") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2LoginButtonCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL login button: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_MODS_SELECTION") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        // The check does its own waiting and reports what it saw, so no
                        // gate here: a gate only hides which part never arrived.
                        try { await Mo2ModSelectionCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL mods selection group: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(4));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_COLUMN_TOGGLE") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2ColumnToggleCheck.Run(); }
                        catch (Exception error) { Console.WriteLine("FAIL column toggle: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_REACHABLE_ACTIONS") == "1") {
                    // Builds seven pages and waits for each to lay out, which takes
                    // longer than the screenshot path waits before shutting down.
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2ReachableActionsCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL reachable actions: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_PHYSICALITY") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2PhysicalityCheck.Run(liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL physicality: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_PANEL_CHROME") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2PanelChromeCheck.Run(); }
                        catch (Exception error) { Console.WriteLine("FAIL panel chrome: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_ROW_STYLE") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2RowStyleCheck.Run(live, liveWindow, Environment.GetEnvironmentVariable("MO2_ROW_STYLE_SCREENSHOTS")); }
                        catch (Exception error) { Console.WriteLine("FAIL row style: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(2));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_QT_WIDGETS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2QtWidgetCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL MO2 widgets: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(2));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DEAD_CONTROLS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2DeadControlsCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL dead controls: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(2));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_EXTRA_BUTTONS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2ExtraButtonsCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL MO2 page buttons: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(2));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_QT_SHORTCUTS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2QtShortcutCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL MO2 shortcuts: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(2));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_QT_ACTIONS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2QtActionCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL MO2 actions: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(2));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SHARED_LISTS") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2SharedListCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL shared list pages: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(2));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_SELECTION_PARITY") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        try { await Mo2SelectionParityCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL selection parity: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    }, TimeSpan.FromSeconds(2));
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_GAME_SWITCH") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2GameSwitchCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL game switch: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DOWNLOAD_TRANSFER_FIT") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2DownloadTransferFitCheck.Run(); }
                        catch (Exception error) { Console.WriteLine("FAIL download transfer fit: " + error); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_LOGS_LIFECYCLE") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2LogsLifecycleCheck.Run(live); }
                        catch (Exception error) { Console.WriteLine("FAIL Logs lifecycle: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_VISIBILITY") is { } visibilitySpecification) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2DataVisibilityCheck.Run(live, visibilitySpecification); }
                        catch (Exception error) { Console.WriteLine("FAIL Data visibility: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_NATIVE_TOOL_DIALOG") is { } toolSpecification) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2NativeToolDialogCheck.Run(live, toolSpecification); }
                        catch (Exception error) { Console.WriteLine("FAIL native tool dialog: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_PREVIEW") is { } previewSpecification) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2DataPreviewCheck.Run(live, previewSpecification); }
                        catch (Exception error) { Console.WriteLine("FAIL Data preview: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_ACTIVATION") is { } activationSpecification) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2DataActivationCheck.Run(live, activationSpecification); }
                        catch (Exception error) { Console.WriteLine("FAIL Data activation: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_EXPORT") is { } exportDestination) {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2DataExportCheck.Run(live, exportDestination); }
                        catch (Exception error) { Console.WriteLine("FAIL Data export: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_SEARCH") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2DataSearchCheck.Run(live); await Mo2DataSearchCheck.RunNative(live); }
                        catch (Exception error) { Console.WriteLine("FAIL Data search: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_TREE") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2DataTreeCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL Data tree: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_VISIBLE_NATIVE_FILTERS") == "1") {
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2NativeFilterUiCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL visible native filters: " + error); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_NATIVE_FILTER_CACHE") == "1") {
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2NativeFilterCacheCheck.Run(); }
                        catch (Exception error) { Console.WriteLine("FAIL native filter cache: " + error.Message); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_NATIVE_FILTERS") == "1") {
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2NativeFilterCheck.Run(live); }
                        catch (Exception error) { Console.WriteLine("FAIL native filter adapter: " + error.Message); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_CATEGORY_TREE") == "1") {
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2CategoryTreeCheck.Run(); }
                        catch (Exception error) { Console.WriteLine("FAIL category tree UI: " + error.Message); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_CATEGORY_UI") == "1") {
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2WidgetBehaviourCheck.Run(live, liveWindow, categoriesOnly: true); }
                        catch (Exception error) { Console.WriteLine("FAIL native category UI: " + error.Message); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_QT_BAR_FIT") == "1") {
                    Mo2CheckTurn.Expect();
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2QtBarFitCheck.Run(Environment.GetEnvironmentVariable("MO2_QT_BAR_SCREENSHOTS")); }
                        catch (Exception error) { Console.WriteLine("FAIL Qt bar fit: " + error.Message); }
                        finally { Mo2CheckTurn.Finished(); }
                    };
                }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_FOLDER_PAGES") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2FolderPagesCheck.Run(live, liveWindow); }
                        catch (Exception error) { Console.WriteLine("FAIL folder pages: " + error.Message); }
                    };
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_COMPONENTS") == "1")
                    liveWindow.Opened += async (_, _) => {
                        try { await Mo2ComponentsCheck.Run(live, liveWindow, Environment.GetEnvironmentVariable("MO2_COMPONENTS_SCREENSHOT")); }
                        catch (Exception error) { Console.WriteLine("FAIL components gallery: " + error.Message); }
                    };
                // Capture aid: the responsive columns need more width than the Mods
                // panel gets while sharing the window, and the window cannot exceed
                // the display. Closing the other panels gives it the whole width.
                if (Environment.GetEnvironmentVariable("MO2_SINGLE_PANEL") == "1")
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(() => {
                        try {
                            live.ShowProfile();
                            var workspace = live.WorkspaceController.ActiveWorkspace;
                            // Keep the panel holding Mods, close the rest.
                            var keep = workspace.Panels.FirstOrDefault(panel =>
                                panel.Tabs.Any(tab => tab.Contents.ViewModel is ScenarioInstalledPage)) ?? workspace.Panels.First();
                            foreach (var panel in workspace.Panels.Where(x => x.Id != keep.Id).ToArray())
                                live.WorkspaceController.ClosePanel(workspace.Id, panel.Id);
                            Console.WriteLine($"CHECK single panel: {workspace.Panels.Count} panel(s) remain");
                        } catch (Exception error) { Console.WriteLine("CHECK single panel failed: " + error.Message); }
                    }, TimeSpan.FromSeconds(8));
                if (Environment.GetEnvironmentVariable("MO2_SCREENSHOT") is { } liveScreenshot)
                    liveWindow.Opened += (_, _) => DispatcherTimer.RunOnce(async () => {
                        // Guarded as a whole, because none of the sixty-one checks below
                        // was guarded at all. Every other block in this file wraps its
                        // check and prints a named FAIL; these were bare awaits, so an
                        // exception in one went unhandled, took the process down, and
                        // left verify.sh reporting NO VERDICT for a check that had in
                        // fact failed definitely and for a stateable reason. Six of them
                        // did exactly that in one sweep — VerifyLiveHistory threw a
                        // NullReferenceException and the log said only that the app had
                        // crashed. One check is enabled per run, so naming the enabled
                        // one turns that back into a verdict.
                      try {
                        // Checks hooked outside this block drive the same tables and are
                        // started by the same window opening, so they are still running
                        // when this timer fires. Wait for the ones that said they would
                        // run, so this path gates, screenshots and shuts down after them
                        // rather than through them.
                        await Mo2CheckTurn.Settled(TimeSpan.FromMinutes(5));
                        // Wait for MO2's tables, not for which page happens to be on
                        // screen. ModsPage and PluginsPage resolve to whatever page is in
                        // an open tab, so a check that navigates the panel — which
                        // MO2_VERIFY_QT_WIDGETS does by design, walking all five tabs —
                        // leaves one of them null and this gate waiting for a page that is
                        // no longer open. It then threw on the dispatcher and aborted the
                        // process part-way through the audit, discarding every check queued
                        // behind it while still having printed PASS for the ones before.
                        // An open page must still be populated; a page that is not open is
                        // judged by the profile rows it would have been built from.
                        if (endpoint.Length > 0) try {
                            // Not merely non-empty, but done filling. A list still being
                            // populated satisfies "more than nothing" on its first row, and
                            // the screenshot this gate exists to protect caught a Plugins
                            // panel showing five of eleven — evidence of a window that the
                            // window never actually looked like. Counts that have stopped
                            // moving are the readable signal; the totals themselves cannot
                            // be predicted here, since separators and Overwrite put the mod
                            // table's own count legitimately apart from the profile's.
                            var steady = 0;
                            (int Mods, int Plugins) last = (-1, -1);
                            await WaitFor(() => {
                                if (!live.Profile.IsConnected || live.Profile.ProfilePath.Length == 0) return false;
                                if (Environment.GetEnvironmentVariable("MO2_VERIFY_LAUNCH") is not null ||
                                    Environment.GetEnvironmentVariable("MO2_VERIFY_DOWNLOAD_CONTEXT") == "1") return true;
                                var now = (live.ModsPage is { } modsPage ? modsPage.Adapter.SourceCount.Value : live.Profile.Mods.Count,
                                           live.PluginsPage is { } pluginsPage ? pluginsPage.Adapter.SourceCount.Value : live.Profile.Order.Plugins.Count);
                                if (now.Item1 == 0 || now.Item2 == 0) { steady = 0; last = now; return false; }
                                steady = now == last ? steady + 1 : 0;
                                last = now;
                                return steady >= 4;
                            }, "Live MO2 profile or required tables did not settle", 30);
                        } catch (Exception error) {
                            Console.WriteLine("FAIL live gate: " + error.Message);
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_DIALOG_QUEUE") == "1") await Mo2DialogQueueCheck.Run();
                                if (Environment.GetEnvironmentVariable("MO2_VERIFY_STARTUP") == "1") {
                            try { await Mo2StartupCheck.Run(live, liveWindow); }
                            catch (Exception error) { Console.WriteLine("FAIL startup: " + error.Message); }
                        }
                if (Environment.GetEnvironmentVariable("MO2_VERIFY_REFRESH_CADENCE") == "1") {
                            try { await Mo2RefreshCadenceCheck.Run(live.Profile); }
                            catch (Exception error) { Console.WriteLine("FAIL refresh cadence: " + error.Message); }
                        }
                        // Awaited here rather than run from the window opening: it walks a
                        // panel through thirteen sizes, which takes longer than the
                        // screenshot path waits before shutting the app down.
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_HEADER_FIT") == "1") {
                            try { await Mo2HeaderFitCheck.Run(Environment.GetEnvironmentVariable("MO2_HEADER_FIT_DIRECTORY")); }
                            catch (Exception error) { Console.WriteLine("FAIL header fit: " + error.Message); }
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_ROW_PADDING") == "1") {
                            try { await Mo2RowPaddingCheck.Live(live, liveWindow); await Mo2ToolbarSearchCheck.Run(liveWindow); }
                            catch (Exception error) { Console.WriteLine("FAIL row padding (live): " + error.Message); }
                        }
                        // Awaited here, like the page audit: it walks every page and
                        // measures the rows each one draws, which takes longer than the
                        // screenshot path waits before shutting the app down.
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_WIDGET_BEHAVIOUR") == "1") {
                            try { await Mo2WidgetBehaviourCheck.Run(live, liveWindow); }
                            catch (Exception error) { Console.WriteLine("FAIL MO2 widget behaviour: " + error.Message); }
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_ROW_MENUS") == "1") {
                            try { await Mo2RowMenuCheck.Run(live, liveWindow); }
                            catch (Exception error) { Console.WriteLine("FAIL MO2 row menus: " + error.Message); }
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PRESET_FIT") == "1") {
                            try { await Mo2PresetFitCheck.Run(live, liveWindow, Environment.GetEnvironmentVariable("MO2_PRESET_FIT_DIRECTORY")); }
                            catch (Exception error) { Console.WriteLine("FAIL MO2 preset fit: " + error.Message); }
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_SEPARATOR_COLOR") == "1") {
                            try { await Mo2SeparatorColorCheck.Run(live, liveWindow); }
                            catch (Exception error) { Console.WriteLine("FAIL separator colour: " + error.Message); }
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_DENSITY") == "1") {
                            try { await Mo2DensityCheck.Run(live, liveWindow); }
                            catch (Exception error) { Console.WriteLine("FAIL MO2 density: " + error.Message); }
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_PAGE_AUDIT") == "1") {
                            try { await Mo2PageAuditCheck.Run(live, liveWindow, Environment.GetEnvironmentVariable("MO2_PAGE_AUDIT_DIRECTORY")); }
                            catch (Exception error) { Console.WriteLine("FAIL page audit: " + error.Message); }
                        }
                        // Last of the opt-in checks, and after the window's own gate: it
                        // navigates to Home and splits panels, so running it earlier
                        // starves the gate that waits for the profile's tables and leaves
                        // every check after it looking at a workspace it rearranged.
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_TAB_DRAG") == "1") {
                            try { await Mo2TabDragDropCheck.Run(live, liveWindow); }
                            catch (Exception error) { Console.WriteLine("FAIL tab drag and drop: " + error.Message); }
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_WORKSPACE_INPUT") == "1") await VerifyWorkspaceInput(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_ARCHIVES") == "1") await VerifyArchives(live,liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_DOWNLOADS_TOOLBAR_SEARCH") == "1") {
                            try {
                                live.ShowProfile();
                                await live.ProfileMenu.LeftMenuItemLibrary.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
                                await WaitFor(() => liveWindow.GetVisualDescendants().OfType<Mo2DownloadsView>().Any(x => x.IsEffectivelyVisible), "Downloads page visible");
                                var downloads = liveWindow.GetVisualDescendants().OfType<Mo2DownloadsView>().Single(x => x.IsEffectivelyVisible);
                                if (Environment.GetEnvironmentVariable("MO2_VERIFY_DOWNLOADS_COLUMNS") == "1") await Mo2DownloadsColumnsCheck.Run(downloads);
                                await Mo2ToolbarSearchCheck.Run(liveWindow, downloads);
                            } catch (Exception error) { Console.WriteLine("FAIL Downloads toolbar search: " + error); }
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_DATA_TOOLBAR_SEARCH") == "1") {
                            try {
                                live.ShowProfile();
                                await live.ProfileMenu.DataItem.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
                                await WaitFor(() => liveWindow.GetVisualDescendants().OfType<Mo2DataView>().Any(x => x.IsEffectivelyVisible), "Data page visible");
                                var data = liveWindow.GetVisualDescendants().OfType<Mo2DataView>().Single(x => x.IsEffectivelyVisible);
                                await Mo2ToolbarSearchCheck.Run(liveWindow, data);
                            } catch (Exception error) { Console.WriteLine("FAIL Data toolbar search: " + error); }
                        }
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_ARCHIVES_SEARCH") == "1") {
                            try {
                                live.ShowProfile();
                                await live.ProfileMenu.ArchivesItem.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
                                await WaitFor(() => liveWindow.GetVisualDescendants().OfType<Mo2ArchivesView>().Any(x => x.IsEffectivelyVisible), "Archives page visible");
                                var archives = liveWindow.GetVisualDescendants().OfType<Mo2ArchivesView>().Single(x => x.IsEffectivelyVisible);
                                await Mo2ToolbarSearchCheck.Run(liveWindow, archives);
                            } catch (Exception error) { Console.WriteLine("FAIL Archives toolbar search: " + error); }
                        }

                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_NEW_UI") == "1") await VerifyNewUi(live, liveWindow);
                        if (Environment.GetEnvironmentVariable("MO2_VERIFY_FINAL_PAGES") == "1") await VerifyFinalPages(live, liveWindow);
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
                            live.ShowProfile();
                            await WaitFor(() => liveWindow.GetVisualDescendants().OfType<Mo2LaunchPanel>().Any() ||
                                liveWindow.GetVisualDescendants().OfType<Button>().Any(button => button.Name == "CompactLaunchToolsButton"), "Game launch controls did not appear");
                            var compactTools = liveWindow.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Name == "CompactLaunchToolsButton");
                            var compactFlyout = compactTools?.Flyout as Flyout;
                            if (compactFlyout is not null) compactFlyout.ShowAt(compactTools!);
                            var launchPanel = liveWindow.GetVisualDescendants().OfType<Mo2LaunchPanel>().FirstOrDefault()
                                ?? compactFlyout?.Content as Mo2LaunchPanel
                                ?? throw new InvalidOperationException("Game launch picker is unavailable");
                            await WaitFor(() => launchPanel.Model.CanLaunch, "Game launch control did not become ready");
                            launchPanel.Executable.SelectedItem = executable;
                            if (launchPanel.Model.SelectedExecutable != executable || !launchPanel.Model.CanLaunch)
                                throw new InvalidOperationException("Native launch control cannot select this executable");
                            compactFlyout?.Hide();
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
                        // Capture aid: open a named profile page before the shot so the
                        // file pages can be inspected, not just whatever was restored.
                        if (Environment.GetEnvironmentVariable("MO2_OPEN_PAGE") is { } wanted) {
                            live.ShowProfile();
                            var menu = live.ProfileMenu;
                            var item = wanted switch {
                                "external" => menu.ExternalFilesItem, "archives" => menu.ArchivesItem,
                                "saves" => menu.SavesItem, "data" => menu.DataItem,
                                "tools" => menu.ToolsItem, "overwrite" => menu.OverwriteItem,
                                _ => menu.LogsItem };
                            await item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
                            await Task.Delay(4000);
                            foreach (var panel in live.WorkspaceController.ActiveWorkspace.Panels)
                            foreach (var tab in panel.Tabs)
                                if (tab.Header.Title != tab.Contents.ViewModel.TabTitle)
                                    throw new InvalidOperationException("Navigated tab caption differs from its page title");
                            Console.WriteLine("PASS page navigation: tab captions match their current page titles after settling");
                        }
                        if (Environment.GetEnvironmentVariable("MO2_SINGLE_PANEL") == "1") {
                            try {
                                var single = live.WorkspaceController.ActiveWorkspace;
                                var keep = single.Panels.FirstOrDefault(panel =>
                                    panel.Tabs.Any(tab => tab.Contents.ViewModel is ScenarioInstalledPage)) ?? single.Panels.First();
                                foreach (var panel in single.Panels.Where(x => x.Id != keep.Id).ToArray())
                                    live.WorkspaceController.ClosePanel(single.Id, panel.Id);
                                await Task.Delay(1500);
                                Console.WriteLine($"CHECK single panel: {single.Panels.Count} panel(s) remain");
                            } catch (Exception error) { Console.WriteLine("CHECK single panel failed: " + error.Message); }
                        }
                        await Task.Delay(500);
                        foreach (var table in liveWindow.GetVisualDescendants().OfType<TreeDataGrid>())
                            Console.WriteLine($"TABLE: source={table.Source?.Items.Cast<object>().Count()} rows={table.Rows?.Count} bounds={table.Bounds} visualRows={table.GetVisualDescendants().Count(x => x is Avalonia.Controls.Primitives.TreeDataGridRow)}");
                        using var bitmap = new RenderTargetBitmap(new PixelSize((int)liveWindow.ClientSize.Width, (int)liveWindow.ClientSize.Height));
                        bitmap.Render(liveWindow);
                        bitmap.Save(liveScreenshot);
                      } catch (Exception error) {
                        var enabled = Environment.GetEnvironmentVariables().Keys.OfType<string>()
                            .Where(x => x.StartsWith("MO2_VERIFY_", StringComparison.Ordinal))
                            // MO2_VERIFY_OUTPUT is where verify.sh puts its logs and
                            // MO2_VERIFY_ATTENDED is its promise that someone is
                            // watching. Neither names a check, and both were being
                            // reported as one.
                            .Where(x => x is not ("MO2_VERIFY_OUTPUT" or "MO2_VERIFY_ATTENDED" or "MO2_VERIFY_TIMEOUT"))
                            .Select(x => x["MO2_VERIFY_".Length..]).OrderBy(x => x).ToArray();
                        Console.WriteLine($"FAIL {(enabled.Length > 0 ? string.Join("+", enabled) : "live checks")}: " +
                            error.Message + "\n" + error.StackTrace);
                      }
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
#if DEBUG
            if (Environment.GetEnvironmentVariable("MO2_DEVELOPER_TOOLS") == "1") window.AttachDevTools();
#endif
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
                    // Guarded, like the live block. A fixture check that threw took the
                    // process down unhandled and left the run with no verdict, which
                    // reads the same as a check that asserted nothing.
                    try { await verification; }
                    catch (Exception error) {
                        var enabled = Environment.GetEnvironmentVariables().Keys.OfType<string>()
                            .Where(x => x.StartsWith("MO2_VERIFY_", StringComparison.Ordinal))
                            .Where(x => x is not ("MO2_VERIFY_OUTPUT" or "MO2_VERIFY_ATTENDED" or "MO2_VERIFY_TIMEOUT"))
                            .Select(x => x["MO2_VERIFY_".Length..]).OrderBy(x => x).ToArray();
                        Console.WriteLine($"FAIL {(enabled.Length > 0 ? string.Join("+", enabled) : "fixture checks")}: " +
                            error.Message + "\n" + error.StackTrace);
                    }
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
        await WaitFor(() => live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count(x => !x.IsOverwrite && !x.IsSeparator), "Cloned profile rows did not activate");
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

    private static async Task VerifyArchives(Mo2LiveWorkspace live,Window window)
    {
        async Task Click(Control control) {
            window.Activate();
            await Task.Delay(400);
            var point = control.PointToScreen(new Point(Math.Min(control.Bounds.Width/2,70),control.Bounds.Height/2));
            Console.WriteLine($"ARCHIVE POINTER: {control.Name ?? control.GetType().Name} at {point.X},{point.Y}");
            var start = new System.Diagnostics.ProcessStartInfo("python3") { UseShellExecute = false };
            start.ArgumentList.Add("/home/deck/mo2/frontend/artifacts/pointer-check.py"); start.ArgumentList.Add(point.X.ToString()); start.ArgumentList.Add(point.Y.ToString());
            using var process = System.Diagnostics.Process.Start(start)!; await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new Exception("Pointer helper failed");
        }
        var item = window.GetVisualDescendants().OfType<NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView>().Single(x => x.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Archives"));
        await Click(item);
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ArchivesView>().Any(),"Archives sidebar did not open");
        var view = window.GetVisualDescendants().OfType<Mo2ArchivesView>().First();
        var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        await WaitFor(() => table.Rows?.Count > 0,"Native archives did not load");
        var count = table.Rows!.Count;
        var filter = view.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "ArchivesFilter");
        filter.Text = "Fallout - Misc.bsa";
        await WaitFor(() => table.Rows?.Count == 1,"Archive filter mismatch");
        var label = table.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Text == filter.Text);
        await Click(label);
        var browse = view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "BrowseArchive");
        if (!browse.IsEnabled) throw new Exception("Archive selection did not enable Browse");
        await Click(browse);
        await WaitFor(() => File.Exists("/tmp/mo2-bsa-preview-verified"),"Original BSA preview was not verified",seconds:60);
        await WaitFor(() => !live.Profile.ManagingMod && live.Profile.IsConnected,"Archive preview did not return to frontend",seconds:30);
        filter.Text = "";
        await WaitFor(() => table.Rows?.Count == count,"Archive listing did not restore");
        Console.WriteLine($"PASS: real pointer Archives navigation, {count} native archives, filtering, selection, original BSA preview and frontend recovery");
    }

    private static async Task VerifyWorkspaceInput(Mo2LiveWorkspace live, Window window)
    {
        // Each lookup says what it was looking through. All three were bare Single()
        // calls reporting "Sequence contains no matching element", which named
        // neither the endpoint, the profile, nor which of the three had missed.
        var catalog = live.Catalog.Read().ToArray();
        var entry = catalog.FirstOrDefault(x => x.Registration.Endpoint == live.Profile.Endpoint)
            ?? throw new InvalidOperationException($"No catalog entry for endpoint {live.Profile.Endpoint} among " +
                string.Join(", ", catalog.Select(x => x.Registration.Endpoint)));
        var known = entry.Instance!.Profiles.ToArray();
        var original = known.FirstOrDefault(x => Path.GetFullPath(x.Directory) == Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath))
            ?? throw new InvalidOperationException($"No profile at {Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath)} among " +
                string.Join(", ", known.Select(x => x.Directory)));
        var test = known.FirstOrDefault(x => x.Name == "Frontend Test")
            ?? throw new InvalidOperationException($"No profile named \"Frontend Test\" among " +
                string.Join(", ", known.Select(x => x.Name)));
        try {
        if (!await live.Profile.SelectProfile(entry.Registration,test)) throw new Exception(live.Profile.Status);
        await Task.Delay(1500);
        async Task Click(Control control) {
            window.Activate();
            var point = control.PointToScreen(new Point(Math.Min(control.Bounds.Width / 2,70),control.Bounds.Height / 2));
            var start = new System.Diagnostics.ProcessStartInfo("python3") { UseShellExecute = false };
            start.ArgumentList.Add("/home/deck/mo2/frontend/artifacts/pointer-check.py"); start.ArgumentList.Add(point.X.ToString()); start.ArgumentList.Add(point.Y.ToString());
            using var process = System.Diagnostics.Process.Start(start)!; await process.WaitForExitAsync();
            if (process.ExitCode != 0) throw new Exception("Pointer helper failed");
        }
        // By the label the sidebar actually draws, and saying what it drew when the
        // name is not among them. This asked for "Mods" where the entry is "My Mods",
        // and reported "Sequence contains no matching element" — which named neither
        // the entry it wanted nor the eleven it could have had.
        async Task Sidebar(string name) {
            var items = window.GetVisualDescendants().OfType<NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView>().ToArray();
            string? Label(NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView view) =>
                view.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).FirstOrDefault(t => !string.IsNullOrEmpty(t));
            var item = items.FirstOrDefault(x => x.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == name))
                ?? throw new InvalidOperationException($"No sidebar entry labelled \"{name}\" among " +
                    string.Join(", ", items.Select(Label).Where(x => x is not null)));
            await Click(item);
        }
        await Sidebar("My Mods");
        var modsView = window.GetVisualDescendants().OfType<Mo2ModsView>().First();
        // Something on the Mods page to click, to prove a pointer press focuses the
        // panel it lands in. This clicked the "Mods" sub-tab label, and reported
        // "Sequence contains no matching element" when it was not there — naming
        // neither the label nor what the page did draw.
        var modsLabels = modsView.NativeView.GetVisualDescendants().OfType<TextBlock>()
            .Where(x => !string.IsNullOrWhiteSpace(x.Text) && x.IsEffectivelyVisible).ToArray();
        // MO2's own "Active:" caption beside its mod count. This clicked the "Mods"
        // sub-tab label, which went with the Mods/Rules strip when the pane was cut
        // back to MO2's widgets — the page draws MO2's captions and column headings
        // now and no sub-tabs at all. What is being proven here is that a pointer
        // press focuses the panel it lands in, so any drawn thing serves; this one is
        // MO2's, is always there, and does nothing when it is clicked.
        var target = modsLabels.FirstOrDefault(x => x.Text == "Active:")
            ?? modsLabels.FirstOrDefault(x => x.Text == "Mod Name")
            ?? throw new InvalidOperationException("No MO2 caption on the mod page to click; it draws " +
                string.Join(", ", modsLabels.Select(x => $"\"{x.Text}\"").Distinct().Take(12)));
        await Click(target);
        if (!modsView.GetVisualAncestors().OfType<PanelView>().Single().ViewModel!.IsSelected) throw new Exception("Mouse did not focus Mods panel");
        await Sidebar("Plugins");
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2PluginsView>().Any(), "Plugins sidebar click did not open Plugins");
        var view = window.GetVisualDescendants().OfType<Mo2PluginsView>().First();
        // An active .esp MO2 will let this toggle. Which plugins MO2 holds is host
        // state, so when there is none this says what there was rather than throwing
        // "Sequence contains no matching element" — the difference between a profile
        // that cannot exercise the check and a frontend that has lost its rows.
        var plugin = live.Profile.Order.Plugins.FirstOrDefault(x => x.CanToggle && x.IsActive && x.DisplayName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("This profile has no active, toggleable .esp to work: " +
                string.Join(", ", live.Profile.Order.Plugins.Select(x => $"{x.DisplayName} (active {x.IsActive}, toggleable {x.CanToggle})")));
        var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        await Task.Delay(300);
        foreach (var pluginScroll in table.GetVisualDescendants().OfType<ScrollViewer>())
            pluginScroll.Offset = new Vector(0,pluginScroll.Extent.Height);
        await WaitFor(() => table.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Text == plugin.DisplayName), "Plugin row not rendered");
        var pluginLabel = table.GetVisualDescendants().OfType<TextBlock>().First(x => x.Text == plugin.DisplayName);
        pluginLabel.BringIntoView(); await Task.Delay(250);
        await Click(pluginLabel);
        await WaitFor(() => view.ViewModel!.Adapter.SelectedModels.Any(x => x.Key.Equals(plugin.Key)), "Pointer plugin selection failed");
        if (!view.GetVisualAncestors().OfType<PanelView>().Single().ViewModel!.IsSelected) throw new Exception("Plugin panel did not highlight");
        try {
            await PluginRowMenu(window, "Disable selected", plugin.DisplayName);
            await WaitFor(() => !live.Profile.Order.Plugins.Single(x => x.Key.Equals(plugin.Key)).IsActive,"Plugin disable failed");
            await PluginRowMenu(window, "Enable selected", plugin.DisplayName);
            await WaitFor(() => live.Profile.Order.Plugins.Single(x => x.Key.Equals(plugin.Key)).IsActive,"Plugin enable failed");
        } finally { await live.Profile.SetPluginsActive([plugin.DisplayName],true); }
        await Sidebar("Profiles");
        await WaitFor(() => window.GetVisualDescendants().OfType<MyLoadoutsView>().Any(), "Profiles click failed");
        var profiles = window.GetVisualDescendants().OfType<MyLoadoutsView>().First().ViewModel as Mo2LoadoutsPage;
        if (profiles?.Game is null || profiles.GameSectionViewModels.Count != 1) throw new Exception("Game Profiles is not scoped to one game");
        await Sidebar("Tools");
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ToolsView>().Any(), "Tools click failed");
        await Task.Delay(1200);
        if (!live.Profile.ExecutableIcons.Values.Any(x => x.Length > 0)) throw new Exception("MO2 executable icons missing");
        var toolsView = window.GetVisualDescendants().OfType<Mo2ToolsView>().First();
        var pinFile = Mo2ToolPins.Path; var existed = File.Exists(pinFile); var saved = existed ? File.ReadAllBytes(pinFile) : null;
        try {
            var pin = toolsView.GetVisualDescendants().OfType<Button>().First(x => x.Content?.ToString() == "Pin");
            await Click(pin);
            await WaitFor(() => window.GetVisualDescendants().OfType<Button>().Any(x => x.Name == "PinnedToolShortcut"), "Pin did not appear above Play");
        } finally { Mo2ToolPins.Save(existed ? System.Text.Json.JsonSerializer.Deserialize<string[]>(saved!)! : []); if (existed) File.WriteAllBytes(pinFile,saved!); else File.Delete(pinFile); }
        await Sidebar("Overwrite");
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2OverwriteView>().Any(), "Overwrite page missing");
        await Task.Delay(600);
        await Sidebar("Logs");
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2LogsView>().Any(), "Logs sidebar click failed");
        var logs = window.GetVisualDescendants().OfType<Mo2LogsView>().First();
        await WaitFor(() => logs.GetVisualDescendants().OfType<ComboBox>().Single(x => x.Name == "Mo2LogFile").ItemCount > 0, "Native log files were not discovered");
        if (!logs.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "Mo2LogOutput").IsReadOnly) throw new Exception("Logs should be read-only");
        await Sidebar("Overwrite");
        var deadRoot = Path.Combine(Path.GetTempPath(),"mo2-stopped-host-" + Guid.NewGuid());
        try {
            var deadEndpoint = Path.Combine(deadRoot,"plugins","data","frontend-bridge");
            Directory.CreateDirectory(Path.Combine(deadEndpoint,"requests"));
            Directory.CreateDirectory(Path.Combine(deadEndpoint,"responses"));
            File.WriteAllText(Path.Combine(deadRoot,"ModOrganizer.exe"),"");
            File.WriteAllText(Path.Combine(deadEndpoint,"endpoint.json"),"{\"protocol\":1,\"session\":\"stopped-test\"}");
            try { await new Mo2BridgeClient(deadEndpoint).SendAsync("snapshot",timeout:TimeSpan.FromSeconds(4)); throw new Exception("Dead host was accepted"); }
            catch (IOException error) when (error.Message.StartsWith("MO2 has stopped")) { }
        } finally { Directory.Delete(deadRoot,true); }
        Console.WriteLine("PASS: real pointer panel focus, Plugins navigation/selection/toggle/restore, scoped Profiles, executable icons, pinned shortcuts, Overwrite navigation");
        Console.WriteLine("PASS: native log files discovered through Logs sidebar; stopped host detected without waiting for dialog timeout");
        static string[] Choices(IWorkspaceViewModel workspace) {
            var data = (PageData)workspace.GetType().GetMethod("GetDefaultPageData")!.Invoke(workspace,null)!;
            return ((NewTabPageContext)data.Context).DiscoveryDetails.Select(x => x.ItemName).ToArray();
        }
        var gameChoices = Choices(live.WorkspaceController.ActiveWorkspace);
        if (gameChoices.Contains("My Games") || gameChoices.Contains("My Loadouts") || !gameChoices.Contains("Logs") || !gameChoices.Contains("Profiles")) throw new Exception("Game page choices leaked Home pages");
        live.ShowHome();
        var homeChoices = Choices(live.WorkspaceController.ActiveWorkspace);
        if (!homeChoices.Contains("My Games") || !homeChoices.Contains("My Loadouts") || homeChoices.Contains("Plugins") || homeChoices.Contains("Logs")) throw new Exception("Home page choices leaked game pages");
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        await spine.LoadoutSpineItems.Single(x => x.Name == "Skyrim Special Edition").Click.Execute();
        await WaitFor(() => live.Profile.IsConnected && live.Profile.GameName.Contains("Skyrim") && !live.Profile.SelectingProfile,"Skyrim icon did not connect",seconds:110);
        if (!live.Profile.ExecutableIcons.Values.Any(x => x.Length > 0)) throw new Exception("Skyrim native icons missing");
        if (Choices(live.WorkspaceController.ActiveWorkspace).Contains("My Loadouts")) throw new Exception("Skyrim has Home page choices");
        await spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance.Game).Click.Execute();
        await WaitFor(() => live.Profile.Endpoint == entry.Registration.Endpoint && !live.Profile.SelectingProfile,"FNV icon did not reconnect",seconds:110);
        Console.WriteLine("PASS: Home/game discovery separated; Skyrim and FNV icon switching; Skyrim native executable icons");
        } finally { await live.Profile.SelectProfile(entry.Registration,original); }
    }

    private static async Task VerifyNewUi(Mo2LiveWorkspace live, Window window)
    {
        async Task Capture(string name) {
            await Task.Delay(600);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
            bitmap.Render(window); bitmap.Save("/home/deck/mo2/frontend/artifacts/new-ui-" + name + ".png");
        }
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        if (spine.LoadoutSpineItems.Count != live.CatalogEntries.Where(x => x.Instance != null).Select(x => x.Instance!.Game).Distinct().Count()) throw new Exception("Expected one spine icon per game");
        var view = window.GetVisualDescendants().OfType<Mo2ModsView>().Single();
        if (view.NativeView.FindControl<TabItem>("RulesTabItem")!.IsVisible) throw new Exception("Rules remains visible");
        var filter = view.GetVisualDescendants().OfType<ComboBox>().Single(x => x.Name == "ModStateFilter");
        filter.SelectedIndex = 2;
        await WaitFor(() => live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count(x => (x.State & 2) == 0 && (x.State & 4) == 0), "Disabled filter mismatch");
        filter.SelectedIndex = 0;
        await WaitFor(() => live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count(x => !x.IsOverwrite && !x.IsSeparator), "Filter did not restore rows");
        var table = view.NativeView.FindControl<TreeDataGrid>("TreeDataGrid")!;
        await WaitFor(() => table.Rows?.Count == live.Profile.Mods.Count(x => !x.IsOverwrite && !x.IsSeparator), "Mod rows not rendered");
        var movable = live.Profile.Mods.Where(x => (x.State & 4) == 0 && x.Priority > 0).OrderBy(x => x.Priority).First();
        var previous = live.Profile.Mods.OrderBy(x => x.Priority).Select(x => (x.Name,x.Priority)).ToArray();
        try {
            var row = live.Profile.Mods.OrderBy(x => x.Priority).ToList().FindIndex(x => x.Id == movable.Id);
            table.RowSelection!.Select(new IndexPath(row));
            await WaitFor(() => view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "SetModPriorityButton").IsEnabled, "Priority action disabled");
            view.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "ModPriorityInput").Text = (movable.Priority - 1).ToString();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "SetModPriorityButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await WaitFor(() => live.Profile.Mods.Single(x => x.Id == movable.Id).Priority == movable.Priority - 1, "UI priority move failed");
            Console.WriteLine("PRIORITY_ACK_MS: " + watch.ElapsedMilliseconds);
        } finally {
            await live.Profile.MoveMod(movable.Id, movable.Priority, absolute: true);
        }
        if (!previous.SequenceEqual(live.Profile.Mods.OrderBy(x => x.Priority).Select(x => (x.Name,x.Priority)))) throw new Exception("Priority restoration mismatch");
        await Capture("mods");
        await live.ProfileMenu.ToolsItem.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ToolsView>().Any(x => x.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "LaunchToolButton")), "Tools did not load");
        var tools = await live.Profile.ReadTools();
        if (tools.Length == 0) throw new Exception("Original MO2 tools missing");
        Console.WriteLine("TOOLS: " + string.Join(", ",tools.Select(x => x.Name)));
        await Capture("tools");
        if (Environment.GetEnvironmentVariable("MO2_VERIFY_TOOLS_DIALOG") == "1") {
            var toolView = window.GetVisualDescendants().OfType<Mo2ToolsView>().Single();
            toolView.GetVisualDescendants().OfType<Button>().Single(x => ToolTip.GetTip(x)?.ToString() == "Open INI Editor").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await WaitFor(() => live.Profile.ManagingMod, "Tool click was dropped");
            await WaitFor(() => !live.Profile.ManagingMod, "Close INI Editor to finish the tool check", seconds: 90);
            if (live.Profile.OriginalUiVisible == true) throw new Exception("Tool launch exposed the main MO2 window");
            await WaitFor(() => File.Exists("/tmp/mo2-tool-window-verified"), "Original INI Editor window was not verified", seconds: 60);
            Console.WriteLine("PASS: Tools button returned with main MO2 hidden");
        }
        live.OpenDownloads();
        await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Downloads.DownloadsPageView>().Any(), "Native downloads view missing");
        var downloadsView = window.GetVisualDescendants().OfType<Mo2DownloadsView>().Single();
        await WaitFor(() => downloadsView.ViewModel!.Adapter.SourceCount.Value == live.Profile.Downloads.Count && downloadsView.GetVisualDescendants().OfType<TreeDataGrid>().Single().Rows?.Count == live.Profile.Downloads.Count, "Download rows missing");
        await Capture("downloads");
        await live.ProfileMenu.ProfilesItem.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
        await WaitFor(() => window.GetVisualDescendants().OfType<MyLoadoutsView>().Any(), "Profiles page missing");
        if (window.GetVisualDescendants().OfType<HomeLeftMenuView>().Any()) throw new Exception("Profiles left game workspace");
        await Capture("profiles");
        if (Environment.GetEnvironmentVariable("MO2_VERIFY_NEW_UI_SWITCH") == "1") {
            var originalTarget = live.Profile.CurrentTarget;
            var originalGame = live.CatalogEntries.First(x => x.Registration.Endpoint == originalTarget.Endpoint).Instance!.Game;
            await spine.LoadoutSpineItems.Single(x => x.Name == "Skyrim Special Edition").Click.Execute();
            await WaitFor(() => live.Profile.IsConnected && live.Profile.GameName.Contains("Skyrim"), "Skyrim icon did not connect", seconds: 110);
            var workspace = live.WorkspaceController.ActiveWorkspace;
            workspace.Panels.First().IsSelected = true;
            await live.ProfileMenu.LeftMenuItemLoadout.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
            await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any(), "Skyrim mods missing");
            var skyrimView = window.GetVisualDescendants().OfType<Mo2ModsView>().Single();
            skyrimView.GetVisualDescendants().OfType<ComboBox>().Single(x => x.Name == "ModStateFilter").SelectedIndex = 3;
            await WaitFor(() => live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count(x => x.Conflicts.Length > 0), "Skyrim conflicts filter mismatch");
            await Capture("skyrim-conflicts");
            await spine.LoadoutSpineItems.Single(x => x.Name == originalGame).Click.Execute();
            await WaitFor(() => live.Profile.IsConnected && live.Profile.CurrentTarget == originalTarget, "Game icon did not restore FNV profile", seconds: 110);
        }
        Console.WriteLine("PASS: unique game icons, no Rules, live filters, original tools, native downloads, game Profiles");
    }

    private static async Task VerifyFinalPages(Mo2LiveWorkspace live, Window window)
    {
        async Task Capture(string name) {
            await Task.Delay(1000);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)window.ClientSize.Width, (int)window.ClientSize.Height));
            bitmap.Render(window);
            bitmap.Save("/home/deck/mo2/frontend/artifacts/final-" + name + ".png");
        }
        live.OpenGames();
        await WaitFor(() => window.GetVisualDescendants().OfType<MyGamesView>().Any(), "My Games missing");
        var games = window.GetVisualDescendants().OfType<MyGamesView>().Single();
        if (games.FindControl<Border>("AllCurrentlySupportedGames")!.IsVisible)
            throw new InvalidOperationException("MO2 games page retained an empty independent support catalog");
        await Capture("my-games");
        live.OpenProfiles();
        await WaitFor(() => window.GetVisualDescendants().OfType<MyLoadoutsView>().Any(), "My Loadouts missing");
        var loadouts = window.GetVisualDescendants().OfType<MyLoadoutsView>().Single();
        if (loadouts.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.PageHeader.PageHeader>().Single().Description!.Contains("apply", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("MO2 loadouts still describe deployment");
        var cards = loadouts.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.LoadoutCard.LoadoutCardView>().Count(x => x.ViewModel is Mo2LoadoutCard);
        if (cards != live.CatalogEntries.Sum(x => x.Instance?.Profiles.Length ?? 0))
            throw new InvalidOperationException("My Loadouts omitted registered MO2 profiles");
        await Capture("my-loadouts");
        var spine = window.GetVisualDescendants().OfType<NexusMods.App.UI.Controls.Spine.Spine>().Single().ViewModel!;
        foreach (var tag in new[] { "fnv", "skyrim", "fnv-return" }) {
            var fnv = tag.StartsWith("fnv");
            var directory = fnv ? "/home/deck/mo2/frontend/artifacts/mo2-fnv-host" : "/home/deck/Games/mod-organizer-2-skyrimspecialedition/modorganizer2";
            var entry = live.CatalogEntries.Single(x => x.Registration.Directory == directory);
            var target = entry.Instance!.Profiles.Single(x => x.Name == (fnv ? "Frontend Test" : "Default"));
            await spine.LoadoutSpineItems.Single(x => x.Name == entry.Instance.Game + " — " + target.Name + " (" + directory + ")").Click.Execute();
            await WaitFor(() => live.Profile.IsConnected && Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath) == target.Directory, "Profile switch did not connect", seconds: 110);
            if (window.GetVisualDescendants().OfType<HomeLeftMenuView>().Any())
                throw new InvalidOperationException("Game workspace retained the Home sidebar");
            var workspace = live.WorkspaceController.ActiveWorkspace;
            workspace.Panels.First().IsSelected = true;
            await live.ProfileMenu.LeftMenuItemLoadout.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
            workspace.Panels.Last().IsSelected = true;
            await live.ProfileMenu.LeftMenuItemExternalChanges!.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
            await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ModsView>().Any() && window.GetVisualDescendants().OfType<Mo2PluginsView>().Any()
                && live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count(x => !x.IsOverwrite && !x.IsSeparator), "Paired MO2 lists did not render");
            await Capture(tag + "mods");
            if (tag == "fnv-return") continue;
            await live.ProfileMenu.LeftMenuItemHealthCheck.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
            await WaitFor(() => window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.Diagnostics.DiagnosticListView>().Any(x => x.ViewModel is Mo2HealthPage { HasResult: true }), "Native Health Check did not obtain MO2 reports", seconds: 110);
            await Capture(tag + "health");
            live.OpenDownloads();
            await WaitFor(() => window.GetVisualDescendants().OfType<Mo2DownloadsView>().Any(), "MO2 downloads missing");
            if (live.WorkspaceController.ActiveWorkspaceId != workspace.Id || window.GetVisualDescendants().OfType<HomeLeftMenuView>().Any())
                throw new InvalidOperationException("Downloads left the selected profile workspace");
            await Capture(tag + "downloads");
            live.ShowProfile();
        }
        Console.WriteLine("PASS: current native Home pages show every MO2 profile and no Apply/support-catalog claims; FNV/Skyrim/FNV spine navigation, paired lists, Health Check and downloads render from live hosts");
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
        // The toggle moved off the removed Connections page into the top bar's MO2
        // menu, so this drives it where it now lives.
        var item = window.GetVisualDescendants().OfType<MenuItem>().FirstOrDefault(x => x.Name == "OriginalMo2UiMenuItem")
            ?? throw new InvalidOperationException("No Show original MO2 entry in the MO2 menu");
        void Click() => item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        try {
            if (!item.IsEnabled || !Equals(item.Header, "Show original MO2")) throw new InvalidOperationException("Original UI action is unavailable");
            Click();
            await WaitFor(() => profile.OriginalUiVisible == true && !profile.ManagingMod && Equals(item.Header, "Hide original MO2"), "Original MO2 did not become visible");
            Console.WriteLine("ORIGINAL_UI_VISIBLE");
            await Task.Delay(15000);
            if (before != await State()) throw new InvalidOperationException("Showing original MO2 changed the profile state");
            Click();
            await WaitFor(() => profile.OriginalUiVisible == false && !profile.ManagingMod && Equals(item.Header, "Show original MO2"), "Original MO2 did not hide");
            if (before != await State()) throw new InvalidOperationException("Hiding original MO2 changed the profile state");
            Console.WriteLine("PASS: original MO2 starts hidden; the MO2 menu entry shows and hides the same host; profile, mods and plugins unchanged");
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
        async Task<Mo2DownloadsView> CurrentDownloads() {
            await WaitFor(() => window.GetVisualDescendants().OfType<Mo2DownloadsView>()
                .Any(x => x.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Name == "DownloadProfileContext")), "Downloads page did not render");
            return window.GetVisualDescendants().OfType<Mo2DownloadsView>().Single();
        }
        var view = await CurrentDownloads();
        Button NexusButton(Mo2DownloadsView page) => page.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "OpenNexusLinkButton");
        Flyout LinkFlyout(Mo2DownloadsView page) => (Flyout)NexusButton(page).Flyout!;
        Button LinkSubmit(Mo2DownloadsView page) => ((Control)LinkFlyout(page).Content!).GetVisualDescendants().OfType<Button>().Single(x => x.Name == "DownloadNexusButton");
        var context = view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "DownloadProfileContext");
        if (context.Text != "Fallout: New Vegas · Frontend Test") throw new InvalidOperationException("Downloads does not identify its MO2 game and profile");
        // The Install archive... button on this page's toolbar is gone with the
        // toolbar; installing an archive is MO2's own Install mod..., which the mod
        // list's list-options button offers. What is watched through the profile
        // switch below is the Nexus link action, which is the one on this page.
        var download = LinkSubmit(view);
        if (!download.IsEnabled) throw new InvalidOperationException("Connected Downloads actions are disabled");
        var started = false; var disabledDuringSwitch = false;
        void Changed() {
            started |= profile.Installing;
            disabledDuringSwitch |= profile.SelectingProfile && !download.IsEnabled;
        }
        profile.Changed += Changed;
        var oldTarget = profile.CurrentTarget;
        try {
            var openedLink = LinkFlyout(view);
            openedLink.ShowAt(NexusButton(view));
            await WaitFor(() => openedLink.IsOpen, "Nexus link dialog did not open");
            var cloneBefore = "";
            await view.ChooseArchive(async () => {
                if (!await profile.SelectProfile(entry.Registration, clone)) throw new InvalidOperationException("Clone profile did not connect");
                if (openedLink.IsOpen) throw new InvalidOperationException("Nexus dialog remained open after switching profiles");
                cloneBefore = await State();
                // Switching profiles now restores its own workspace. Inspect the
                // newly active downloads page while the old picker retains its target.
                live.OpenDownloads();
                view = await CurrentDownloads();
                context = view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "DownloadProfileContext");
                download = LinkSubmit(view);
                return missing;
            });
            if (started || !profile.IsConnected || !profile.Status.StartsWith("The MO2 profile changed.") || !disabledDuringSwitch ||
                context.Text != "Fallout: New Vegas · Frontend Clone Test" || await State() != cloneBefore)
                throw new InvalidOperationException("Archive picker continuation crossed its original profile boundary");
            await profile.ControlDownload(missing, "cancel", oldTarget);
            if (!profile.IsConnected || !profile.Status.StartsWith("The MO2 profile changed.") || await State() != cloneBefore)
                throw new InvalidOperationException("Stale download row acted on another profile");
            await profile.DownloadNexus("not-a-link", oldTarget);
            if (!profile.IsConnected || !profile.Status.StartsWith("The MO2 profile changed.") || await State() != cloneBefore)
                throw new InvalidOperationException("Stale Nexus dialog reached parsing or another profile");
            await profile.DownloadNexus("not-a-link", new Mo2ProfileTarget(oldTarget.Endpoint + "-different-instance", profile.ProfilePath));
            if (!profile.IsConnected || !profile.Status.StartsWith("The MO2 profile changed.") || await State() != cloneBefore)
                throw new InvalidOperationException("Stale Nexus dialog ignored instance identity");
            foreach (var (link, message) in new[] {
                ("not-a-link", "Paste a Nexus file link"),
                ("nxm://skyrimspecialedition/mods/1/files/1", "Choose a Nexus file for the current game")
            }) {
                await profile.DownloadNexus(link, profile.CurrentTarget);
                if (!profile.IsConnected || profile.Status != message || !download.IsEnabled || await State() != cloneBefore)
                    throw new InvalidOperationException("Local Nexus link validation disconnected or changed MO2");
            }
            await profile.InstallArchive(missing, new Mo2ProfileTarget(oldTarget.Endpoint + "-different-instance", profile.ProfilePath));
            if (started || !profile.IsConnected || !profile.Status.StartsWith("The MO2 profile changed."))
                throw new InvalidOperationException("Archive target guard ignored instance identity");
            await view.ChooseArchive(() => Task.FromResult<string?>(missing));
            if (!started || !profile.Status.Contains("The mod archive does not exist") || !profile.IsConnected || !download.IsEnabled)
                throw new InvalidOperationException("Native archive rejection did not preserve the connected profile and usable controls");
            var downloadTable = view.GetVisualDescendants().OfType<TreeDataGrid>().Single(x => x.Name == "TreeDataGridDownloads");
            if (view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Name == "DownloadsUnavailable" && x.IsEffectivelyVisible) ||
                view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.IsEffectivelyVisible && x.Text == "No downloads yet") ||
                downloadTable.Rows?.Count != profile.Downloads.Count)
                throw new InvalidOperationException("Native command rejection cleared the connected Downloads table");
            await profile.Refresh();
            await WaitFor(() => downloadTable.Rows?.Count == profile.Downloads.Count, "Fresh MO2 download rows did not settle");
            if (view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Name == "DownloadsUnavailable" && x.IsEffectivelyVisible))
                throw new InvalidOperationException("Fresh MO2 snapshot did not restore the download rows");
            if (!profile.IsConnected || !download.IsEnabled || await State() != cloneBefore)
                throw new InvalidOperationException("Downloads did not recover after refresh or changed clone state");
        } finally {
            profile.Changed -= Changed;
            await profile.SelectProfile(entry.Registration, original);
        }
        if (await State() != before) throw new InvalidOperationException("Download context check changed original MO2 state");
        Console.WriteLine("PASS: Nexus flyout closes on profile switch; stale Nexus links, picker continuations and archive/download actions stay bound to their instance/profile; malformed and wrong-game links preserve connection; busy controls disable; native archive rejection preserves connection and rows; refresh succeeds; both profiles unchanged");
    }

    private static async Task VerifyPanelDrag(Mo2LiveWorkspace live, Window window)
    {
        if (!live.Profile.ProfilePath.EndsWith("/frontend/artifacts/mo2-fnv-host/profiles/Frontend Test"))
            throw new InvalidOperationException("Panel pointer test requires the isolated FNV profile");
        var workspace = live.WorkspaceController.ActiveWorkspace;
        var originalPanels = workspace.Panels.ToDictionary(x => x.Id, x => x.LogicalBounds);
        var tabs = workspace.Panels.SelectMany(x => x.Tabs).Select(x => x.Id).ToHashSet();
        // The state to compare against at the end, taken once MO2 has actually sent
        // it. The profile-path guard above means a profile is selected, not that its
        // mods and plugins have arrived, and a baseline captured part-way through
        // filling makes the comparison at the end disagree with itself.
        await WaitFor(() => live.Profile.Mods.Count > 0 && live.Profile.Order.Plugins.Count > 0, "MO2's mods and plugins to arrive");
        var steady = 0;
        var seen = (Mods: -1, Plugins: -1);
        for (var attempt = 0; attempt < 120 && steady < 5; attempt++) {
            var now = (live.Profile.Mods.Count, live.Profile.Order.Plugins.Count);
            steady = now == seen ? steady + 1 : 0;
            seen = now;
            await Task.Delay(100);
        }
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
            // The plugin view on screen, not the only one. Switching games retains the
            // view built for the profile left behind — that is what Mo2RetainedViews is
            // for — so after the FNV/Skyrim/FNV loop below there are two, and Single()
            // threw "Sequence contains more than one element" on the last phase.
            var pluginViews = window.GetVisualDescendants().OfType<Mo2PluginsView>().ToArray();
            var pluginView = pluginViews.FirstOrDefault(x => x.IsEffectivelyVisible && x.Bounds.Width > 0)
                ?? pluginViews.FirstOrDefault()
                ?? throw new InvalidOperationException("No plugin view is drawn to measure");
            var pluginTable = pluginView.GetVisualDescendants().OfType<TreeDataGrid>().Single();
            if (phase == "panel-height-expand") {
                // The last two rows this profile actually has. Rows 10 and 11 were
                // named outright, and MO2 holds eleven plugins here — indices 0 to 10 —
                // so the second select addressed a row that does not exist and the wait
                // for two selected could never come true. It read as a short panel that
                // cannot take a selection, which is what the check is for, rather than
                // as a count this host does not reach.
                var rows = pluginTable.Rows?.Count ?? 0;
                if (rows < 2) throw new InvalidOperationException($"The plugin list has {rows} row(s); two are needed to select a pair");
                pluginTable.RowSelection!.Clear();
                pluginTable.RowSelection.Select(new IndexPath(rows - 2));
                pluginTable.RowSelection.Select(new IndexPath(rows - 1));
                await WaitFor(() => live.PluginsPage!.Adapter.SelectedModels.Count == 2,
                    $"Short panel could not select plugins: rows {rows - 2} and {rows - 1} of {rows} left " +
                    $"{live.PluginsPage!.Adapter.SelectedModels.Count} selected");
                await Task.Delay(300);
            }
            // The scroller the rows are in, and what every scroller measured if none
            // of them has room. This took whichever had the most overflow and asserted
            // on its height, which is not necessarily the one holding the rows — a
            // header or a horizontal scroller can out-overflow it — and then said only
            // that there was no room for a row, naming neither the scroller it picked
            // nor how tall it was.
            // The scroller the rows are in, which is the tallest — not the one with the
            // most overflow. Sorting by overflow ties every scroller at zero the moment
            // the list fits without scrolling, and First() then took whichever came
            // first in the tree: PART_HeaderScrollViewer, which measures 0x0. The check
            // read that as a panel with no room for a row while PART_ScrollViewer beside
            // it was 370x496, and blamed a resize that had worked.
            var scrollers = pluginTable.GetVisualDescendants().OfType<ScrollViewer>().ToArray();
            var viewport = scrollers.OrderByDescending(x => x.Viewport.Height).First();
            if (viewport.Viewport.Height < 48)
                throw new InvalidOperationException($"Resized plugin panel has no room for a complete row: picked a " +
                    $"scroller {viewport.Viewport.Width:F0}x{viewport.Viewport.Height:F0} of extent " +
                    $"{viewport.Extent.Width:F0}x{viewport.Extent.Height:F0}, out of " +
                    string.Join(", ", scrollers.Select(x => $"{x.Name ?? x.GetType().Name} {x.Viewport.Width:F0}x{x.Viewport.Height:F0}")) +
                    $"; plugin view {pluginTable.Bounds.Width:F0}x{pluginTable.Bounds.Height:F0}");
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
                // One icon per game, named for the game. The spine carried an icon per
                // profile once and this matched the name it built then — "<game> — 
                // <profile> (<directory>)" — which no item has had since NMA_FIDELITY
                // replaced them with one icon per game. Nothing caught it because
                // nothing ran this check.
                var wanted = entry.Instance!.Game;
                var item = spine.LoadoutSpineItems.FirstOrDefault(x => x.Name == wanted)
                    ?? throw new InvalidOperationException($"No spine item named \"{wanted}\" among " +
                        string.Join(", ", spine.LoadoutSpineItems.Select(x => $"\"{x.Name}\"")));
                await item.Click.Execute();
                // Connected somewhere inside that instance. The icon names a game, not a
                // profile, so which profile it lands on is the instance's business —
                // what this check is about is that switching games leaves the
                // pointer-resized workspace alone.
                // Connected to that game. Waiting for a particular directory was still
                // too strict: two Skyrim instances are registered here, one icon stands
                // for the game, and clicking it landed on /home/deck/ModOrganizer2
                // rather than the instance this loop happened to name. Which instance a
                // game icon selects is the spine's business; what this check is about
                // is that switching games leaves the pointer-resized workspace alone.
                // Inside any instance registered for that game. Two naming sources meet
                // here and they do not agree: the spine names an item from
                // entry.Instance.Game ("New Vegas") and Mo2LiveProfile.GameName spells
                // it "Fallout: New Vegas", so comparing them failed for FNV on a switch
                // that had in fact connected to exactly the right profile. Comparing
                // directories instead failed for Skyrim, where two instances are
                // registered and one icon stands for the game. The instances belonging
                // to the game are what the icon can land in, so that is the question.
                var homes = live.CatalogEntries.Where(x => x.Instance?.Game == wanted)
                    .Select(x => x.Registration.Directory).ToArray();
                await WaitFor(() => !live.Profile.SelectingProfile && homes.Any(home =>
                        Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath).StartsWith(home, StringComparison.Ordinal)),
                    $"Game switch to {wanted} did not connect — profile is " +
                    $"{Mo2InstanceCatalog.LocalPath(live.Profile.ProfilePath)}, and {wanted} is registered at " +
                    string.Join(", ", homes), seconds: 110);
            }
            if (live.WorkspaceController.ActiveWorkspace.Id != workspace.Id || workspace.Panels.Count != layout.Count || workspace.Panels.Any(x => layout[x.Id] != x.LogicalBounds))
                throw new InvalidOperationException("Game switching lost the pointer-resized workspace");
            await Drag("panel-height-restore", true, -80);
            await live.Profile.Refresh();
            // What changed, if anything did. "Panel interaction changed MO2 state" is
            // a serious claim — it says dragging a divider wrote to the host — and it
            // was made without naming a single plugin or mod, so it could be neither
            // trusted nor dismissed.
            // Compared by name, because the order these collections are enumerated in
            // is not the state under test — SortIndex and Priority carry that, and both
            // are in the tuples. SequenceEqual on the raw enumeration said "Panel
            // interaction changed MO2 state" with an empty difference: 11 of 11 plugins
            // and 14 of 14 mods identical, rebuilt in another sequence after the game
            // switch reconnected. VerifyLiveHistory already sorts before comparing.
            var nowPlugins = live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).OrderBy(x => x.DisplayName).ToArray();
            var nowMods = live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority)).OrderBy(x => x.Name).ToArray();
            var wasPlugins = original.OrderBy(x => x.DisplayName).ToArray();
            var wasMods = mods.OrderBy(x => x.Name).ToArray();
            if (!wasPlugins.SequenceEqual(nowPlugins) || !wasMods.SequenceEqual(nowMods)) {
                var pluginDiff = wasPlugins.Except(nowPlugins).Select(x => $"was {x.DisplayName} #{x.SortIndex} active={x.IsActive}")
                    .Concat(nowPlugins.Except(wasPlugins).Select(x => $"now {x.DisplayName} #{x.SortIndex} active={x.IsActive}"));
                var modDiff = wasMods.Except(nowMods).Select(x => $"was {x.Name} state={x.State} priority={x.Priority}")
                    .Concat(nowMods.Except(wasMods).Select(x => $"now {x.Name} state={x.State} priority={x.Priority}"));
                throw new InvalidOperationException($"Panel interaction changed MO2 state — " +
                    $"{wasPlugins.Length}/{nowPlugins.Length} plugins, {wasMods.Length}/{nowMods.Length} mods; " +
                    string.Join("; ", pluginDiff.Concat(modDiff).Take(8)));
            }
            Console.WriteLine("PASS: FNV/Skyrim switching preserves pointer-resized FNV workspace; original mod/plugin state unchanged");
        } finally {
            File.Delete("/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json");
            foreach (var panel in workspace.Panels.Where(x => !originalPanels.ContainsKey(x.Id)).ToArray())
                await panel.CloseCommand.Execute();
        }
        // The plugin view on screen, as above: switching games leaves the view built
        // for the profile left behind in the tree, so Single() finds two by the time
        // the panels are restored.
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2PluginsView>()
                .Where(x => x.IsEffectivelyVisible).Select(x => x.Bounds.Height).DefaultIfEmpty(0).Max() > 500,
            "Original two-panel view did not restore — tallest drawn plugin view is " +
            $"{window.GetVisualDescendants().OfType<Mo2PluginsView>().Where(x => x.IsEffectivelyVisible).Select(x => x.Bounds.Height).DefaultIfEmpty(0).Max():F0}px");
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
        // The plugin view, once the panel has built it. The profile-path guard above
        // means a profile is selected, not that its pages are on screen, and Single()
        // over an empty tree reported "Sequence contains no elements" before this
        // check had looked at anything at all.
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2PluginsView>().Any(x => x.IsEffectivelyVisible),
            "the plugin view to be drawn");
        var pluginView = window.GetVisualDescendants().OfType<Mo2PluginsView>().First(x => x.IsEffectivelyVisible);
        var table = pluginView.GetVisualDescendants().OfType<TreeDataGrid>().Single();
        table.RowDragStarted += (_, e) => Console.WriteLine("DRAG START: " + e.Models.Count() + " rows");
        table.RowDrop += (_, e) => Console.WriteLine("DRAG DROP: " + e.Position);
        // And the state to restore MO2 to, once MO2 has sent it. This drags a plugin
        // into a new load order and puts it back from these two arrays, so capturing
        // them part-way through filling decides what the host is left holding.
        await WaitFor(() => profile.Mods.Count > 0 && profile.Order.Plugins.Count > 0, "MO2's mods and plugins to arrive");
        var steady = 0;
        var seen = (Mods: -1, Plugins: -1);
        for (var attempt = 0; attempt < 120 && steady < 5; attempt++) {
            var now = (profile.Mods.Count, profile.Order.Plugins.Count);
            steady = now == seen ? steady + 1 : 0;
            seen = now;
            await Task.Delay(100);
        }
        var original = profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive)).ToArray();
        var mods = profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray();
        var names = original.Select(x => x.DisplayName).ToArray();
        var moving = profile.Order.Plugins.Where(x => x.DisplayName.StartsWith("MCM Example") && x.CanMove).Take(2).Select(x => x.DisplayName).ToArray();
        const string target = "The Mod Configuration Menu.esp";
        // What the profile holds, when it does not hold what this needs. The check
        // wants two movable "MCM Example" plugins sitting fourth and third from the
        // end with The Mod Configuration Menu.esp last — an arrangement of the
        // isolated FNV profile, not anything the frontend decides — and said only
        // "Unexpected isolated plugin order", which cannot be acted on without
        // opening MO2 and reading the list by hand.
        if (moving.Length != 2 || names[^1] != target || !names.Skip(names.Length - 4).Take(2).SequenceEqual(moving))
            throw new InvalidOperationException($"Unexpected isolated plugin order: needs two movable \"MCM Example\" " +
                $"plugins at positions {names.Length - 4} and {names.Length - 3} with \"{target}\" last; found " +
                $"{moving.Length} movable MCM Example plugin(s) and the order " +
                string.Join(" | ", profile.Order.Plugins.Select(x => $"{x.DisplayName}{(x.CanMove ? "" : " (fixed)")}")));
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
        // The page's toolbar carried an Enable and a Disable for the selection. It is
        // gone — MO2 puts no toolbar on a tab — and the entries it duplicated are on
        // the menu MO2 builds for a plugin row, gated by the same condition. Opened
        // fresh each time: the menu is built when it opens and its entries are
        // enabled against the selection as it then is.
        async Task<MenuItem> Entry(string caption, string plugin) {
            var row = PluginRow(window, plugin);
            var menu = (MenuFlyout)row.ContextFlyout!;
            menu.ShowAt(row);
            await Task.Delay(500);
            return menu.Items.OfType<MenuItem>().FirstOrDefault(x => x.Header?.ToString() == caption)
                ?? throw new InvalidOperationException($"MO2's plugin menu has no {caption}");
        }
        async Task<bool> Offers(string caption, string plugin) {
            var item = await Entry(caption, plugin);
            var offered = item.IsEnabled;
            ((MenuFlyout)PluginRow(window, plugin).ContextFlyout!).Hide();
            await Task.Delay(150);
            return offered;
        }
        var original = live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.IsActive, x.SortIndex)).ToArray();
        var mods = live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority)).ToArray();
        var targets = live.Profile.Order.Plugins.Where(x => x.DisplayName.StartsWith("MCM Example") && x.CanToggle && x.IsActive).Take(2).Select(x => x.DisplayName).ToArray();
        if (targets.Length != 2) throw new InvalidOperationException("Two active MCM example plugins are required");
        async Task MousePhase(string phase, string caption) {
            window.Activate();
            await SelectPluginsWithPointer(table, live.PluginsPage!, targets, phase);
            var item = await Entry(caption, targets[0]);
            var point = item.PointToScreen(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2));
            File.WriteAllText("/home/deck/mo2/frontend/artifacts/plugin-mouse-phase.json", System.Text.Json.JsonSerializer.Serialize(new {
                Phase = phase, Points = new[] { new { X = point.X, Y = point.Y, Ctrl = false } }
            }));
        }
        try {
            await MousePhase("disable", "Disable selected");
            await WaitFor(() => targets.All(name => !live.Profile.Order.Plugins.Single(x => x.DisplayName == name).IsActive), "Mouse multi-selection did not disable both ESPs", seconds: 60);
            if (!mods.SequenceEqual(live.Profile.Mods.Select(x => (x.Name, x.State, x.Priority))))
                throw new InvalidOperationException("Disabling plugins changed mod activation");
            await MousePhase("enable", "Enable selected");
            await WaitFor(() => targets.All(name => live.Profile.Order.Plugins.Single(x => x.DisplayName == name).IsActive), "Mouse multi-selection did not re-enable both ESPs", seconds: 60);
            if (await Offers("Enable selected", targets[0]) || !await Offers("Disable selected", targets[0]) || live.PluginsPage!.Adapter.SelectedModels.Count != 2)
                throw new InvalidOperationException($"MO2's menu disagrees with the restored plugins: selected={live.PluginsPage!.Adapter.SelectedModels.Count}");
            table.RowSelection!.Clear();
            var fixedIndex = live.Profile.Order.Plugins.ToList().FindIndex(x => !x.CanToggle);
            table.RowSelection.Select(new IndexPath(fixedIndex));
            await Task.Delay(400);
            // Asked of the locked plugin's own row, which is what MO2 gates these on.
            var locked = live.Profile.Order.Plugins.First(x => !x.CanToggle).DisplayName;
            if (await Offers("Enable selected", locked) || await Offers("Disable selected", locked))
                throw new InvalidOperationException("A locked plugin's menu offers activation");
            var targetIndex = live.Profile.Order.Plugins.ToList().FindIndex(x => x.DisplayName == targets[0]);
            table.RowSelection.Select(new IndexPath(targetIndex));
            await Task.Delay(400);
            if (!await Offers("Disable selected", targets[0])) throw new InvalidOperationException("Mixed selection did not allow its editable plugin");
            await PluginRowMenu(window, "Disable selected", targets[0]);
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
        Console.WriteLine("PASS: real Ctrl-click multi-selection and MO2's own menu entries disable/enable two ESPs independently of mods; fixed and mixed selections respect restrictions; original state restored");
    }

    private static async Task VerifyTopBar(Mo2LiveWorkspace live, Window window, bool openLogs = false)
    {
        var view = window.GetVisualDescendants().OfType<TopBarView>().Single();
        var model = (Mo2TopBar)view.ViewModel!;
        if (typeof(TopBarDesignViewModel).IsAssignableFrom(model.GetType()) || model.Username is not null || model.Avatar is not null || model.IsLoggedIn)
            throw new InvalidOperationException("Live top bar still contains a demo identity");
        // The native top bar's Nexus actions that this frontend has nothing behind:
        // they stay dead rather than being drawn live and doing nothing, which is the
        // same rule the MO2 widgets are held to.
        foreach (var command in new[] { model.ShowWelcomeMessageCommand, model.OpenNexusModsProfileCommand,
            model.OpenNexusModsPremiumCommand, model.OpenForumsCommand })
            if (((System.Windows.Input.ICommand)command).CanExecute(System.Reactive.Unit.Default)) throw new InvalidOperationException("Unmapped top-bar action remains enabled");
        // The two that do have something behind them go through MO2: its Nexus
        // settings, and signing out of the account MO2 holds. Logout follows whether
        // anyone is signed in; account settings is always reachable, as MO2's is.
        // These were in the list above, written before the account work, so this
        // check had been failing on its own frontend's features ever since.
        if (!((System.Windows.Input.ICommand)model.OpenNexusModsAccountSettingsCommand).CanExecute(System.Reactive.Unit.Default))
            throw new InvalidOperationException("MO2's Nexus settings are unreachable from the top bar");
        if (((System.Windows.Input.ICommand)model.LogoutCommand).CanExecute(System.Reactive.Unit.Default) != model.IsLoggedIn)
            throw new InvalidOperationException($"Sign out is {(model.IsLoggedIn ? "disabled while signed in" : "enabled while signed out")}");
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
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault()?.NativeView.FindControl<TextBlock>("ModsCount")?.Text == live.Profile.Mods.Count(x => !x.IsOverwrite && !x.IsSeparator).ToString(),
            "Native mod count did not return after Home navigation");
        if (window.GetVisualDescendants().OfType<Button>().Any(x => Equals(x.Content, "Run through MO2") || Equals(x.Content, "Refresh from MO2")))
            throw new InvalidOperationException("Duplicate header controls remain");
        Console.WriteLine($"PASS: native sidebar PLAY command bound to MO2; {expected.Length} executable choices match host; invalid selection disables PLAY; Home hides controls and restores selection");
    }

    private static async Task VerifyOverwriteRefresh(Mo2LiveWorkspace live, Window window)
    {
        await live.ProfileMenu.OverwriteItem.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default));
        await WaitFor(() => window.GetVisualDescendants().OfType<Mo2OverwriteView>().Any(), "Overwrite panel did not open");
        var view = window.GetVisualDescendants().OfType<Mo2OverwriteView>().Single();
        await WaitFor(() => view.GetVisualDescendants().OfType<TreeDataGrid>().Any(x => x.Name == "OverwriteFiles"), "Overwrite table did not render");
        var table = view.GetVisualDescendants().OfType<TreeDataGrid>().Single(x => x.Name == "OverwriteFiles");
        const string selectedPath = "__frontend_overwrite_check_A.txt";
        await WaitFor(() => table.Rows?.Any(x => x.Model is Mo2OverwriteFile { Path: selectedPath }) == true, "Required Overwrite fixture did not load");
        var index = Enumerable.Range(0, table.Rows!.Count).Single(i => table.Rows[i].Model is Mo2OverwriteFile { Path: selectedPath });
        table.RowSelection!.Select(new IndexPath(index));
        // The search box lives behind the magnifier on the header line now, as it does
        // on Data and External Files, so this opens it the way a person would rather
        // than reaching for a box that is not on screen yet.
        view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "SearchToggleButton")
            .Command?.Execute(null);
        if (view.GetVisualDescendants().OfType<Button>().Single(x => x.Name == "SearchToggleButton") is { } toggle)
            toggle.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await WaitFor(() => view.GetVisualDescendants().OfType<TextBox>().Any(x => x.Name == "OverwriteSearch"),
            "Overwrite search did not open from its header action");
        var filter = view.GetVisualDescendants().OfType<TextBox>().Single(x => x.Name == "OverwriteSearch");
        filter.Text = "__frontend_overwrite_check_B.txt";
        await WaitFor(() => table.Rows!.Count == 1 && table.RowSelection!.SelectedItem is null, "Overwrite filter did not hide the selected file");
        filter.Text = "";
        await WaitFor(() => table.RowSelection!.SelectedItem is Mo2OverwriteFile { Path: selectedPath }, "Clearing Overwrite filter lost selection");
        await view.Refresh();
        if (table.RowSelection!.SelectedItem is not Mo2OverwriteFile { Path: selectedPath })
            throw new InvalidOperationException("Overwrite refresh lost selection");
        Console.WriteLine("PASS live Overwrite filter/clear/refresh preserve file selection");
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
        await WaitFor(() => table.Rows!.Count == 0, "Overwrite must be absent from the Mods panel");
        search.FindControl<Button>("SearchClearButton")!.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await WaitFor(() => table.Rows!.Count == live.Profile.Mods.Count(x => !x.IsOverwrite && !x.IsSeparator), "Native clear search did not restore all mods");
        if (native.FindControl<TabItem>("RulesTabItem")!.IsVisible)
            throw new InvalidOperationException("Removed Rules tab became visible");
        live.ModsPage!.CommandDeselectItems.Execute(R3.Unit.Default);
        await live.Profile.Refresh();
        if (!modsBefore.SequenceEqual(live.Profile.Mods.OrderBy(x => x.Name).Select(x => (x.Name, x.Priority, x.State))) ||
            !pluginsBefore.SequenceEqual(live.Profile.Order.Plugins.Select(x => (x.DisplayName, x.SortIndex, x.IsActive))))
            throw new InvalidOperationException("Native mods UI test did not restore MO2 state");
        var priorities = live.ModsPage.Adapter.Source.Value.Items.Select(x => x.Get<ValueComponent<int>>(Mo2ModsAdapter.PriorityKey).Value.Value).ToArray();
        if (!priorities.SequenceEqual(priorities.Order())) throw new InvalidOperationException("Default mod rows do not follow MO2 priority");
        Console.WriteLine("PASS: native mods search/clear, selection/deselect, activation, Overwrite exclusion and hidden Rules tab match the frontend; original state restored");
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
            x.GetProperty("canToggle").GetBoolean(), x.GetProperty("canMove").GetBoolean(),
            x.TryGetProperty("priorityText", out var priority) ? priority.GetString() ?? "" : "")).OrderBy(x => x.Item1).ToArray();
        if (!expected.SequenceEqual(live.Profile.Order.Plugins.Select(x => ((string?)x.DisplayName, (string?)x.Diagnostics,
                (string?)x.ModIndex, x.CanToggle, x.CanMove, x.PriorityText)).OrderBy(x => x.Item1)))
            throw new InvalidOperationException("Native plugin diagnostics disagree with MO2");
        var plugin = live.Profile.Order.Plugins.First(x => !x.CanToggle && !x.CanMove);
        await WaitFor(() => live.PluginsPage!.Adapter.Source.Value.Items.Any(x => x.Key.Equals(plugin.Key)), "Plugin rows did not refresh");
        var page = live.PluginsPage!;
        var originalSelection = page.Adapter.SelectedModels.ToArray();
        var originalDetails = page.ShowPluginDetails;
        try {
            page.Adapter.SelectedModels.Clear();
            page.Adapter.SelectedModels.Add(page.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key)));
            var view = window.GetVisualDescendants().OfType<Mo2PluginsView>().Single();
            var detailsButton = view.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
                .Single(x => x.Name == "PluginDetailsButton");
            await WaitFor(() => detailsButton.IsEffectivelyEnabled, "Selected plugin cannot open details");
            if (!page.ShowPluginDetails) detailsButton.IsChecked = true;
            bool ContainsDetails(string? text) => text is not null && text.Contains(plugin.DisplayName) &&
                text.Contains(plugin.Diagnostics) && (plugin.ModIndex.Length == 0 || text.Contains("Mod index: " + plugin.ModIndex)) &&
                (plugin.PriorityText.Length == 0 || text.Contains("Priority: " + plugin.PriorityText));
            await WaitFor(() => view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Name == "Mo2PluginDiagnostics" &&
                x.IsEffectivelyVisible && ContainsDetails(x.Text)), "Native plugin details are not visible");
            var name = view.GetVisualDescendants().OfType<TextBlock>().Single(x => x.Name == "PluginName" && x.Text == plugin.DisplayName);
            if (!ContainsDetails(ToolTip.GetTip(name) as string)) throw new InvalidOperationException("Plugin tooltip lost native order details");
            var row = page.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key));
            var index = row.Get<SharedComponents.IndexComponent>(LoadOrderColumns.IndexColumn.IndexComponentKey);
            if (index.MoveUp.CanExecute() || index.MoveDown.CanExecute())
                throw new InvalidOperationException("Forced plugin movement controls are enabled");
            var toggle = view.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ToggleButton>()
                .Single(x => x.Name == "PluginActivationToggle" && Equals(x.Tag, plugin.DisplayName));
            if (toggle.IsEnabled) throw new InvalidOperationException("Forced plugin activation is enabled");
            detailsButton.IsChecked = false;
            await WaitFor(() => !view.GetVisualDescendants().OfType<TextBlock>().Any(x => x.Name == "Mo2PluginDiagnostics" && x.IsEffectivelyVisible),
                "Plugin details toggle did not close details");
            Console.WriteLine("PASS: " + expected.Length + " native plugin diagnostics, mod indices and restrictions match MO2; toolbar opens/closes details; tooltip retains order metadata");
        } finally {
            page.ShowPluginDetails = originalDetails;
            page.Adapter.SelectedModels.Clear();
            foreach (var row in originalSelection) page.Adapter.SelectedModels.Add(row);
        }
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
        // SelectedPanel is set by the workspace view's own subscription when it
        // activates, so it is null for the frames straight after the loadouts page is
        // opened. Reading it immediately threw a NullReferenceException on the line
        // below — which, in an unguarded block, took the whole process down and left
        // the run with no verdict at all rather than a named failure.
        await WaitFor(() => home.Panels.Count > 0 && home.SelectedPanel is not null && home.SelectedPanel.SelectedTab is not null,
            "Home workspace did not select a panel and tab");
        var panel = home.SelectedPanel;
        var tab = panel.SelectedTab;
        // Which step, and what the page actually held. All six call sites shared one
        // message — "History restored an incorrect game page" — including the first,
        // which runs before any history navigation and so cannot be about history at
        // all. A failure named none of: the step, the game the page was showing, the
        // headings it drew, or whether its view was bound.
        async Task AssertPage(string game, [System.Runtime.CompilerServices.CallerLineNumber] int line = 0) {
            string Saw() {
                var context = tab.Contents.PageData.Context is Mo2GamePageContext c ? c.Game : "(not a game page)";
                var page = tab.Contents.ViewModel as Mo2LoadoutsPage;
                var headings = page is null ? "(no loadouts page)"
                    : page.GameSectionViewModels.Count == 0 ? "(no sections)"
                    : string.Join("/", page.GameSectionViewModels.Select(x => x.HeadingText));
                var bound = page is not null && window.GetVisualDescendants()
                    .OfType<NexusMods.App.UI.Pages.MyLoadouts.MyLoadoutsView>().Any(x => ReferenceEquals(x.ViewModel, page));
                return $"context {context}, headings {headings}, view bound {bound}";
            }
            await WaitFor(() => tab.Contents.PageData.Context is Mo2GamePageContext context && context.Game == game &&
                tab.Contents.ViewModel is Mo2LoadoutsPage page && page.GameSectionViewModels.Count > 0 &&
                // MO2's word, not NMA's. Mo2HomePages heads a live game section
                // "<game> profiles" because that is what MO2 calls them; " Loadouts"
                // is ScenarioData's fixture wording, which this was written against
                // and never moved off. The check then failed on the very first
                // assertion — before any history navigation — and reported "History
                // restored an incorrect game page", which named neither the step nor
                // the heading it had actually found.
                page.GameSectionViewModels.All(x => x.HeadingText == game + " profiles") &&
                window.GetVisualDescendants().OfType<NexusMods.App.UI.Pages.MyLoadouts.MyLoadoutsView>().Any(x => ReferenceEquals(x.ViewModel, page)),
                $"the page for {game} at line {line} — saw {Saw()}");
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
            await WaitFor(() => live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count(x => !x.IsOverwrite && !x.IsSeparator), "Native mod rows did not refresh after game switch");
            var priorities = live.ModsPage!.Adapter.Source.Value.Items.Select(x => x.Get<ValueComponent<int>>(Mo2ModsAdapter.PriorityKey).Value.Value).ToArray();
            if (!priorities.SequenceEqual(priorities.Order())) throw new InvalidOperationException("Mod rows lost MO2 priority order after switching games");
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_SIDEBAR_LAUNCH") == "1") await VerifySidebarLaunch(live, window);
            if (Environment.GetEnvironmentVariable("MO2_VERIFY_TOPBAR") == "1") await VerifyTopBar(live, window, openLogs: true);
        }
        try {
            await VisitGameProfile(skyrim, selected);
            if (live.WorkspaceController.ActiveWorkspace.Panels.SelectMany(x => x.Tabs).Count(x => x.Contents.ViewModel is ScenarioInstalledPage { IsMo2Profile: true }) != 1)
                throw new InvalidOperationException("Profile selection duplicated the mod-list tab");
            await WaitFor(() => live.ModsPage!.Adapter.SourceCount.Value == live.Profile.Mods.Count(x => !x.IsOverwrite && !x.IsSeparator) && live.PluginsPage!.Adapter.SourceCount.Value == live.Profile.Order.Plugins.Count, "Both panels did not change to Skyrim");
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
            foreach (var mod in live.Profile.Mods.Where(x => !x.IsOverwrite)) {
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
        // MO2 is asked again until it agrees, rather than once. A single read taken
        // the instant the frontend's action returned caught MO2 before it had
        // finished applying it, and the check then threw — which ends the process,
        // so the restore in the finally below had asked MO2 to put the plugin back
        // but MO2 had not yet written its profile. A run that fails on a race and
        // leaves the host changed is worse than one that fails.
        async Task AssertHost(bool active, int priority)
        {
            var until = DateTime.UtcNow.AddSeconds(15);
            string? disagreement;
            do {
                var snapshot = await new Mo2BridgeClient(endpoint).SendAsync("snapshot");
                var state = snapshot.GetProperty("plugins").EnumerateArray()
                    .Single(x => x.GetProperty("name").GetString() == plugin.DisplayName).GetProperty("state").GetInt32();
                var at = snapshot.GetProperty("mods").EnumerateArray()
                    .Single(x => x.GetProperty("name").GetString() == mod.Name).GetProperty("priority").GetInt32();
                disagreement =
                    (state == 2) != active ? $"MO2 has {plugin.DisplayName} {(state == 2 ? "enabled" : "disabled")} where the frontend has it {(active ? "enabled" : "disabled")}"
                    : at != priority ? $"MO2 has {mod.Name} at priority {at} where the frontend has it at {priority}"
                    : null;
                if (disagreement is null) return;
                await Task.Delay(250);
            } while (DateTime.UtcNow < until);
            throw new InvalidOperationException("MO2 did not come to agree: " + disagreement);
        }
        try {
            live.PluginsPage!.Adapter.SelectedModels.Add(live.PluginsPage.Adapter.Source.Value.Items.Single(x => x.Key.Equals(plugin.Key)));
            // Through MO2's own menu on the row. The page's toolbar carried a Disable
            // button that called the same thing; the toolbar is gone and the entry it
            // duplicated is what MO2 offers.
            await PluginRowMenu(window, "Disable selected", plugin.DisplayName);
            await WaitFor(() => !live.Profile.Order.Plugins.Single(x => x.Key.Equals(plugin.Key)).IsActive, "Plugin disable failed");
            if ((live.Profile.Mods.Single(x => x.Id == mod.Id).State & 2) == 0) throw new InvalidOperationException("Plugin disable also disabled its mod");
            await AssertHost(false, mod.Priority);
            live.ModsPage!.Adapter.SelectedModels.Add(live.ModsPage.Adapter.Source.Value.Items.Single(x => x.Key == mod.Id));
            // There is no earlier/later pair to press any more, and MO2 has none
            // either: it moves a mod by dragging it or from its own Send to... menu,
            // which is what this frontend offers and what this drives. The check
            // still exists for what follows — that MO2's own snapshot agrees with
            // the move — rather than for which control started it.
            await ((Mo2ModsAdapter)live.ModsPage.Adapter).Move(mod.Id, -1);
            await WaitFor(() => live.Profile.Mods.Single(x => x.Id == mod.Id).Priority == mod.Priority - 1, "Mod priority button failed: " + live.Profile.Status);
            await AssertHost(false, mod.Priority - 1);
        } finally {
            await live.Profile.SetPluginsActive([plugin.DisplayName], plugin.IsActive);
            var current = live.Profile.Mods.Single(x => x.Id == mod.Id);
            await live.Profile.MoveMod(mod.Id, mod.Priority - current.Priority);
            // Waited for here, inside the finally: a run that is failing is exactly
            // the run whose restore has to have landed before the process ends, and
            // this used to be checked afterwards where a failure skipped it.
            // Thirty seconds, not the ten a wait gets by default: this is the one
            // that has to land, and a run ending with MO2 changed is worse than a
            // run that failed. It timed out at ten on a host that had just been
            // worked hard by the check before it, and left the plugin disabled.
            await WaitFor(() => live.Profile.Order.Plugins.Single(x => x.Key.Equals(plugin.Key)).IsActive == plugin.IsActive &&
                live.Profile.Mods.Single(x => x.Id == mod.Id).Priority == mod.Priority, "MO2 did not take the restore", 30);
        }
        await AssertHost(plugin.IsActive, mod.Priority);
        if (!live.Profile.Mods.OrderBy(x => x.Priority).Select(x => x.Name).SequenceEqual(originalMods)) throw new InvalidOperationException("Original mod order was not restored");
        Console.WriteLine("PASS: the plugin list's Disable and a mod priority move update MO2; plugin activation independent of mod activation; original state restored");
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

    private static async Task VerifyLiveCollectionRename(Mo2LiveWorkspace live, IClassicDesktopStyleApplicationLifetime desktop)
    {
        const string original = "__Rename UI check_separator", renamed = "UI renamed_separator";
        await WaitFor(() => live.Profile.IsConnected && live.Profile.Mods.Any(x => x.Name == original), "Required temporary collection is missing");
        var controller = live.WorkspaceController;
        var data = Mo2CollectionFactory.Data(original);
        for (var i = 0; i < 2; i++)
            controller.OpenPage(controller.ActiveWorkspaceId, data, controller.GetOpenPageBehavior(data, NavigationInformation.From(OpenPageBehaviorType.NewPanel)));
        await Task.Delay(500);
        var pages = controller.ActiveWorkspace.Panels.SelectMany(x => x.Tabs).Select(x => x.Contents)
            .Where(x => x.PageData.Context is Mo2CollectionContext c && c.Key == original).ToArray();
        if (pages.Length < 2) throw new InvalidOperationException("Multiple collection panels were not created");
        var model = (ScenarioInstalledPage)pages[0].ViewModel;
        controller.OpenPage(controller.ActiveWorkspaceId, data, controller.GetOpenPageBehavior(data, NavigationInformation.From(OpenPageBehaviorType.NewTab)));
        var historyTab = controller.ActiveWorkspace.SelectedTab;
        var historyPanel = controller.ActiveWorkspace.Panels.Single(x => x.Tabs.Contains(historyTab));
        controller.OpenPage(controller.ActiveWorkspaceId, Mo2CollectionFactory.Data(""),
            new OpenPageBehavior.ReplaceTab(historyPanel.Id, historyTab.Id), checkOtherPanels: false);
        async Task Submit(string title, ButtonDefinitionId action) {
            model.CommandRenameGroup.Execute(R3.Unit.Default);
            await WaitFor(() => desktop.Windows.OfType<DialogWindow>().Any(x => x.IsVisible), "Rename dialog did not open");
            var dialog = desktop.Windows.OfType<DialogWindow>().Single(x => x.IsVisible);
            ((IDialogStandardContentViewModel)dialog.ViewModel!.ContentViewModel!).InputText = title;
            await Task.Delay(100);
            dialog.ViewModel.ButtonPressCommand.Execute(action);
            await WaitFor(() => !desktop.Windows.OfType<DialogWindow>().Any(x => x.IsVisible), "Rename dialog did not close");
        }
        await Submit("Cancelled", ButtonDefinitionId.Cancel);
        if (!live.Profile.Mods.Any(x => x.Name == original)) throw new InvalidOperationException("Cancel changed native collection");
        await Submit("UI renamed", ButtonDefinitionId.Accept);
        await WaitFor(() => live.Profile.Mods.Any(x => x.Name == renamed), "Accepted dialog did not rename native separator");
        if (pages.Any(x => x.PageData.Context is not Mo2CollectionContext { Key: renamed } || ((ScenarioInstalledPage)x.ViewModel).CollectionKey != renamed))
            throw new InvalidOperationException("Open collection panel identities did not update");
        live.SaveLayouts();
        Console.WriteLine("PASS real NMA rename dialog Cancel/Accept; native separator and multiple open panel identities updated");
        await historyTab.GoBackInHistoryCommand.Execute();
        if (historyTab.Contents.PageData.Context is not Mo2CollectionContext { Key: renamed })
            throw new InvalidOperationException("Back reopened the old collection name");
        await historyTab.GoForwardInHistoryCommand.Execute();
        if (historyTab.Contents.PageData.Context is not Mo2CollectionContext { Key: "" })
            throw new InvalidOperationException("Rename changed an unrelated Forward entry");
        Console.WriteLine("PASS Back follows collection rename; unrelated Forward entry preserved");
        await Submit("__Rename UI check", ButtonDefinitionId.Accept);
        await WaitFor(() => live.Profile.Mods.Any(x => x.Name == original), "Test collection name was not restored");
        Console.WriteLine("PASS collection test name restored");
        async Task Remove(ButtonDefinitionId action) {
            model.CommandDeleteGroup.Execute(R3.Unit.Default);
            await WaitFor(() => desktop.Windows.OfType<DialogWindow>().Any(x => x.IsVisible), "Removal confirmation did not open");
            var dialog = desktop.Windows.OfType<DialogWindow>().Single(x => x.IsVisible);
            dialog.ViewModel!.ButtonPressCommand.Execute(action);
            await WaitFor(() => !desktop.Windows.OfType<DialogWindow>().Any(x => x.IsVisible), "Removal confirmation did not close");
        }
        await Remove(ButtonDefinitionId.Cancel);
        if (!live.Profile.Mods.Any(x => x.Name == original) || pages.Any(x => ((ScenarioInstalledPage)x.ViewModel).CollectionKey != original))
            throw new InvalidOperationException("Cancelled collection removal changed native state or panel identities");
        await Remove(ButtonDefinitionId.Accept);
        await WaitFor(() => !live.Profile.Mods.Any(x => x.Name == original) && !live.Profile.ManagingMod, "Accepted collection removal did not finish");
        if (pages.Any(x => x.PageData.Context is Mo2CollectionContext || ((ScenarioInstalledPage)x.ViewModel).CollectionKey is not null))
            throw new InvalidOperationException("Removed collection panels did not switch to My Mods");
        live.SaveLayouts();
        Console.WriteLine("PASS panel removal Cancel/Accept; native separator removed; all open collection panels switched to My Mods");
        await historyTab.GoBackInHistoryCommand.Execute();
        if (historyTab.Contents.PageData.Context is Mo2CollectionContext || ((ScenarioInstalledPage)historyTab.Contents.ViewModel).CollectionKey is not null)
            throw new InvalidOperationException("Back reopened a removed collection");
        await historyTab.GoForwardInHistoryCommand.Execute();
        if (historyTab.Contents.PageData.Context is not Mo2CollectionContext { Key: "" })
            throw new InvalidOperationException("Removal changed an unrelated Forward entry");
        Console.WriteLine("PASS Back follows collection removal to My Mods; Forward remains usable");
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

    // MO2's own Enable selected / Disable selected, from the menu it builds on a
    // plugin row. The page used to carry two buttons for these on a toolbar of its
    // own; that toolbar is gone, and these entries — which were always there beside
    // them — are what MO2 offers.
    // The row is named, not taken as whichever comes first: MO2 gates these entries
    // on the plugin the menu was opened over — a locked plugin's menu offers them
    // greyed however the selection stands — so opening the first row's menu asked
    // about the wrong plugin and reported Disable selected greyed out on a list
    // where it was live.
    private static async Task PluginRowMenu(Avalonia.Controls.Window window, string entry, string plugin)
    {
        var row = PluginRow(window, plugin);
        var menu = (MenuFlyout)row.ContextFlyout!;
        menu.ShowAt(row);
        await Task.Delay(500);
        var item = menu.Items.OfType<MenuItem>().FirstOrDefault(x => x.Header?.ToString() == entry);
        menu.Hide();
        await Task.Delay(200);
        if (item is null) throw new InvalidOperationException($"MO2's plugin menu has no {entry}");
        if (!item.IsEnabled) throw new InvalidOperationException($"MO2's plugin menu has {entry} greyed out");
        item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        await Task.Delay(300);
    }

    // The row MO2's menu is to be opened over, found by the plugin drawn in it.
    private static Control PluginRow(Avalonia.Controls.Window window, string plugin) =>
        window.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(x => x.Name == "PluginRedesignRow" && x.ContextFlyout is MenuFlyout &&
                x.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == plugin))
        ?? throw new InvalidOperationException($"No plugin row is drawing {plugin}");

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
