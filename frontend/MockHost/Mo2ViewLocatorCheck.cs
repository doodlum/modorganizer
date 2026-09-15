using ReactiveUI;
using NexusMods.App.UI.Pages.MyGames;

namespace Mo2.Frontend;

internal static class Mo2ViewLocatorCheck
{
    internal static void Run()
    {
        var types = typeof(MyGamesView).Assembly.GetTypes();
        var interfaces = types.Where(t => !t.IsAbstract && !t.ContainsGenericParameters && t.GetConstructor(Type.EmptyTypes) is not null)
            .SelectMany(t => t.GetInterfaces())
            .Where(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IViewFor<>))
            .Select(t => t.GenericTypeArguments.Single()).Distinct().ToArray();
        foreach (var model in interfaces) {
            var expected = types.First(t => !t.IsAbstract && typeof(IViewFor<>).MakeGenericType(model).IsAssignableFrom(t)
                && t.GetConstructor(Type.EmptyTypes) is not null);
            if (FixtureViewLocator.ResolveNativeViewType(model) != expected || FixtureViewLocator.ResolveNativeViewType(model) != expected)
                throw new Exception("Cached view resolution changed the selected native view for " + model);
        }
        try {
            FixtureViewLocator.ResolveNativeViewType(typeof(Mo2ViewLocatorCheck));
            throw new Exception("Missing native view was accepted");
        } catch (InvalidOperationException error) when (error.Message.StartsWith("No upstream view for ")) { }
        Console.WriteLine($"PASS cached view locator: {interfaces.Length} native model mappings match original resolution; missing views rejected");
    }
}
