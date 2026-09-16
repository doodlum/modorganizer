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

    internal static async Task Run()
    {
        var host = new ContentControl();
        var window = new Window { Width = 1000, Height = 700, Content = host, ShowInTaskbar = false };
        window.Show();
        var faults = new List<string>();
        var reached = new List<string>();
        try {
            foreach (var (name, build) in Pages) {
                var page = build();
                host.Content = page;
                for (var attempt = 0; attempt < 40; attempt++) { await Task.Delay(50); window.UpdateLayout(); }
                if (!Mo2PanelChrome.Handed.TryGetValue(page, out var handed)) {
                    faults.Add($"{name} never handed its actions to the shared chrome");
                    continue;
                }
                var lost = new List<string>();
                var kept = 0;
                foreach (var action in handed) {
                    var named = action.Name ?? action.GetType().Name;
                    if (LetGo.Contains(named)) continue;
                    var drawn = page.GetVisualDescendants().OfType<Control>()
                        .Any(x => ReferenceEquals(x, action) && x.IsEffectivelyVisible && x.Bounds.Width > 0);
                    if (drawn) kept++; else lost.Add(named);
                }
                if (lost.Count > 0) faults.Add($"{name} cannot reach {string.Join(", ", lost)}");
                else reached.Add($"{name} reaches all {kept}");
            }
            if (faults.Count > 0) Console.WriteLine("FAIL reachable actions: " + string.Join("; ", faults));
            else Console.WriteLine("PASS reachable actions: every action a page hands over is still on it — " + string.Join(", ", reached));
        } finally { window.Close(); }
    }
}
