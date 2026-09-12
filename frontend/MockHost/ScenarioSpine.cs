using System.Collections.ObjectModel;
using System.Reactive.Linq;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.Controls.Spine;
using NexusMods.App.UI.Controls.Spine.Buttons.Download;
using NexusMods.App.UI.Controls.Spine.Buttons.Icon;
using NexusMods.App.UI.Controls.Spine.Buttons.Image;
using NexusMods.App.UI.LeftMenu;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class ScenarioSpine : AViewModel<ISpineViewModel>, ISpineViewModel
{
    private readonly ScenarioWorkspace _scenario;
    private readonly ObservableCollection<IImageButtonViewModel> _loadouts = new();
    public ILeftMenuViewModel? LeftMenuViewModel => _scenario.HomeMenu;
    public IIconButtonViewModel Home { get; }
    public IIconButtonViewModel AddLoadout { get; }
    public ISpineDownloadButtonViewModel Downloads { get; } = new SpineDownloadButtonDesignerViewModel {
        Number = 0, Units = "", Click = ReactiveCommand.Create(() => { }) };
    public ReadOnlyObservableCollection<IImageButtonViewModel> LoadoutSpineItems { get; }
    public ScenarioSpine(ScenarioWorkspace scenario)
    {
        _scenario = scenario;
        Home = new IconButtonViewModel { Name = "Home", IsActive = true, Click = ReactiveCommand.Create(NavigateToHome) };
        AddLoadout = new IconButtonViewModel { Name = "Add game", Click = ReactiveCommand.Create(NavigateToHome) };
        LoadoutSpineItems = new(_loadouts);
        ((System.Collections.Specialized.INotifyCollectionChanged)scenario.Data.Section.CardViewModels).CollectionChanged += (_, _) => Refresh();
        Refresh();
    }
    public void NavigateToHome() => _scenario.HomeMenu.LeftMenuItemMyGames.NavigateCommand
        .Execute(NavigationInformation.From(NavigationInput.Default)).Subscribe();
    private void Refresh()
    {
        _loadouts.Clear();
        foreach (var card in _scenario.Data.Section.Loadouts)
            _loadouts.Add(new ImageButtonViewModel { Name = card.LoadoutName, Image = card.LoadoutImage,
                LoadoutBadgeViewModel = card.LoadoutBadgeViewModel,
                Click = ReactiveCommand.Create(() => { _scenario.HomeMenu.LeftMenuItemMyLoadouts.NavigateCommand
                    .Execute(NavigationInformation.From(NavigationInput.Default)).Subscribe(); }) });
    }
}
