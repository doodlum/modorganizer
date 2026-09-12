using DynamicData.Kernel;
using NexusMods.App.UI.Pages;
using NexusMods.App.UI.Windows;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

internal sealed record ScenarioLoadoutContext(int Number, bool IsCollection = false, bool IsLibrary = false) : IPageFactoryContext;
internal sealed class ScenarioLoadoutFactory(IServiceProvider services, IWindowManager windows, ScenarioData scenarios, ScenarioLibrary library) : IPageFactory
{
    public PageFactoryId Id { get; } = PageFactoryId.From(Guid.Parse("5f4a4e38-3b08-40d9-9ab3-d3a2a5f30006"));
    public Optional<OpenPageBehaviorType> DefaultOpenPageBehavior => default;
    public PageData Data(int number, bool isCollection = false) => new() { FactoryId = Id, Context = new ScenarioLoadoutContext(number, isCollection) };
    public PageData LibraryData(int number) => new() { FactoryId = Id, Context = new ScenarioLoadoutContext(number, IsLibrary: true) };
    public Page Create(IPageFactoryContext context)
    {
        var details = (ScenarioLoadoutContext)context;
        var number = details.Number;
        var card = scenarios.Section.Loadouts.Single(x => x.Number == number);
        if (details.IsLibrary) return new Page { PageData = LibraryData(number),
            ViewModel = new ScenarioLibraryPage(services, windows, library, card.InstalledMods) };
        var local = new FixtureServices(services);
        local.Add<IEnumerable<ILoadoutDataProvider>>([card.InstalledMods]);
        return new Page { PageData = Data(number, details.IsCollection), ViewModel = new ScenarioInstalledPage(local, windows, card.InstalledMods, card.PluginOrder, isCollection: details.IsCollection) };
    }
    public IEnumerable<PageDiscoveryDetails?> GetDiscoveryDetails(IWorkspaceContext workspaceContext)
    {
        if (workspaceContext is not ScenarioWorkspaceContext context) yield break;
        yield return new PageDiscoveryDetails { SectionName = "Mods", ItemName = "Library", Icon = NexusMods.UI.Sdk.Icons.IconValues.LibraryOutline, PageData = LibraryData(context.Number) };
        yield return new PageDiscoveryDetails { SectionName = "Mods", ItemName = "All",
            Icon = NexusMods.UI.Sdk.Icons.IconValues.FormatAlignJustify, PageData = Data(context.Number) };
        yield return new PageDiscoveryDetails { SectionName = "Mods", ItemName = "My Mods",
            Icon = NexusMods.UI.Sdk.Icons.IconValues.CollectionsOutline, PageData = Data(context.Number, true) };
    }
}
