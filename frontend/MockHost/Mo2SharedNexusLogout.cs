namespace Mo2.Frontend;

internal sealed record Mo2LogoutResult(int Disconnected, int Total, IReadOnlyList<string> Failures);
internal static class Mo2SharedNexusLogout
{
    public static Task<Mo2LogoutResult> Apply(IReadOnlyList<Mo2Registration> registrations, Action<string> progress, CancellationToken cancellation) =>
        ApplyCore(registrations, progress, cancellation,
            registration => Mo2HostStartup.Connect(registration, progress),
            async registration => (await new Mo2BridgeClient(registration.Endpoint).SendAsync("disconnectNexusAccount")).GetProperty("disconnected").GetBoolean());

    internal static async Task<Mo2LogoutResult> ApplyCore(IReadOnlyList<Mo2Registration> registrations, Action<string> progress,
        CancellationToken cancellation, Func<Mo2Registration, Task> connect, Func<Mo2Registration, Task<bool>> disconnect)
    {
        var unique = registrations.DistinctBy(x => x.Endpoint).ToArray();
        var failures = new List<string>();
        var disconnected = 0;
        for (var i = 0; i < unique.Length; i++) {
            if (cancellation.IsCancellationRequested) break;
            progress($"Disconnecting MO2 instance {i + 1} of {unique.Length}…");
            try {
                // Closed instances must be started too: their stored credential
                // would otherwise silently reconnect at the next game launch.
                await connect(unique[i]);
                if (cancellation.IsCancellationRequested) break;
                // Observe submitted changes to completion, just as shared login does.
                if (!await disconnect(unique[i])) throw new InvalidOperationException();
                disconnected++;
            } catch {
                failures.Add($"Instance {i + 1}: disconnection was not confirmed. Refresh before retrying.");
            }
        }
        if (cancellation.IsCancellationRequested) failures.Add("Cancelled; remaining instances were not changed.");
        return new(disconnected, unique.Length, failures);
    }
}
