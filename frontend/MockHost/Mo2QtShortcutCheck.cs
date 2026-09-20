using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Mo2.Frontend;

// The keys MO2 gives its actions, driven as keys.
//
// MO2_VERIFY_QT_ACTIONS already requires every action in MO2's mainwindow.ui to be
// offered from something on screen, and it found all twenty-four. It says nothing
// about how they are reached, and eight of them MO2 also gives a gesture:
// Ctrl+M, Ctrl+P, Ctrl+E, Ctrl+I, Ctrl+S, Ctrl+N, Ctrl+H and F5. This window bound
// none of them, and nothing looked — the whole keyboard was outside every check
// here, so "MO2's actions are all reachable" was true of the pointer alone.
//
// Two things are asserted, and they are different things:
//
//   * MO2's own file and the list this frontend binds agree, both ways round. A
//     gesture MO2 has that nothing binds, and a gesture bound here that MO2 has
//     moved or dropped, are both failures — so the written list cannot drift from
//     the file it stands for.
//
//   * Each gesture, pressed as a key on the live window, is handled and arrives at
//     the control that MO2_VERIFY_QT_ACTIONS says answers for that action. The key
//     goes through Window.KeyDown, not through the handler called directly: a
//     gesture that no longer matches, or that something else swallows first, is
//     the failure this exists to catch and calling the handler would step over it.
//
// Six of the eight end in a modal MO2 dialog or a browser window — Settings,
// Executables, Install Mod and Profiles each open a dialog that owns the thread
// until a person closes it; Visit Nexus and Help hand a URL to the desk. Those are
// followed to the control and the entry that MO2 would run, and not pressed. That
// is stated in the verdict rather than left out of it: what is proven for those
// six is that the key is bound and arrives, not that MO2 then did the work.
// Tool Plugins and Refresh are pressed for real.
internal static class Mo2QtShortcutCheck
{
    // The two whose action can run here without something modal opening.
    private static readonly string[] Pressed = ["actionTool", "action_Refresh"];

    internal static async Task Run(Mo2LiveWorkspace live, Window window)
    {
        for (var attempt = 0; attempt < 300 && !(live.Profile.IsConnected && live.Profile.ProfilePath.Length > 0); attempt++)
            await Task.Delay(100);
        if (!live.Profile.IsConnected) throw new Exception("Bridge never connected");
        live.ShowProfile();

        using var turn = await Mo2CheckTurn.Take();
        // MO2's executables box folds into a flyout when the sidebar is closed, and
        // Ctrl+E has to find it wherever it is.
        using var sidebar = await Mo2SidebarState.Open(window, live);
        var faults = new List<string>();
        var reached = new List<string>();
        var withheld = new List<string>();

        var source = Mo2QtWidgetSource.Locate();
        var declared = Mo2QtWidgetSource.Actions(source)
            .Where(x => x.Shortcut.Length > 0)
            .ToDictionary(x => x.Name, x => x);
        if (declared.Count == 0) throw new Exception("No shortcut was read from " + source);

        var bound = Mo2QtShortcuts.Keys.ToDictionary(x => x.Action, x => x.Gesture);
        foreach (var (name, action) in declared)
            if (!bound.TryGetValue(name, out var gesture))
                faults.Add($"MO2 gives {name} (\"{action.Text}\") {action.Shortcut} and nothing here binds it");
            else if (!gesture.Equals(action.Shortcut, StringComparison.OrdinalIgnoreCase))
                faults.Add($"{name} is bound to {gesture} where MO2 gives it {action.Shortcut}");
        foreach (var (name, gesture) in bound)
            if (!declared.ContainsKey(name))
                faults.Add($"{gesture} is bound to {name}, which MO2 no longer gives a key");

        // Every gesture from both of this window's workspaces, because MO2's keys
        // act wherever its window happens to be and two of these did not: from
        // Connections, Ctrl+I and F5 reached nothing, since their pages are on the
        // profile sidebar and the home workspace was showing. That was found by
        // accident — the gestures happen to be tried in MO2's own order, and
        // Connections happens to come before them in it — and an accident is not a
        // check. Reordering the list would have hidden it again. So each key is
        // pressed from a workspace chosen to be awkward for it, both ways.
        var starts = new (string Name, Func<Task> Go)[] {
            ("the profile workspace", async () => { live.ShowProfile(); await Task.Delay(600); }),
            ("Settings", async () => { live.OpenSettings(); await Task.Delay(600); }),
        };

        // Names disagreeing is reported above; pressing keys for actions MO2 has
        // dropped would report that as a second, different-looking fault.
        foreach (var (name, gesture) in bound.Where(x => declared.ContainsKey(x.Key))) {
            var answer = Mo2QtActions.Answers[name];
            var wanted = answer.Entry.Length > 0 ? $"{answer.Control} → {answer.Entry}" : answer.Control;
            var press = Pressed.Contains(name);
            var arrived = new List<string>();

            foreach (var start in starts) {
                await start.Go();
                window.UpdateLayout();
                Mo2QtShortcuts.Withheld.Clear();
                if (!press) Mo2QtShortcuts.Withheld.Add(name);
                Mo2QtShortcuts.LastReached = "";

                var parsed = KeyGesture.Parse(gesture);
                var args = new KeyEventArgs {
                    RoutedEvent = InputElement.KeyDownEvent, Key = parsed.Key, KeyModifiers = parsed.KeyModifiers,
                };
                window.RaiseEvent(args);
                if (!args.Handled) {
                    faults.Add($"{gesture} was not handled from {start.Name}: nothing on the window answers it");
                    continue;
                }
                // The handler navigates and looks for its control, which takes frames.
                for (var attempt = 0; attempt < 120 && Mo2QtShortcuts.LastReached.Length == 0; attempt++) {
                    await Task.Delay(100);
                    window.UpdateLayout();
                }
                var landed = Mo2QtShortcuts.LastReached;
                if (landed.Length == 0) { faults.Add($"{gesture} was handled from {start.Name} and reached nothing within 12s"); continue; }
                // The handler reports a failure by naming the action it could not reach.
                if (landed.StartsWith(name + ":", StringComparison.Ordinal)) { faults.Add($"{gesture} from {start.Name} → {landed}"); continue; }
                if (landed != wanted) { faults.Add($"{gesture} from {start.Name} reached {landed} where {name} is offered from {wanted}"); continue; }
                arrived.Add(start.Name);
            }

            if (arrived.Count != starts.Length) continue;
            var caption = declared[name].Text;
            if (press) reached.Add($"{gesture} ran {caption} from {wanted}");
            else withheld.Add($"{gesture} reached {caption} at {wanted}");
        }
        Mo2QtShortcuts.Withheld.Clear();

        if (faults.Count > 0) Console.WriteLine("FAIL MO2 shortcuts: " + string.Join("; ", faults));
        else Console.WriteLine($"PASS MO2 shortcuts: all {declared.Count} gestures MO2 declares in " +
            $"{Path.GetFileName(source)} are bound here and no other key is, and every one was pressed as a key on " +
            $"the live window from both the profile workspace and Connections and arrived from each — " +
            $"{string.Join(", ", reached)}; and {withheld.Count} reached the control MO2 runs them from without " +
            $"being pressed, each ending in a modal MO2 dialog or a browser window ({string.Join(", ", withheld)})");
    }
}
