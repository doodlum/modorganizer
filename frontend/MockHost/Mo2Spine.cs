using System.Collections.ObjectModel;
using System.Reactive.Linq;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.App.UI.Controls.Spine;
using NexusMods.App.UI.Controls.Spine.Buttons.Download;
using NexusMods.App.UI.Controls.Spine.Buttons.Icon;
using NexusMods.App.UI.Controls.Spine.Buttons.Image;
using NexusMods.App.UI.LeftMenu;
using NexusMods.UI.Sdk;
using ReactiveUI;
namespace Mo2.Frontend;
internal sealed class Mo2Spine : AViewModel<ISpineViewModel>, ISpineViewModel
{
    private readonly Mo2LiveWorkspace _shell;
    public void NavigateToHome() => _shell.OpenGames();
    public ILeftMenuViewModel? LeftMenuViewModel { get; }
    public IIconButtonViewModel Home { get; }
    public IIconButtonViewModel AddLoadout { get; }
    public ISpineDownloadButtonViewModel Downloads { get; }
    public ReadOnlyObservableCollection<IImageButtonViewModel> LoadoutSpineItems { get; }
    public Mo2Spine(Mo2LiveWorkspace shell)
    {
        _shell = shell;
        LeftMenuViewModel = shell.HomeMenu;
        Home = new IconButtonViewModel { Name = "Home", Click = ReactiveCommand.Create(shell.OpenGames) };
        AddLoadout = new IconButtonViewModel { Name = "Manage MO2 instances", Click = ReactiveCommand.Create(shell.OpenConnections) };
        Downloads = new SpineDownloadButtonDesignerViewModel { Number = 0, Units = "", Click = ReactiveCommand.Create(shell.OpenDownloads) };
        LoadoutSpineItems = new(new ObservableCollection<IImageButtonViewModel>(shell.Catalog.Read().Where(x => x.Instance is not null)
            .GroupBy(x => x.Instance!.Game).Select(group => new ImageButtonViewModel {
                Name = group.Key, Image = Mo2GameArt.Cover(group.Key), Click = ReactiveCommand.Create(() => shell.OpenLoadouts(group.Key)) })));
        void RefreshSelection() {
            var home = shell.WorkspaceController.ActiveWorkspace.Context is HomeContext;
            ((IconButtonViewModel)Home).IsActive = home;
            foreach (var item in LoadoutSpineItems.Cast<ImageButtonViewModel>()) item.IsActive = !home && item.Name == shell.GameName;
        }
        shell.WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace).Subscribe(_ => RefreshSelection());
        shell.Profile.Changed += RefreshSelection;
    }
}
