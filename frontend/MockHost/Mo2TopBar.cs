using System.Reactive;
using NexusMods.App.UI.Controls.Navigation;
using System.Reactive.Linq;
using NexusMods.App.UI.Controls.TopBar;
using NexusMods.App.UI.WorkspaceSystem;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class Mo2TopBar : TopBarDesignViewModel, ITopBarViewModel
{
    public new ReactiveCommand<Unit, Unit> NewTabCommand { get; }
    public new ReactiveCommand<NavigationInformation, Unit> OpenSettingsCommand { get; }
    public new ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }
    public new ReactiveCommand<Unit, Unit> OpenDiscordCommand { get; }
    public new ReactiveCommand<Unit, Unit> OpenStatusPageCommand { get; }
    public new ReactiveCommand<Unit, Unit> OpenForumsCommand { get; } = ReactiveCommand.Create(() => { }, Observable.Return(false));
    private IPanelTabViewModel? _selectedTab;
    public new IPanelTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public Mo2TopBar(Mo2LiveWorkspace shell)
    {
        var controller = shell.WorkspaceController;
        OpenGitHubCommand = ReactiveCommand.Create(() => shell.DesktopInterop.OpenUri(new Uri("https://github.com/doodlum/modorganizer")));
        OpenDiscordCommand = ReactiveCommand.Create(() => shell.DesktopInterop.OpenUri(new Uri("https://discord.gg/ewUVAqyrQX")));
        OpenStatusPageCommand = ReactiveCommand.Create(() => shell.DesktopInterop.OpenUri(NexusMods.App.UI.ConstantLinks.StatusPageUri));
        OpenSettingsCommand = ReactiveCommand.Create<NavigationInformation>(info =>
            controller.OpenPage(controller.ActiveWorkspaceId, shell.ConnectionsPage, controller.GetOpenPageBehavior(shell.ConnectionsPage, info)));
        ActiveWorkspaceSubtitle = "";
        IsLoggedIn = false;
        Username = null;
        LoginCommand = R3.ReactiveCommandExtensions.ToReactiveCommand<R3.Unit, R3.Unit>(R3.Observable.Return(false), _ => R3.Unit.Default);
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
