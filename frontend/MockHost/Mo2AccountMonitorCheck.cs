namespace Mo2.Frontend;

internal static class Mo2AccountMonitorCheck
{
    public static async Task Live(string endpoint)
    {
        var registrations = new Mo2InstanceCatalog(endpoint).Read().Select(x => x.Registration).ToArray();
        var results = new List<Mo2LoginResult>();
        using var monitor = new Mo2AccountMonitor(() => Mo2SharedNexusLogin.ReadStatus(registrations), results.Add);
        await monitor.Refresh();
        await monitor.Refresh();
        if (results.Count != 2 || results.Any(x => x.Total == 0 || x.Connected != x.Total || x.Failures.Count != 0))
            throw new Exception("Repeated native account checks did not confirm all registered instances");
        Console.WriteLine($"PASS repeated read-only account refresh confirmed {results[0].Connected}/{results[0].Total} instances");
    }

    public static async Task Run()
    {
        var confirmed = new Mo2LoginResult(3, 3, "Fixture", [], Premium: true, Scope: "one\ntwo\nthree", UserId: 42);
        var busy = confirmed with { Connected = 2, Unavailable = 1 };
        if (!busy.CanRetainConfirmedAccount(confirmed) ||
            (busy with { Connected = 0, Unavailable = 3, Username = null }).CanRetainConfirmedAccount(confirmed) == false ||
            (busy with { Connected = 1 }).CanRetainConfirmedAccount(confirmed) ||
            (busy with { Scope = "different catalog" }).CanRetainConfirmedAccount(confirmed) ||
            (busy with { Username = "Other account" }).CanRetainConfirmedAccount(confirmed) ||
            (busy with { UserId = 43 }).CanRetainConfirmedAccount(confirmed) ||
            busy.CanRetainConfirmedAccount(null) ||
            (busy with { Failures = ["Account mismatch"] }).CanRetainConfirmedAccount(confirmed))
            throw new Exception("Unknown account status confused with confirmed logout, changed account or catalog");
        Console.WriteLine("PASS busy account display retains only the same previously confirmed account and catalog; confirmed logout clears it");
        var connected = new Mo2LoginResult(2, 2, "Fixture", []);
        var disconnected = new Mo2LoginResult(1, 2, null, []);
        var applied = new List<Mo2LoginResult>();
        var pending = new TaskCompletionSource<Mo2LoginResult>();
        var calls = 0;
        using var monitor = new Mo2AccountMonitor(() => ++calls == 1 ? pending.Task : Task.FromResult(disconnected), applied.Add);
        var first = monitor.Refresh();
        await monitor.Refresh(); // Catalog changed while the first read was pending.
        if (calls != 1) throw new Exception("Account reads overlapped");
        pending.SetResult(connected);
        await first;
        if (calls != 2 || !applied.SequenceEqual(new[] { disconnected })) throw new Exception("Stale catalog status was applied");

        applied.Clear(); pending = new(); calls = 0;
        using var login = new Mo2AccountMonitor(() => ++calls == 1 ? pending.Task : Task.FromResult(connected), applied.Add);
        first = login.Refresh();
        if (!login.BeginLogin() || login.BeginLogin()) throw new Exception("Concurrent login allowed");
        await login.Refresh();
        await login.EndLogin(connected);
        pending.SetResult(disconnected);
        await first;
        if (calls != 2 || applied.Count != 2 || applied.Any(x => x != connected)) throw new Exception("Old read overwrote a new login");

        applied.Clear(); pending = new();
        var disposed = new Mo2AccountMonitor(() => pending.Task, applied.Add);
        first = disposed.Refresh(); disposed.Dispose(); pending.SetResult(connected); await first;
        await disposed.Refresh();
        if (applied.Count != 0 || disposed.BeginLogin()) throw new Exception("Closed window received account updates");

        using var failure = new Mo2AccountMonitor(() => throw new IOException("private fixture body"), applied.Add);
        await failure.Refresh();
        if (applied.Single().Connected != 0 || applied.Single().Failures.Any(x => x.Contains("private fixture"))) throw new Exception("Failed read retained authentication or leaked details");
        Console.WriteLine("PASS account refresh serialization, catalog invalidation, login race, disposal and read failure");
    }
}
