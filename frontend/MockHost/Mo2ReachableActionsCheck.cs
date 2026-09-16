using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Mo2.Frontend;

// Whether a page's own actions can still be reached.
//
// Every page hands its actions to Mo2PanelChrome, which detaches each one from the
// row the page had put it in. Nothing draws them afterwards — MO2 carries no
// toolbar on a tab, so the header's action group is deliberately empty — and a page
// whose only route to an action was that row has silently lost it. Nothing about
// the page says so: the control still exists, still has its handler, and still
// answers every question put to it except the one that matters.
//
// So each page is built and asked where its handed actions ended up. An action is
// reachable if it is in the page's tree, effectively visible and has a size. One
// that is not has to be named here as deliberately let go, with the reason, or it
// is a fault.
internal static class Mo2ReachableActionsCheck
{
    private static readonly (string Page, Func<Control> Build)[] Pages = [
        ("Tools", () => new Mo2ToolsView()), ("Archives", () => new Mo2ArchivesView()),
        ("Data", () => new Mo2DataView()), ("Saves", () => new Mo2SavesView()),
        ("Overwrite", () => new Mo2OverwriteView()), ("Logs", () => new Mo2LogsView()),
        ("External Files", () => new Mo2ExternalFilesView()),
    ];

    // Actions the frontend hands over on purpose and does not draw. Empty, and meant
    // to stay that way: a page that builds an action and wires it has said the action
    // is worth having, and the only honest answers are to draw it or to stop building
    // it. Anything added here needs its reason beside it, so letting go of an action
    // is a decision rather than something that just happens.
    private static readonly HashSet<string> LetGo = new(StringComparer.Ordinal);

    // Whether every action this view handed over is still on it. Shared by the pages
    // built here and by the live ones found in the window, so both are held to one
    // rule rather than to two that could drift apart.
    private static (int Kept, string[] Lost) Reachable(Control page)
    {
        if (!Mo2PanelChrome.Handed.TryGetValue(page, out var handed)) return (-1, []);
        var lost = new List<string>();
        var kept = 0;
        foreach (var action in handed) {
            var named = action.Name ?? action.GetType().Name;
            if (LetGo.Contains(named)) continue;
            var drawn = page.GetVisualDescendants().OfType<Control>()
                .Any(x => ReferenceEquals(x, action) && x.IsEffectivelyVisible && x.Bounds.Width > 0);
            if (drawn) kept++; else lost.Add(named);
        }
        return (kept, lost.ToArray());
    }

    internal static async Task Run(Window? live = null)
    {
        var host = new ContentControl();
        var window = new Window { Width = 1000, Height = 700, Content = host, ShowInTaskbar = false };
        window.Show();
        var faults = new List<string>();
        var reached = new List<string>();
        try {
            foreach (var (name, build) in Pages) {
                Control page;
                // A page that cannot be built on its own is reported rather than
                // allowed to take the run down: My Mods and Plugins both throw
                // without the services their native views resolve, which is why they
                // are inspected in the live window below instead of here.
                try { page = build(); }
                catch (Exception error) { faults.Add($"{name} could not be built: {error.Message}"); continue; }
                host.Content = page;
                for (var attempt = 0; attempt < 40; attempt++) { await Task.Delay(50); window.UpdateLayout(); }
                var (kept, lost) = Reachable(page);
                if (kept < 0) faults.Add($"{name} never handed its actions to the shared chrome");
                else if (lost.Length > 0) faults.Add($"{name} cannot reach {string.Join(", ", lost)}");
                else reached.Add($"{name} reaches all {kept}");
            }

            // And the pages already on screen, which is the only way to ask this of
            // My Mods and Plugins: both resolve services their native views need, so
            // neither can be built beside the window the way the seven above are.
            // Read in the window the user is looking at, at whatever size it is.
            if (live is not null) {
                live.UpdateLayout();
                foreach (var page in live.GetVisualDescendants().OfType<Control>()
                             .Where(x => x is Mo2ModsView or Mo2PluginsView)) {
                    var name = page is Mo2ModsView ? "My Mods" : "Plugins";
                    var (kept, lost) = Reachable(page);
                    if (kept < 0) continue;
                    if (lost.Length > 0 && Environment.GetEnvironmentVariable("MO2_REACHABLE_TRACE") == "1") {
                        // Why, not just that: the chain from the lost control up to the
                        // page, with what each link says about itself.
                        foreach (var action in Mo2PanelChrome.Handed.TryGetValue(page, out var all) ? all : []) {
                            var chain = new List<string>();
                            for (Avalonia.Visual? at = action; at is not null && !ReferenceEquals(at, page); at = at.GetVisualParent())
                                chain.Add($"{(at as Control)?.Name ?? at.GetType().Name}[vis={at.IsVisible},eff={at.IsEffectivelyVisible},w={((Control)at).Bounds.Width:F0}]");
                            Console.WriteLine($"TRACE {name} {(action.Name ?? action.GetType().Name)}: " +
                                (chain.Count == 0 ? "not in this page's tree at all" : string.Join(" < ", chain)));
                        }
                    }
                    if (lost.Length > 0) faults.Add($"{name} cannot reach {string.Join(", ", lost)}");
                    else reached.Add($"{name} reaches all {kept} in the open window");
                }
            }
            if (faults.Count > 0) Console.WriteLine("FAIL reachable actions: " + string.Join("; ", faults));
            else Console.WriteLine("PASS reachable actions: every action a page hands over is still on it — " + string.Join(", ", reached));
        } finally { window.Close(); }
    }
}
