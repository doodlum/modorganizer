using System.Collections.ObjectModel;
using System.Reactive.Linq;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.App.UI.Controls.Spine;
using NexusMods.App.UI.Controls.LoadoutBadge;
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
        var targets = new Dictionary<ImageButtonViewModel, (Mo2Registration Registration, Mo2ProfileSnapshot Profile)>();
        string? fingerprint = null;
        void RefreshGames() {
            var entries = shell.CatalogEntries.Where(x => x.Instance is not null).ToArray();
            var next = System.Text.Json.JsonSerializer.Serialize(entries.Select(x => new {
                x.Registration, x.Instance!.Game, Profiles = x.Instance.Profiles.Select(p => new { p.Name, p.Directory })
            }));
            if (next == fingerprint) return;
            fingerprint = next;
            games.Clear(); targets.Clear();
            foreach (var entry in entries)
                foreach (var (profile, index) in entry.Instance!.Profiles.Select((profile, index) => (profile, index))) {
                    var item = new ImageButtonViewModel {
                        Name = entry.Instance.Game + " — " + profile.Name + " (" + entry.Registration.Directory + ")",
                        Image = Mo2GameArt.Icon(entry.Instance.Game),
                        LoadoutBadgeViewModel = new LoadoutBadgeDesignViewModel { LoadoutShortName = (index + 1).ToString() },
                        Click = ReactiveCommand.CreateFromTask(async () => {
                            if (await shell.Profile.SelectProfile(entry.Registration, profile)) shell.ShowProfile();
                        })
                    };
                    targets.Add(item, (entry.Registration, profile)); games.Add(item);
                }
            RefreshSelection();
        }
        RefreshGames();
        shell.CatalogChanged += RefreshGames;
        void RefreshSelection() {
            var home = shell.WorkspaceController.ActiveWorkspace.Context is HomeContext;
            ((IconButtonViewModel)Home).IsActive = home;
            // Direct endpoint startup precedes the first native profile snapshot.
            var selectedPath = shell.Profile.ProfilePath.Length == 0 ? null : Mo2InstanceCatalog.LocalPath(shell.Profile.ProfilePath);
            foreach (var (item, target) in targets)
                item.IsActive = !home && selectedPath is not null && shell.Profile.Endpoint == target.Registration.Endpoint &&
                    selectedPath == target.Profile.Directory;
        }
        shell.WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace).Subscribe(_ => RefreshSelection());
        shell.Profile.Changed += RefreshSelection;
    }
}
