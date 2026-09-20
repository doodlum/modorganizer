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
        AddLoadout = new IconButtonViewModel { Name = "Add a game", Click = ReactiveCommand.Create(shell.OpenGames) };
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
            // Grouped by what the sidebar is showing rather than by game. A Wabbajack
            // modlist is its own MO2 instance and the whole point of it is that it
            // stands apart from the plain game it is built on — grouping by game put
            // Viva New Vegas and a person's own New Vegas behind one button, where
            // picking one of them was left to the tie-break below.
            foreach (var group in entries.GroupBy(Mo2SpineKey.For)) {
                var key = group.Key;
                var game = group.First().Instance!.Game;
                var item = new ImageButtonViewModel {
                    Name = Mo2SpineKey.Name(group.First()), Image = Mo2SpineKey.Image(group.First()),
                    Click = ReactiveCommand.CreateFromTask(async () => {
                        var available = shell.CatalogEntries.Where(x => x.Instance is not null &&
                            Mo2SpineKey.For(x) == key && x.Instance.Profiles.Length > 0).ToArray();
                        if (available.Length == 0) { shell.OpenLoadouts(game); return; }
                        (Mo2Registration, Mo2ProfileSnapshot) target;
                        if (remembered.TryGetValue(key, out var previous) && available.Any(x => x.Registration == previous.Registration && x.Instance!.Profiles.Any(p => p.Directory == previous.Profile.Directory)))
                            target = previous;
                        else {
                            // Multiple installations may manage the same game.
                            // Prefer its running host over launching an older one
                            // merely because it was registered first. Remembered
                            // choices above need no process scan and remain primary.
                            var entry = available.OrderByDescending(x => Mo2HostStartup.IsRunning(x.Registration))
                                .ThenByDescending(x => x.Registration.Launcher is not null).First();
                            var profile = entry.Instance!.Profiles.FirstOrDefault(x => x.Name == entry.Instance.SelectedProfile) ?? entry.Instance.Profiles.First();
                            target = (entry.Registration, profile);
                        }
                        if (shell.Profile.IsConnected && shell.Profile.CurrentTarget.Endpoint == target.Item1.Endpoint && Mo2InstanceCatalog.LocalPath(shell.Profile.ProfilePath) == Path.GetFullPath(target.Item2.Directory)) shell.ShowProfile();
                        else if (await shell.Profile.SelectProfile(target.Item1, target.Item2)) shell.ShowProfile();
                    })
                };
                targets.Add(item, key); games.Add(item);
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
            if (selected?.Instance is not null && profile is not null) remembered[Mo2SpineKey.For(selected)] = (selected.Registration, profile);
            var active = selected?.Instance is null ? null : Mo2SpineKey.For(selected);
            foreach (var (item, key) in targets)
                item.IsActive = !home && selectedPath is not null && active == key;

        }
        shell.WorkspaceController.WhenAnyValue(x => x.ActiveWorkspace).Subscribe(_ => RefreshSelection());
        shell.Profile.Changed += RefreshSelection;
    }
}

// What the sidebar shows an instance as.
//
// A base-game instance is its game, which is how it has always been and how two
// MO2 setups for one game still share a button. A Wabbajack modlist is itself: its
// own button, its own name and its own artwork, sitting beside the plain game it
// was built on rather than inside it. Keyed on the instance directory, because two
// modlists for one game are also two separate things.
internal static class Mo2SpineKey
{
    internal static string For(Mo2CatalogEntry entry) =>
        Mo2ModlistInstances.Describe(entry.Registration.Directory) is not null
            ? "modlist:" + Mo2InstanceCatalog.LocalPath(entry.Registration.Directory)
            : entry.Instance!.Game;

    internal static string Name(Mo2CatalogEntry entry) =>
        Mo2ModlistInstances.Describe(entry.Registration.Directory)?.Title ?? entry.Instance!.Game;

    internal static Avalonia.Media.Imaging.Bitmap Image(Mo2CatalogEntry entry)
    {
        var game = entry.Instance!.Game;
        if (Mo2ModlistInstances.Describe(entry.Registration.Directory) is not { } modlist)
            return Mo2GameArt.SquareIcon(game);
        var art = Mo2ModlistInstances.ArtworkPath(entry.Registration.Directory);
        return File.Exists(art)
            ? Mo2GameArt.SquareIconFile(modlist.NamespacedName, art, game)
            : Mo2GameArt.SquareIcon(game);
    }
}
