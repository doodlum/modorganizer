using Avalonia.Platform;
using SkiaSharp;
using SteamKit2;

namespace Mo2.Frontend;

// Icons for games this machine does not have installed.
//
// An installed game supplies its own icon out of its executable, which is the
// best source there is: publisher artwork, already square, no network. Games MO2
// supports but the user has not installed have no executable to read, and Steam's
// cached art for them is portrait cover artwork, which only ever yields a crop.
//
// The remainder therefore ship with the app, baked once from Steam's own client
// icons by the fetch command below and committed under Assets/GameIcons. These
// are the original Steam assets rather than community artwork. Nothing is
// downloaded at runtime, so the frontend behaves identically offline and on Linux.
internal static class Mo2GameIconLibrary
{
    internal static Stream? Open(string game)
    {
        var entry = Mo2SupportedGames.Find(game);
        if (entry is null) return null;
        var uri = new Uri($"avares://NexusModsApp/Assets/GameIcons/{entry.IconKey}.png");
        try { return AssetLoader.Exists(uri) ? AssetLoader.Open(uri) : null; } catch { return null; }
    }

    // Authoring tool, not part of a normal session:
    //   MockHost --fetch-game-icons [output directory]
    //
    // The icon hash is not in any keyless web endpoint — the store API returns a
    // small community .jpg under a different hash — so it comes from Steam's PICS
    // app info over an anonymous logon, which SteamKit2 already does for the
    // External Files scan.
    internal static async Task Fetch(string[] arguments)
    {
        var output = arguments.Skip(1).FirstOrDefault()
            ?? Path.Combine("frontend", "MockHost", "Assets", "GameIcons");
        Directory.CreateDirectory(output);

        var games = Mo2SupportedGames.All;
        var steamOnly = games.Where(entry => entry.IconUrl is null).SelectMany(entry => entry.SteamAppIds).Distinct().ToArray();
        var icons = await ClientIcons(steamOnly);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var written = 0;
        var missing = new List<string>();
        foreach (var game in games) {
            // A game with its own artwork declared takes it; the rest take Steam's
            // client icon for whichever edition Steam knows about.
            var url = game.IconUrl;
            if (url is null) {
                var hash = game.SteamAppIds
                    .Select(id => (Id: id, Icon: icons.TryGetValue(id, out var found) ? found : null))
                    .FirstOrDefault(pair => pair.Icon is not null);
                if (hash.Icon is null) { missing.Add(game.Name); continue; }
                url = $"https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/{hash.Id}/{hash.Icon}.ico";
            }
            byte[] bytes;
            try { bytes = await client.GetByteArrayAsync(url); } catch (HttpRequestException) { missing.Add(game.Name); continue; }
            using var decoded = Mo2ExeIcon.DecodeIcon(bytes) ?? SKBitmap.Decode(bytes);
            if (decoded is null) { missing.Add(game.Name); continue; }
            using var image = SKImage.FromBitmap(decoded);
            using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
            var path = Path.Combine(output, game.IconKey + ".png");
            await File.WriteAllBytesAsync(path, encoded.ToArray());
            Console.WriteLine($"  {game.Name}: {decoded.Width}x{decoded.Height} -> {Path.GetFileName(path)}");
            written++;
        }
        Console.WriteLine($"Wrote {written} icon(s) to {output}");
        if (missing.Count > 0) Console.WriteLine("No Steam client icon for: " + string.Join(", ", missing));
    }

    private static async Task<Dictionary<string, string>> ClientIcons(string[] appIds)
    {
        var found = new Dictionary<string, string>();
        var client = new SteamClient();
        var manager = new CallbackManager(client);
        var steamUser = client.GetHandler<SteamUser>()!;
        var apps = client.GetHandler<SteamApps>()!;
        var ready = new TaskCompletionSource();
        var finished = new TaskCompletionSource();

        manager.Subscribe<SteamClient.ConnectedCallback>(_ => steamUser.LogOnAnonymous());
        manager.Subscribe<SteamUser.LoggedOnCallback>(callback => {
            if (callback.Result != EResult.OK) ready.TrySetException(new Exception("Anonymous Steam logon failed: " + callback.Result));
            else ready.TrySetResult();
        });
        manager.Subscribe<SteamClient.DisconnectedCallback>(_ => {
            ready.TrySetException(new Exception("Steam disconnected before app info arrived"));
            finished.TrySetResult();
        });

        client.Connect();
        var pump = Task.Run(() => { while (!finished.Task.IsCompleted) manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(200)); });
        try {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(45));
            var request = appIds.Select(id => new SteamApps.PICSRequest(uint.Parse(id))).ToArray();
            var result = await apps.PICSGetProductInfo(request, []).ToTask().WaitAsync(TimeSpan.FromSeconds(45));
            foreach (var response in result.Results)
                foreach (var app in response.Apps) {
                    var icon = app.Value.KeyValues["common"]["clienticon"].AsString();
                    if (!string.IsNullOrEmpty(icon)) found[app.Key.ToString()] = icon;
                }
        } finally {
            finished.TrySetResult();
            client.Disconnect();
            await pump;
        }
        return found;
    }
}
