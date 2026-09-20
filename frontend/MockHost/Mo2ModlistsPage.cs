using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;

namespace Mo2.Frontend;

internal interface IMo2ModlistsPage : IPageViewModelInterface { }

// The Wabbajack browser, in the Home area beside My Games and My Loadouts.
//
// It shows the modlists Wabbajack publishes, filtered to the games this machine
// actually has and Mod Organizer can actually manage — a gallery of two hundred
// lists is only useful once it is the handful a person could install today. The
// filter can be widened to every supported game, because a person deciding what to
// install next is a reason to look at a game they have not bought yet.
internal sealed class Mo2ModlistsPage : APageViewModel<IMo2ModlistsPage>, IMo2ModlistsPage
{
    public static readonly IconValue ModlistsIcon = new ProjektankerIcon("mdi-cube-scan");

    private readonly Mo2LiveWorkspace _shell;

    public ObservableCollection<Mo2Modlist> Visible { get; } = [];
    public ObservableCollection<string> Games { get; } = [];

    private IReadOnlyList<Mo2Modlist> _all = [];
    private string _game = AllDetected;
    private string _search = "";
    private string _status = "";
    private bool _busy;
    private bool _onlyInstallable = true;

    internal const string AllDetected = "Games on this PC";
    internal const string AllSupported = "All supported games";

    public string Game { get => _game; set { this.RaiseAndSetIfChanged(ref _game, value); Apply(); } }
    public string Search { get => _search; set { this.RaiseAndSetIfChanged(ref _search, value); Apply(); } }
    public string Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }
    public bool Busy { get => _busy; private set => this.RaiseAndSetIfChanged(ref _busy, value); }
    public bool OnlyInstallable { get => _onlyInstallable; set { this.RaiseAndSetIfChanged(ref _onlyInstallable, value); Apply(); } }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> RefreshCommand { get; }

    public Mo2ModlistsPage(IWindowManager windows, Mo2LiveWorkspace shell) : base(windows)
    {
        _shell = shell;
        TabTitle = "Modlists"; TabIcon = ModlistsIcon;
        RefreshCommand = ReactiveCommand.CreateFromTask(() => Load(refresh: true));
        this.WhenActivated(d => {
            if (_all.Count == 0) _ = Load(refresh: false);
            Disposable.Empty.DisposeWith(d);
        });
    }

    // The games worth offering: what MO2 is already managing here, plus what is
    // sitting on disk unmanaged. A modlist for a game that is not installed cannot
    // be installed — Wabbajack needs the game's own files to build from.
    internal static HashSet<string> Detected()
    {
        var found = Mo2SupportedGames.Installed().Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return found;
    }

    internal async Task Load(bool refresh)
    {
        if (Busy) return;
        Busy = true;
        try {
            Status = refresh ? "Refreshing the Wabbajack gallery…" : "Reading the Wabbajack gallery…";
            _all = await Task.Run(() => Mo2ModlistCatalog.Read(line => Status = line ?? Status, refresh));
            Rebuild();
            Apply();
        } catch (Exception error) {
            Status = "Could not read the Wabbajack gallery: " + error.Message;
        } finally { Busy = false; }
    }

    private void Rebuild()
    {
        var games = _all.Where(x => x.Supported).Select(x => x.Game!).Distinct()
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase).ToArray();
        Games.Clear();
        Games.Add(AllDetected);
        Games.Add(AllSupported);
        foreach (var game in games) Games.Add(game);
        if (!Games.Contains(Game)) _game = AllDetected;
    }

    private void Apply()
    {
        var detected = Detected();
        var filtered = _all.Where(list => {
            if (OnlyInstallable && !list.Supported) return false;
            if (Game == AllDetected) { if (list.Game is null || !detected.Contains(list.Game)) return false; }
            else if (Game != AllSupported && !string.Equals(list.Game, Game, StringComparison.OrdinalIgnoreCase)) return false;
            if (Search is { Length: > 0 } text &&
                !list.Title.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !list.Author.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !list.Description.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !list.Tags.Any(tag => tag.Contains(text, StringComparison.OrdinalIgnoreCase))) return false;
            return true;
        }).ToArray();

        Visible.Clear();
        foreach (var list in filtered) Visible.Add(list);

        if (Busy) return;
        Status = filtered.Length == 0
            ? Game == AllDetected
                ? "No modlists for the games found on this PC. Choose a game above to see the rest."
                : "No modlists match."
            : $"{filtered.Length} modlist{(filtered.Length == 1 ? "" : "s")}" +
              (Game == AllDetected ? $" for the {detected.Count} game{(detected.Count == 1 ? "" : "s")} found on this PC" : "");
    }

    internal bool Installed(Mo2Modlist list) => Mo2ModlistInstances.Installed(list);
    internal Task Install(Mo2Modlist list) => _shell.InstallModlist(list);
    internal void OpenReadme(Mo2Modlist list)
    {
        if (list.ReadmeUrl is { Length: > 0 } url) _shell.DesktopInterop.OpenUri(new Uri(url));
    }
}
