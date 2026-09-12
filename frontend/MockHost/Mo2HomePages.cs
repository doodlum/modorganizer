using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using NexusMods.Abstractions.GameLocators;
using NexusMods.App.UI.Controls.GameWidget;
using NexusMods.App.UI.Controls.LoadoutBadge;
using NexusMods.App.UI.Controls.LoadoutCard;
using NexusMods.App.UI.Pages.MyGames;
using NexusMods.App.UI.Pages.MyLoadouts;
using NexusMods.App.UI.Pages.MyLoadouts.GameLoadoutsSectionEntry;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class Mo2GamesPage : APageViewModel<IMyGamesViewModel>, IMyGamesViewModel
{
    public ReactiveCommand<Unit, Unit> OpenRoadmapCommand { get; } = ReactiveCommand.Create(() => { }, Observable.Return(false));
    public ReadOnlyObservableCollection<IGameWidgetViewModel> InstalledGames { get; }
    public ReadOnlyObservableCollection<IViewModelInterface> SupportedGames { get; } = new(new());
    public Mo2GamesPage(IWindowManager windows, Mo2LiveWorkspace shell) : base(windows)
    {
        TabTitle = "My Games"; TabIcon = IconValues.GamepadOutline;
        InstalledGames = new(new ObservableCollection<IGameWidgetViewModel>(shell.Catalog.Read()
            .Where(x => x.Instance is not null).GroupBy(x => x.Instance!.Game)
            .Select(group => new Mo2GameCard(group.Key, () => shell.OpenLoadouts(group.Key)))));
    }
}
internal sealed class Mo2GameCard : AViewModel<IGameWidgetViewModel>, IGameWidgetViewModel
{
    public GameInstallation Installation { get; set; } = new() { Store = GameStore.Steam };
    public string Name { get; }
    public string Version => "Mod Organizer 2";
    public string Store => "Steam";
    public IconValue GameStoreIcon => IconValues.Steam;
    public Bitmap Image { get; }
    public ReactiveCommand<Unit, Unit> AddGameCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ViewGameCommand { get; set; }
    public ReactiveCommand<Unit, Unit> RemoveAllLoadoutsCommand { get; set; } = ReactiveCommand.Create(() => { }, Observable.Return(false));
    public IObservable<bool> IsManagedObservable { get; set; } = Observable.Return(true);
    public GameWidgetState State { get; set; } = GameWidgetState.ManagedGame;
    public Mo2GameCard(string name, Action visit)
    { Name = name; Image = Mo2GameArt.Cover(name); ViewGameCommand = ReactiveCommand.Create(visit); AddGameCommand = ViewGameCommand; }
}
internal static class Mo2GameArt
{
    public static Bitmap Cover(string game)
    {
        var id = game.Contains("Skyrim", StringComparison.OrdinalIgnoreCase) ? "489830" : "22380";
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".local/share/Steam/appcache/librarycache/{id}/library_600x900.jpg");
        return File.Exists(path) ? new Bitmap(path) : new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://NexusMods.App.UI/Assets/mod-thumbnail-fallback.png")));
    }
}
internal sealed class Mo2LoadoutsPage : APageViewModel<IMyLoadoutsViewModel>, IMyLoadoutsViewModel
{
    public ReadOnlyObservableCollection<IGameLoadoutsSectionEntryViewModel> GameSectionViewModels { get; }
    public Mo2LoadoutsPage(IWindowManager windows, Mo2LiveWorkspace shell, string? game) : base(windows)
    {
        TabTitle = "My Loadouts"; TabIcon = IconValues.Package;
        GameSectionViewModels = new(new ObservableCollection<IGameLoadoutsSectionEntryViewModel>(shell.Catalog.Read()
            .Where(x => x.Instance is not null && (game is null || x.Instance.Game == game))
            .GroupBy(entry => entry.Instance!.Game).Select(group => new Mo2LoadoutsSection(shell, group.Key, group))));
    }
}
internal sealed class Mo2LoadoutsSection : AViewModel<IGameLoadoutsSectionEntryViewModel>, IGameLoadoutsSectionEntryViewModel
{
    public void Dispose() { }
    public string HeadingText { get; }
    public ReadOnlyObservableCollection<IViewModelInterface> CardViewModels { get; }
    public Mo2LoadoutsSection(Mo2LiveWorkspace shell, string game, IEnumerable<Mo2CatalogEntry> entries)
    {
        HeadingText = game + " Loadouts";
        CardViewModels = new(new ObservableCollection<IViewModelInterface>(entries.SelectMany(entry => entry.Instance!.Profiles.Select((profile, index) =>
            new Mo2LoadoutCard(shell, entry, profile, index + 1)))));
    }
}
internal sealed class Mo2LoadoutCard : AViewModel<ILoadoutCardViewModel>, ILoadoutCardViewModel
{
    public Mo2Registration Registration { get; }
    public Mo2ProfileSnapshot Profile { get; }
    public ILoadoutBadgeViewModel LoadoutBadgeViewModel { get; }
    public string LoadoutName => Profile.Name;
    public IImage LoadoutImage { get; }
    public bool IsLoadoutApplied => false;
    public string HumanizedLoadoutLastApplyTime => "";
    public string HumanizedLoadoutCreationTime => Path.GetFileName(Registration.Directory) == "modorganizer2"
        ? Path.GetFileName(Path.GetDirectoryName(Registration.Directory)) ?? "MO2" : Path.GetFileName(Registration.Directory);
    public string LoadoutModCount => $"Mods {Profile.ModEntries.Count(x => x.Enabled)} enabled / {Profile.ModEntries.Length}";
    public bool IsDeleting => false;
    public bool IsSkeleton => false;
    public bool IsLastLoadout => true;
    public ReactiveCommand<Unit, Unit> VisitLoadoutCommand { get; }
    public ReactiveCommand<Unit, Unit> CloneLoadoutCommand { get; } = ReactiveCommand.Create(() => { }, Observable.Return(false));
    public ReactiveCommand<Unit, Unit> DeleteLoadoutCommand { get; } = ReactiveCommand.Create(() => { }, Observable.Return(false));
    public Mo2LoadoutCard(Mo2LiveWorkspace shell, Mo2CatalogEntry entry, Mo2ProfileSnapshot profile, int number)
    {
        Registration = entry.Registration; Profile = profile;
        LoadoutImage = Mo2GameArt.Cover(entry.Instance!.Game);
        LoadoutBadgeViewModel = new LoadoutBadgeDesignViewModel { LoadoutShortName = number.ToString() };
        VisitLoadoutCommand = ReactiveCommand.CreateFromTask(async () => {
            if (await shell.Profile.SelectProfile(Registration, profile)) shell.ShowProfile();
        });
    }
}

internal sealed record Mo2GamePageContext(PageFactoryId FactoryId, string? Game) : IPageFactoryContext;
internal sealed class Mo2GameLoadoutsFactory(IWindowManager windows, Mo2LiveWorkspace shell) : IPageFactory
{
    public PageFactoryId Id { get; } = PageFactoryId.From(Guid.Parse("bcde2778-955d-4b57-a14e-85a878b82107"));
    public PageData Data => new() { FactoryId = Id, Context = new Mo2GamePageContext(Id, null) };
    public DynamicData.Kernel.Optional<OpenPageBehaviorType> DefaultOpenPageBehavior => default;
    public Page Create(IPageFactoryContext context) => new() {
        ViewModel = new Mo2LoadoutsPage(windows, shell, ((Mo2GamePageContext)context).Game),
        PageData = new PageData { FactoryId = Id, Context = context }
    };
    public IEnumerable<PageDiscoveryDetails?> GetDiscoveryDetails(IWorkspaceContext context) => [];
}
