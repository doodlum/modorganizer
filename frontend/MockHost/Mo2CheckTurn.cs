namespace Mo2.Frontend;

// Checks that work the same two tables — selecting rows, clearing them, scrolling
// — are started from the window opening and so run at the same time as each
// other. Left to overlap they read each other's leftovers: one reported a clear
// action that "left rows selected" because another had just selected some, and
// another saw the two toolbars laid out differently because only one of the pages
// had a selection at that instant. Each of them takes a turn here instead.
internal static class Mo2CheckTurn
{
    private static readonly SemaphoreSlim Turn = new(1, 1);
    private static int _outstanding;

    // Checks started from the window opening race the screenshot path, which
    // renders and calls Shutdown on a four-second timer. That race was being won
    // by accident: the screenshot path's gate waited ten seconds for a page a
    // navigating check had moved, and the delay was what let that check finish.
    // Fixing the gate made the app shut down mid-check instead, and the widget
    // check then reported nothing at all — a run that printed no failures because
    // it never got to look. A check enabled before the window opens says so here,
    // and the screenshot path waits for it rather than for a timeout.
    internal static void Expect() => Interlocked.Increment(ref _outstanding);

    internal static void Finished() => Interlocked.Decrement(ref _outstanding);

    // Every expected check has run, and no turn is still held. The timeout reports
    // rather than throws: a check that hangs should not also destroy the run's
    // screenshot and the checks queued behind it.
    internal static async Task<bool> Settled(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (Volatile.Read(ref _outstanding) > 0) {
            if (DateTime.UtcNow >= deadline) {
                Console.WriteLine($"FAIL check turn: {Volatile.Read(ref _outstanding)} check(s) had not finished after {timeout.TotalSeconds:0}s");
                return false;
            }
            await Task.Delay(50);
        }
        using var turn = await Take();
        return true;
    }

    internal static async Task<IDisposable> Take()
    {
        await Turn.WaitAsync();
        return new Release();
    }

    private sealed class Release : IDisposable
    {
        private bool _done;
        public void Dispose() { if (!_done) { _done = true; Turn.Release(); } }
    }
}
