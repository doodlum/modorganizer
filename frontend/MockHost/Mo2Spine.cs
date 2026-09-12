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
        var games = new ObservableCollection<IImageButtonViewModel>();
        LoadoutSpineItems = new(games);
        void RefreshGames() {
            var names = shell.CatalogEntries.Where(x => x.Instance is not null).Select(x => x.Instance!.Game).Distinct().ToArray();
            if (names.SequenceEqual(games.Select(x => x.Name))) return;
            games.Clear();
            foreach (var name in names) games.Add(new ImageButtonViewModel {
                Name = name, Image = Mo2GameArt.Cover(name), Click = ReactiveCommand.Create(() => shell.OpenLoadouts(name)) });
            RefreshSelection();
        }
        RefreshGames();
        shell.CatalogChanged += RefreshGames;
        void RefreshSelection() {
            var home = shell.WorkspaceController.ActiveWorkspace.Context is HomeContext;
            ((IconButtonViewModel)Home).IsActive = home;
            foreach (var item in LoadoutSpineItems.Cast<ImageButtonViewModel>()) item.IsActive = !home && (item.Name == shell.GameName || (item.Name == "New Vegas" && shell.Profile.NexusGame == "newvegas"));
        }
        shell.WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace).Subscribe(_ => RefreshSelection());
        shell.Profile.Changed += RefreshSelection;
    }
}
