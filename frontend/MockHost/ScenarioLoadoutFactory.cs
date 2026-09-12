using DynamicData.Kernel;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

internal sealed record ScenarioLoadoutContext(int Number) : IPageFactoryContext;
internal sealed class ScenarioLoadoutFactory(IServiceProvider services, IWindowManager windows, ScenarioData scenarios) : IPageFactory
{
    public PageFactoryId Id { get; } = PageFactoryId.From(Guid.Parse("5f4a4e38-3b08-40d9-9ab3-d3a2a5f30006"));
    public Optional<OpenPageBehaviorType> DefaultOpenPageBehavior => default;
    public PageData Data(int number) => new() { FactoryId = Id, Context = new ScenarioLoadoutContext(number) };
    public Page Create(IPageFactoryContext context)
    {
        var number = ((ScenarioLoadoutContext)context).Number;
        var card = scenarios.Section.Loadouts.Single(x => x.Number == number);
        var local = new FixtureServices(services);
        local.Add<IEnumerable<ILoadoutDataProvider>>([card.InstalledMods]);
        return new Page { PageData = Data(number), ViewModel = new ScenarioInstalledPage(local, windows, card.InstalledMods, card.PluginOrder) };
    }
    public IEnumerable<PageDiscoveryDetails?> GetDiscoveryDetails(IWorkspaceContext workspaceContext) => [];
}
