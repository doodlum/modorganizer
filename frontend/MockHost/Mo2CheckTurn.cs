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
