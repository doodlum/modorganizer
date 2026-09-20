using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// Startup diagnostics for the paired Mods/Plugins layout. Layout bounds and a
// dispatcher marker establish readiness; a desktop capture is still needed to
// verify compositor output, and this does not measure physical input latency.
internal static class Mo2StartupCheck
{
    private static readonly bool Enabled = Environment.GetEnvironmentVariable("MO2_VERIFY_STARTUP") == "1";
    private static readonly Stopwatch Since = Stopwatch.StartNew();
    private static double _windowOpened;
    private static bool _collectionPlaceholder;

    internal static void ProcessStarted() => Since.Restart();

    // Where the wait before the first window actually goes. Recorded rather than
    // guessed: the startup path builds a database, scans every registered instance
    // and builds a page factory for each page, and only measuring says which of
    // those is the one worth moving off the path.
    private static readonly List<(string Name, double Started, double Ended)> Timeline = [];
    internal static IDisposable Phase(string name) => Enabled ? new Span(name) : System.Reactive.Disposables.Disposable.Empty;

    private static readonly List<string> Notes = [];
    internal static void Note(string note) { if (Enabled) lock (Notes) Notes.Add(note); }

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
        // Normal sessions do not need a visual-tree census on every layout pass.
        if (!Enabled) return;
        window.LayoutUpdated += Observe;
        window.Closed += Closed;
        void Closed(object? sender, EventArgs args) {
            window.LayoutUpdated -= Observe;
            window.Closed -= Closed;
        }
        void Observe(object? sender, EventArgs args) {
            if (live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0) Stamp("connected");
            if (live.ModsPage?.Adapter.SourceCount.Value > 0) Stamp("listed");
            var mods = window.GetVisualDescendants().OfType<Mo2ModsView>().FirstOrDefault(x => x.IsEffectivelyVisible);
            if (mods is not null && mods.GetVisualDescendants().OfType<Control>().Any(x =>
                    x.Name is "WritableCollectionPageHeader" or "Statusbar" && x.IsEffectivelyVisible))
                _collectionPlaceholder = true;
            var plugins = window.GetVisualDescendants().OfType<Mo2PluginsView>().FirstOrDefault(x => x.IsEffectivelyVisible);
            var pluginTable = plugins?.GetVisualDescendants().OfType<TreeDataGrid>().FirstOrDefault();
            if (mods is null || pluginTable is null || Stamped("connected") is null || Stamped("listed") is null) return;
            var modNames = live.Profile.Mods.Select(x => x.DisplayName).ToHashSet();
            var readableMod = mods.GetVisualDescendants().OfType<TreeDataGridRow>()
                .Where(x => x.IsEffectivelyVisible).Any(row => row.GetVisualDescendants().OfType<TextBlock>().Any(x =>
                    x.IsEffectivelyVisible && x.Bounds.Width >= 60 && x.Bounds.Height > 0 && x.Text is not null && modNames.Contains(x.Text)));
            if (!readableMod || !Mo2PluginRenderCheck.HasReadableRows(pluginTable,
                    live.Profile.Order.Plugins.Select(x => x.DisplayName).ToHashSet())) return;
            Stamp("drawn");
            window.LayoutUpdated -= Observe;
            // Measure an actual queued turn after readable layout. This is not a
            // physical-input test or the first idle turn since process launch.
            Dispatcher.UIThread.Post(() => Stamp("ready"), DispatcherPriority.Background);
        }
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
        await Until(() => Stamped("ready") is not null, "Mods and Plugins never reached readable layout and a background dispatcher turn");
        var idle = Stamped("ready")!.Value;
        var connected = Stamped("connected") ?? idle;
        var listed = Stamped("listed") ?? idle;
        var drawn = Stamped("drawn") ?? idle;
        Console.WriteLine($"STARTUP: first window {_windowOpened:F0}ms, MO2 connected {connected:F0}ms, " +
            $"mod list built {listed:F0}ms, readable rows laid out {drawn:F0}ms, background turn after rows {idle:F0}ms");
        Console.WriteLine("STARTUP PHASES: " + Phases());
        lock (Notes) Console.WriteLine("STARTUP TEMPLATES: " + string.Join(" | ", Notes));
        if (_collectionPlaceholder) throw new Exception("Upstream collection controls appeared in Mods during startup");
        // The window has to be on screen quickly even if MO2 is slow to answer, which
        // is the part of startup the frontend actually controls.
        // Bounds set from what this machine actually reaches, so a regression shows
        // up as a failure rather than as a number nobody reads. They are not targets
        // and they are one machine's timings.
        if (_windowOpened > 3200) throw new Exception($"The first window took {_windowOpened:F0}ms");
        if (drawn > 6000) throw new Exception($"Readable rows were laid out at {drawn:F0}ms");
        if (idle > 8000) throw new Exception($"The background turn after readable layout ran at {idle:F0}ms");
        Console.WriteLine($"PASS startup: window {_windowOpened:F0}ms, readable Mods and Plugins layout {drawn:F0}ms, " +
            $"background turn {idle:F0}ms; no collection placeholders observed");
    }
}
