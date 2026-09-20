namespace Mo2.Frontend;

internal readonly record struct Mo2FilterRequestKey(Mo2ProfileTarget Target, long Revision);

// One coalesced reader per panel. A detached/hidden panel cannot publish a late
// completion; a changed target/revision always wins over an older request.
internal sealed class Mo2NativeFilterCache(Func<Mo2ProfileTarget, Mo2CategoryNode[], Task<IReadOnlyDictionary<(int Type, int Id), string[]>>> read)
{
    private bool enabled, running;
    private long generation;
    private Mo2FilterRequestKey? wanted, completed;
    private Mo2CategoryNode[] criteria = [];
    private IReadOnlyDictionary<(int Type, int Id), HashSet<string>>? matches;
    private Mo2ProfileTarget? displayedTarget;
    private IReadOnlyDictionary<(int Type, int Id), HashSet<string>>? displayedMatches;
    internal event Action? Changed;
    internal string? Error { get; private set; }
    internal bool IsReading => enabled && running;
    internal IReadOnlyDictionary<(int Type, int Id), HashSet<string>>? For(Mo2FilterRequestKey key) => completed == key && Error is null ? matches : null;
    // Preserve the last accepted presentation while this same profile refreshes.
    // For() remains strict: consumers must not mistake this for current data.
    internal IReadOnlyDictionary<(int Type, int Id), HashSet<string>>? ForPresentation(Mo2FilterRequestKey key) =>
        For(key) ?? (wanted == key && displayedTarget == key.Target ? displayedMatches : null);

    internal void SetEnabled(bool value)
    {
        if (enabled == value) return;
        enabled = value; generation++;
        if (enabled) Start();
    }
    internal void Request(Mo2FilterRequestKey key, Mo2CategoryNode[] nodes, bool active = true)
    {
        if (enabled != active) { enabled = active; generation++; }
        if (wanted != key) {
            if (displayedTarget != key.Target || displayedMatches is not null &&
                !displayedMatches.Keys.ToHashSet().SetEquals(nodes.Select(x => x.Key))) {
                displayedTarget = null; displayedMatches = null;
            }
            wanted = key; criteria = nodes; generation++;
            completed = null; matches = null; Error = null;
        }
        Start();
    }
    internal void Retry()
    {
        completed = null; matches = null; Error = null; generation++;
        Start(); Changed?.Invoke();
    }
    private void Start()
    {
        if (!enabled || running || wanted is null || completed == wanted) return;
        running = true; _ = Run();
    }
    private async Task Run()
    {
        try {
            while (enabled && wanted is { } key && completed != key) {
                var stamp = generation;
                var nodes = criteria;
                bool Current() => enabled && generation == stamp && wanted == key;
                try {
                    var result = new Dictionary<(int Type, int Id), HashSet<string>>();
                    foreach (var chunk in nodes.Chunk(128)) {
                        var reply = await read(key.Target, chunk);
                        if (!Current()) break;
                        foreach (var entry in reply) result.Add(entry.Key, entry.Value.ToHashSet(StringComparer.Ordinal));
                    }
                    if (!Current()) continue;
                    matches = result; completed = key; Error = null;
                    displayedTarget = key.Target; displayedMatches = result;
                } catch (Exception error) {
                    if (!Current()) continue;
                    matches = null; completed = key; Error = error.Message;
                }
                Changed?.Invoke();
            }
        } finally { running = false; Changed?.Invoke(); }
    }
}
