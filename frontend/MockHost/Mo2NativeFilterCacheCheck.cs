namespace Mo2.Frontend;

internal static class Mo2NativeFilterCacheCheck
{
    internal static async Task Run()
    {
        var pending = new List<TaskCompletionSource<IReadOnlyDictionary<(int Type, int Id), string[]>>>();
        var sizes = new List<int>();
        var cache = new Mo2NativeFilterCache((target, nodes) => {
            sizes.Add(nodes.Length);
            var source = new TaskCompletionSource<IReadOnlyDictionary<(int Type, int Id), string[]>>();
            pending.Add(source); return source.Task;
        });
        var target = new Mo2ProfileTarget("fixture-endpoint", "fixture-profile");
        Mo2CategoryNode[] nodes = [new(10000, 0, "Enabled", [])];
        var key1 = new Mo2FilterRequestKey(target, 1);
        var key2 = key1 with { Revision = 2 };
        void Reply(int index, params string[] names) => pending[index].SetResult(new Dictionary<(int, int), string[]> { [(0, 10000)] = names });
        void Expect(bool value, string error) { if (!value) throw new Exception(error); }
        async Task Wait(Func<bool> ready) {
            var end = DateTime.UtcNow.AddSeconds(3);
            while (!ready() && DateTime.UtcNow < end) await Task.Delay(10);
            Expect(ready(), "Native filter cache did not settle");
        }
        cache.Request(key1, nodes); cache.Request(key1, nodes);
        Expect(pending.Count == 1, "Duplicate requests overlapped");
        cache.Request(key2, nodes); Reply(0, "stale"); await Wait(() => pending.Count == 2);
        Expect(cache.For(key1) is null && cache.For(key2) is null, "Old result was published");
        Reply(1, "current"); await Wait(() => cache.For(key2) is not null);
        cache.Request(key2, nodes); cache.SetEnabled(false); cache.Request(key2, nodes);
        Expect(pending.Count == 2 && cache.For(key2)![(0, 10000)].Contains("current"), "Stable cache was re-read on visibility change");
        var key3 = key1 with { Revision = 3 };
        cache.Request(key3, nodes);
        Expect(cache.For(key3) is null && cache.ForPresentation(key3)![(0, 10000)].SetEquals(["current"]),
            "Refresh cleared the last accepted presentation or marked it current");
        cache.SetEnabled(false); Reply(2, "detached");
        await Task.Delay(20);
        Expect(cache.For(key3) is null && !cache.IsReading, "Detached result was published");
        var other = new Mo2FilterRequestKey(new("other-endpoint", "fixture-profile"), 3);
        cache.Request(other, nodes); pending[3].SetException(new IOException("controlled read error"));
        await Wait(() => cache.Error is not null);
        Expect(cache.ForPresentation(other) is null && cache.ForPresentation(key3) is null, "Presentation leaked across profiles");
        cache.Request(other, nodes); Expect(pending.Count == 4, "Failed reads looped automatically");
        cache.Retry(); Reply(4, "recovered"); await Wait(() => cache.For(other) is not null);
        Expect(cache.Error is null && cache.For(other)![(0, 10000)].Contains("recovered"), "Retry did not recover");
        var obsolete = key1 with { Revision = 5 };
        var latest = other with { Revision = 6 };
        cache.Request(obsolete, nodes); cache.Request(latest, nodes);
        pending[5].SetException(new IOException("stale target error")); await Wait(() => pending.Count == 7);
        Expect(cache.Error is null, "Old target failure leaked into new target");
        Reply(6, "latest"); await Wait(() => cache.For(latest) is not null);
        var refresh = latest with { Revision = 7 };
        var oldPresentation = cache.ForPresentation(latest);
        cache.Request(refresh, nodes);
        Expect(ReferenceEquals(cache.ForPresentation(refresh), oldPresentation), "Same-profile refresh recreated the presentation");
        pending[7].SetException(new IOException("refresh failed")); await Wait(() => cache.Error is not null);
        Expect(cache.For(refresh) is null && ReferenceEquals(cache.ForPresentation(refresh), oldPresentation), "Refresh failure emptied the previous results");
        cache.Retry();
        Expect(ReferenceEquals(cache.ForPresentation(refresh), oldPresentation), "Retry emptied the previous results");
        Reply(8, "replacement"); await Wait(() => cache.For(refresh) is not null);
        Expect(cache.ForPresentation(refresh)![(0, 10000)].SetEquals(["replacement"]) && cache.Error is null,
            "New revision did not replace the previous presentation");
        var reshaped = refresh with { Revision = 8 };
        cache.Request(reshaped, [new(10001, 0, "Different criterion", [])]);
        Expect(cache.ForPresentation(reshaped) is null, "Changed criterion identities reused incompatible results");
        pending[9].SetResult(new Dictionary<(int, int), string[]> { [(0, 10001)] = ["reshaped"] });
        await Wait(() => cache.For(reshaped) is not null);
        var count = 0;
        var batchCache = new Mo2NativeFilterCache((_, batch) => {
            Expect(batch.Length <= 128, "Native request exceeded limit"); count++;
            return Task.FromResult<IReadOnlyDictionary<(int Type, int Id), string[]>>(batch.ToDictionary(x => x.Key, _ => Array.Empty<string>()));
        });
        var large = Enumerable.Range(1, 260).Select(i => new Mo2CategoryNode(i, 1, "Category " + i, [])).ToArray();
        batchCache.Request(key1, large); await Wait(() => batchCache.For(key1) is not null);
        Expect(count == 3 && batchCache.For(key1)!.Count == 260, "Batched result was incomplete");
        cache.SetEnabled(false); batchCache.SetEnabled(false);
        Console.WriteLine("PASS native filter cache: coalescing, revision and target guards, detach discard, stable refresh/error/retry presentation, criterion change invalidation and 128-criterion batching");
    }
}
