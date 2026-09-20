using System.Text.Json;

namespace Mo2.Frontend;

internal sealed record Mo2Registration(string Directory, string Endpoint, string? Launcher = null);
internal sealed record Mo2CatalogEntry(Mo2Registration Registration, Mo2InstanceSnapshot? Instance, string? Error);

// Only frontend connection preferences live here. All profile content stays in MO2.
internal sealed class Mo2InstanceCatalog
{
    public string? LoadError { get; }
    private readonly string _config;
    private readonly List<Mo2Registration> _registrations = [];
    public Mo2InstanceCatalog(string activeEndpoint)
    {
        _config = Mo2ConfigPaths.Combine("instances.json");
        try {
            if (File.Exists(_config)) _registrations.AddRange(JsonSerializer.Deserialize<Mo2Registration[]>(File.ReadAllText(_config)) ?? []);
        } catch (Exception error) { LoadError = "Unable to read saved instance connections: " + error.Message; }
        // The frontend creates and owns its instances, so the catalogue is what it
        // has written rather than whatever MO2 setups happen to be on the machine.
        // It used to scan ~/ModOrganizer2 and ~/Games/*/modorganizer2 and adopt
        // them; that is no longer how a game gets here.
        //
        // A bridge directory passed in is different: it names one running host
        // explicitly, which is how a live session and the live checks attach.
        if (activeEndpoint.Length > 0) {
            var activeRoot = Path.GetFullPath(Path.Combine(activeEndpoint, "..", "..", ".."));
            Discover(activeRoot, activeEndpoint);
        }
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
        Save();
    }
    public void SetLauncher(Mo2Registration registration, string launcher)
    {
        launcher = Path.GetFullPath(launcher);
        if (!File.Exists(launcher)) throw new FileNotFoundException("MO2 launcher not found");
        var index = _registrations.FindIndex(x => x.Directory == registration.Directory);
        if (index < 0) throw new InvalidOperationException("MO2 instance no longer registered");
        var previous = _registrations[index];
        _registrations[index] = previous with { Launcher = launcher };
        try { Save(); } catch { _registrations[index] = previous; throw; }
    }
    private void Save()
    {
        if (LoadError is not null) throw new InvalidOperationException(LoadError);
        Directory.CreateDirectory(Path.GetDirectoryName(_config)!);
        File.WriteAllText(_config + ".tmp", JsonSerializer.Serialize(_registrations, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(_config + ".tmp", _config, true);
    }
    public Mo2Registration[] Registrations => _registrations.ToArray();
    public IReadOnlyList<Mo2CatalogEntry> Read() => Read(Registrations);
    public Task<IReadOnlyList<Mo2CatalogEntry>> ReadAsync()
    {
        // Capture registrations on the caller's thread; only filesystem reads
        // run in the worker, never enumeration of the mutable registration list.
        var registrations = Registrations;
        return Task.Run(() => Read(registrations));
    }
    private static IReadOnlyList<Mo2CatalogEntry> Read(Mo2Registration[] registrations) => registrations.Select(registration => {
        try { return new Mo2CatalogEntry(registration, Mo2ProfileFiles.Read(registration.Directory), null); }
        catch (Exception error) { return new Mo2CatalogEntry(registration, null, error.Message); }
    }).ToArray();
    public static string LocalPath(string path)
    {
        path = path.Replace('\\', '/');
        if (!OperatingSystem.IsWindows() && path.StartsWith("Z:/", StringComparison.OrdinalIgnoreCase)) path = path[2..];
        if (OperatingSystem.IsWindows()) path = WithoutPackageRedirection(path);
        return Path.GetFullPath(path);
    }

    // Windows gives a packaged application its own view of the per-user local data
    // directory, so a write to AppData/Local/X lands in
    // AppData/Local/Packages/<package>/LocalCache/Local/X. A process reports the
    // real location of its files while a process outside that view reports the
    // virtual one, and the two spellings name the same directory. MO2 and this
    // frontend compare instance and profile directories as strings, so one side
    // seeing through the redirection and the other not made a host disown its own
    // profile. Both spellings are collapsed to the virtual one.
    private static string WithoutPackageRedirection(string path)
    {
        const string marker = "/AppData/Local/Packages/";
        var start = path.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return path;
        const string tail = "/LocalCache/Local/";
        var cache = path.IndexOf(tail, start, StringComparison.OrdinalIgnoreCase);
        if (cache < 0) return path;
        return path[..(start + "/AppData/Local/".Length)] + path[(cache + tail.Length)..];
    }

    // The other direction, for handing a path to something outside this application.
    //
    // Everything above collapses both spellings to the virtual one, because that is
    // what comparing this application's idea of a folder with MO2's needs. Explorer
    // is not comparing: it has to find the folder, and it is not inside the view that
    // makes the virtual spelling mean anything. Handed one it cannot resolve, it
    // opens on its own home instead of the folder — which is what "Open in Explorer"
    // and "Reveal in Explorer" did.
    //
    // Outside a packaged application there is no redirection and nothing to undo, so
    // this returns the path it was given.
    internal static string DesktopPath(string path)
    {
        var full = LocalPath(path).Replace('\\', '/');
        if (!OperatingSystem.IsWindows()) return Path.GetFullPath(full);

        const string marker = "/AppData/Local/";
        var start = full.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return Path.GetFullPath(full);
        var rest = full[(start + marker.Length)..];
        if (rest.Length == 0 || rest.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            return Path.GetFullPath(full);

        // Found by looking rather than by asking. A redirected process is not told it
        // is one: the folder APIs still answer with the plain per-user directory, and
        // only the bytes go elsewhere. So the store this application's files are
        // really in is the package whose cache holds them.
        var packages = Path.Combine(full[..(start + marker.Length)].Replace('/', Path.DirectorySeparatorChar), "Packages");
        if (_realRoot is null && Directory.Exists(packages)) {
            try {
                foreach (var package in Directory.EnumerateDirectories(packages)) {
                    var candidate = Path.Combine(package, "LocalCache", "Local");
                    if (!Directory.Exists(Path.Combine(candidate, rest.Replace('/', Path.DirectorySeparatorChar))) &&
                        !File.Exists(Path.Combine(candidate, rest.Replace('/', Path.DirectorySeparatorChar)))) continue;
                    _realRoot = candidate; break;
                }
            } catch (Exception) { }
            _realRoot ??= "";
        }
        if (string.IsNullOrEmpty(_realRoot)) return Path.GetFullPath(full);
        return Path.GetFullPath(Path.Combine(_realRoot, rest.Replace('/', Path.DirectorySeparatorChar)));
    }

    // Empty once looked for and not found, so the look happens once.
    private static string? _realRoot;
}
