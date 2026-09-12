using System.Reactive;
using NexusMods.App.UI.Controls.Navigation;
using System.Reactive.Linq;
using NexusMods.App.UI.Controls.TopBar;
using NexusMods.App.UI.WorkspaceSystem;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class ScenarioTopBar : TopBarDesignViewModel, ITopBarViewModel
{
    public new ReactiveCommand<Unit, Unit> NewTabCommand { get; }
    public new ReactiveCommand<NavigationInformation, Unit> OpenSettingsCommand { get; }
    private IPanelTabViewModel? _selectedTab;
    public new IPanelTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public ScenarioTopBar(ScenarioWorkspace scenario)
    {
        var controller = scenario.WorkspaceController;
        OpenSettingsCommand = ReactiveCommand.Create<NavigationInformation>(info =>
            controller.OpenPage(controller.ActiveWorkspaceId, scenario.SettingsPage, controller.GetOpenPageBehavior(scenario.SettingsPage, info)));
        ActiveWorkspaceSubtitle = "";
        Username = "Test User";
        AddPanelDropDownViewModel = new AddPanelDropDownViewModel(controller);
        NewTabCommand = ReactiveCommand.Create(() => controller.ActiveWorkspace.SelectedPanel.AddDefaultTab());
        controller.WhenAnyValue(c => c.ActiveWorkspace)
            .Select(workspace => workspace.WhenAnyValue(w => w.SelectedTab))
            .Switch()
            .Subscribe(tab => SelectedTab = tab);
    }
}
