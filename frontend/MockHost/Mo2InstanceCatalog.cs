using System.Text.Json;

namespace Mo2.Frontend;

internal sealed record Mo2Registration(string Directory, string Endpoint);
internal sealed record Mo2CatalogEntry(Mo2Registration Registration, Mo2InstanceSnapshot? Instance, string? Error);

// Only frontend connection preferences live here. All profile content stays in MO2.
internal sealed class Mo2InstanceCatalog
{
    public string? LoadError { get; }
    private readonly string _config;
    private readonly List<Mo2Registration> _registrations = [];
    public Mo2InstanceCatalog(string activeEndpoint)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _config = Path.Combine(Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") ?? Path.Combine(home, ".config"), "mo2-nexus-frontend", "instances.json");
        try {
            if (File.Exists(_config)) _registrations.AddRange(JsonSerializer.Deserialize<Mo2Registration[]>(File.ReadAllText(_config)) ?? []);
        } catch (Exception error) { LoadError = "Unable to read saved instance connections: " + error.Message; }
        if (activeEndpoint.Length > 0) {
            var activeRoot = Path.GetFullPath(Path.Combine(activeEndpoint, "..", "..", ".."));
            Discover(activeRoot, activeEndpoint);
        }
        Discover(Path.Combine(home, "ModOrganizer2"));
        var games = Path.Combine(home, "Games");
        if (Directory.Exists(games))
            foreach (var game in Directory.EnumerateDirectories(games)) Discover(Path.Combine(game, "modorganizer2"));
    }
    private void Discover(string root, string? endpoint = null)
    {
        root = Path.GetFullPath(root);
        if (!File.Exists(Path.Combine(root, "ModOrganizer.ini")) || _registrations.Any(x => x.Directory == root)) return;
        _registrations.Add(new(root, endpoint ?? Path.Combine(root, "plugins", "data", "frontend-bridge")));
    }
    public void Add(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "ModOrganizer.ini"))) throw new ArgumentException("Choose the MO2 instance folder containing ModOrganizer.ini");
        Discover(directory);
        Directory.CreateDirectory(Path.GetDirectoryName(_config)!);
        File.WriteAllText(_config + ".tmp", JsonSerializer.Serialize(_registrations, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(_config + ".tmp", _config, true);
    }
    public IReadOnlyList<Mo2CatalogEntry> Read() => _registrations.Select(registration => {
        try { return new Mo2CatalogEntry(registration, Mo2ProfileFiles.Read(registration.Directory), null); }
        catch (Exception error) { return new Mo2CatalogEntry(registration, null, error.Message); }
    }).ToArray();
    public static string LocalPath(string path)
    {
        path = path.Replace('\\', '/');
        if (!OperatingSystem.IsWindows() && path.StartsWith("Z:/", StringComparison.OrdinalIgnoreCase)) path = path[2..];
        return Path.GetFullPath(path);
    }
}
