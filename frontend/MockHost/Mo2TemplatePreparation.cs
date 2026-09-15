using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// Keep the real logical/resource ancestry while applying one
// existing template per background turn. Never detach prepared template parts.
internal static class Mo2TemplatePreparation
{
    // How long one preparation turn may run before giving the dispatcher back, and
    // at what priority it asks for the next one.
    //
    // Both were wrong in the same direction. One control per turn at Background
    // priority meant a panel's 200-odd templates took twenty turns and 1.1 seconds
    // of wall-clock for 119ms of actual work — the rest was waiting to be scheduled
    // behind first-display layout and render, with the panel hidden and showing
    // "Loading panel…" for the whole of it. The point of preparing templates is to
    // get the panel on screen sooner, so spreading 119ms of work over a second
    // defeats it. A slice of roughly a frame and a half at a priority that actually
    // runs finishes it in a handful of turns, and the cap still means a single turn
    // cannot hold up input.
    // Both are overridable so the previous values can be measured against these
    // rather than remembered.
    internal static readonly TimeSpan Slice = TimeSpan.FromMilliseconds(
        double.TryParse(Environment.GetEnvironmentVariable("MO2_TEMPLATE_SLICE_MS"), out var slice) && slice > 0 ? slice : 24);
    internal static readonly DispatcherPriority Priority =
        Environment.GetEnvironmentVariable("MO2_TEMPLATE_PRIORITY") == "background"
            ? DispatcherPriority.Background : DispatcherPriority.Normal;

    // What the most recent preparation cost: how many dispatcher turns it took, how
    // many templates it applied, and how much of the elapsed time was actually spent
    // applying them rather than waiting to be scheduled again.
    internal static (int Turns, int Applied, TimeSpan Working) LastRun { get; private set; }

    internal static void Attach(Control root, Action attach, Func<bool> current, Action<bool> completed)
    {
        var visible = root.IsVisible;
        // Counted so the cost can be split between the work itself and the turns it
        // is spread over; the two call for opposite fixes.
        var turns = 0; var applied = 0; var working = TimeSpan.Zero;
        var pending = new Queue<Control>();
        var visited = new HashSet<Control>();
        var finished = false;
        void Finish(bool success)
        {
            if (finished) return;
            finished = true;
            pending.Clear();
            root.SetCurrentValue(Control.IsVisibleProperty, visible);
            LastRun = (turns, applied, working);
            completed(success);
        }
        void Step()
        {
            try {
                if (!current() || root.GetVisualRoot() is null) { Finish(false); return; }
                // As much as fits in one slice, not one control per dispatcher turn.
                // A page body is thousands of controls deep, and one per turn made
                // the connected profile's first list take seconds to reach the
                // screen. Yielding on a budget keeps the window answering input just
                // as well while getting through the tree orders of magnitude sooner.
                var clock = System.Diagnostics.Stopwatch.StartNew();
                turns++;
                do {
                    if (!pending.TryDequeue(out var control)) { Finish(true); return; }
                    if (!visited.Add(control) || (!ReferenceEquals(control, root) && !control.IsVisible) ||
                        !ReferenceEquals(control.GetVisualRoot(), root.GetVisualRoot())) continue;
                    {
                        using var timing = Mo2UiLatencyProbe.Measure("Prepare template: " + control.GetType().FullName, always: true);
                        // ContentPresenter also overrides ApplyTemplate to create
                        // its child, despite not deriving from TemplatedControl.
                        control.ApplyTemplate();
                    }
                    applied++;
                    foreach (var child in control.GetVisualChildren().OfType<Control>()) pending.Enqueue(child);
                } while (clock.Elapsed < Slice);
                working += clock.Elapsed;
                Dispatcher.UIThread.Post(Step, Priority);
            } catch { Finish(false); throw; }
        }
        root.SetCurrentValue(Control.IsVisibleProperty, false);
        try { attach(); }
        catch { Finish(false); throw; }
        pending.Enqueue(root);
        Dispatcher.UIThread.Post(Step, Priority);
    }
}
