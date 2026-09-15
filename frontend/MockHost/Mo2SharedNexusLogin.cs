using System.Text.Json;

namespace Mo2.Frontend;

internal sealed record Mo2LoginResult(int Connected, int Total, string? Username, IReadOnlyList<string> Failures, bool Premium = false, bool Supporter = false, int Unavailable = 0, string Scope = "", long? UserId = null)
{
    public bool CanRetainConfirmedAccount(Mo2LoginResult? previous) =>
        Unavailable > 0 && Connected + Unavailable == Total && Failures.Count == 0 &&
        Scope.Length > 0 && previous is { Total: > 0, Unavailable: 0 } &&
        previous.Connected == previous.Total && previous.Failures.Count == 0 &&
        previous.Scope == Scope && previous.Total == Total &&
        (UserId is null || UserId == previous.UserId) && (Username is null || Username == previous.Username);
}
internal static class Mo2SharedNexusLogin
{
    public static async Task<Mo2LoginResult> ReadStatus(IReadOnlyList<Mo2Registration> registrations)
    {
        var connected = 0;
        var unavailable = 0;
        long? identity = null;
        string? username = null;
        bool premium = false, supporter = false;
        var unique = registrations.DistinctBy(x => x.Endpoint).ToArray();
        foreach (var registration in unique) {
            try {
                if (!Mo2HostStartup.IsRunning(registration)) { unavailable++; continue; }
                var state = await new Mo2BridgeClient(registration.Endpoint).SendAsync("readNativeNexusAccount");
                if (!state.GetProperty("connected").GetBoolean()) continue;
                var id = state.GetProperty("userId").GetInt64();
                if (identity is { } previous && previous != id) return new(0, unique.Length, null, ["MO2 instances use different Nexus accounts."]);
                if (identity is null) {
                    var validated = await new Mo2BridgeClient(registration.Endpoint).SendAsync("readNexusAccount");
                    var account = validated.GetProperty("account");
                    if (account.GetProperty("userId").GetInt64() != id) continue;
                    premium = account.GetProperty("premium").GetBoolean(); supporter = account.GetProperty("supporter").GetBoolean();
                }
                identity = id; username = state.GetProperty("name").GetString(); connected++;
            } catch { unavailable++; /* Unavailable is distinct from a confirmed logout. */ }
        }
        return new(connected, unique.Length, username, [], premium, supporter, unavailable, AccountScope(unique), identity);
    }
    private static string AccountScope(IEnumerable<Mo2Registration> registrations) =>
        string.Join("\n", registrations.Select(x => x.Endpoint).Distinct().Order(StringComparer.Ordinal));
    public static Task<Mo2LoginResult> Apply(string key, IReadOnlyList<Mo2Registration> registrations,
        Action<string> progress, CancellationToken cancellationToken) => ApplyCore(key, registrations, progress, cancellationToken,
            registration => Mo2HostStartup.Connect(registration, progress),
            (registration, path) => new Mo2BridgeClient(registration.Endpoint).SendAsync("connectNexusAccount", new() { ["path"] = path }, timeout: TimeSpan.FromSeconds(45)));

    internal static async Task<Mo2LoginResult> ApplyCore(string key, IReadOnlyList<Mo2Registration> registrations,
        Action<string> progress, CancellationToken cancellationToken, Func<Mo2Registration, Task> connect,
        Func<Mo2Registration, string, Task<JsonElement>> apply)
    {
        registrations = registrations.DistinctBy(x => x.Endpoint).ToArray();
        if (registrations.Count == 0) throw new InvalidOperationException("Register an MO2 instance before logging in.");
        if (key.Length is 0 or > 4096 || key.Any(c => c < 33 || c > 126)) throw new InvalidOperationException("Nexus returned an invalid credential.");
        var directory = Path.Combine(Path.GetTempPath(), "mo2-nexus-login-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = Path.Combine(directory, "credential");
        var failures = new List<string>();
        var connected = 0;
        long? identity = null;
        string? username = null;
        bool premium = false, supporter = false;
        try {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var stream = new FileStream(path, options))
            await using (var writer = new StreamWriter(stream)) await writer.WriteAsync(key);
            foreach (var registration in registrations.DistinctBy(x => x.Endpoint)) {
                if (cancellationToken.IsCancellationRequested) break;
                string label;
                try { label = Mo2ProfileFiles.Read(registration.Directory).Game; } catch { label = Path.GetFileName(registration.Directory); }
                progress($"Connecting MO2 instance {connected + failures.Count + 1} of {registrations.Count}…");
                try {
                    await connect(registration);
                    if (cancellationToken.IsCancellationRequested) break;
                    var hostPath = OperatingSystem.IsWindows() ? path : "Z:" + path;
                    // Once submitted, finish observing this mutation even if Cancel is
                    // pressed. Only later instances are skipped; never retry on timeout.
                    var result = await apply(registration, hostPath);
                    if (!result.GetProperty("connected").GetBoolean()) throw new InvalidOperationException();
                    var account = result.GetProperty("account");
                    var userId = account.GetProperty("userId").GetInt64();
                    if (identity is { } expected && expected != userId) throw new InvalidOperationException();
                    identity = userId; username = account.GetProperty("name").GetString();
                    premium = account.TryGetProperty("premium", out var paid) && paid.ValueKind == JsonValueKind.True;
                    supporter = account.TryGetProperty("supporter", out var support) && support.ValueKind == JsonValueKind.True;
                    connected++;
                } catch (Exception) {
                    // Native/network exceptions may contain arbitrary host text. The UI
                    // reports only which instance needs attention, never the reply body.
                    failures.Add(label + ": connection was not confirmed. Refresh before retrying.");
                }
            }
            if (cancellationToken.IsCancellationRequested) failures.Add("Cancelled; remaining instances were not changed.");
            return new(connected, registrations.Select(x => x.Endpoint).Distinct().Count(), username, failures, premium, supporter, Scope: AccountScope(registrations), UserId: identity);
        } finally {
            if (File.Exists(path)) File.Delete(path);
            Directory.Delete(directory);
        }
    }
}
