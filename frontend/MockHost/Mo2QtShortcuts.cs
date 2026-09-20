using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using NexusMods.App.UI.Controls.Navigation;
using NexusMods.App.UI.WorkspaceSystem;

namespace Mo2.Frontend;

// The keys MO2 gives its actions.
//
// Eight of MO2's actions carry a <property name="shortcut"> in mainwindow.ui, and
// this window bound none of them. Every other way into those actions was checked —
// MO2_VERIFY_QT_ACTIONS requires all twenty-four to be offered from something on
// screen — and the keyboard was not a way in at all, because nothing had ever
// looked: a grep for KeyGesture, HotKey or KeyBinding across this frontend found
// nothing, and no check named a gesture.
//
// The gestures are written down here rather than read out of MO2's file at run
// time: this app has to start with no MO2 source tree beside it, and a missing
// file is not a reason to have no keyboard. MO2_VERIFY_QT_SHORTCUTS reads
// mainwindow.ui and compares the two both ways round — a key MO2 has that this
// does not, and a key here that MO2 has moved or dropped, are both failures — so
// the written list cannot quietly fall behind the file it stands for.
internal static class Mo2QtShortcuts
{
    // MO2's action, and the gesture MO2 gives it.
    internal static readonly (string Action, string Gesture)[] Keys = [
        ("actionInstallMod", "Ctrl+M"),
        ("actionAdd_Profile", "Ctrl+P"),
        ("actionModify_Executables", "Ctrl+E"),
        ("actionTool", "Ctrl+I"),
        ("actionSettings", "Ctrl+S"),
        ("actionNexus", "Ctrl+N"),
        ("actionHelp", "Ctrl+H"),
        ("action_Refresh", "F5"),
    ];

    // What a press last reached, for the check to read. A gesture that resolves to
    // nothing leaves the reason here rather than throwing on the UI thread.
    internal static string LastReached = "";

    // Actions whose key is followed all the way to the control that offers it, and
    // then not pressed. Empty in an ordinary run — this is how the check drives a
    // real key through the real handler for the six of MO2's eight that end in a
    // modal MO2 dialog or a browser window: Settings, Executables, Install Mod and
    // Profiles each open a dialog that owns the thread until a person closes it,
    // and Visit Nexus and Help hand a URL to the desk. Withholding the last step is
    // the difference between a check that can run unattended and one that cannot
    // run at all; what it gives up is stated in the check's own verdict.
    internal static readonly HashSet<string> Withheld = [];

    internal static void Install(Window window, Mo2LiveWorkspace live)
    {
        var bound = Keys.Select(x => (x.Action, Gesture: KeyGesture.Parse(x.Gesture))).ToArray();
        // Tunnelling, so the key reaches the window before whatever has focus. A
        // list with focus handles F5 for itself and a text box takes most of the
        // rest; MO2's are window shortcuts and act wherever the caret is.
        window.AddHandler(InputElement.KeyDownEvent, async (_, args) => {
            var match = bound.FirstOrDefault(x => x.Gesture.Matches(args));
            if (match.Action is null) return;
            args.Handled = true;
            try { LastReached = await Reach(match.Action, live, window); }
            catch (Exception error) { LastReached = $"{match.Action}: {error.Message}"; }
        }, RoutingStrategies.Tunnel);
    }

    // Opens the page the action is offered on and works the control that offers it,
    // which is the same control MO2_VERIFY_QT_ACTIONS requires to be there. Answers
    // with what it reached, so a check can say which control a key arrived at rather
    // than only that a key was pressed.
    internal static async Task<string> Reach(string action, Mo2LiveWorkspace live, Window window)
    {
        var press = !Withheld.Contains(action);
        if (!Mo2QtActions.Answers.TryGetValue(action, out var answer))
            throw new Exception("nothing here answers for this action");
        if (answer.Kind != Mo2QtActions.Kind.Offered)
            throw new Exception("this action is Qt's own window furniture");

        if (answer.Page.Length > 0) {
            var menu = live.ProfileMenu;
            switch (answer.Page) {
                // Back to the profile workspace first. These two pages are on the
                // profile sidebar, and Connections is on the home one — the two
                // sidebars swap, so navigating a profile page while the home
                // workspace is showing put the page nowhere and left the control it
                // is offered from out of the window entirely. MO2's keys work from
                // wherever the window happens to be, and these two did not: from
                // Connections, Ctrl+I and F5 reached nothing at all.
                case "my-mods": live.ShowProfile(); await Task.Delay(400); await Navigate(menu.LeftMenuItemLoadout); break;
                case "tools": live.ShowProfile(); await Task.Delay(400); await Navigate(menu.ToolsItem); break;
                case "connections": live.OpenSettings(); await Task.Delay(400); break;
                default: throw new Exception($"no way to open {answer.Page}");
            }
        }

        Control? found = null;
        for (var attempt = 0; attempt < 60 && found is null; attempt++) {
            await Task.Delay(100);
            window.UpdateLayout();
            found = Roots(window, live).SelectMany(root => root.GetVisualDescendants().OfType<Control>().Prepend(root))
                .FirstOrDefault(x => x.Name == answer.Control);
        }
        if (found is null) throw new Exception($"{answer.Control} is not on screen");

        if (answer.Entry.Length == 0) {
            if (press) Press(found);
            return answer.Control;
        }
        // MO2's executables box: its own first row opens the Edit Executables dialog,
        // so the key selects that row as a pointer would.
        if (found is ComboBox box) {
            var item = box.Items.OfType<object>().FirstOrDefault(x => x?.ToString() == answer.Entry)
                ?? throw new Exception($"{answer.Control} does not offer \"{answer.Entry}\"");
            if (press) box.SelectedItem = item;
            return $"{answer.Control} → {answer.Entry}";
        }
        // A menu that builds itself when it is shown has to be shown before its
        // entries exist, which is why this opens the flyout rather than reading
        // Items as they stand.
        var flyout = found.ContextFlyout as MenuFlyout ?? (found as Button)?.Flyout as MenuFlyout
            ?? throw new Exception($"{answer.Control} carries no menu to find \"{answer.Entry}\" on");
        flyout.ShowAt(found);
        await Task.Delay(400);
        var entry = flyout.Items.OfType<MenuItem>().FirstOrDefault(x => x.Header?.ToString() == answer.Entry);
        if (entry is null) { flyout.Hide(); throw new Exception($"{answer.Control} does not offer \"{answer.Entry}\""); }
        if (press) entry.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        flyout.Hide();
        await Task.Delay(200);
        return $"{answer.Control} → {answer.Entry}";
    }

    // The run row sits in the profile sidebar, which moves into a flyout when the
    // sidebar is collapsed, so it is reached through the workspace holding it.
    private static IEnumerable<Control> Roots(Window window, Mo2LiveWorkspace live) =>
        live.LaunchPanel is { } panel ? [window, panel] : [window];

    // The path a press takes. These buttons carry Click handlers rather than
    // commands, and RaiseEvent over the Click event raises the event without
    // calling the method that runs them.
    private static void Press(Control control)
    {
        if (control is Button button) {
            typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.Invoke(button, null);
            return;
        }
        // A page reached by opening it — MO2's tool plugins are a page here — needs
        // nothing further pressed.
        if (control is NexusMods.App.UI.LeftMenu.Items.LeftMenuItemView) return;
        throw new Exception($"{control.Name ?? control.GetType().Name} is not something a key can work");
    }

    private static async Task Navigate(NexusMods.App.UI.LeftMenu.Items.ILeftMenuItemViewModel? item)
    {
        if (item is null) return;
        await System.Reactive.Threading.Tasks.TaskObservableExtensions.ToTask(
            item.NavigateCommand.Execute(NavigationInformation.From(NavigationInput.Default)));
        await Task.Delay(300);
    }
}
