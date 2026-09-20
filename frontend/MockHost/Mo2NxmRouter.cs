using System.Text.Json;

namespace Mo2.Frontend;

// Routing preferences contain instance paths only, never NXM authorization data.
internal static class Mo2NxmRouter
{
    private static string Preferences => Mo2ConfigPaths.Combine("nxm-routes.json");
    private static string? Domain(string game) => game switch {
        "Fallout: New Vegas" or "New Vegas" => "newvegas",
        "Skyrim Special Edition" => "skyrimspecialedition",
        _ => null,
    };
    internal static Mo2Registration Resolve(string game, IReadOnlyList<Mo2CatalogEntry> entries, IReadOnlyDictionary<string,string> routes)
    {
        var candidates = entries.Where(x => x.Instance is { } instance && Domain(instance.Game) == game).ToArray();
        if (routes.TryGetValue(game,out var preferred))
            return candidates.SingleOrDefault(x => x.Registration.Directory == preferred)?.Registration
                ?? throw new InvalidOperationException("The preferred NXM instance is unavailable. Select this game in the frontend to update its route.");
        if (candidates.Length != 1) throw new InvalidOperationException(candidates.Length == 0
            ? "No registered MO2 instance supports this NXM game."
            : "More than one MO2 instance supports this game. Select the intended instance in the frontend first.");
        return candidates[0].Registration;
    }
    private static Dictionary<string,string> Read() => File.Exists(Preferences)
        ? JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Preferences)) ?? [] : [];
    public static void Remember(string game, Mo2Registration registration)
    {
        if (string.IsNullOrEmpty(game)) return;
        var routes = Read();
        if (routes.GetValueOrDefault(game) == registration.Directory) return;
        routes[game] = registration.Directory;
        Directory.CreateDirectory(Path.GetDirectoryName(Preferences)!);
        var temporary = Preferences + "." + Guid.NewGuid() + ".tmp";
        File.WriteAllText(temporary,JsonSerializer.Serialize(routes)); File.Move(temporary,Preferences,true);
    }
    public static async Task Forward(string value)
    {
        var link = Mo2NexusLink.Parse(value);
        if (link.NxmUri is null) throw new ArgumentException("The protocol handler requires an NXM file link");
        var catalog = new Mo2InstanceCatalog("");
        if (catalog.LoadError is not null) throw new InvalidOperationException(catalog.LoadError);
        var registration = Resolve(link.Game,catalog.Read(),Read());
        var snapshot = await Mo2HostStartup.Connect(registration,_ => { });
        if (!string.Equals(snapshot.GetProperty("nexusGame").GetString(),link.Game,StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The native host's game does not match the NXM link");
        await new Mo2BridgeClient(registration.Endpoint).SendAsync("startNxmDownload",new() {
            ["profilePath"] = snapshot.GetProperty("profile").GetProperty("path").GetString(), ["url"] = link.NxmUri,
        });
    }
}
