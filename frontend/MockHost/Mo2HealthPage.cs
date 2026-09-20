using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia.Threading;
using NexusMods.Abstractions.Diagnostics;
using NexusMods.Abstractions.Loadouts;
using NexusMods.App.UI.Controls.Diagnostics;
using NexusMods.App.UI.Controls.MarkdownRenderer;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.Pages.Diagnostics;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;

namespace Mo2.Frontend;

internal sealed class Mo2HealthPage : APageViewModel<IDiagnosticListViewModel>, IDiagnosticListViewModel
{
    public LoadoutId LoadoutId { get; set; }
    private IDiagnosticEntryViewModel[] _all = [];
    public IDiagnosticEntryViewModel[] DiagnosticEntries => _all.Where(x => x.Severity switch {
        DiagnosticSeverity.Critical => Filter.HasFlag(DiagnosticFilter.Critical),
        DiagnosticSeverity.Warning => Filter.HasFlag(DiagnosticFilter.Warnings),
        _ => Filter.HasFlag(DiagnosticFilter.Suggestions)
    }).ToArray();
    public int NumCritical => _all.Count(x => x.Severity == DiagnosticSeverity.Critical);
    public int NumWarnings => _all.Count(x => x.Severity == DiagnosticSeverity.Warning);
    public int NumSuggestions => _all.Count(x => x.Severity == DiagnosticSeverity.Suggestion);
    private DiagnosticFilter _filter = DiagnosticFilter.Critical | DiagnosticFilter.Warnings | DiagnosticFilter.Suggestions;
    public DiagnosticFilter Filter { get => _filter; set { this.RaiseAndSetIfChanged(ref _filter, value); this.RaisePropertyChanged(nameof(DiagnosticEntries)); } }
    private readonly Mo2LiveWorkspace _shell;
    private bool _refreshing;
    private Mo2ProfileTarget? _target;
    private Mo2ProfileTarget? _entriesTarget;
    private readonly Mo2LiveProfile _source;
    private readonly Func<Task<(string Title, string Details)[]>> _read;
    private bool _active;
    private long _activation;
    public bool HasResult { get; private set; }
    public Mo2HealthPage(IWindowManager windows, Mo2LiveWorkspace shell) : this(windows, shell, shell.Profile) { }
    internal Mo2HealthPage(IWindowManager windows, Mo2LiveWorkspace shell, Mo2LiveProfile source,
        Func<Task<(string Title, string Details)[]>>? read = null) : base(windows)
    {
        _source = source; _read = read ?? source.ReadHealth;
        _shell = shell; TabTitle = "Health Check"; TabIcon = IconValues.Cardiology;
        Set([Entry("Checking MO2", "Waiting for MO2’s diagnostic extensions.", DiagnosticSeverity.Suggestion)]);
        this.WhenActivated(d => {
            _active = true; ++_activation;
            void Changed() {
                if (_target != source.CurrentTarget) {
                    HasResult = false;
                    Set([Entry("Checking MO2", "Reading the selected profile’s diagnostics.", DiagnosticSeverity.Suggestion)]);
                }
                _ = Refresh();
            }
            source.Changed += Changed;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timer.Tick += (_, _) => _ = Refresh(); timer.Start();
            Disposable.Create(() => { _active = false; ++_activation; timer.Stop(); source.Changed -= Changed; }).DisposeWith(d);
            _ = Refresh();
        });
    }
    private IDiagnosticEntryViewModel Entry(string title, string details, DiagnosticSeverity severity) => new Mo2HealthEntry(title, details, severity, _shell);
    private void Set(IDiagnosticEntryViewModel[] entries)
    {
        if (_entriesTarget == _source.CurrentTarget && _all.Select(x => (x.Title, x.Summary, x.Severity))
            .SequenceEqual(entries.Select(x => (x.Title, x.Summary, x.Severity)))) return;
        _entriesTarget = _source.CurrentTarget;
        _all = entries;
        this.RaisePropertyChanged(nameof(DiagnosticEntries));
        this.RaisePropertyChanged(nameof(NumCritical)); this.RaisePropertyChanged(nameof(NumWarnings)); this.RaisePropertyChanged(nameof(NumSuggestions));
    }
    public async Task Refresh()
    {
        if (_refreshing) return;
        _refreshing = true; var target = _source.CurrentTarget; var activation = _activation;
        if (_target != target) {
            HasResult = false;
            Set([Entry("Checking MO2", "Reading the selected profile’s diagnostics.", DiagnosticSeverity.Suggestion)]);
        }
        _target = target;
        try {
            var problems = await _read();
            if (activation != _activation || _source.CurrentTarget != target) return;
            // MO2's diagnostic API provides no severity; preserve every report as a warning.
            Set(problems.Select(x => Entry(x.Title, x.Details, DiagnosticSeverity.Warning)).ToArray());
            HasResult = true;
        } catch (Exception error) {
            if (activation == _activation && _source.CurrentTarget == target) {
                HasResult = false;
                Set([Entry("Health check unavailable", error.Message, DiagnosticSeverity.Warning)]);
            }
        } finally {
            _refreshing = false;
            if (_active && (activation != _activation || _source.CurrentTarget != target)) await Refresh();
        }
    }
}
internal sealed class Mo2HealthEntry : AViewModel<IDiagnosticEntryViewModel>, IDiagnosticEntryViewModel
{
    public Diagnostic Diagnostic { get; }
    public string Title => Diagnostic.Title;
    public string Summary { get; }
    public DiagnosticSeverity Severity => Diagnostic.Severity;
    public ReactiveCommand<NavigationInformation, (Diagnostic, NavigationInformation)> SeeDetailsCommand { get; }
    public Mo2HealthEntry(string title, string details, DiagnosticSeverity severity, Mo2LiveWorkspace shell)
    {
        Summary = details;
        Diagnostic = new Diagnostic { Id = new DiagnosticId("MO2", 1), Title = title, Severity = severity,
            Summary = DiagnosticMessage.From(details), Details = DiagnosticMessage.From(details), DataReferences = [] };
        var context = new Mo2HealthDetailsContext(Diagnostic, shell.Profile.Endpoint, shell.Profile.ProfilePath);
        SeeDetailsCommand = ReactiveCommand.Create<NavigationInformation, (Diagnostic, NavigationInformation)>(info => {
            shell.OpenHealthDetails(context, info); return (Diagnostic, info);
        });
    }
}
internal sealed record Mo2HealthDetailsContext(Diagnostic Diagnostic, string Endpoint, string ProfilePath) : IPageFactoryContext;
internal sealed class Mo2HealthDetailsFactory(IWindowManager windows, Mo2LiveWorkspace shell) : IPageFactory
{
    public PageFactoryId Id { get; } = PageFactoryId.From(Guid.Parse("bcde2778-955d-4b57-a14e-85a878b82109"));
    public DynamicData.Kernel.Optional<OpenPageBehaviorType> DefaultOpenPageBehavior => default;
    public Page Create(IPageFactoryContext context) => new() { PageData = new() { FactoryId = Id, Context = context },
        ViewModel = new Mo2HealthDetails(windows, shell.Profile, (Mo2HealthDetailsContext)context) };
    public IEnumerable<PageDiscoveryDetails?> GetDiscoveryDetails(IWorkspaceContext context) => [];
}
internal sealed class Mo2HealthDetails : APageViewModel<IDiagnosticDetailsViewModel>, IDiagnosticDetailsViewModel
{
    public DiagnosticSeverity Severity { get; }
    public IMarkdownRendererViewModel MarkdownRendererViewModel { get; }
    private readonly Mo2LiveProfile _profile;
    private readonly Mo2HealthDetailsContext _context;
    private bool _refreshing;
    private bool _active;
    private long _activation;
    private readonly Func<Task<(string Title, string Details)[]>> _read;
    public bool HasResult { get; private set; }
    private bool IsSourceProfile => _profile.IsConnected && !_profile.SelectingProfile &&
        _profile.Endpoint == _context.Endpoint && _profile.ProfilePath == _context.ProfilePath;

    public Mo2HealthDetails(IWindowManager windows, Mo2LiveProfile profile, Mo2HealthDetailsContext context,
        Func<Task<(string Title, string Details)[]>>? read = null) : base(windows)
    {
        _profile = profile; _context = context; _read = read ?? profile.ReadHealth;
        TabTitle = context.Diagnostic.Title; TabIcon = IconValues.Cardiology; Severity = context.Diagnostic.Severity;
        MarkdownRendererViewModel = new Mo2DiagnosticText();
        Set("Checking this diagnostic with MO2.");
        this.WhenActivated(d => {
            _active = true; ++_activation;
            void Changed() {
                if (!IsSourceProfile) ShowUnavailable();
                _ = Refresh();
            }
            profile.Changed += Changed;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timer.Tick += (_, _) => _ = Refresh(); timer.Start();
            Disposable.Create(() => { _active = false; ++_activation; timer.Stop(); profile.Changed -= Changed; }).DisposeWith(d);
            Changed();
        });
    }
    private void Set(string text)
    {
        MarkdownRendererViewModel.Contents = text;
    }
    private void ShowUnavailable()
    {
        HasResult = false;
        Set("This diagnostic belongs to another or disconnected MO2 profile. Select its original profile to check it again.");
    }
    public async Task Refresh()
    {
        if (!IsSourceProfile) { ShowUnavailable(); return; }
        if (_refreshing) return;
        _refreshing = true; var activation = _activation;
        try {
            var reports = await _read();
            if (activation != _activation) return;
            if (!IsSourceProfile) { ShowUnavailable(); return; }
            var matches = reports.Where(x => x.Title == _context.Diagnostic.Title).ToArray();
            var exact = matches.Where(x => x.Details == _context.Diagnostic.Details.Value).ToArray();
            if (exact.Length > 0) Set(exact[0].Details);
            else if (matches.Length == 1) Set(matches[0].Details);
            else Set(matches.Length == 0 ? "MO2 no longer reports this diagnostic. Open Health Check for the current reports." :
                "This diagnostic has changed. Open Health Check to select the current report.");
            HasResult = true;
        } catch (Exception error) {
            if (activation != _activation) return;
            HasResult = false;
            if (!IsSourceProfile) ShowUnavailable();
            else Set("Health check unavailable: " + error.Message);
        } finally {
            _refreshing = false;
            if (_active && activation != _activation) await Refresh();
        }
    }
}
