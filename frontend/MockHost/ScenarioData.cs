using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
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

// Every page observes the same in-memory objects, including pages opened in other panels.
internal sealed class ScenarioData
{
    public ScenarioGame Game { get; }
    public ScenarioLoadoutsSection Section { get; }
    public ObservableCollection<IGameLoadoutsSectionEntryViewModel> Sections { get; } = new();
    public ScenarioData(Action viewLoadouts)
    {
        Section = new ScenarioLoadoutsSection();
        Game = new ScenarioGame(() => {
            if (!Sections.Contains(Section)) Sections.Add(Section);
            if (Section.Loadouts.Count == 0) Section.CreateLoadout();
        }, () => { Section.Clear(); Sections.Clear(); }, viewLoadouts);
    }
}

internal sealed class ScenarioGame : AViewModel<IGameWidgetViewModel>, IGameWidgetViewModel
{
    public GameInstallation Installation { get; set; } = new() { Store = GameStore.Steam };
    public string Name => "Fallout: New Vegas";
    public string Version => "Version: test scenario";
    public string InstallationPath => Environment.GetEnvironmentVariable("MO2_SCENARIO_GAME_PATH") ?? "/home/deck/.local/share/Steam/steamapps/common/Fallout New Vegas/";
    public string Store => $"Steam — {InstallationPath}";
    public IconValue GameStoreIcon => IconValues.Steam;
    public Bitmap Image { get; } = ScenarioImages.Cover();
    public ReactiveCommand<Unit, Unit> AddGameCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ViewGameCommand { get; set; }
    public ReactiveCommand<Unit, Unit> RemoveAllLoadoutsCommand { get; set; }
    public IObservable<bool> IsManagedObservable { get; set; }
    private GameWidgetState _state = GameWidgetState.DetectedGame;
    public GameWidgetState State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }
    public ScenarioGame(Action add, Action remove, Action view)
    {
        IsManagedObservable = this.WhenAnyValue(x => x.State).Select(x => x == GameWidgetState.ManagedGame);
        AddGameCommand = ReactiveCommand.Create(() => { add(); State = GameWidgetState.ManagedGame; });
        RemoveAllLoadoutsCommand = ReactiveCommand.Create(() => { remove(); State = GameWidgetState.DetectedGame; });
        ViewGameCommand = ReactiveCommand.Create(view);
    }
}

internal sealed class ScenarioGamesPage : APageViewModel<IMyGamesViewModel>, IMyGamesViewModel
{
    public ReactiveCommand<Unit, Unit> OpenRoadmapCommand { get; } = ReactiveCommand.Create(() => { });
    public ReadOnlyObservableCollection<IGameWidgetViewModel> InstalledGames { get; }
    public ReadOnlyObservableCollection<IViewModelInterface> SupportedGames { get; } = new(new());
    public ScenarioGamesPage(IWindowManager windows, ScenarioData data) : base(windows)
    {
        TabTitle = "My Games"; TabIcon = IconValues.GamepadOutline;
        InstalledGames = new(new ObservableCollection<IGameWidgetViewModel> { data.Game });
    }
}

internal sealed class ScenarioLoadoutsPage : APageViewModel<IMyLoadoutsViewModel>, IMyLoadoutsViewModel
{
    public ReadOnlyObservableCollection<IGameLoadoutsSectionEntryViewModel> GameSectionViewModels { get; }
    public ScenarioLoadoutsPage(IWindowManager windows, ScenarioData data) : base(windows)
    {
        TabTitle = "My Loadouts"; TabIcon = IconValues.Package;
        GameSectionViewModels = new(data.Sections);
    }
}

internal sealed class ScenarioLoadoutsSection : AViewModel<IGameLoadoutsSectionEntryViewModel>, IGameLoadoutsSectionEntryViewModel
{
    public string HeadingText => "Fallout: New Vegas Loadouts";
    private readonly ObservableCollection<IViewModelInterface> _cards = new();
    public ReadOnlyObservableCollection<IViewModelInterface> CardViewModels { get; }
    public IReadOnlyList<ScenarioLoadoutCard> Loadouts => _cards.OfType<ScenarioLoadoutCard>().ToArray();
    private int _nextId;
    public ScenarioLoadoutsSection()
    {
        CardViewModels = new(_cards);
        _cards.Add(new CreateNewLoadoutCardViewModel { AddLoadoutCommand = ReactiveCommand.Create(() => CreateLoadout()) });
    }
    public void CreateLoadout(string? name = null)
    {
        var number = ++_nextId;
        _cards.Add(new ScenarioLoadoutCard(this, name ?? $"Loadout {number}", number));
        Refresh();
    }
    public void Remove(ScenarioLoadoutCard card) { _cards.Remove(card); Refresh(); }
    public void Clear() { foreach (var card in Loadouts) _cards.Remove(card); }
    private void Refresh() { foreach (var card in Loadouts) card.Refresh(); }
    public void Dispose() { foreach (var card in Loadouts) { card.CloneLoadoutCommand.Dispose(); card.DeleteLoadoutCommand.Dispose(); } }
}

internal sealed class ScenarioLoadoutCard : AViewModel<ILoadoutCardViewModel>, ILoadoutCardViewModel
{
    private readonly ScenarioLoadoutsSection _section;
    public ILoadoutBadgeViewModel LoadoutBadgeViewModel { get; }
    public string LoadoutName { get; }
    public IImage LoadoutImage { get; } = ScenarioImages.Cover();
    public bool IsLoadoutApplied => false;
    public string HumanizedLoadoutLastApplyTime => "Not applied";
    public string HumanizedLoadoutCreationTime => "Created just now";
    public string LoadoutModCount => "Mods 0";
    public bool IsDeleting => false;
    public bool IsSkeleton => false;
    public bool IsLastLoadout => _section.Loadouts.Count == 1;
    public ReactiveCommand<Unit, Unit> VisitLoadoutCommand { get; } = ReactiveCommand.Create(() => { });
    public ReactiveCommand<Unit, Unit> CloneLoadoutCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteLoadoutCommand { get; }
    public ScenarioLoadoutCard(ScenarioLoadoutsSection section, string name, int number)
    {
        _section = section; LoadoutName = name;
        LoadoutBadgeViewModel = new LoadoutBadgeDesignViewModel { LoadoutShortName = number.ToString() };
        CloneLoadoutCommand = ReactiveCommand.Create(() => section.CreateLoadout($"{name} (Copy)"));
        DeleteLoadoutCommand = ReactiveCommand.Create(() => section.Remove(this));
    }
    public void Refresh() => this.RaisePropertyChanged(nameof(IsLastLoadout));
}

// Optional local Steam art is loaded at runtime, not redistributed as part of the source.
internal static class ScenarioImages
{
    public static Bitmap Cover()
    {
        var path = Environment.GetEnvironmentVariable("MO2_SCENARIO_COVER") ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/Steam/appcache/librarycache/22380/library_600x900.jpg");
        if (File.Exists(path)) return new Bitmap(path);
        return new Bitmap(AssetLoader.Open(new Uri("avares://NexusMods.App.UI/Assets/mod-thumbnail-fallback.png")));
    }
}
