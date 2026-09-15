using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// What the user waits through before the frontend is usable, split into the parts
// that can be worked on separately: the process reaching its first window, the
// window reaching a first painted frame, and the connected profile's own lists
// reaching the screen.
internal static class Mo2StartupCheck
{
    private static readonly Stopwatch Since = Stopwatch.StartNew();
    private static double _windowOpened;

    internal static void ProcessStarted() => Since.Restart();

    // Where the wait before the first window actually goes. Recorded rather than
    // guessed: the startup path builds a database, scans every registered instance
    // and builds a page factory for each page, and only measuring says which of
    // those is the one worth moving off the path.
    private static readonly List<(string Name, double Started, double Ended)> Timeline = [];
    internal static IDisposable Phase(string name) => new Span(name);

    private static readonly List<string> Notes = [];
    internal static void Note(string note) { lock (Notes) Notes.Add(note); }

    // Stamped from the layout pass that first satisfies something, rather than from
    // a polling loop. Everything here polled with Task.Delay, whose continuations
    // queue on the same dispatcher the startup work is saturating, so every figure
    // came back as "when the dispatcher went idle" — all three within a millisecond
    // of each other, four seconds after the screenshot already showed both panels
    // full of rows.
    private static readonly Dictionary<string, double> Stamps = new();
    internal static void Stamp(string name)
    {
        lock (Stamps) Stamps.TryAdd(name, Since.Elapsed.TotalMilliseconds);
    }
    private static double? Stamped(string name)
    {
        lock (Stamps) return Stamps.TryGetValue(name, out var value) ? value : null;
    }
    private sealed class Span(string name) : IDisposable
    {
        private readonly double _started = Since.Elapsed.TotalMilliseconds;
        public void Dispose()
        {
            lock (Timeline) Timeline.Add((name, _started, Since.Elapsed.TotalMilliseconds));
        }
    }
    private static string Phases()
    {
        lock (Timeline)
            return string.Join(", ", Timeline.OrderBy(x => x.Started)
                .Select(x => $"{x.Name} {x.Ended - x.Started:F0}ms @{x.Started:F0}"));
    }
    // Registered when the window opens, not when the check runs: the check runs
    // after the window's own gate has already waited for the profile's tables, by
    // which point the rows have been on screen for seconds and there may be no
    // further layout pass to notice them in.
    internal static void WindowOpened(Window window, Mo2LiveWorkspace live)
    {
        _windowOpened = Since.Elapsed.TotalMilliseconds;
        window.LayoutUpdated += (_, _) => {
            if (live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0) Stamp("connected");
            if (live.ModsPage?.Adapter.SourceCount.Value > 0) Stamp("listed");
            if (window.GetVisualDescendants().OfType<TreeDataGrid>().Any(x => x.Rows?.Count > 0)) Stamp("drawn");
        };
    }

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        // Startup is measured on an otherwise idle run or not at all. Every other
        // check drives the same dispatcher this is timing, and a run with the suite
        // alongside reported nine seconds to first rows where the same build alone
        // reports under seven — a number that says more about the suite than the
        // product.
        var others = Environment.GetEnvironmentVariables().Keys.OfType<string>()
            .Where(x => x.StartsWith("MO2_VERIFY_", StringComparison.Ordinal) && x != "MO2_VERIFY_STARTUP" &&
                Environment.GetEnvironmentVariable(x) == "1").ToArray();
        if (others.Length > 0) {
            Console.WriteLine("SKIP startup: measured only on a run of its own; " + string.Join(", ", others) + " are also active");
            return;
        }

        async Task<double> Until(Func<bool> wanted, string what, int seconds = 60)
        {
            for (var attempt = 0; attempt < seconds * 20; attempt++) {
                if (wanted()) return Since.Elapsed.TotalMilliseconds;
                await Task.Delay(50);
            }
            throw new Exception(what + " never happened");
        }
        // Observed from the UI thread, so these are the moments the window could
        // have shown each thing, not the moments the data arrived. That is the
        // number that matters here: work the dispatcher is busy with is time the
        // user cannot click through.
        // The stamps are taken from layout; this loop only adds the moment
        // the dispatcher had room to answer at all, which is when the window first
        // responds to the person using it.
        var idle = await Until(() => Stamped("drawn") is not null, "No table ever drew a row");
        var connected = Stamped("connected") ?? idle;
        var listed = Stamped("listed") ?? idle;
        var drawn = Stamped("drawn") ?? idle;
        Console.WriteLine($"STARTUP: first window {_windowOpened:F0}ms, MO2 connected {connected:F0}ms, " +
            $"mod list built {listed:F0}ms, first rows drawn {drawn:F0}ms, dispatcher first idle {idle:F0}ms");
        Console.WriteLine("STARTUP PHASES: " + Phases());
        lock (Notes) Console.WriteLine("STARTUP TEMPLATES: " + string.Join(" | ", Notes));
        // The window has to be on screen quickly even if MO2 is slow to answer, which
        // is the part of startup the frontend actually controls.
        // Bounds set from what this machine actually reaches, so a regression shows
        // up as a failure rather than as a number nobody reads. They are not targets
        // and they are one machine's timings.
        if (_windowOpened > 3200) throw new Exception($"The first window took {_windowOpened:F0}ms");
        if (drawn > 6000) throw new Exception($"The first rows were drawn at {drawn:F0}ms");
        if (idle > 8000) throw new Exception($"The dispatcher was busy until {idle:F0}ms");
        Console.WriteLine($"PASS startup: the window is up in {_windowOpened:F0}ms, the connected profile's rows are on screen " +
            $"by {drawn:F0}ms, and the dispatcher first has room to answer at {idle:F0}ms");
    }
}
