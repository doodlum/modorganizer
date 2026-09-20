using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mo2.Frontend;

// One Wabbajack modlist, as its author published it.
internal sealed record Mo2Modlist(
    string Title,
    string Description,
    string Author,
    string Repository,
    string MachineUrl,
    string WabbajackGame,
    string? Game,
    string Version,
    string[] Tags,
    bool Official,
    bool Nsfw,
    bool Utility,
    long DownloadSize,
    long TotalSize,
    string? ImageUrl,
    string? ReadmeUrl,
    DateTime Updated)
{
    // What Wabbajack calls a list on its command line: the repository it came from
    // and the author's own machine name for it.
    internal string NamespacedName => $"{Repository}/{MachineUrl}";

    // A modlist is its own MO2 instance, and instances are directories, so the name
    // has to survive being one.
    internal string InstanceKey => new string($"{Repository}-{MachineUrl}"
        .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-').ToArray())
        .Trim('-').Replace("--", "-").Replace("--", "-");

    internal bool Supported => Game is not null;
}

// The Wabbajack modlist gallery.
//
// Wabbajack's own command line can list these, but only by writing them to a log a
// line at a time, with none of the description, artwork or size a person choosing
// between them needs. The gallery is plain published JSON, so it is read directly:
// a list of repositories, a file of modlists per repository, and the names of the
// lists Wabbajack shows as official.
//
// Read, never written. Nothing here posts to Wabbajack or identifies the person
// reading; choosing a modlist should not announce itself.
internal static class Mo2ModlistCatalog
{
    private const string Repositories = "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/repositories.json";
    private const string Featured = "https://raw.githubusercontent.com/wabbajack-tools/mod-lists/master/featured_lists.json";

    private static string CachePath => Mo2ConfigPaths.DataCombine("modlists.json");
    // Long enough that opening the browser twice in a sitting is instant, short
    // enough that a list published today is offered today.
    private static readonly TimeSpan CacheLife = TimeSpan.FromHours(6);

    // Wabbajack names a game by its own enum; MO2 names the same game its own way,
    // and this application follows MO2. Wabbajack publishes the pairing itself as
    // MO2Name in its game registry, so this is that pairing for the games MO2 has a
    // plugin for. A modlist for anything else — and the gallery carries lists for
    // Stardew Valley, No Man's Sky, Dishonored — is left unsupported rather than
    // guessed at, because MO2 could not manage it.
    private static readonly Dictionary<string, string> Games = new(StringComparer.OrdinalIgnoreCase) {
        ["morrowind"] = "Morrowind",
        ["oblivion"] = "Oblivion",
        ["fallout3"] = "Fallout 3",
        ["falloutnewvegas"] = "New Vegas",
        ["skyrim"] = "Skyrim",
        ["skyrimspecialedition"] = "Skyrim Special Edition",
        ["skyrimvr"] = "Skyrim VR",
        ["fallout4"] = "Fallout 4",
        ["fallout4vr"] = "Fallout 4 VR",
        ["enderal"] = "Enderal",
        ["enderalspecialedition"] = "Enderal Special Edition",
        ["starfield"] = "Starfield",
        ["taleoftwowastelands"] = "TTW",
    };

    internal static string? GameFor(string wabbajackGame) =>
        Games.TryGetValue(wabbajackGame, out var name) && Mo2SupportedGames.Find(name) is { } found ? found.Name : null;

    internal static async Task<IReadOnlyList<Mo2Modlist>> Read(
        Action<string?>? report = null, bool refresh = false, CancellationToken token = default)
    {
        if (!refresh && Cached() is { Count: > 0 } cached) return cached;
        try {
            var fetched = await Fetch(report, token);
            Save(fetched);
            return fetched;
        } catch (Exception) when (Cached(ignoreAge: true) is { Count: > 0 }) {
            // A gallery that cannot be reached is not a reason to show nothing: what
            // was read last time is still a true list of what exists, only older.
            return Cached(ignoreAge: true)!;
        }
    }

    private static async Task<IReadOnlyList<Mo2Modlist>> Fetch(Action<string?>? report, CancellationToken token)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("mo2-nexus-frontend");

        report?.Invoke("Reading the Wabbajack gallery…");
        var repositories = await client.GetFromJsonOrEmpty<Dictionary<string, string>>(Repositories, token)
            ?? throw new InvalidOperationException("The Wabbajack gallery listed no repositories");

        var official = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in await client.GetFromJsonOrEmpty<string[]>(Featured, token) ?? [])
            official.Add(name);

        // Every repository is asked at once; one that is gone must not hold up the
        // rest, and there are well over a hundred of them.
        var found = await Task.WhenAll(repositories.Select(async pair => {
            try { return (pair.Key, Lists: await client.GetFromJsonOrEmpty<JsonElement[]>(pair.Value, token) ?? []); }
            catch (Exception) { return (pair.Key, Lists: []); }
        }));

        var all = new List<Mo2Modlist>();
        foreach (var (repository, lists) in found)
            foreach (var entry in lists)
                if (Parse(entry, repository, official) is { } modlist)
                    all.Add(modlist);

        report?.Invoke($"Read {all.Count} modlists.");
        return all.OrderByDescending(x => x.Official).ThenBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private static Mo2Modlist? Parse(JsonElement entry, string repository, HashSet<string> official)
    {
        string Text(string name, string fallback = "") =>
            entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? fallback : fallback;
        bool Flag(string name) => entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

        var links = entry.TryGetProperty("links", out var l) && l.ValueKind == JsonValueKind.Object ? l : default;
        string? Link(string name) => links.ValueKind == JsonValueKind.Object &&
            links.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() : null;

        var machine = Link("machineURL");
        var title = Text("title");
        if (string.IsNullOrWhiteSpace(machine) || string.IsNullOrWhiteSpace(title)) return null;

        var download = entry.TryGetProperty("download_metadata", out var d) && d.ValueKind == JsonValueKind.Object ? d : default;
        long Size(string name) => download.ValueKind == JsonValueKind.Object &&
            download.TryGetProperty(name, out var value) && value.TryGetInt64(out var size) ? size : 0;

        var tags = entry.TryGetProperty("tags", out var t) && t.ValueKind == JsonValueKind.Array
            ? t.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray()
            : [];

        var game = Text("game");
        var updated = entry.TryGetProperty("dateUpdated", out var u) && u.TryGetDateTime(out var when) ? when : default;

        var modlist = new Mo2Modlist(
            Title: title,
            Description: Text("description"),
            Author: Text("author"),
            Repository: repository,
            MachineUrl: machine!,
            WabbajackGame: game,
            Game: GameFor(game),
            Version: Text("version"),
            Tags: tags,
            // Wabbajack marks its own featured repository official outright, and
            // otherwise takes the word from the featured list.
            Official: repository == "wj-featured" || Flag("official"),
            Nsfw: Flag("nsfw"),
            Utility: Flag("utility_list"),
            DownloadSize: Size("Size"),
            TotalSize: Size("TotalSize"),
            ImageUrl: Link("image"),
            ReadmeUrl: Link("readme"),
            Updated: updated);

        return official.Contains(modlist.NamespacedName) ? modlist with { Official = true } : modlist;
    }

    private static IReadOnlyList<Mo2Modlist>? Cached(bool ignoreAge = false)
    {
        try {
            if (!File.Exists(CachePath)) return null;
            if (!ignoreAge && DateTime.UtcNow - File.GetLastWriteTimeUtc(CachePath) > CacheLife) return null;
            return JsonSerializer.Deserialize<Mo2Modlist[]>(File.ReadAllText(CachePath), Options);
        } catch (Exception) { return null; }
    }

    private static void Save(IReadOnlyList<Mo2Modlist> lists)
    {
        try {
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(lists, Options));
        } catch (IOException) { }
    }

    private static readonly JsonSerializerOptions Options = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static async Task<T?> GetFromJsonOrEmpty<T>(this HttpClient client, string url, CancellationToken token)
    {
        using var response = await client.GetAsync(url, token);
        if (!response.IsSuccessStatusCode) return default;
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(token), Options);
    }
}
