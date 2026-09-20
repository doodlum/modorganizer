using System.Reactive;
using NexusMods.App.UI.Controls.Navigation;
using System.Reactive.Linq;
using NexusMods.App.UI.Controls.TopBar;
using NexusMods.App.UI.WorkspaceSystem;
using ReactiveUI;
using Avalonia.Media;
using NexusMods.Abstractions.NexusWebApi.Types;
using NexusMods.UI.Sdk;
using System.Reactive.Subjects;

namespace Mo2.Frontend;

internal sealed class Mo2TopBar : AViewModel<ITopBarViewModel>, ITopBarViewModel, IDisposable
{
    public ReactiveCommand<Unit, Unit> NewTabCommand { get; }
    public ReactiveCommand<NavigationInformation, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDiscordCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenStatusPageCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenForumsCommand { get; } = ReactiveCommand.Create(() => { }, Observable.Return(false));
    public ReactiveCommand<NavigationInformation, Unit> ViewChangelogCommand { get; } = ReactiveCommand.Create<NavigationInformation>(_ => { }, Observable.Return(false));
    public ReactiveCommand<Unit, Unit> ShowWelcomeMessageCommand { get; } = Disabled();
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenNexusModsProfileCommand { get; } = Disabled();
    public ReactiveCommand<Unit, Unit> OpenNexusModsPremiumCommand { get; } = Disabled();
    public ReactiveCommand<Unit, Unit> OpenNexusModsAccountSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ViewAppLogsCommand { get; }
    public R3.ReactiveCommand<R3.Unit, R3.Unit> LoginCommand { get; }
    public Func<bool, Task> ManageAccount { get; }
    private bool _isLoggedIn;
    private string? _username;
    private Mo2LoginResult? _confirmedAccount;
    public bool IsLoggedIn { get => _isLoggedIn; private set => this.RaiseAndSetIfChanged(ref _isLoggedIn, value); }
    private UserRole _userRole = UserRole.Free;
    public UserRole UserRole { get => _userRole; private set => this.RaiseAndSetIfChanged(ref _userRole, value); }
    public string? Username { get => _username; private set => this.RaiseAndSetIfChanged(ref _username, value); }
    public IImage? Avatar => null;
    private string _title = "Home", _subtitle = "";
    public string ActiveWorkspaceTitle { get => _title; private set => this.RaiseAndSetIfChanged(ref _title, value); }
    public string ActiveWorkspaceSubtitle { get => _subtitle; private set => this.RaiseAndSetIfChanged(ref _subtitle, value); }
    public IAddPanelDropDownViewModel AddPanelDropDownViewModel { get; set; }
    private static ReactiveCommand<Unit, Unit> Disabled() => ReactiveCommand.Create(() => { }, Observable.Return(false));
    private readonly BehaviorSubject<bool> _canOpenLogs = new(false);
    private readonly Mo2AccountMonitor _account;
    private readonly Avalonia.Threading.DispatcherTimer _accountTimer;
    private readonly Mo2LiveWorkspace _shell;
    private async void RefreshAccount() => await _account.Refresh();
    public void Dispose()
    {
        _accountTimer.Stop();
        _shell.CatalogChanged -= RefreshAccount;
        _account.Dispose();
    }
    public string? LogsDirectory { get; private set; }
    private IPanelTabViewModel? _selectedTab;
    public IPanelTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public Mo2TopBar(Mo2LiveWorkspace shell)
    {
        _shell = shell;
        var controller = shell.WorkspaceController;
        void UpdateLogs() {
            var directory = shell.Profile.LogsDirectory;
            LogsDirectory = shell.Profile.IsConnected && !shell.Profile.SelectingProfile && directory is not null && Directory.Exists(directory) ? directory : null;
            _canOpenLogs.OnNext(LogsDirectory is not null);
        }
        ViewAppLogsCommand = ReactiveCommand.Create(() => {
            UpdateLogs();
            if (LogsDirectory is { } directory) shell.DesktopInterop.OpenDirectory(
                NexusMods.Paths.FileSystem.Shared.FromUnsanitizedFullPath(Mo2InstanceCatalog.DesktopPath(directory)));
        }, _canOpenLogs.DistinctUntilChanged());
        shell.Profile.Changed += UpdateLogs;
        UpdateLogs();
        OpenGitHubCommand = ReactiveCommand.Create(() => shell.DesktopInterop.OpenUri(new Uri("https://github.com/doodlum/modorganizer")));
        OpenDiscordCommand = ReactiveCommand.Create(() => shell.DesktopInterop.OpenUri(new Uri("https://discord.gg/ewUVAqyrQX")));
        OpenStatusPageCommand = ReactiveCommand.Create(() => shell.DesktopInterop.OpenUri(NexusMods.App.UI.ConstantLinks.StatusPageUri));
        OpenSettingsCommand = ReactiveCommand.Create<NavigationInformation>(info =>
            controller.OpenPage(controller.ActiveWorkspaceId, shell.SettingsPage, controller.GetOpenPageBehavior(shell.SettingsPage, info)));
        ActiveWorkspaceSubtitle = "";
        _account = new(() => Mo2SharedNexusLogin.ReadStatus(shell.Catalog.Registrations), ApplyAccount);
        async Task Account(bool logout) {
            if (!_account.BeginLogin()) {
                // Without this the button silently does nothing while a previous
                // attempt is still open.
                shell.NexusAccountStatus.OnNext("Nexus sign-in is already open. Finish or cancel it first.");
                return;
            }
            Mo2LoginResult? result = null;
            try {
                result = await shell.LoginToNexus(logout);
                if (result is null) shell.NexusAccountStatus.OnNext(
                    logout ? "Sign-out did not start; the frontend window is not ready."
                           : "Nexus sign-in did not start; the frontend window is not ready.");
            } catch (Exception error) {
                // A throwing command is swallowed by ReactiveCommand, which is what
                // made a failed sign-in look like a dead button.
                shell.NexusAccountStatus.OnNext("Nexus sign-in failed: " + error.Message);
                Console.Error.WriteLine("Nexus sign-in failed: " + error);
            }
            finally { await _account.EndLogin(result); }
        }
        ManageAccount = Account;
        LoginCommand = R3.ReactiveCommandExtensions.ToReactiveCommand<R3.Unit, R3.Unit>(R3.Observable.Return(true), async (_, _) => {
            await Account(false);
            return R3.Unit.Default;
        }, awaitOperation: R3.AwaitOperation.Drop);
        void ApplyAccount(Mo2LoginResult? result) {
            if (result is null) return;
            var status = $"Nexus account · {result.Connected} of {result.Total} instances connected";
            if (result.Unavailable > 0) status += $" · {result.Unavailable} unavailable; status not confirmed";
            if (result.Failures.Count > 0) status += ". " + result.Failures[0];
            var retainAccount = result.CanRetainConfirmedAccount(_confirmedAccount);
            if (retainAccount) status += ". Showing the last confirmed account.";
            shell.NexusAccountStatus.OnNext(status);
            if (retainAccount) return;
            IsLoggedIn = result.Total > 0 && result.Connected == result.Total && result.Failures.Count == 0;
            _confirmedAccount = IsLoggedIn ? result : null;
            Username = IsLoggedIn ? result.Username : null;
            UserRole = !IsLoggedIn ? UserRole.Free : result.Premium ? UserRole.Premium : result.Supporter ? UserRole.Supporter : UserRole.Free;
        }
        OpenNexusModsAccountSettingsCommand = ReactiveCommand.CreateFromTask(() => Account(false));
        LogoutCommand = ReactiveCommand.CreateFromTask(() => Account(true), this.WhenAnyValue(x => x.IsLoggedIn));
        shell.CatalogChanged += RefreshAccount;
        _accountTimer = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _accountTimer.Tick += (_, _) => RefreshAccount();
        _accountTimer.Start();
        RefreshAccount();
        AddPanelDropDownViewModel = new AddPanelDropDownViewModel(controller);
        NewTabCommand = ReactiveCommand.Create(() => controller.ActiveWorkspace.SelectedPanel.AddDefaultTab());
        controller.WhenAnyValue(c => c.ActiveWorkspace).Subscribe(workspace => {
            ActiveWorkspaceTitle = workspace.Context is Mo2WorkspaceContext ? shell.GameName : workspace.Title;
            ActiveWorkspaceSubtitle = workspace.Context is Mo2WorkspaceContext ? shell.Profile.CollectionName.Value : "";
        });
        shell.Profile.Changed += () => {
            if (controller.ActiveWorkspace.Context is Mo2WorkspaceContext) {
                ActiveWorkspaceTitle = shell.GameName;
                ActiveWorkspaceSubtitle = shell.Profile.CollectionName.Value;
            }
        };
        controller.WhenAnyValue(c => c.ActiveWorkspace)
            .Select(workspace => workspace.WhenAnyValue(w => w.SelectedTab))
            .Switch()
            .Subscribe(tab => SelectedTab = tab);
    }
}
