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
    public ReadOnlyObservableCollection<IViewModelInterface> SupportedGames { get; }
    public Mo2GamesPage(IWindowManager windows, Mo2LiveWorkspace shell) : base(windows)
    {
        TabTitle = "My Games"; TabIcon = IconValues.GamepadOutline;
        var games = new ObservableCollection<IGameWidgetViewModel>();
        InstalledGames = new(games);
        var supported = new ObservableCollection<IViewModelInterface>();
        SupportedGames = new(supported);
        void Refresh() {
            var managed = shell.CatalogEntries.Where(x => x.Instance is not null).Select(x => x.Instance!.Game).Distinct().ToArray();
            // MO2 reports its own plugin name for a game ("New Vegas"), which is not
            // the name this catalogue lists it under ("Fallout: New Vegas"), so both
            // sides are resolved to the same entry before being compared. Matching
            // the raw strings listed the same game as managed and as detected at once.
            var managedKeys = managed.Select(Mo2SupportedGames.Find).Where(entry => entry is not null)
                .Select(entry => entry!.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // A supported game sitting on disk has been found, whether or not MO2
            // manages it yet. It belongs in the detected list with NMA's Add game
            // button, not in the "not found" list below.
            var detected = Mo2SupportedGames.Installed().Select(entry => entry.Name)
                .Where(name => !managedKeys.Contains(name)).ToArray();
            var names = managed.Concat(detected).ToArray();
            if (!names.SequenceEqual(games.Select(x => x.Name))) {
                games.Clear();
                foreach (var name in managed) games.Add(new Mo2GameCard(name, () => _ = shell.SelectGame(name, openProfiles: true)));
                foreach (var name in detected) games.Add(Mo2GameCard.Detected(name, () => shell.AddGame(name)));
            }
            // NMA fills its second section with the games it supports but has not
            // found. For MO2 those are its bundled game plugins with no registered
            // instance. NMA also ends the list with a "more coming" tile; MO2 makes
            // no such promise, so nothing stands in for it.
            //
            // Rebuilt on its own terms rather than behind the installed-games guard:
            // the first refresh of an unregistered host leaves that list empty and
            // unchanged, which would otherwise skip this list too.
            var missing = Mo2SupportedGames.Missing(names);
            if (missing.Select(game => game.Name).SequenceEqual(supported.OfType<Mo2MiniGameCard>().Select(card => card.Name))) return;
            supported.Clear();
            foreach (var game in missing)
                supported.Add(new Mo2MiniGameCard(game, uri => shell.DesktopInterop.OpenUri(uri)));
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
    // NMA puts the game version in this slot, not the name — the name is the
    // image's tooltip. We have no version for a game MO2 is not managing yet, and
    // NMA says so in that case rather than leaving it blank.
    public string Version { get; init; } = "Version: Unknown";
    public string Store => "Steam";
    public IconValue GameStoreIcon => IconValues.Steam;
    public Bitmap Image { get; }
    public ReactiveCommand<Unit, Unit> AddGameCommand { get; set; }
    public ReactiveCommand<Unit, Unit> ViewGameCommand { get; set; }
    public ReactiveCommand<Unit, Unit> RemoveAllLoadoutsCommand { get; set; } = ReactiveCommand.Create(() => { }, Observable.Return(false));
    public IObservable<bool> IsManagedObservable { get; set; } = Observable.Return(true);
    // The widget watches this to swap between its Add, Adding and View rows, so it
    // has to raise: a plain property left the card on Add while the work ran.
    private GameWidgetState _state = GameWidgetState.ManagedGame;
    public GameWidgetState State { get => _state; set => this.RaiseAndSetIfChanged(ref _state, value); }
    public Mo2GameCard(string name, Action visit)
    { Name = name; Image = Mo2GameArt.Cover(name); ViewGameCommand = ReactiveCommand.Create(visit); AddGameCommand = ViewGameCommand; }

    // A game found on disk with no MO2 instance behind it yet. NMA's widget shows
    // its Add game button in this state, its spinner while the game is being added,
    // and its View row once it is managed — which is what the card reports here.
    internal static Mo2GameCard Detected(string name, Func<Task> add)
    {
        var card = new Mo2GameCard(name, () => { }) {
            State = GameWidgetState.DetectedGame,
            IsManagedObservable = Observable.Return(false),
        };
        card.AddGameCommand = ReactiveCommand.CreateFromTask(async () => {
            card.State = GameWidgetState.AddingGame;
            // On success the catalog refresh rebuilds this list and replaces the
            // card with a managed one; on failure it has to go back to offering Add.
            try { await add(); } finally { card.State = GameWidgetState.DetectedGame; }
        });
        return card;
    }
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
    // A square icon, taken from the game's own executable wherever we can find it.
    // That is the only source that is square on both platforms and present for
    // every game: Steam installs desktop icons on Linux alone, and its library art
    // is portrait cover artwork. It also supersedes the black-square problem
    // described below, since the extracted icons carry their own alpha.
    public static Bitmap Icon(string game)
    {
        // A game with its own declared artwork keeps it, ahead of any executable:
        // Tale of Two Wastelands runs on the New Vegas binary and would otherwise
        // take New Vegas's icon.
        if (Mo2SupportedGames.Find(game)?.IconUrl is not null && Mo2GameIconLibrary.Open(game) is { } declared) {
            using (declared) return new Bitmap(declared);
        }
        if (Mo2SupportedGames.ExecutablePath(game) is { } executable) {
            using var extracted = Mo2ExeIcon.Extract(executable);
            if (extracted is not null) {
                using var image = SkiaSharp.SKImage.FromBitmap(extracted);
                using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                using var bytes = encoded.AsStream();
                return new Bitmap(bytes);
            }
        }
        // Then the icons baked from Steam's own client icons, for games this
        // machine does not have installed.
        if (Mo2GameIconLibrary.Open(game) is { } bundled) {
            using (bundled) return new Bitmap(bundled);
        }
        if (!OperatingSystem.IsWindows() && Mo2SupportedGames.Find(game) is { } entry) {
            var root = Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            foreach (var id in entry.SteamAppIds)
                foreach (var size in new[] { 256, 128, 96, 64, 48, 32 }) {
                    var path = Path.Combine(root, "icons", "hicolor", $"{size}x{size}", "apps", $"steam_icon_{id}.png");
                    if (File.Exists(path)) return new Bitmap(path);
                }
        }
        // Steam's library art is portrait cover artwork. Take its centre square, so
        // a game we cannot open still gets a square tile instead of a tall sliver
        // letterboxed onto the plate.
        if (Mo2SupportedGames.LibraryArt(game) is { } cover && SquareCrop(cover) is { } square) return square;
        return new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri("avares://NexusMods.App.UI/Assets/mod-thumbnail-fallback.png")));
    }

    // A superellipse rather than a rounded rectangle: the corners stay continuous
    // instead of meeting the straight edges at a visible join, which is what makes
    // a squircle read as an icon shape at tile size.
    private static SkiaSharp.SKPath Squircle(float size)
    {
        const double Exponent = 4.0;
        var path = new SkiaSharp.SKPath();
        var radius = size / 2.0;
        for (var step = 0; step <= 240; step++) {
            var angle = step / 240.0 * Math.PI * 2;
            var cos = Math.Cos(angle); var sin = Math.Sin(angle);
            var x = radius + radius * Math.Sign(cos) * Math.Pow(Math.Abs(cos), 2.0 / Exponent);
            var y = radius + radius * Math.Sign(sin) * Math.Pow(Math.Abs(sin), 2.0 / Exponent);
            if (step == 0) path.MoveTo((float)x, (float)y); else path.LineTo((float)x, (float)y);
        }
        path.Close();
        return path;
    }

    private static Bitmap? SquareCrop(string path)
    {
        try {
            using var source = SkiaSharp.SKBitmap.Decode(path);
            if (source is null || source.Width == 0 || source.Height == 0) return null;
            var side = Math.Min(source.Width, source.Height);
            using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(side, side));
            using var paint = new SkiaSharp.SKPaint { IsAntialias = true };
            var from = SkiaSharp.SKRect.Create((source.Width - side) / 2f, (source.Height - side) / 2f, side, side);
            surface.Canvas.DrawBitmap(source, from, SkiaSharp.SKRect.Create(0, 0, side, side), paint);
            using var composed = surface.Snapshot();
            using var encoded = composed.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var bytes = encoded.AsStream();
            return new Bitmap(bytes);
        } catch { return null; }
    }
    // A square icon at one size, transparent wherever the artwork is.
    //
    // This used to composite onto a white plate, because the only icon source was
    // Steam's desktop icon and Skyrim's is opaque artwork with black baked in,
    // which read as a black square. Icons now come from the game executable and
    // carry their own alpha, so there is nothing left for a plate to rescue — and
    // NMA's widgets already clip the image to a rounded square and draw their own
    // weak border, so a plate only showed up as a white ring around the artwork.
    private const int PlateSize = 96;
    private static readonly Dictionary<string,Bitmap> Plated = new();
    public static Bitmap SquareIcon(string game)
    {
        if (Plated.TryGetValue(game, out var ready)) return ready;
        using var art = Icon(game);
        using var png = new MemoryStream(); art.Save(png); png.Position = 0;
        using var source = SkiaSharp.SKBitmap.Decode(png);
        using var surface = SkiaSharp.SKSurface.Create(new SkiaSharp.SKImageInfo(PlateSize, PlateSize));
        surface.Canvas.Clear(SkiaSharp.SKColors.Transparent);
        using var paint = new SkiaSharp.SKPaint { IsAntialias = true };
        var fit = Math.Min((float)PlateSize / source.Width, (float)PlateSize / source.Height);
        var width = source.Width * fit; var height = source.Height * fit;
        var target = SkiaSharp.SKRect.Create((PlateSize-width)/2, (PlateSize-height)/2, width, height);
        // Artwork that fills its square — a cropped cover, or a full-bleed icon —
        // is masked to a squircle so it reads as an icon rather than a photo.
        // Extracted icons already carry their own silhouette and are left alone.
        if (Opaque(source)) {
            using var squircle = Squircle(PlateSize);
            surface.Canvas.Save();
            surface.Canvas.ClipPath(squircle, antialias: true);
            surface.Canvas.DrawBitmap(source, target, paint);
            surface.Canvas.Restore();
        } else surface.Canvas.DrawBitmap(source, target, paint);
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
    // Cover artwork keeps using Steam's library art, which is what it is for. The
    // app id now comes from the supported-game catalogue rather than a guess
    // between two hardcoded titles, and the cache is found on either platform.
    // NMA decodes its game tiles to the tile width rather than handing the widget a
    // full-resolution bitmap, so the card scales the same way here.
    public static Bitmap Cover(string game)
    {
        var width = (int)NexusMods.App.UI.ImageSizes.GameTile.Width;
        var path = Mo2SupportedGames.LibraryArt(game);
        if (path is not null) {
            using var file = File.OpenRead(path);
            return Bitmap.DecodeToWidth(file, width);
        }
        using var fallback = Avalonia.Platform.AssetLoader.Open(new Uri("avares://NexusMods.App.UI/Assets/mod-thumbnail-fallback.png"));
        return Bitmap.DecodeToWidth(fallback, width);
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
        LoadoutImage = Mo2GameArt.SquareIcon(entry.Instance!.Game);
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
