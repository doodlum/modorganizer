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
    private string? _profile;
    public bool HasResult { get; private set; }
    public Mo2HealthPage(IWindowManager windows, Mo2LiveWorkspace shell) : base(windows)
    {
        _shell = shell; TabTitle = "Health Check"; TabIcon = IconValues.Cardiology;
        Set([Entry("Checking MO2", "Waiting for MO2’s diagnostic extensions.", DiagnosticSeverity.Suggestion)]);
        this.WhenActivated(d => {
            void Changed() {
                if (_profile != shell.Profile.ProfilePath) {
                    HasResult = false;
                    Set([Entry("Checking MO2", "Reading the selected profile’s diagnostics.", DiagnosticSeverity.Suggestion)]);
                }
                _ = Refresh();
            }
            shell.Profile.Changed += Changed;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            timer.Tick += (_, _) => _ = Refresh(); timer.Start();
            Disposable.Create(() => { timer.Stop(); shell.Profile.Changed -= Changed; }).DisposeWith(d);
            _ = Refresh();
        });
    }
    private IDiagnosticEntryViewModel Entry(string title, string details, DiagnosticSeverity severity) => new Mo2HealthEntry(title, details, severity, _shell);
    private void Set(IDiagnosticEntryViewModel[] entries)
    {
        _all = entries;
        this.RaisePropertyChanged(nameof(DiagnosticEntries));
        this.RaisePropertyChanged(nameof(NumCritical)); this.RaisePropertyChanged(nameof(NumWarnings)); this.RaisePropertyChanged(nameof(NumSuggestions));
    }
    public async Task Refresh()
    {
        if (_refreshing) return;
        _refreshing = true; var path = _shell.Profile.ProfilePath;
        if (_profile != path) {
            HasResult = false;
            Set([Entry("Checking MO2", "Reading the selected profile’s diagnostics.", DiagnosticSeverity.Suggestion)]);
        }
        _profile = path;
        try {
            var problems = await _shell.Profile.ReadHealth();
            if (_shell.Profile.ProfilePath != path) return;
            // MO2's diagnostic API provides no severity; preserve every report as a warning.
            Set(problems.Select(x => Entry(x.Title, x.Details, DiagnosticSeverity.Warning)).ToArray());
            HasResult = true;
        } catch (Exception error) {
            if (_shell.Profile.ProfilePath == path) {
                HasResult = false;
                Set([Entry("Health check unavailable", error.Message, DiagnosticSeverity.Warning)]);
            }
        } finally {
            _refreshing = false;
            if (_shell.Profile.ProfilePath != path) await Refresh();
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
        SeeDetailsCommand = ReactiveCommand.Create<NavigationInformation, (Diagnostic, NavigationInformation)>(info => {
            shell.OpenHealthDetails(Diagnostic, info); return (Diagnostic, info);
        });
    }
}
internal sealed record Mo2HealthDetailsContext(Diagnostic Diagnostic) : IPageFactoryContext;
internal sealed class Mo2HealthDetailsFactory(IWindowManager windows) : IPageFactory
{
    public PageFactoryId Id { get; } = PageFactoryId.From(Guid.Parse("bcde2778-955d-4b57-a14e-85a878b82109"));
    public DynamicData.Kernel.Optional<OpenPageBehaviorType> DefaultOpenPageBehavior => default;
    public Page Create(IPageFactoryContext context) => new() { PageData = new() { FactoryId = Id, Context = context },
        ViewModel = new Mo2HealthDetails(windows, ((Mo2HealthDetailsContext)context).Diagnostic) };
    public IEnumerable<PageDiscoveryDetails?> GetDiscoveryDetails(IWorkspaceContext context) => [];
}
internal sealed class Mo2HealthDetails : APageViewModel<IDiagnosticDetailsViewModel>, IDiagnosticDetailsViewModel
{
    public DiagnosticSeverity Severity { get; }
    public IMarkdownRendererViewModel MarkdownRendererViewModel { get; }
    public Mo2HealthDetails(IWindowManager windows, Diagnostic diagnostic) : base(windows)
    {
        TabTitle = diagnostic.Title; TabIcon = IconValues.Cardiology; Severity = diagnostic.Severity;
        // MO2 descriptions are plain text, not executable Markdown or remote image references.
        var escaped = System.Text.RegularExpressions.Regex.Replace(diagnostic.Details.Value, @"([\\`*_{}\[\]<>()#+.!|~-])", @"\$1");
        MarkdownRendererViewModel = new MarkdownRendererViewModel { Contents = escaped };
    }
}
