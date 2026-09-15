using System.Text.Json;

namespace Mo2.Frontend;

internal sealed class Mo2BridgeCommandException(string action, string message) : InvalidOperationException(message)
{
    public string Action { get; } = action;
}

internal sealed class Mo2BridgeClient(string directory)
{
    // Every action used to read and parse endpoint.json first. The session only
    // changes when MO2 restarts and rewrites the file, so it is kept until the file
    // itself changes: a stat instead of a read and a parse, on the path that runs
    // before every single request.
    private static readonly Dictionary<string, (DateTime Written, long Length, string Session)> Sessions = new();

    private static async Task<string> SessionFor(string directory, CancellationToken cancellationToken)
    {
        var path = Path.Combine(directory, "endpoint.json");
        var info = new FileInfo(path);
        var stamp = (info.LastWriteTimeUtc, info.Length);
        lock (Sessions) {
            if (Sessions.TryGetValue(path, out var cached) && cached.Written == stamp.LastWriteTimeUtc && cached.Length == stamp.Length)
                return cached.Session;
        }
        using var endpoint = JsonDocument.Parse(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false));
        if (endpoint.RootElement.GetProperty("protocol").GetInt32() != 1) throw new NotSupportedException("Unsupported MO2 bridge protocol");
        var session = endpoint.RootElement.GetProperty("session").GetString()!;
        lock (Sessions) Sessions[path] = (stamp.LastWriteTimeUtc, stamp.Length, session);
        return session;
    }

    // How long to wait between looks for the response. MO2 answers most actions in
    // a few milliseconds, and a flat 50ms poll charged every one of them up to 50ms
    // of pure waiting; this starts nearly immediately and only backs off for the
    // requests that really are slow.
    internal static TimeSpan PollAfter(int attempt) =>
        TimeSpan.FromMilliseconds(Math.Min(50, 1 << Math.Min(attempt, 6)));

    public async Task<JsonElement> SendAsync(string action, Dictionary<string, object?>? arguments = null, CancellationToken cancellationToken = default, TimeSpan? timeout = null)
    {
        // Everything inside a request runs off the UI thread. Avalonia's
        // synchronization context otherwise puts each await — including the loop
        // that waits for MO2's reply — back on the dispatcher, so an action issued
        // while the window was building took two seconds to complete what takes
        // sixty milliseconds on a quiet thread. The caller's own continuation still
        // resumes on the UI thread, which is where the answer is applied.
        var session = await SessionFor(directory, cancellationToken).ConfigureAwait(false);
        var id = Guid.NewGuid().ToString();
        var request = new Dictionary<string, object?>(arguments ?? []) { ["protocol"] = 1, ["session"] = session, ["action"] = action };
        var path = Path.Combine(directory, "requests", id + ".json");
        var responsePath = Path.Combine(directory, "responses", id + ".json");
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using (var stream = new FileStream(path + ".tmp", options))
            await JsonSerializer.SerializeAsync(stream, request, cancellationToken: cancellationToken).ConfigureAwait(false);
        File.Move(path + ".tmp", path);
        var deadline = DateTime.UtcNow.Add(timeout ?? TimeSpan.FromSeconds(15));
        var nextHostCheck = DateTime.UtcNow.AddSeconds(1);
        var instance = Path.GetFullPath(Path.Combine(directory, "..", "..", ".."));
        var attempt = 0;
        while (!File.Exists(responsePath)) {
            if (DateTime.UtcNow >= nextHostCheck) {
                nextHostCheck = DateTime.UtcNow.AddSeconds(1);
                if (OperatingSystem.IsLinux() && File.Exists(Path.Combine(instance, "ModOrganizer.exe")) && !Mo2HostStartup.IsRunning(new Mo2Registration(instance, directory)))
                    throw new IOException("MO2 has stopped. Select the game icon to reconnect. The last action’s outcome is unknown; refresh before retrying.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"MO2 bridge request {id} timed out. Its outcome is unknown; refresh before retrying a change.");
            await Task.Delay(PollAfter(attempt++), cancellationToken).ConfigureAwait(false);
        }
        using var response = JsonDocument.Parse(await File.ReadAllTextAsync(responsePath, cancellationToken).ConfigureAwait(false));
        var root = response.RootElement;
        if (root.GetProperty("id").GetString() != id || root.GetProperty("session").GetString() != session)
            throw new InvalidDataException("MO2 bridge response identity mismatch");
        if (!root.GetProperty("ok").GetBoolean()) throw new Mo2BridgeCommandException(action, root.GetProperty("error").GetString() ?? "MO2 rejected the request.");
        var result = root.GetProperty("result").Clone();
        // Leave the response receipt until the request has been removed by MO2,
        // so a delayed poll cannot repeat a mutation after response consumption.
        if (!File.Exists(path)) File.Delete(responsePath);
        return result;
    }
}
