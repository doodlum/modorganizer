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

internal sealed class Mo2TopBar : AViewModel<ITopBarViewModel>, ITopBarViewModel
{
    public ReactiveCommand<Unit, Unit> NewTabCommand { get; }
    public ReactiveCommand<NavigationInformation, Unit> OpenSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenDiscordCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenStatusPageCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenForumsCommand { get; } = ReactiveCommand.Create(() => { }, Observable.Return(false));
    public ReactiveCommand<NavigationInformation, Unit> ViewChangelogCommand { get; } = ReactiveCommand.Create<NavigationInformation>(_ => { }, Observable.Return(false));
    public ReactiveCommand<Unit, Unit> ShowWelcomeMessageCommand { get; } = Disabled();
    public ReactiveCommand<Unit, Unit> LogoutCommand { get; } = Disabled();
    public ReactiveCommand<Unit, Unit> OpenNexusModsProfileCommand { get; } = Disabled();
    public ReactiveCommand<Unit, Unit> OpenNexusModsPremiumCommand { get; } = Disabled();
    public ReactiveCommand<Unit, Unit> OpenNexusModsAccountSettingsCommand { get; } = Disabled();
    public ReactiveCommand<Unit, Unit> ViewAppLogsCommand { get; }
    public R3.ReactiveCommand<R3.Unit, R3.Unit> LoginCommand { get; }
    // The account button opens MO2's account settings. No separate NMA login state.
    public bool IsLoggedIn => false;
    public UserRole UserRole => UserRole.Free;
    public string? Username => null;
    public IImage? Avatar => null;
    private string _title = "Home", _subtitle = "";
    public string ActiveWorkspaceTitle { get => _title; private set => this.RaiseAndSetIfChanged(ref _title, value); }
    public string ActiveWorkspaceSubtitle { get => _subtitle; private set => this.RaiseAndSetIfChanged(ref _subtitle, value); }
    public IAddPanelDropDownViewModel AddPanelDropDownViewModel { get; set; }
    private static ReactiveCommand<Unit, Unit> Disabled() => ReactiveCommand.Create(() => { }, Observable.Return(false));
    private readonly BehaviorSubject<bool> _canOpenLogs = new(false);
    public string? LogsDirectory { get; private set; }
    private IPanelTabViewModel? _selectedTab;
    public IPanelTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public Mo2TopBar(Mo2LiveWorkspace shell)
    {
        var controller = shell.WorkspaceController;
        void UpdateLogs() {
            var directory = shell.Profile.LogsDirectory;
            LogsDirectory = shell.Profile.IsConnected && !shell.Profile.SelectingProfile && directory is not null && Directory.Exists(directory) ? directory : null;
            _canOpenLogs.OnNext(LogsDirectory is not null);
        }
        ViewAppLogsCommand = ReactiveCommand.Create(() => {
            UpdateLogs();
            if (LogsDirectory is { } directory) shell.DesktopInterop.OpenDirectory(NexusMods.Paths.FileSystem.Shared.FromUnsanitizedFullPath(directory));
        }, _canOpenLogs.DistinctUntilChanged());
        shell.Profile.Changed += UpdateLogs;
        UpdateLogs();
        OpenGitHubCommand = ReactiveCommand.Create(() => shell.DesktopInterop.OpenUri(new Uri("https://github.com/doodlum/modorganizer")));
        OpenDiscordCommand = ReactiveCommand.Create(() => shell.DesktopInterop.OpenUri(new Uri("https://discord.gg/ewUVAqyrQX")));
        OpenStatusPageCommand = ReactiveCommand.Create(() => shell.DesktopInterop.OpenUri(NexusMods.App.UI.ConstantLinks.StatusPageUri));
        OpenSettingsCommand = ReactiveCommand.Create<NavigationInformation>(info =>
            controller.OpenPage(controller.ActiveWorkspaceId, shell.ConnectionsPage, controller.GetOpenPageBehavior(shell.ConnectionsPage, info)));
        ActiveWorkspaceSubtitle = "";
        LoginCommand = R3.ReactiveCommandExtensions.ToReactiveCommand<R3.Unit, R3.Unit>(R3.Observable.Return(true), async (_, _) => {
            if (shell.Profile.ProfilePath.Length == 0) shell.OpenLoadouts(null);
            else await shell.Profile.ManageNexusAccount();
            return R3.Unit.Default;
        }, awaitOperation: R3.AwaitOperation.Drop);
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
