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
        var targets = new Dictionary<ImageButtonViewModel, string>();
        var remembered = new Dictionary<string, (Mo2Registration Registration, Mo2ProfileSnapshot Profile)>();
        string? fingerprint = null;
        void RefreshGames() {
            var entries = shell.CatalogEntries.Where(x => x.Instance is not null).ToArray();
            var next = System.Text.Json.JsonSerializer.Serialize(entries.Select(x => new {
                x.Registration, x.Instance!.Game, Profiles = x.Instance.Profiles.Select(p => new { p.Name, p.Directory })
            }));
            if (next == fingerprint) return;
            fingerprint = next;
            games.Clear(); targets.Clear();
            foreach (var group in entries.GroupBy(x => x.Instance!.Game)) {
                var game = group.Key;
                var item = new ImageButtonViewModel {
                    Name = game, Image = Mo2GameArt.PlatedIcon(game),
                    Click = ReactiveCommand.CreateFromTask(async () => {
                        var available = shell.CatalogEntries.Where(x => x.Instance?.Game == game && x.Instance.Profiles.Length > 0).ToArray();
                        if (available.Length == 0) { shell.OpenLoadouts(game); return; }
                        var entry = available.OrderByDescending(x => x.Registration.Launcher is not null).First();
                        var profile = entry.Instance!.Profiles.FirstOrDefault(x => x.Name == entry.Instance.SelectedProfile) ?? entry.Instance.Profiles.First();
                        var target = remembered.TryGetValue(game, out var previous) && available.Any(x => x.Registration == previous.Registration && x.Instance!.Profiles.Any(p => p.Directory == previous.Profile.Directory))
                            ? previous : (entry.Registration, profile);
                        if (shell.Profile.IsConnected && shell.Profile.CurrentTarget.Endpoint == target.Item1.Endpoint && Mo2InstanceCatalog.LocalPath(shell.Profile.ProfilePath) == Path.GetFullPath(target.Item2.Directory)) shell.ShowProfile();
                        else if (await shell.Profile.SelectProfile(target.Item1, target.Item2)) shell.ShowProfile();
                    })
                };
                targets.Add(item, game); games.Add(item);
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
            var selected = shell.CatalogEntries.FirstOrDefault(x => x.Registration.Endpoint == shell.Profile.Endpoint);
            var profile = selected?.Instance?.Profiles.FirstOrDefault(x => x.Directory == selectedPath);
            if (selected?.Instance is not null && profile is not null) remembered[selected.Instance.Game] = (selected.Registration, profile);
            foreach (var (item, game) in targets)
                item.IsActive = !home && selectedPath is not null && selected?.Instance?.Game == game;

        }
        shell.WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace).Subscribe(_ => RefreshSelection());
        shell.Profile.Changed += RefreshSelection;
    }
}
