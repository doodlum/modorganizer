using System.Diagnostics;
using System.Text.Json;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

internal static class Mo2UiLatencyProbe
{
    private static bool _enabled;
    private static readonly System.Collections.Concurrent.ConcurrentQueue<object> Phases = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> Counts = new();
    private static readonly System.Collections.Concurrent.ConcurrentQueue<object> RowBuilds = new();
    private static readonly System.Collections.Concurrent.ConcurrentQueue<object> ControlCensuses = new();
    internal static void Census(string panel, Control body)
    {
        if (!_enabled || Environment.GetEnvironmentVariable("MO2_UI_CONTROL_CENSUS") != "1") return;
        void Capture(string stage)
        {
            if (!_enabled || body.GetVisualRoot() is null) return;
            var clock = Stopwatch.StartNew();
            var controls = body.GetVisualDescendants().Prepend(body).OfType<Control>().ToArray();
            var types = controls
                .GroupBy(control => control.GetType().FullName)
                .Select(group => new { Type = group.Key, Count = group.Count(),
                    Visible = group.Count(control => control.IsEffectivelyVisible),
                    HasBounds = group.Count(control => control.Bounds.Width > 0 && control.Bounds.Height > 0) })
                .OrderByDescending(group => group.Count).ToArray();
            ControlCensuses.Enqueue(new { Panel = panel, Stage = stage, UtcTicks = DateTime.UtcNow.Ticks,
                DurationMs = clock.Elapsed.TotalMilliseconds, Types = types });
            if (stage == "After layout" && Environment.GetEnvironmentVariable("MO2_UI_STYLE_CENSUS") == "1")
                ControlCensuses.Enqueue(Mo2StyleCensus.Capture(panel, controls));
        }
        Capture("Attached");
        DispatcherTimer.RunOnce(() => Capture("After layout"), TimeSpan.FromSeconds(2));
    }
    private sealed record NativePointerSample(double X, double Y, long UtcTicks);
    private static readonly System.Collections.Concurrent.ConcurrentQueue<NativePointerSample> NativePointers = new();
    internal static void Row(string kind, object owner, object model, string name)
    {
        if (!_enabled) return;
        RowBuilds.Enqueue(new { Kind = kind, UtcTicks = DateTime.UtcNow.Ticks, Owner = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(owner),
            Model = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(model), Name = name,
            Stack = name == "Skyrim.esm" ? new StackTrace().ToString() : null });
    }
    internal static void Count(string name)
    {
        if (_enabled) Counts.AddOrUpdate(name, 1, (_, value) => value + 1);
    }
    internal static IDisposable? Measure(string phase, bool always = false)
    {
        if (!_enabled) return null;
        var started = DateTime.UtcNow.Ticks;
        var clock = Stopwatch.StartNew();
        return System.Reactive.Disposables.Disposable.Create(() => {
            if (always || clock.Elapsed.TotalMilliseconds >= 50) Phases.Enqueue(new { Phase = phase, DurationMs = clock.Elapsed.TotalMilliseconds,
                StartUtcTicks = started, EndUtcTicks = DateTime.UtcNow.Ticks });
        });
    }
    // Opt-in diagnostic: measures input-priority dispatcher queue delay, not FPS.
    internal static async Task Run(Mo2LiveWorkspace workspace, Avalonia.Controls.Window window, string report)
    {
        await Task.Delay(TimeSpan.FromSeconds(10));
        void PointerMoved(object? sender, Avalonia.Input.PointerEventArgs args) {
            var point = args.GetPosition(window);
            // The diagnostic injector moves only over the empty top-bar band.
            if (_enabled && point.X >= 650 && point.X < 1000 && point.Y is >= 0 and < 40 && NativePointers.Count < 1000)
                NativePointers.Enqueue(new(point.X, point.Y, DateTime.UtcNow.Ticks));
        }
        window.AddHandler(Avalonia.Input.InputElement.PointerMovedEvent, PointerMoved, Avalonia.Interactivity.RoutingStrategies.Tunnel, handledEventsToo: true);
        using var pointerObservation = System.Reactive.Disposables.Disposable.Create(() =>
            window.RemoveHandler(Avalonia.Input.InputElement.PointerMovedEvent, PointerMoved));
        var switchGame = Environment.GetEnvironmentVariable("MO2_UI_LATENCY_SWITCH_GAME");
        var switchStatus = "Not requested";
        double? switchDuration = null;
        _enabled = true;
        Console.WriteLine("UI latency sampling started (45 seconds)");
        await Task.Run(async () => {
            var samples = new List<double>();
            // Compare input scheduling against the highest dispatcher priority.
            // A long input wait with short Send waits indicates queue starvation;
            // long waits in both indicate a handler/layout pass blocking the UI.
            var sendSamples = new List<double>();
            using var stopSend = new CancellationTokenSource();
            var sendProbe = Task.Run(async () => {
                while (!stopSend.IsCancellationRequested) {
                    var queued = Stopwatch.StartNew();
                    await Dispatcher.UIThread.InvokeAsync(() => sendSamples.Add(queued.Elapsed.TotalMilliseconds), DispatcherPriority.Send);
                    await Task.Delay(16);
                }
            });
            var games = new Dictionary<string, int>();
            var maxMods = 0;
            var elapsed = Stopwatch.StartNew();
            Task? switchTask = null;
            if (!string.IsNullOrWhiteSpace(switchGame)) {
                switchTask = Task.Run(async () => {
                    await Task.Delay(500);
                    var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    Dispatcher.UIThread.Post(async () => {
                        var clock = Stopwatch.StartNew();
                        try {
                            var entry = workspace.CatalogEntries.Where(x => x.Instance?.Game == switchGame && x.Instance.Profiles.Length > 0)
                                .OrderByDescending(x => x.Registration.Launcher is not null).FirstOrDefault()
                                ?? throw new InvalidOperationException("Requested diagnostic game is not registered");
                            var selected = entry.Instance!.Profiles.FirstOrDefault(x => x.Name == entry.Instance.SelectedProfile) ?? entry.Instance.Profiles.First();
                            var steps = new List<(Mo2Registration Registration, Mo2ProfileSnapshot Profile, string Label)> {
                                (entry.Registration, selected, "Cold target")
                            };
                            if (Environment.GetEnvironmentVariable("MO2_UI_LATENCY_ROUND_TRIP") == "1") {
                                var original = workspace.CatalogEntries.Single(x => x.Registration.Endpoint == workspace.Profile.Endpoint);
                                var originalProfile = original.Instance!.Profiles.Single(p => Path.GetFullPath(p.Directory) == Mo2InstanceCatalog.LocalPath(workspace.Profile.ProfilePath));
                                steps.Add((original.Registration, originalProfile, "Return original"));
                                steps.Add((entry.Registration, selected, "Revisit target"));
                            }
                            for (var index = 0; index < steps.Count; index++) {
                                var step = steps[index];
                                using (Measure("Switch step: " + step.Label, always: true)) {
                                    if (!await workspace.Profile.SelectProfile(step.Registration, step.Profile)) throw new InvalidOperationException("Diagnostic profile switch failed");
                                    workspace.ShowProfile();
                                }
                                if (index + 1 < steps.Count) await Task.Delay(6000);
                            }
                            switchStatus = "Connected: " + workspace.Profile.NexusGame;
                        } catch (Exception error) { switchStatus = "Failed: " + error.GetType().Name; }
                        finally { switchDuration = clock.Elapsed.TotalMilliseconds; finished.SetResult(); }
                    });
                    await finished.Task;
                });
            }
            while (elapsed.Elapsed < TimeSpan.FromSeconds(45)) {
                var queued = Stopwatch.StartNew();
                await Dispatcher.UIThread.InvokeAsync(() => {
                    samples.Add(queued.Elapsed.TotalMilliseconds);
                    var game = workspace.Profile.NexusGame;
                    games[game] = games.GetValueOrDefault(game) + 1;
                    maxMods = Math.Max(maxMods, workspace.Profile.Mods.Count);
                }, DispatcherPriority.Input);
                await Task.Delay(16);
            }
            var ordered = samples.Order().ToArray();
            stopSend.Cancel();
            await sendProbe;
            if (switchTask?.IsCompleted == true) await switchTask;
            var sendOrdered = sendSamples.Order().ToArray();
            var result = new {
                Metric = "Input-priority dispatcher queue delay; not render frame time",
                Samples = samples.Count, DurationSeconds = elapsed.Elapsed.TotalSeconds,
                MedianMs = ordered[ordered.Length / 2], P95Ms = ordered[(int)(ordered.Length * .95)],
                MaxMs = ordered[^1], Over100Ms = samples.Count(x => x > 100),
                GameSamples = games, MaximumMods = maxMods, SlowPhases = Phases.ToArray(), Counts, RowBuilds = RowBuilds.ToArray(), ControlCensuses = ControlCensuses.ToArray(), NativePointerSamples = NativePointers.ToArray(),
                SwitchStatus = switchTask is { IsCompleted: false } ? "Still pending at end of sample" : switchStatus,
                SwitchDurationMs = switchDuration,
                SendPriority = new { Samples = sendSamples.Count, P95Ms = sendOrdered[(int)(sendOrdered.Length * .95)], MaxMs = sendOrdered[^1], Over100Ms = sendSamples.Count(x => x > 100) }
            };
            await File.WriteAllTextAsync(report, JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"UI latency sampling complete: p95={result.P95Ms:F1} ms, max={result.MaxMs:F1} ms, >100 ms={result.Over100Ms}");
            _enabled = false;
        });
    }
}
