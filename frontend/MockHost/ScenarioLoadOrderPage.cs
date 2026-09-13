using NexusMods.App.UI.Pages.Sorting;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;
using NexusMods.UI.Sdk.Icons;

namespace Mo2.Frontend;

// Reuses the original editor including its row buttons, sort direction and drag/drop handlers.
internal sealed class ScenarioLoadOrderPage(IServiceProvider services, NexusMods.Abstractions.Games.ISortOrderVariety order)
    : LoadOrderViewModel(services, order, default), IPageViewModelInterface
{
    public Mo2LiveProfile? LiveProfile { get; init; }
    public Task SetSelectedActive(bool enabled) => LiveProfile is { } profile
        ? profile.SetPluginsActive(profile.Order.Plugins.Where(plugin => plugin.CanToggle && plugin.IsActive != enabled && Adapter.SelectedModels.Any(row => plugin.Key.Equals(row.Key))).Select(plugin => plugin.DisplayName), enabled)
        : Task.CompletedTask;
    public IconValue TabIcon => IconValues.Package;
    public string TabTitle => "Plugin load order";
    public WindowId WindowId { get; set; }
    public WorkspaceId WorkspaceId { get; set; }
    public PanelId PanelId { get; set; }
    public PanelTabId TabId { get; set; }
    public bool CanClose() => true;
}
