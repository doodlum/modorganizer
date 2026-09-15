using System.Reactive.Linq;
using System.Reactive.Disposables;
using DynamicData;
using ObservableCollections;
using NexusMods.Abstractions.Downloads;
using NexusMods.App.UI.Controls;
using NexusMods.App.UI.Pages.Downloads;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.Paths;
using NexusMods.Sdk.Jobs;
using NexusMods.Sdk.NexusModsApi;
using R3;
using ReactiveUI;
using Observable = R3.Observable;

namespace Mo2.Frontend;

internal sealed class Mo2DownloadProvider : IDownloadsDataProvider
{
    private static readonly NexusMods.App.UI.Converters.SizeToStringTypeConverter SizeFormatter = new();
    private readonly SourceCache<(DownloadId Key, Mo2Download File, Mo2LiveProfile Profile, Mo2ProfileTarget Target), DownloadId> _rows = new(x => x.Key);
    private readonly Dictionary<string, DownloadId> _ids = new();
    private readonly Dictionary<DownloadId, Mo2Download> _values = new();
    public static readonly ComponentKey BytesKey = ComponentKey.From("MO2.DownloadBytes");
    // MO2's own remaining download columns, so this tab can list what MO2 lists.
    public static readonly ComponentKey FiletimeKey = ComponentKey.From("MO2.DownloadFiletime");
    public static readonly ComponentKey ModNameKey = ComponentKey.From("MO2.DownloadModName");
    public static readonly ComponentKey VersionKey = ComponentKey.From("MO2.DownloadVersion");
    public static readonly ComponentKey ModIdKey = ComponentKey.From("MO2.DownloadModId");
    public static readonly ComponentKey SourceGameKey = ComponentKey.From("MO2.DownloadSourceGame");
    // MO2 writes the archive's own timestamp; shown as a date rather than the raw
    // seconds the bridge carries it as.
    internal static string Filetime(string seconds) => long.TryParse(seconds, out var value)
        ? DateTimeOffset.FromUnixTimeSeconds(value).LocalDateTime.ToString("yyyy-MM-dd HH:mm") : "";
    public Mo2Download? Find(DownloadId id) => _values.GetValueOrDefault(id);
    private Mo2ProfileTarget? _target;
    // MO2's two filters under its download list: the text typed in downloadFilterEdit
    // and whether showHiddenBox is ticked. Held here rather than in the view because
    // this is what builds the rows the list is made of.
    private string _filter = "";
    private bool _hidden;
    private Mo2LiveProfile? _last;
    public void SetFilter(string text, bool hidden)
    {
        if (_filter == text && _hidden == hidden) return;
        _filter = text; _hidden = hidden;
        if (_last is { } profile) Refresh(profile);
    }
    public void Refresh(Mo2LiveProfile profile)
    {
        _last = profile;
        if (_target != profile.CurrentTarget) { _rows.Clear(); _values.Clear(); _ids.Clear(); _target = profile.CurrentTarget; }
        Mo2Download[] visible = !profile.IsConnected ? []
            : (_hidden ? profile.Downloads.Concat(profile.HiddenDownloads) : profile.Downloads)
                .Where(x => _filter.Length == 0 || x.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        var keep = new HashSet<DownloadId>();
        foreach (var file in visible) {
            if (!_ids.TryGetValue(file.Path, out var id)) _ids[file.Path] = id = DownloadId.From(Guid.NewGuid());
            keep.Add(id);
            if (_values.GetValueOrDefault(id) == file) continue;
            _values[id] = file;
            _rows.AddOrUpdate((id, file, profile, profile.CurrentTarget));
        }
        foreach (var id in _values.Keys.Where(x => !keep.Contains(x)).ToArray()) { _rows.RemoveKey(id); _ids.Remove(_values[id].Path); _values.Remove(id); }
    }
    public IObservable<IChangeSet<CompositeItemModel<DownloadId>, DownloadId>> ObserveDownloads(DownloadsFilter filter) => _rows.Connect().Transform(value => {
            var (id, file, profile, capturedTarget) = value;
            var row = new CompositeItemModel<DownloadId>(id);
            row.Add(DownloadColumns.Name.NameComponentKey, new NameComponent(file.Name));
            row.Add(DownloadColumns.Game.ComponentKey, new DownloadComponents.GameComponent(profile.GameName));
            SizeFormatter.TryConvert(NexusMods.Paths.Size.From(checked((ulong)file.Bytes)), typeof(string), null, out var bytes);
            row.Add(BytesKey, new ValueComponent<string>((string)bytes));
            row.Add(FiletimeKey, new ValueComponent<string>(Filetime(file.Filetime)));
            row.Add(ModNameKey, new ValueComponent<string>(file.ModName));
            row.Add(VersionKey, new ValueComponent<string>(file.Version));
            row.Add(ModIdKey, new ValueComponent<string>(file.ModId));
            row.Add(SourceGameKey, new ValueComponent<string>(file.SourceGame));
            var state = file.Failed ? JobStatus.Failed : file.Partial ? file.Paused ? JobStatus.Paused : JobStatus.Running : JobStatus.Completed;
            var progress = file.Partial ? Percent.Zero : Percent.One;
            var status = new DownloadComponents.StatusComponent(progress, state, Observable.Return(progress), Observable.Return(state), canRetryFailed: true, canCancelInactive: false);
            var target = capturedTarget;
            status.PauseCommand.SubscribeAwait(async (_, _) => await profile.ControlDownload(file.Path, "pause", target));
            status.ResumeCommand.SubscribeAwait(async (_, _) => await profile.ControlDownload(file.Path, "resume", target));
            status.CancelCommand.SubscribeAwait(async (_, _) => await profile.ControlDownload(file.Path, "cancel", target));
            row.Add(DownloadColumns.Status.ComponentKey, status);
            return row;
        });
    public IObservable<int> CountDownloads(DownloadsFilter filter) => _rows.CountChanged.StartWith(_rows.Count);
    public string ResolveGameName(NexusModsGameId id) => "MO2";
}

internal sealed class Mo2DownloadsPage : APageViewModel<IDownloadsPageViewModel>, IDownloadsPageViewModel
{
    public Mo2LiveProfile Profile { get; }
    public Mo2DownloadProvider Provider { get; } = new();
    public DownloadsTreeDataGridAdapter Adapter { get; }
    private readonly BindableReactiveProperty<bool> _running = new(false), _paused = new(false), _selectedRunning = new(false), _selectedPaused = new(false), _active = new(false);
    public R3.Observable<bool> SelectionHasRunningItems => _selectedRunning;
    public R3.Observable<bool> SelectionHasPausedItems => _selectedPaused;
    public R3.Observable<bool> SelectionHasActiveItems => _active;
    public R3.Observable<bool> HasRunningItems => _running;
    public R3.Observable<bool> HasPausedItems => _paused;
    public R3.ReactiveCommand<R3.Unit> PauseAllCommand { get; } = new();
    public R3.ReactiveCommand<R3.Unit> ResumeAllCommand { get; } = new();
    public R3.ReactiveCommand<R3.Unit> PauseSelectedCommand { get; } = new();
    public R3.ReactiveCommand<R3.Unit> ResumeSelectedCommand { get; } = new();
    public R3.ReactiveCommand<R3.Unit> CancelSelectedCommand { get; } = new();
    private bool _empty;
    public bool IsEmptyStateActive { get => _empty; set => this.RaiseAndSetIfChanged(ref _empty, value); }
    public int SelectionCount => Adapter.SelectedModels.Count;
    public string HeaderTitle => "Downloads";
    public string HeaderDescription => Profile.ProfilePath.Length == 0 ? "Select a profile to open its MO2 downloads folder." :
        (Profile.IsConnected ? "" : "Unavailable · ") + Profile.GameName + " · " + Profile.CollectionName.Value;
    public Mo2DownloadsPage(IWindowManager windows, Mo2LiveProfile profile, IServiceProvider services) : base(windows)
    {
        Profile = profile; TabTitle = "Downloads"; TabIcon = NexusMods.UI.Sdk.Icons.IconValues.Download;
        Adapter = new DownloadsTreeDataGridAdapter(services, Provider, DownloadsFilter.All());
        Adapter.ViewHierarchical.Value = false;
        PauseAllCommand.SubscribeAwait(async (_, _) => await Control("pause", false));
        ResumeAllCommand.SubscribeAwait(async (_, _) => await Control("resume", false));
        PauseSelectedCommand.SubscribeAwait(async (_, _) => await Control("pause", true));
        ResumeSelectedCommand.SubscribeAwait(async (_, _) => await Control("resume", true));
        CancelSelectedCommand.SubscribeAwait(async (_, _) => await Control("cancel", true));
        this.WhenActivated(d => {
            Adapter.Activate().DisposeWith(d);
            void Refresh() { Provider.Refresh(Profile); this.RaisePropertyChanged(nameof(HeaderDescription)); IsEmptyStateActive = Profile.IsConnected && Profile.Downloads.Count == 0; UpdateSelection(); }
            Profile.Changed += Refresh;
            System.Reactive.Disposables.Disposable.Create(() => Profile.Changed -= Refresh).DisposeWith(d);
            Adapter.SelectedModels.ObserveChanged().Subscribe(_ => UpdateSelection()).DisposeWith(d);
            Refresh();
        });
    }
    public Mo2Download[] Selected => Adapter.SelectedModels.Select(x => Provider.Find(x.Key)).OfType<Mo2Download>().ToArray();
    private void UpdateSelection() {
        var selected = Selected;
        _running.Value = Profile.CanUseDownloads && Profile.Downloads.Any(x => x.CanControl("pause"));
        _paused.Value = Profile.CanUseDownloads && Profile.Downloads.Any(x => x.CanControl("resume"));
        _selectedRunning.Value = Profile.CanUseDownloads && selected.Any(x => x.CanControl("pause"));
        _selectedPaused.Value = Profile.CanUseDownloads && selected.Any(x => x.CanControl("resume"));
        _active.Value = Profile.CanUseDownloads && selected.Any(x => x.CanControl("cancel"));
        this.RaisePropertyChanged(nameof(SelectionCount));
    }
    private async Task Control(string operation, bool selected) {
        var target = Profile.CurrentTarget;
        var files = (selected ? Selected : Profile.Downloads).Where(x => x.CanControl(operation)).ToArray();
        foreach (var file in files) await Profile.ControlDownload(file.Path, operation, target);
    }
}
