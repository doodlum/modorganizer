using System.Diagnostics;

namespace Mo2.Frontend;

// What an action costs between the frontend asking and MO2 answering. Both sides
// polled on a fixed tick — the host looked for requests every 100ms and the
// frontend looked for replies every 50ms — so every action paid up to 150ms before
// any work happened. This measures the round trip rather than inferring it.
internal static class Mo2BridgeLatencyCheck
{
    internal static async Task Run(string directory, int samples = 25)
    {
        var client = new Mo2BridgeClient(directory);
        // One request first so a cold endpoint read and any host warm-up are not
        // counted as latency.
        await client.SendAsync("snapshot");

        // Measured twice, because "an action is slow" has two separate causes and
        // only one of them is the transport. A rejected action does no work in MO2
        // at all, so what it costs is the handover; a snapshot adds the host's own
        // work on top, which no amount of polling can remove.
        async Task<(double Median, double Mean, double Worst)> Measure(Func<Task> send)
        {
            var timings = new List<double>(samples);
            for (var index = 0; index < samples; index++) {
                var watch = Stopwatch.StartNew();
                try { await send(); } catch (Mo2BridgeCommandException) { }
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }
            timings.Sort();
            return (timings[timings.Count / 2], timings.Average(), timings[^1]);
        }

        var handover = await Measure(() => client.SendAsync("frontendLatencyProbe"));
        var snapshot = await Measure(() => client.SendAsync("snapshot"));
        Console.WriteLine($"BRIDGE LATENCY: {samples} samples each. Handover median {handover.Median:F0}ms, " +
            $"mean {handover.Mean:F0}ms, worst {handover.Worst:F0}ms. Snapshot median {snapshot.Median:F0}ms, " +
            $"mean {snapshot.Mean:F0}ms, worst {snapshot.Worst:F0}ms " +
            $"(host tick {Idle(directory)}, frontend first look {Mo2BridgeClient.PollAfter(0).TotalMilliseconds:F0}ms)");
        // A flat 100ms host tick and a flat 50ms frontend poll put the handover alone
        // at 75ms on average. This is the bound the adaptive ticks were made to
        // clear; it is one machine's run, not a general guarantee.
        if (handover.Median > 40) throw new Exception($"Median handover is {handover.Median:F0}ms");
        Console.WriteLine($"PASS bridge latency: an action reaches MO2 and returns in {handover.Median:F0}ms, " +
            $"under the 40ms bound; a full snapshot costs {snapshot.Median:F0}ms, the rest of which is MO2's own work");
    }

    // Reported rather than asserted: the host's tick lives in the MO2 plugin, and a
    // host running an older copy of it is exactly what this number would explain.
    private static string Idle(string directory)
    {
        try {
            var plugin = Path.GetFullPath(Path.Combine(directory, "..", "..", "nexus_frontend_bridge", "core.py"));
            if (!File.Exists(plugin)) return "unknown";
            var line = File.ReadLines(plugin).FirstOrDefault(x => x.Contains("self.idle_interval ="));
            var busy = File.ReadLines(plugin).FirstOrDefault(x => x.Contains("self.busy_interval ="));
            return line is null ? "fixed 100ms" : $"{line.Split('=')[^1].Trim()}ms idle / {busy?.Split('=')[^1].Trim() ?? "?"}ms busy";
        } catch (IOException) { return "unknown"; }
    }
}
