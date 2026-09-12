using System.Reactive;
using System.Reactive.Linq;
using NexusMods.App.UI.Controls.TopBar;
using NexusMods.App.UI.WorkspaceSystem;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class ScenarioTopBar : TopBarDesignViewModel, ITopBarViewModel
{
    public new ReactiveCommand<Unit, Unit> NewTabCommand { get; }
    private IPanelTabViewModel? _selectedTab;
    public new IPanelTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set => this.RaiseAndSetIfChanged(ref _selectedTab, value);
    }

    public ScenarioTopBar(IWorkspaceController controller)
    {
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
