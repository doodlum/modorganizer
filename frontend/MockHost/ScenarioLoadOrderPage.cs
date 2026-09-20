using NexusMods.App.UI.Pages.Sorting;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;
using ReactiveUI;

namespace Mo2.Frontend;

// Reuses the original editor including its row buttons, sort direction and drag/drop handlers.
internal sealed class ScenarioLoadOrderPage(IServiceProvider services, NexusMods.Abstractions.Games.ISortOrderVariety order)
    : LoadOrderViewModel(services, order, default), IPageViewModelInterface
{
    private string _mo2SearchText = "";
    private bool _mo2SearchExpanded;
    private bool _showPluginDetails;
    public bool ShowPluginDetails { get => _showPluginDetails; set => this.RaiseAndSetIfChanged(ref _showPluginDetails, value); }
    public string Mo2SearchText { get => _mo2SearchText; set => this.RaiseAndSetIfChanged(ref _mo2SearchText, value); }
    public bool Mo2SearchExpanded { get => _mo2SearchExpanded; set => this.RaiseAndSetIfChanged(ref _mo2SearchExpanded, value); }
    private Mo2LiveProfile? _liveProfile;
    public Mo2LiveProfile? LiveProfile {
        get => _liveProfile;
        init {
            _liveProfile = value;
            // Establish the MO2 list shape before activation. Changing it in
            // the view can first create and style the upstream hierarchical
            // source, then discard it to build our flat plugin list.
            if (value is not null) Adapter.ViewHierarchical.Value = false;
        }
    }
    public Task SetSelectedActive(bool enabled) => LiveProfile is { } profile
        ? profile.SetPluginsActive(profile.Order.Plugins.Where(plugin => plugin.CanToggle && plugin.IsActive != enabled && Adapter.SelectedModels.Any(row => plugin.Key.Equals(row.Key))).Select(plugin => plugin.DisplayName), enabled)
        : Task.CompletedTask;
    public static readonly IconValue PluginIcon = new ProjektankerIcon("mdi-power-plug-outline");
    public static readonly IconValue HeaderIcon = new AvaloniaSvg("avares://NexusModsApp/Assets/power-plug-3d.svg");
    public IconValue TabIcon => PluginIcon;
    public string TabTitle => "Plugins";
    public WindowId WindowId { get; set; }
    public WorkspaceId WorkspaceId { get; set; }
    public PanelId PanelId { get; set; }
    public PanelTabId TabId { get; set; }
    public bool CanClose() => true;
}
