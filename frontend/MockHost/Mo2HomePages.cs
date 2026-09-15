using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Disposables;
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
        var games = new ObservableCollection<IGameWidgetViewModel>();
        InstalledGames = new(games);
        void Refresh() {
            var names = shell.CatalogEntries.Where(x => x.Instance is not null).Select(x => x.Instance!.Game).Distinct().ToArray();
            if (names.SequenceEqual(games.Select(x => x.Name))) return;
            games.Clear();
            foreach (var name in names) games.Add(new Mo2GameCard(name, () => shell.OpenLoadouts(name)));
        }
        Refresh();
        this.WhenActivated(d => {
            Refresh(); shell.CatalogChanged += Refresh;
            Disposable.Create(() => shell.CatalogChanged -= Refresh).DisposeWith(d);
        });
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
    private static readonly Dictionary<string,Bitmap> Thumbnails = new();
    public static Bitmap Thumbnail(string game) {
        if (Thumbnails.TryGetValue(game,out var ready)) return ready;
        using var art = Cover(game);
        return Thumbnails[game] = Compose(art, blurBackground: false);
    }
    public static Bitmap? ModThumbnail(string game,int nexusId) {
        using var timing = Mo2UiLatencyProbe.Measure("Load mod thumbnail");
        var key = $"{game}/{nexusId}";
        if (Thumbnails.TryGetValue(key,out var ready)) return ready;
        var root = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),".cache");
        var path = Path.Combine(root,"mo2-nexus-frontend","thumbnails",game,$"{nexusId}.image");
        if (!File.Exists(path)) return null;
        try { using var art = new Bitmap(path); return Thumbnails[key] = Compose(art); } catch { return null; }
    }
    private static Bitmap Compose(Bitmap art, bool blurBackground = true) {
        using var png = new MemoryStream(); art.Save(png); png.Position = 0;
        using var source = SkiaSharp.SKBitmap.Decode(png);
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(184,104));
        var canvas = surface.Canvas;
        // Game art is letterboxed onto a white plate, as Vortex and NMA both show it.
        canvas.Clear(SkiaSharp.SKColors.White);
        if (blurBackground) {
        // Render the blur into pixels: unattached Avalonia effect controls do not
        // reliably render their effect into a RenderTargetBitmap.
        using var blur = SkiaSharp.SKImageFilter.CreateBlur(16,16);
        using var background = new SkiaSharp.SKPaint { IsAntialias = true, ImageFilter = blur };
        var scale = Math.Max(184f / source.Width,104f / source.Height) * 1.35f;
        var backgroundWidth = source.Width * scale; var backgroundHeight = source.Height * scale;
        canvas.DrawBitmap(source,SkiaSharp.SKRect.Create((184-backgroundWidth)/2,(104-backgroundHeight)/2,backgroundWidth,backgroundHeight),background);
        }
        using var foreground = new SkiaSharp.SKPaint { IsAntialias = true };
        var fit = Math.Min(184f / source.Width,104f / source.Height);
        var width = source.Width * fit; var height = source.Height * fit;
        canvas.DrawBitmap(source,SkiaSharp.SKRect.Create((184-width)/2,(104-height)/2,width,height),foreground);
        using var composed = surface.Snapshot(); using var encoded = composed.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
        using var bytes = encoded.AsStream();
        return new Bitmap(bytes);
    }
    public static Bitmap Icon(string game)
    {
        var id = game.Contains("Skyrim", StringComparison.OrdinalIgnoreCase) ? "489830"
            : game.Contains("Vegas", StringComparison.OrdinalIgnoreCase) ? "22380" : null;
        if (id is not null) {
            var root = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            foreach (var size in new[] { 256, 128, 96, 64, 48, 32 }) {
                var path = Path.Combine(root, "icons", "hicolor", $"{size}x{size}", "apps", $"steam_icon_{id}.png");
                if (File.Exists(path)) return new Bitmap(path);
            }
        }
        return new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://NexusMods.App.UI/Assets/mod-thumbnail-fallback.png")));
    }
    // Game icons sit on white, never on black. A transparent Steam icon only needed
    // a white plate behind it — that is the Fallout one, and it fills the plate.
    // Skyrim's is opaque artwork with the black baked in, so a plate behind it was
    // never visible and the icon stayed a black square. Keying the black out would
    // erase a logo that is white on black, and every piece of Steam art for that
    // game is dark (its cover averages 31 of 255), so there is no lighter source to
    // switch to. Opaque artwork is inset instead: the plate reads as the icon's
    // background, the artwork as a tile on it.
    private const int PlateSize = 96;
    private const float OpaqueInset = .78f;
    private static readonly Dictionary<string,Bitmap> Plated = new();
    public static Bitmap PlatedIcon(string game)
    {
        if (Plated.TryGetValue(game, out var ready)) return ready;
        using var art = Icon(game);
        using var png = new MemoryStream(); art.Save(png); png.Position = 0;
        using var source = SkiaSharp.SKBitmap.Decode(png);
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(PlateSize, PlateSize));
        surface.Canvas.Clear(SkiaSharp.SKColors.White);
        using var paint = new SkiaSharp.SKPaint { IsAntialias = true };
        var span = Opaque(source) ? PlateSize * OpaqueInset : PlateSize;
        var fit = Math.Min(span / source.Width, span / source.Height);
        var width = source.Width * fit; var height = source.Height * fit;
        var target = SkiaSharp.SKRect.Create((PlateSize-width)/2, (PlateSize-height)/2, width, height);
        surface.Canvas.Save();
        // Rounded, so an inset tile reads as part of the icon rather than a photo
        // dropped on it. Harmless for artwork that already has its own silhouette.
        surface.Canvas.ClipRoundRect(new SkiaSharp.SKRoundRect(target, 12, 12), antialias: true);
        surface.Canvas.DrawBitmap(source, target, paint);
        surface.Canvas.Restore();
        using var composed = surface.Snapshot();
        using var encoded = composed.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        using var bytes = encoded.AsStream();
        return Plated[game] = new Bitmap(bytes);
    }

    // Opaque here means "the plate behind it can never show", which is what decides
    // whether a white plate is worth anything. Sampled on a grid rather than every
    // pixel: a 256px icon is 65k reads and this runs for every game at startup.
    private static bool Opaque(SkiaSharp.SKBitmap bitmap)
    {
        if (bitmap.Width == 0 || bitmap.Height == 0) return true;
        var step = Math.Max(1, Math.Min(bitmap.Width, bitmap.Height) / 32);
        for (var y = 0; y < bitmap.Height; y += step)
            for (var x = 0; x < bitmap.Width; x += step)
                if (bitmap.GetPixel(x, y).Alpha < 200) return false;
        return true;
    }
    public static Bitmap Cover(string game)
    {
        var id = game.Contains("Skyrim", StringComparison.OrdinalIgnoreCase) ? "489830" : "22380";
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), $".local/share/Steam/appcache/librarycache/{id}/library_600x900.jpg");
        return File.Exists(path) ? new Bitmap(path) : new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://NexusMods.App.UI/Assets/mod-thumbnail-fallback.png")));
    }
}
internal sealed class Mo2LoadoutsPage : APageViewModel<IMyLoadoutsViewModel>, IMyLoadoutsViewModel
{
    public string? Game { get; private set; }
    public bool GameScoped { get; }
    public ReadOnlyObservableCollection<IGameLoadoutsSectionEntryViewModel> GameSectionViewModels { get; }
    public Mo2LoadoutsPage(IWindowManager windows, Mo2LiveWorkspace shell, string? game, bool gameScoped = false) : base(windows)
    {
        Game = game; GameScoped = gameScoped || game is not null; TabTitle = GameScoped ? "Profiles" : "My Loadouts"; TabIcon = IconValues.Package;
        var sections = new ObservableCollection<IGameLoadoutsSectionEntryViewModel>();
        GameSectionViewModels = new(sections);
        void Refresh() {
            sections.Clear();
            if (GameScoped && Game is null) Game = shell.CatalogEntries.FirstOrDefault(x => x.Registration.Endpoint == shell.Profile.Endpoint)?.Instance?.Game;
            foreach (var group in shell.CatalogEntries.Where(x => x.Instance is not null && (!GameScoped || (Game is not null && x.Instance.Game == Game))).GroupBy(x => x.Instance!.Game))
                sections.Add(new Mo2LoadoutsSection(shell, group.Key, group));
        }
        Refresh();
        this.WhenActivated(d => {
            Refresh(); shell.CatalogChanged += Refresh;
            Disposable.Create(() => shell.CatalogChanged -= Refresh).DisposeWith(d);
        });
    }
}
internal sealed class Mo2LoadoutsSection : AViewModel<IGameLoadoutsSectionEntryViewModel>, IGameLoadoutsSectionEntryViewModel
{
    public void Dispose() { }
    public string HeadingText { get; }
    public ReadOnlyObservableCollection<IViewModelInterface> CardViewModels { get; }
    public Mo2LoadoutsSection(Mo2LiveWorkspace shell, string game, IEnumerable<Mo2CatalogEntry> entries)
    {
        HeadingText = game + " profiles";
        CardViewModels = new(new ObservableCollection<IViewModelInterface>(entries.SelectMany(entry =>
            (entry.Instance!.Profiles.Length > 0 ? new IViewModelInterface[] { new Mo2CreateProfileCard(shell, entry) } : [])
                .Concat(entry.Instance.Profiles.Select((profile, index) => new Mo2LoadoutCard(shell, entry, profile, index + 1))))));
    }
}
internal sealed class Mo2CreateProfileCard : AViewModel<ICreateNewLoadoutCardViewModel>, ICreateNewLoadoutCardViewModel
{
    public Mo2Registration Registration { get; }
    public string InstanceLabel { get; }
    public ReactiveCommand<Unit, Unit> AddLoadoutCommand { get; }
    public Mo2CreateProfileCard(Mo2LiveWorkspace shell, Mo2CatalogEntry entry)
    {
        Registration = entry.Registration;
        InstanceLabel = Mo2ProfileLabels.Instance(shell, entry);
        var target = entry.Instance!.Profiles.FirstOrDefault(x => x.Name == entry.Instance.SelectedProfile) ?? entry.Instance.Profiles.First();
        AddLoadoutCommand = ReactiveCommand.CreateFromTask(() => shell.Profile.ManageProfile(Registration, target, "create"));
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
    public string HumanizedLoadoutCreationTime { get; }
    public string LoadoutModCount => $"Mods {Profile.ModEntries.Count(x => x.Enabled)} enabled / {Profile.ModEntries.Length}";
    public bool IsDeleting => false;
    public bool IsSkeleton => false;
    public bool IsLastLoadout { get; }
    public ReactiveCommand<Unit, Unit> VisitLoadoutCommand { get; }
    public ReactiveCommand<Unit, Unit> CloneLoadoutCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteLoadoutCommand { get; }
    public ReactiveCommand<Unit, Unit> RenameProfileCommand { get; }
    public Mo2LoadoutCard(Mo2LiveWorkspace shell, Mo2CatalogEntry entry, Mo2ProfileSnapshot profile, int number)
    {
        Registration = entry.Registration; Profile = profile;
        HumanizedLoadoutCreationTime = Mo2ProfileLabels.Instance(shell, entry);
        IsLastLoadout = entry.Instance!.Profiles.Length <= 1;
        RenameProfileCommand = ReactiveCommand.CreateFromTask(() => shell.Profile.ManageProfile(Registration, Profile, "rename"),
            Observable.Return(entry.Instance.SelectedProfile != profile.Name));
        CloneLoadoutCommand = ReactiveCommand.CreateFromTask(() => shell.Profile.ManageProfile(Registration, Profile, "copy"));
        DeleteLoadoutCommand = ReactiveCommand.CreateFromTask(() => shell.Profile.ManageProfile(Registration, Profile, "remove"),
            Observable.Return(!IsLastLoadout && entry.Instance.SelectedProfile != profile.Name));
        // Plated, like the spine: the Steam icons are transparent, and the card's
        // image section is otherwise the panel's own dark surface behind them.
        LoadoutImage = Mo2GameArt.PlatedIcon(entry.Instance!.Game);
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
        ViewModel = new Mo2LoadoutsPage(windows, shell, ((Mo2GamePageContext)context).Game, gameScoped: true),
        PageData = new PageData { FactoryId = Id, Context = context }
    };
    public IEnumerable<PageDiscoveryDetails?> GetDiscoveryDetails(IWorkspaceContext context) {
        if (context is not Mo2WorkspaceContext gameContext) yield break;
        var game = shell.CatalogEntries.FirstOrDefault(x => x.Registration.Endpoint == gameContext.Endpoint)?.Instance?.Game;
        if (game is not null) yield return new PageDiscoveryDetails { SectionName = "Game", ItemName = "Profiles", Icon = IconValues.Package,
            PageData = Data with { Context = new Mo2GamePageContext(Id,game) } };
    }
}

internal static class Mo2ProfileLabels
{
    public static string Instance(Mo2LiveWorkspace shell, Mo2CatalogEntry entry)
    {
        var entries = shell.CatalogEntries.Where(x => x.Instance?.Game == entry.Instance?.Game).ToArray();
        return entries.Length == 1 ? "" : "Instance " + (Array.FindIndex(entries, x => x.Registration == entry.Registration) + 1);
    }
}
